using System;
using EpicLoot;
using HarmonyLib;
using SeneaL.API;
using UnityEngine;

namespace PoVPlus.Compat
{
    // TrollingFishing binds EpicLoot.ShardStones.SocketsUI, which EpicLoot 0.15 no longer has. The failed bind makes
    // EpicLootBagCompatibility.ShouldYieldUse permanently true, so Use over a rod never opens or closes its bag.
    // Same contract as the tankards bridge: E is the bag, Alt+E is a socketed rod's shard slots, and EpicLoot keeps its
    // prompts and open overlay. TrollingFishing keeps all bag, storage and saving logic.
    internal static class RodBag
    {
        private const string Owner = "povplusplus.rodbaginputbridge";

        private static AccessTools.FieldRef<bool> _unavailable;
        private static Func<bool> _overlayOpen;

        internal static void Install()
        {
            var compat = AccessTools.TypeByName("TrollingFishing.EpicLootBagCompatibility");
            var unavailable = compat == null ? null : AccessTools.Field(compat, "_unavailable");
            var gate = compat == null ? null : AccessTools.PropertyGetter(compat, "ShouldYieldUse");
            var isRod = AccessTools.Method("TrollingFishing.FishingOverrideSystem:IsFishingRod");
            var isBag = AccessTools.Method("TrollingFishing.FishingOverrideSystem:IsFishingRodBagContainer");
            if (unavailable == null || gate == null || isRod == null || isBag == null)
            {
                Plugin.Log.LogWarning("TrollingFishing 1.1.5 input contract not recognised; nothing changed.");
                return;
            }

            // Undocked, the bag shifts the inventory sideways and the next press lands on a different item.
            ContainerDocks.Add(Owner, "rodbag", (Func<Container, bool>)Delegate.CreateDelegate(typeof(Func<Container, bool>), isBag), null);

            var overlay = AccessTools.PropertyGetter(typeof(API).Assembly.GetType("EpicLoot.ShardStones.ShardUI"), "IsOpen");
            _overlayOpen = overlay == null ? () => false : (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), overlay);
            var rod = (Func<ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, bool>), isRod);

            // EpicLoot yields a bare Use press over a registered container; Alt+E and an open overlay stay EpicLoot's.
            if (!API.RegisterItemContainer("trollingfishing.rodbag", item => !SocketGesture() && rod(item)))
                Plugin.Log.LogWarning("EpicLoot refused the rod registration; a bare Use over a socketed rod stays with EpicLoot.");

            _unavailable = AccessTools.StaticFieldRefAccess<bool>(unavailable);
            new Harmony(Owner).Patch(gate, postfix: new HarmonyMethod(typeof(RodBag), nameof(EpicLootGatePostfix)));
        }

        private static bool SocketGesture()
        {
            return _overlayOpen() || ZInput.GetKey(KeyCode.LeftAlt, false) || ZInput.GetKey(KeyCode.RightAlt, false);
        }

        // Only while TrollingFishing's own binding is the broken one. TrollingFishing reads this gate every frame, so
        // the hovered item is looked up only on a real Use press.
        private static void EpicLootGatePostfix(ref bool __result)
        {
            if (!_unavailable()) return;
            if (InventoryPromptHost.IsOpen || _overlayOpen()) { __result = true; return; }
            if (!ZInput.GetButtonDown("Use")) { __result = false; return; }
            var grid = InventoryGui.instance ? InventoryGui.instance.m_playerGrid : null;
            var mouse = Input.mousePosition;
            __result = grid && API.IsShardSlotPress(grid.GetItem(new Vector2i(Mathf.RoundToInt(mouse.x), Mathf.RoundToInt(mouse.y))));
        }
    }
}
