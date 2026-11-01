using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PoVPlus.Compat
{
    // EpicLoot replaced its bounty and treasure map toggles with pin filters in the vanilla panel, which SeneaL UI's map
    // covers, and SeneaL UI still looks for the old toggles, so its two chips hide and the filters cannot be reached.
    // This hands SeneaL's existing chips a Toggle wired to the vanilla filter that EpicLoot's own icons use.
    internal static class EpicMapChips
    {
        private const string Owner = "povplus.compat.epicmapchips";

        private static readonly Toggle[] Proxies = new Toggle[2];
        private static System.Reflection.MethodInfo _bountyPin, _treasurePin;
        private static System.Reflection.MethodInfo _toggleFilter;
        private static readonly AccessTools.FieldRef<Minimap, bool[]> VisibleTypes = AccessTools.FieldRefAccess<Minimap, bool[]>("m_visibleIconTypes");

        internal static void Install()
        {
            var chips = AccessTools.TypeByName("SeneaLUI.Mods.EpicMap");
            var toggle = chips == null ? null : AccessTools.Method(chips, "Toggle");
            var enabled = chips == null ? null : AccessTools.PropertyGetter(chips, "Enabled");
            var epicLoot = AccessTools.TypeByName("EpicLoot.EpicLoot");
            var bounty = epicLoot == null ? null : AccessTools.PropertyGetter(epicLoot, "BountyPinType");
            var treasure = epicLoot == null ? null : AccessTools.PropertyGetter(epicLoot, "TreasureMapPinType");
            _toggleFilter = AccessTools.Method(typeof(Minimap), "ToggleIconFilter");
            if (toggle == null || enabled == null || bounty == null || treasure == null || _toggleFilter == null)
            {
                Plugin.Log.LogWarning("SeneaL UI or EpicLoot map contract not recognised; bounty and treasure chips stay hidden.");
                return;
            }
            _bountyPin = bounty;
            _treasurePin = treasure;
            var harmony = new Harmony(Owner);
            harmony.Patch(toggle, postfix: new HarmonyMethod(typeof(EpicMapChips), nameof(ChipToggle)));
            harmony.Patch(enabled, postfix: new HarmonyMethod(typeof(EpicMapChips), nameof(ChipsEnabled)));
        }

                // EpicLoot assigns its pin types during game setup, so they are read when SeneaL asks rather than at install.
        private static Minimap.PinType Pin(int i) => (Minimap.PinType)(i == 0 ? _bountyPin : _treasurePin).Invoke(null, null);

        private static bool Ready => Minimap.instance != null && (int)Pin(0) > 0 && (int)Pin(1) > 0 && (int)Pin(1) < VisibleTypes(Minimap.instance).Length;

        private static void ChipsEnabled(ref bool __result) => __result |= Ready;

        private static void ChipToggle(int i, ref Toggle __result)
        {
            if (__result != null || !Ready || i < 0 || i >= Proxies.Length) return;
            var proxy = Proxies[i];
            if (proxy == null)
            {
                var pin = Pin(i);
                proxy = Proxies[i] = new GameObject("PoVPlus_EpicMapChip" + i, typeof(RectTransform)).AddComponent<Toggle>();
                proxy.transform.SetParent(Minimap.instance.transform, false);
                proxy.onValueChanged.AddListener(on =>
                {
                    if (Minimap.instance != null && VisibleTypes(Minimap.instance)[(int)pin] != on)
                        _toggleFilter.Invoke(Minimap.instance, new object[] { pin });
                });
            }
            proxy.SetIsOnWithoutNotify(VisibleTypes(Minimap.instance)[(int)Pin(i)]);
            __result = proxy;
        }
    }
}
