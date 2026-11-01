using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using EpicLoot;
using HarmonyLib;
using SeneaL.API;
using UnityEngine;

namespace PoVPlus.Compat
{
    // Connects UsefulTankards' Use-to-open storage to SeneaL UI and EpicLoot 0.15. UsefulTankards keeps storage,
    // drinking, saving and ownership.
    internal static class Tankards
    {
        private const string Owner = "avesatanas.senealui.usefultankards.bridge";

        internal static void Install()
        {
            var isStorage = AccessTools.Method("UsefulTankards.TankardStorageSystem:IsTankardStorageContainer");
            var tryProfile = AccessTools.Method("UsefulTankards.TankardTweaks:TryGetProfile");
            var slots = tryProfile == null ? null : AccessTools.PropertyGetter(tryProfile.GetParameters()[1].ParameterType.GetElementType(), "TankardStorageSlots");
            if (isStorage == null || tryProfile == null || slots == null)
            {
                Plugin.Log.LogError("UsefulTankards 1.0.9 contract not found; bridge disabled, Use on tankards stays unchanged.");
                return;
            }

            ContainerDocks.Add(Owner, "tankard", (Func<Container, bool>)Delegate.CreateDelegate(typeof(Func<Container, bool>), isStorage), null);
            if (!Chainloader.PluginInfos.ContainsKey(EpicLootInput.Guid)) return;

            Func<ItemDrop.ItemData, bool> isTankard = item =>
            {
                var args = new object[] { item, null };
                return (bool)tryProfile.Invoke(null, args) && (int)slots.Invoke(args[1], null) > 0;
            };
            var problem = EpicLootInput.Bind(new Harmony(Owner), isTankard);
            if (problem != null) Plugin.Log.LogWarning(problem);
        }

        // Referenced only after EpicLoot is known to be loaded, so the dock still works without it.
        private static class EpicLootInput
        {
            internal const string Guid = "randyknapp.mods.epicloot";

            private static AccessTools.FieldRef<bool> _unavailable;
            private static Func<bool> _overlayOpen;

            [MethodImpl(MethodImplOptions.NoInlining)]
            internal static string Bind(Harmony harmony, Func<ItemDrop.ItemData, bool> isTankard)
            {
                var overlay = AccessTools.PropertyGetter(typeof(API).Assembly.GetType("EpicLoot.ShardStones.ShardUI"), "IsOpen");
                _overlayOpen = overlay == null ? () => false : (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), overlay);

                // EpicLoot yields a bare Use press over a registered container. Alt+E and an open overlay stay EpicLoot's,
                // so a socketed tankard keeps both functions: E is storage, Alt+E is its shard slots.
                if (!API.RegisterItemContainer("usefultankards.tankard", item => !SocketGesture() && isTankard(item)))
                    return "EpicLoot refused the tankard registration; a bare Use over a socketed tankard stays with EpicLoot.";

                // UsefulTankards 1.0.9 binds EpicLoot.ShardStones.SocketsUI, gone since EpicLoot 0.15, and then yields every press.
                var compat = AccessTools.TypeByName("UsefulTankards.EpicLootStorageCompatibility");
                var unavailable = compat == null ? null : AccessTools.Field(compat, "_unavailable");
                var gate = compat == null ? null : AccessTools.PropertyGetter(compat, "ShouldYieldUse");
                if (unavailable == null || gate == null)
                    return "UsefulTankards' EpicLoot gate not found; Use stays with its own check.";

                _unavailable = AccessTools.StaticFieldRefAccess<bool>(unavailable);
                harmony.Patch(gate, postfix: new HarmonyMethod(typeof(EpicLootInput), nameof(GatePostfix)));
                return null;
            }

            private static bool SocketGesture()
            {
                return _overlayOpen() || ZInput.GetKey(KeyCode.LeftAlt, false) || ZInput.GetKey(KeyCode.RightAlt, false);
            }

            // UsefulTankards tests the button first, so this runs only on a Use press.
            private static void GatePostfix(ref bool __result)
            {
                if (!_unavailable()) return;
                __result = InventoryPromptHost.IsOpen || _overlayOpen() || API.IsShardSlotPress(Hovered());
            }

            // Vanilla's lookup: real client presses showed it stays correct under SeneaL UI when SeneaL's hover state is stale.
            private static ItemDrop.ItemData Hovered()
            {
                var grid = InventoryGui.instance ? InventoryGui.instance.m_playerGrid : null;
                if (!grid) return null;
                var mouse = Input.mousePosition;
                return grid.GetItem(new Vector2i(Mathf.RoundToInt(mouse.x), Mathf.RoundToInt(mouse.y)));
            }
        }
    }
}
