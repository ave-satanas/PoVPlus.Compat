using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace PoVPlus.Compat
{
    // ValheimCuisine snapshots the cooking stations once, in the first ZNetScene.Awake, then ReapplyConversions rebuilds them
    // from that snapshot plus its config. Monstrum, Warfare and Wizardry add theirs later in the same Awake, so the rebuild that
    // Cuisine runs after every world load drops them. Those foods then cannot be cooked and FineDining stops tracking them.
    // Adding them to Cuisine's snapshot keeps Cuisine as the owner of the rebuild, including its world instance update.
    internal static class CuisineConversions
    {
        private const string Owner = "povplus.compat.cuisineconversions";
        private const string PatchType = "ValheimCuisine.ValheimCuisinePlugin+ConversionsZNetScene_AwakePost_Patch";
        private static readonly string[][] CookingStations =
        {
            new[] { "piece_cookingstation", "vanillaCookingStationConversions" },
            new[] { "piece_cookingstation_iron", "vanillaCookingStationIronConversions" },
            new[] { "piece_oven", "vanillaOvenConversions" }
        };
        private const string FermenterPrefab = "fermenter", FermenterField = "vanillaFermenterConversions";

        // What each station held right after Cuisine's last rebuild, so its own config rows are never adopted.
        private static readonly Dictionary<string, HashSet<string>> CuisineOwned = new Dictionary<string, HashSet<string>>();
        private static Type _patch;

        internal static void Install()
        {
            _patch = AccessTools.TypeByName(PatchType);
            var reapply = _patch == null ? null : AccessTools.DeclaredMethod(_patch, "ReapplyConversions");
            var fields = CookingStations.Select(s => s[1]).Concat(new[] { FermenterField }).Select(f => _patch == null ? null : AccessTools.DeclaredField(_patch, f)).ToList();
            if (reapply == null || fields.Any(f => f == null))
            {
                Plugin.Log.LogWarning("ValheimCuisine conversion contract not recognised; nothing changed. Other mods' cooking conversions may be lost after world load.");
                return;
            }
            var harmony = new Harmony(Owner);
            harmony.Patch(reapply, postfix: new HarmonyMethod(typeof(CuisineConversions), nameof(AfterReapply)));
            // Must run after every mod's ZNetScene.Awake postfix has registered its conversions.
            harmony.Patch(AccessTools.Method(typeof(ZNetScene), "Awake"), postfix: new HarmonyMethod(typeof(CuisineConversions), nameof(AdoptForeign)) { priority = Priority.Last });
        }

        private static void AfterReapply(ZNetScene scene)
        {
            if (scene == null) return;
            foreach (var station in CookingStations)
            {
                var live = scene.GetPrefab(station[0])?.GetComponent<CookingStation>()?.m_conversion;
                if (live != null) CuisineOwned[station[0]] = new HashSet<string>(live.Select(Key));
            }
            var fermenter = scene.GetPrefab(FermenterPrefab)?.GetComponent<Fermenter>()?.m_conversion;
            if (fermenter != null) CuisineOwned[FermenterPrefab] = new HashSet<string>(fermenter.Select(Key));
        }

        private static void AdoptForeign(ZNetScene __instance)
        {
            foreach (var station in CookingStations)
            {
                var live = __instance.GetPrefab(station[0])?.GetComponent<CookingStation>()?.m_conversion;
                var snapshot = AccessTools.DeclaredField(_patch, station[1]).GetValue(null) as List<CookingStation.ItemConversion>;
                if (live == null || snapshot == null || !CuisineOwned.TryGetValue(station[0], out var owned)) continue;
                var kept = new HashSet<string>(snapshot.Select(Key));
                foreach (var conversion in Foreign(live, Key, owned, kept))
                    snapshot.Add(new CookingStation.ItemConversion { m_from = conversion.m_from, m_to = conversion.m_to, m_cookTime = conversion.m_cookTime });
            }

            var fermenter = __instance.GetPrefab(FermenterPrefab)?.GetComponent<Fermenter>()?.m_conversion;
            var fermenterSnapshot = AccessTools.DeclaredField(_patch, FermenterField).GetValue(null) as List<Fermenter.ItemConversion>;
            if (fermenter == null || fermenterSnapshot == null || !CuisineOwned.TryGetValue(FermenterPrefab, out var fermenterOwned)) return;
            var fermenterKept = new HashSet<string>(fermenterSnapshot.Select(Key));
            foreach (var conversion in Foreign(fermenter, Key, fermenterOwned, fermenterKept))
                fermenterSnapshot.Add(new Fermenter.ItemConversion { m_from = conversion.m_from, m_to = conversion.m_to, m_producedItems = conversion.m_producedItems });
        }

        // Mods re-add their entries on each world load, so kept also drops repeats within one pass.
        private static IEnumerable<T> Foreign<T>(IEnumerable<T> live, Func<T, string> key, HashSet<string> owned, HashSet<string> kept)
        {
            return live.Where(c => key(c) != null && !owned.Contains(key(c)) && kept.Add(key(c))).ToList();
        }

        private static string Key(CookingStation.ItemConversion c) => c.m_from == null || c.m_to == null ? null : c.m_from.name + ">" + c.m_to.name;
        private static string Key(Fermenter.ItemConversion c) => c.m_from == null || c.m_to == null ? null : c.m_from.name + ">" + c.m_to.name;
    }
}
