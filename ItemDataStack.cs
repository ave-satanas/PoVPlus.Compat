using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;

namespace PoVPlus.Compat
{
    // FeedLikeGrandma 1.2.1 and FineDining 1.1.12 support EpicLoot's replacement for ItemDrop.AutoStackItems, but both
    // refuse any other transpiler on that method. ItemDataManager, bundled in several mods here, adds one. EpicLoot 0.15.4's
    // prefix returns false on every path, so that transpiler is dead code; the contracts just cannot prove it.
    // ItemDataManager patches from a lazy static constructor and FeedLikeGrandma rechecks on every dispense, so a late
    // first use would stop the barrels mid-session. Running the constructors here means none can fire after the removal.
    // Anything but the exact EpicLoot prefix declines, which leaves their contracts to stop dispensing safely.
    internal static class ItemDataStack
    {
        private const string Owner = "povplusplus.itemdatastackcompat";
        private const string EpicLootGuid = "randyknapp.mods.epicloot";
        private const string EpicLootPrefixType = "EpicLoot.MagicItemData+ItemDrop_AutoStackItems_Patch";
        private const string EpicLootPrefixIl = "A040E65A21FE2EB36C13917D40ABC92794713DC367859D40E64381439F1971D3";
        private const string TranspilerName = "HandleAutostackableItems";
        private static readonly string[] ItemInfoTypes = { "ItemDataManager.ItemInfo", "Vapok.Common.Managers.ItemInfo" };

        internal static void Install()
        {
            var target = AccessTools.DeclaredMethod(typeof(ItemDrop), "AutoStackItems");
            var patches = target == null ? null : Harmony.GetPatchInfo(target);
            var prefixes = patches?.Prefixes.Where(p => p.owner == EpicLootGuid).ToList();
            // Without EpicLoot's replacement the transpiler is live code, so it stays.
            if (prefixes == null || prefixes.Count == 0) return;
            if (prefixes.Count != 1 || !IsKnownReplacement(prefixes[0].PatchMethod))
            {
                Plugin.Log.LogWarning("EpicLoot's AutoStackItems prefix is not the one this patch verified; ItemDataManager's transpiler was left alone.");
                return;
            }

            var bundled = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => ItemInfoTypes.Select(n => a.GetType(n, false))).Where(t => t != null).ToList();
            foreach (var type in bundled)
                try { RuntimeHelpers.RunClassConstructor(type.TypeHandle); }
                catch (Exception e) { Plugin.Log.LogWarning("Could not initialise " + type.Assembly.GetName().Name + "'s ItemInfo: " + e.GetBaseException().Message); }

            var harmony = new Harmony(Owner);
            var dead = Harmony.GetPatchInfo(target).Transpilers.Where(p => p.PatchMethod.Name == TranspilerName && bundled.Contains(p.PatchMethod.DeclaringType)).ToList();
            foreach (var patch in dead)
                harmony.Unpatch(target, patch.PatchMethod);
            Plugin.Log.LogInfo("Removed " + dead.Count + " unreachable ItemDataManager transpiler(s) from " + bundled.Count + " bundled copies.");
        }

        private static bool IsKnownReplacement(MethodInfo prefix)
        {
            if (prefix.DeclaringType?.FullName != EpicLootPrefixType || prefix.Name != "Prefix") return false;
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(prefix.GetMethodBody().GetILAsByteArray())).Replace("-", "") == EpicLootPrefixIl;
        }
    }
}
