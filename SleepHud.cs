using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PoVPlus.Compat
{
    // BetterSleepBruh 2.1.2 looks for the minimap at the fixed path hudroot/MiniMap/small, which SeneaL UI moves. Its client
    // RPCs live on the HUD view, so without a view a client never learns the sleep boost and its timer and boat protections
    // stay off during partial sleep. This builds the view from Minimap's own root, only after BetterSleepBruh's build failed.
    internal static class SleepHud
    {
        private const string Owner = "povplusplus.bettersleephudbridge";
        private const string ViewName = "BetterSleepBruh_SleepHud";
        private const int MaxMissingMinimap = 100;
        private const float StripGap = 12f, StripHeight = 40f;
        private const string SeneaLUi = "seneaL.valheim.ui";
        private static readonly string[] ViewRpcs = { "RPC_SleepingPlayerInfo", "RPC_StartSleep", "RPC_StopSleep", "BSB_SyncSleepBoostTotal" };

        private static FieldInfo _hudField, _registeredField, _handlers;
        private static MethodInfo _tryCreate, _inSleepWindow, _register;
        private static Type _viewType;
        private static object _sleepMod;
        private static int _missingMinimap;
        private static bool _unsupported, _foreignHandler;
        private static float _nextHome;
        private static int _waveFrame = -1;
        private static Func<double> _extraRate;
        private static Func<bool> _decoupleWaves;

        internal static void Install()
        {
            var mod = AccessTools.TypeByName("BetterSleepBruh.BetterSleepBruh");
            var view = AccessTools.TypeByName("BetterSleepBruh.Components.SleepHudView");
            var build = mod == null ? null : AccessTools.DeclaredMethod(mod, "BuildSleepGui");
            var update = mod == null ? null : AccessTools.DeclaredMethod(mod, "Update");
            var register = view == null ? null : AccessTools.DeclaredMethod(view, "RegisterRpcs");
            _viewType = view;
            _register = register;
            _handlers = AccessTools.Field(typeof(ZRoutedRpc), "m_functions");
            _hudField = mod == null ? null : AccessTools.DeclaredField(mod, "_sleepHud");
            _registeredField = view == null ? null : AccessTools.DeclaredField(view, "_rpcsRegistered");
            _tryCreate = view == null ? null : AccessTools.DeclaredMethod(view, "TryCreate");
            _inSleepWindow = AccessTools.Method("BetterSleepBruh.Patches.EnvManPatches:IsInSleepWindow");
            if (build == null || update == null || register == null || _handlers == null || _hudField == null || _registeredField == null || _tryCreate == null || _inSleepWindow == null)
            {
                Plugin.Log.LogWarning("BetterSleepBruh contract not recognised; nothing changed. Sleep boost protections stay off for clients on dedicated servers under SeneaL UI.");
                return;
            }
            var harmony = new Harmony(Owner);
            harmony.Patch(build, postfix: new HarmonyMethod(typeof(SleepHud), nameof(AfterBuild)));
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(SleepHud), nameof(BeforeModUpdate)));
            harmony.Patch(register, prefix: new HarmonyMethod(typeof(SleepHud), nameof(BeforeRegister)), postfix: new HarmonyMethod(typeof(SleepHud), nameof(AfterRegister)));
            BindWaves(harmony);
        }

        // WaterVolume.StaticUpdate runs from FixedUpdate, Update and LateUpdate. Vanilla re-anchors to the network clock each
        // call, but BetterSleepBruh's boost path adds dt on every call, so physical waves ran about 3.4x against its own
        // "decouple wave speed" intent. During a boost one call per frame, from Update, gives the real-time speed it meant.
        private static void BindWaves(Harmony harmony)
        {
            var rate = AccessTools.PropertyGetter(AccessTools.TypeByName("BetterSleepBruh.Components.SleepTracker"), "CurrentExtraRate");
            var decouple = AccessTools.Field(AccessTools.TypeByName("BetterSleepBruh.Configuration.ConfigRegistry"), "DecoupleWaterWaveSpeed");
            var target = AccessTools.Method(typeof(WaterVolume), "StaticUpdate");
            if (rate == null || decouple == null || target == null)
            {
                Plugin.Log.LogWarning("BetterSleepBruh wave contract not recognised; boosted waves stay as upstream runs them.");
                return;
            }
            _extraRate = (Func<double>)Delegate.CreateDelegate(typeof(Func<double>), rate);
            _decoupleWaves = () => decouple.GetValue(null) is BepInEx.Configuration.ConfigEntry<bool> entry && entry.Value;
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SleepHud), nameof(OneWaveStepPerFrame)));
        }

        private static bool OneWaveStepPerFrame()
        {
            if (_extraRate() <= 0.0 || !_decoupleWaves()) return true;
            if (Time.inFixedTimeStep || _waveFrame == Time.frameCount) return false;
            _waveFrame = Time.frameCount;
            return true;
        }

        internal static void Tick()
        {
            if (_hudField == null || Time.unscaledTime < _nextHome) return;
            _nextHome = Time.unscaledTime + 1f;
            // BetterSleepBruh re-arms its own build when the view is gone, so this only corrects a live view.
            if (ZNet.instance == null || ZNet.instance.HaveStopped || Player.m_localPlayer == null || Minimap.instance == null || ZRoutedRpc.instance == null) return;
            var view = CurrentView();
            if (view == null) return;
            Home(view);
            if (_foreignHandler || OwnsHandlers(ZRoutedRpc.instance, view)) return;
            Plugin.Log.LogInfo("Sleep HUD handlers were not bound to the live view; rebinding.");
            _registeredField.SetValue(view, false);
            _register.Invoke(view, null);
        }

        private static Component CurrentView() => _sleepMod == null ? null : _hudField.GetValue(_sleepMod) as Component;

        private static void AfterBuild(object __instance)
        {
            _sleepMod = __instance;
            if (CurrentView() != null) return;
            var root = Minimap.instance != null && Minimap.instance.m_smallRoot != null ? Minimap.instance.m_smallRoot.transform : null;
            if (root == null)
            {
                if (++_missingMinimap >= MaxMissingMinimap) GiveUp("Minimap was not available");
                return;
            }

            var leftover = HomeParent(root).Find(ViewName);
            if (leftover != null) UnityEngine.Object.Destroy(leftover.gameObject);
            var view = _tryCreate.Invoke(null, new object[] { root, 1f }) as Component;
            if (view == null)
            {
                GiveUp("BetterSleepBruh could not create its view");
                return;
            }
            _missingMinimap = 0;
            _hudField.SetValue(__instance, view);
            Home(view);
            Plugin.Log.LogInfo("Sleep HUD built from Minimap under " + view.transform.parent.name + ".");
            view.gameObject.SetActive(EnvMan.instance != null && (bool)_inSleepWindow.Invoke(null, new object[] { EnvMan.instance }));
        }

        // A session that gave up stays quiet. Once ZNet stops, BetterSleepBruh's own Update handles the reset.
        private static bool BeforeModUpdate(object __instance)
        {
            if (ZNet.instance != null && !ZNet.instance.HaveStopped) return !_unsupported;
            _unsupported = _foreignHandler = false;
            _missingMinimap = 0;
            return true;
        }

        // ZRoutedRpc.Register throws on a repeated name and BetterSleepBruh never unregisters, so a replacement view takes over.
        // Only entries whose delegate targets a SleepHudView are cleared. Anything else is not ours to remove.
        private static bool BeforeRegister(object __instance)
        {
            var rpc = ZRoutedRpc.instance;
            if ((bool)_registeredField.GetValue(__instance) || rpc == null) return true;
            var handlers = (IDictionary)_handlers.GetValue(rpc);
            var hashes = ViewRpcs.Select(name => name.GetStableHashCode()).Where(hash => handlers.Contains(hash)).ToList();
            if (hashes.Any(hash => Target(handlers[hash])?.GetType() != _viewType))
            {
                if (!_foreignHandler) Plugin.Log.LogWarning("Another mod owns a BetterSleepBruh RPC name; the sleep HUD did not register, so this client's sleep boost protections stay off until it rejoins.");
                _foreignHandler = true;
                return false;
            }
            foreach (var hash in hashes) handlers.Remove(hash);
            return true;
        }

        private static void AfterRegister(Component __instance)
        {
            if (!(bool)_registeredField.GetValue(__instance) || ZRoutedRpc.instance == null) return;
            var guard = __instance.GetComponent<ViewGuard>() ?? __instance.gameObject.AddComponent<ViewGuard>();
            guard.Rpc = ZRoutedRpc.instance;
        }

        private static object Target(object routedMethod)
        {
            var action = AccessTools.Field(routedMethod.GetType(), "m_action")?.GetValue(routedMethod) as Delegate;
            return action?.Target;
        }

        private static bool OwnsHandlers(ZRoutedRpc rpc, Component view)
        {
            var handlers = (IDictionary)_handlers.GetValue(rpc);
            return ViewRpcs.All(name => handlers[name.GetStableHashCode()] is object entry && ReferenceEquals(Target(entry), view));
        }

        // Clears a destroyed view's callbacks from the RPC instance it registered on, leaving any other owner's entries.
        private sealed class ViewGuard : MonoBehaviour
        {
            internal ZRoutedRpc Rpc;

            private void OnDestroy()
            {
                if (Rpc == null) return;
                var view = GetComponent(_viewType);
                var handlers = (IDictionary)_handlers.GetValue(Rpc);
                foreach (var hash in ViewRpcs.Select(name => name.GetStableHashCode()).Where(hash => handlers.Contains(hash)).ToList())
                    if (ReferenceEquals(Target(handlers[hash]), view)) handlers.Remove(hash);
            }
        }

        private static void GiveUp(string reason)
        {
            _unsupported = true;
            ((MonoBehaviour)_sleepMod).CancelInvoke("TryBuildSleepHud");
            Plugin.Log.LogWarning(reason + "; no sleep HUD this session, and this client's sleep boost protections stay off until it rejoins.");
        }

        private static Transform HomeParent(Transform root)
        {
            // A mask above the minimap clips anything anchored outside it, so the strip must sit beside the masked disc.
            var mask = root.parent != null ? root.parent.GetComponentInParent<Mask>() : null;
            return mask != null && mask.transform.parent != null ? mask.transform.parent : root;
        }

        private static void Home(Component view)
        {
            if (Minimap.instance == null || Minimap.instance.m_smallRoot == null) return;
            var root = Minimap.instance.m_smallRoot.transform;
            var parent = (RectTransform)HomeParent(root);
            var strip = (RectTransform)view.transform;
            var moved = strip.parent != parent;
            if (moved)
            {
                strip.SetParent(parent, worldPositionStays: false);
                strip.SetAsLastSibling();
                strip.localScale = Vector3.one;
            }

            // SeneaL UI's info card sits just below the map whichever root holds it, so under SeneaL the strip stays beside the map.
            var beside = parent != root || Chainloader.PluginInfos.ContainsKey(SeneaLUi);
            if (!beside)
            {
                if (!moved) return;
                strip.anchorMin = Vector2.zero;
                strip.anchorMax = Vector2.right;
                strip.pivot = new Vector2(0.5f, 1f);
                strip.sizeDelta = new Vector2(0f, StripHeight);
                strip.anchoredPosition = new Vector2(0f, -1f);
                return;
            }

            var width = parent.rect.width;
            var corners = new Vector3[4];
            parent.GetWorldCorners(corners);
            var scale = parent.lossyScale.x;
            var fitsLeft = corners[0].x - (width + StripGap) * scale >= 0f;
            var anchor = fitsLeft ? Vector2.zero : Vector2.right;
            strip.anchorMin = strip.anchorMax = anchor;
            strip.pivot = fitsLeft ? Vector2.right : Vector2.zero;
            strip.sizeDelta = new Vector2(width, StripHeight);
            strip.anchoredPosition = new Vector2(fitsLeft ? -StripGap : StripGap, 0f);
        }
    }
}
