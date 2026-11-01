using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;

namespace PoVPlus.Compat
{
    // The pack's compatibility fixes in one plugin. Each class keeps its own Harmony owner, guards and refusal behaviour.
    // QuietLookups has to run before other mods probe in their Awake, so this GUID sorts first and declares no dependencies.
    // Everything else needs all Awakes finished but must precede the FeedLikeGrandma and FineDining contract checks in their
    // Start, so it runs from a prefix on those Starts, with this plugin's own Start as the fallback.
    [BepInPlugin(Guid, "PoVPlus.Compat", Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "0.povplus.compat";
        internal const string Version = "1.0.1";

        internal static ManualLogSource Log;
        private static bool _deferredRan;
        private static readonly Harmony StartHooks = new Harmony("povplus.compat.starthooks");

        private void Awake()
        {
            Log = Logger;
            Install("quiet lookups", new string[0], QuietLookups.Install);
            AppDomain.CurrentDomain.AssemblyLoad += HookContractStart;
        }

        private void Start() => RunDeferred();

        private void Update() => SleepHud.Tick();

        private static void HookContractStart(object sender, AssemblyLoadEventArgs args)
        {
            var name = args.LoadedAssembly.GetName().Name;
            var type = name == "FeedLikeGrandma" ? "FeedLikeGrandma.FeedLikeGrandmaPlugin" : name == "FineDining" ? "FineDining.FineDiningPlugin" : null;
            if (type == null) return;
            var start = AccessTools.DeclaredMethod(args.LoadedAssembly.GetType(type), "Start");
            if (start != null) StartHooks.Patch(start, prefix: new HarmonyMethod(typeof(Plugin), nameof(RunDeferred)));
            else Log.LogWarning(name + " has no Start to precede; its contract check may run before ItemDataStack does.");
        }

        private static void RunDeferred()
        {
            if (_deferredRan) return;
            _deferredRan = true;
            AppDomain.CurrentDomain.AssemblyLoad -= HookContractStart;
            const string epicLoot = "randyknapp.mods.epicloot", seneal = "seneaL.valheim.api";
            var installed = new List<string>();
            Action<string, string[], Action> run = (name, needs, body) => { if (Install(name, needs, body)) installed.Add(name); };
            run("item data stacks", new[] { epicLoot }, ItemDataStack.Install);
            run("health regen", new[] { epicLoot, "sighsorry.FineDining" }, HealthRegen.Install);
            run("rod bag", new[] { "sighsorry.TrollingFishing", epicLoot, seneal }, RodBag.Install);
            run("tankards", new[] { "sighsorry.UsefulTankards", seneal }, Tankards.Install);
            run("burst multishot", new[] { epicLoot, "sighsorry.SecondaryAttacks" }, BurstMultiShot.Install);
            run("sleep hud", new[] { "vapok.mods.BetterSleepBruh" }, SleepHud.Install);
            run("map chips", new[] { epicLoot, "seneaL.valheim.ui" }, EpicMapChips.Install);
            run("cuisine conversions", new[] { "XutzBR.ValheimCuisine" }, CuisineConversions.Install);
            Log.LogMessage("PoVPlus.Compat " + Version + ": " + string.Join(", ", installed.ToArray()));
        }

        // One integration failing or missing must not take the others down.
        private static bool Install(string name, string[] needs, Action body)
        {
            foreach (var guid in needs)
            {
                if (Chainloader.PluginInfos.ContainsKey(guid)) continue;
                Log.LogWarning(name + " skipped: " + guid + " is not installed.");
                return false;
            }
            try
            {
                body();
                return true;
            }
            catch (Exception e)
            {
                Log.LogError(name + " failed and changed nothing further: " + e.GetBaseException());
                return false;
            }
        }
    }
}
