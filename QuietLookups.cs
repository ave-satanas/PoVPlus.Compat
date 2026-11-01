using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace PoVPlus.Compat
{
    // Mods here probe optional mods through AccessTools, which warns on every miss. Each entry below has a traced caller and
    // a verified verdict (REPORT.md, warning ledger). Anything not listed logs exactly as before, and a listed name that does
    // exist still resolves. Misses that mean a real gap stay visible on purpose.
    internal static class QuietLookups
    {
        private static readonly HashSet<string> Types = new HashSet<string>
        {
            "kg_ItemDrawers.DrawerComponent", "ItemDrawers.ItemInfo", "ItemDrawers.Drawer", "ExtraSlots.Slots", "BetterArchery.BetterArchery",
            "BetterRiding.Behaviour.MountAttackController", "BetterRiding.Behaviour.MountAttackController+Ygg_RestrictPlayerAttack",
            "BestAutoSort.Runtime.NearbyResourceService", "QuickStackStore.CompatibilitySupport", "QuickStackStore.UserConfig",
            "Balrond.Features.Combat.EquipmentTransitionCoordinator", "RunicBuildCamera.Integration.BuildCameraRuntime",
            "ConfigurationManager.ConfigSettingEntry", "EpicLoot.MagicTooltipPatches"
        };
        private static readonly HashSet<string> NullTypeMethods = new HashSet<string> { "IsSlotFavorited", "InternalIsEquipOrQuickSlot", "GetSocketOpenHint" };
        private static readonly HashSet<string> Members = new HashSet<string>
        {
            "ZInput.GetJoyRightStickY", "PassiveTree.Equipment.Second", "PassiveTree.Equipment.On", "PassiveTree.Equipment.second",
            "PassiveTree.Gear.Module.Neutralized", "PassiveTree.Gear.Module.Restore",
            "EpicLoot.Adventure.MinimapController._adventureBountyToggle", "EpicLoot.Adventure.MinimapController._adventureTreasureToggle",
            "EpicLoot.Adventure.MinimapController._adventureToggleContainer"
        };

        internal static void Install()
        {
            var harmony = new Harmony("0.povplusplus.quietlookups");
            harmony.Patch(typeof(AccessTools).GetMethod("TypeByName", new[] { typeof(string) }), prefix: new HarmonyMethod(typeof(QuietLookups), nameof(QuietType)));
            foreach (var name in new[] { "Method", "DeclaredMethod" })
                foreach (var method in Overloads(name)) harmony.Patch(method, prefix: new HarmonyMethod(typeof(QuietLookups), nameof(QuietMethod)));
            foreach (var method in Overloads("Field")) harmony.Patch(method, prefix: new HarmonyMethod(typeof(QuietLookups), nameof(QuietField)));
            foreach (var method in Overloads("Property")) harmony.Patch(method, prefix: new HarmonyMethod(typeof(QuietLookups), nameof(QuietProperty)));
        }

        private static IEnumerable<MethodInfo> Overloads(string name) => typeof(AccessTools).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == name && !m.IsGenericMethod && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(Type) && m.GetParameters()[1].ParameterType == typeof(string));

        private static bool QuietType(string __0, ref Type __result)
        {
            if (__0 == null || !Types.Contains(__0)) return true;
            __result = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(__0, false)).FirstOrDefault(t => t != null);
            return false;
        }

        private static bool Listed(Type type, string name) => type != null && Members.Contains(type.FullName + "." + name);

        private static bool QuietMethod(Type __0, string __1, Type[] __2, ref MethodInfo __result)
        {
            if (__0 == null && NullTypeMethods.Contains(__1)) { __result = null; return false; }
            if (!Listed(__0, __1)) return true;
            // ConfigManager needs the no-argument GetJoyRightStickY; only SeneaL UI's probe for a (bool) overload is listed.
            if (__0.Name == "ZInput" && (__2 == null || __2.Length != 1 || __2[0] != typeof(bool))) return true;
            if (__2 != null ? __0.GetMethod(__1, AccessTools.all, null, __2, null) != null : __0.GetMethods(AccessTools.all).Any(m => m.Name == __1)) return true;
            __result = null;
            return false;
        }

        private static bool QuietField(Type __0, string __1, ref FieldInfo __result)
        {
            if (!Listed(__0, __1) || __0.GetField(__1, AccessTools.all) != null) return true;
            __result = null;
            return false;
        }

        private static bool QuietProperty(Type __0, string __1, ref PropertyInfo __result)
        {
            if (!Listed(__0, __1) || __0.GetProperty(__1, AccessTools.all) != null) return true;
            __result = null;
            return false;
        }
    }
}
