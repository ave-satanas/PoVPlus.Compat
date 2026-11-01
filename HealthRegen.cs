using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PoVPlus.Compat
{
    // FineDining replaces Player.UpdateFood, so EpicLoot's transpiler that adds the AddHealthRegen magic effect sits in
    // a body that never runs. This inserts EpicLoot's own AddHealthTicks where FineDining heals, keeping vanilla's
    // (regen + bonus) * multiplier order. FineDining already handles the multiplier and PoV's regen cadence.
    internal static class HealthRegen
    {
        private const string Owner = "povplusplus.healthregencompat";

        private static MethodInfo _healFood, _addHealthTicks;

        internal static void Install()
        {
            var updateFood = AccessTools.Method("FineDining.PlayerFoodLogic:UpdateFood");
            _healFood = AccessTools.Method("FineDining.PathOfValheimanCompatibility:HealFood");
            _addHealthTicks = AccessTools.Method("EpicLoot.MagicItemEffects.ModifyPlayerRegen+SEMan_ModifyRegen_Patches:AddHealthTicks");
            if (updateFood == null || _healFood == null || _addHealthTicks == null)
            {
                Plugin.Log.LogWarning("FineDining or EpicLoot contract not recognised; nothing changed.");
                return;
            }
            new Harmony(Owner).Patch(updateFood, transpiler: new HarmonyMethod(typeof(HealthRegen), nameof(AddEpicLootRegen)));
        }

        private static IEnumerable<CodeInstruction> AddEpicLootRegen(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var heal = code.FindIndex(c => c.Calls(_healFood));
            // Shape is: regen, multiplier, mul, HealFood.
            if (heal >= 3 && code.Count(c => c.Calls(_healFood)) == 1 && !code.Any(c => c.Calls(_addHealthTicks))
                && code[heal - 1].opcode == OpCodes.Mul && code[heal - 2].IsLdloc() && code[heal - 3].IsLdloc())
                code.Insert(heal - 2, new CodeInstruction(OpCodes.Call, _addHealthTicks));
            else
                Plugin.Log.LogWarning("FineDining's food heal has changed shape; AddHealthRegen was not added.");
            return code;
        }
    }
}

