using System;
using HarmonyLib;

namespace PoVPlus.Compat
{
    // EpicLoot's multishot multiplies Attack.m_projectiles for one shot and scales that shot's damage, then clears only
    // the damage scale. SecondaryAttacks' burst fires its later shots without going back through FireProjectileBurst, so
    // they inherit the multiplied count at full damage. SecondaryAttacks has its own compat that restores the count, but
    // it no longer binds to EpicLoot 0.15. This restores the count when EpicLoot's scale ends, and only while that is so.
    internal static class BurstMultiShot
    {
        private const string Owner = "povplusplus.burstmultishotcompat";

        private static AccessTools.FieldRef<Attack> _scaledAttack;
        private static AccessTools.FieldRef<int> _shotProjectiles;
        private static Func<bool> _secondaryAttacksCompatActive;

        internal static void Install()
        {
            var multiShot = AccessTools.TypeByName("EpicLoot.MagicItemEffects.MultiShot");
            var scaled = multiShot == null ? null : AccessTools.Field(multiShot, "_scaledBurstAttack");
            var count = multiShot == null ? null : AccessTools.Field(multiShot, "ShotProjectiles");
            var compat = AccessTools.TypeByName("SecondaryAttacks.EpicLootCompat");
            var api = compat == null ? null : AccessTools.Field(compat, "_api");
            var burst = AccessTools.Method(typeof(Attack), "FireProjectileBurst");
            if (scaled == null || count == null || api == null || burst == null)
            {
                Plugin.Log.LogWarning("EpicLoot or SecondaryAttacks contract not recognised; nothing changed.");
                return;
            }

            _scaledAttack = AccessTools.StaticFieldRefAccess<Attack>(scaled);
            _shotProjectiles = AccessTools.StaticFieldRefAccess<int>(count);
            _secondaryAttacksCompatActive = () => api.GetValue(null) != null;
            // Must run before EpicLoot's finalizer clears _scaledBurstAttack.
            new Harmony(Owner).Patch(burst, finalizer: new HarmonyMethod(typeof(BurstMultiShot), nameof(RestoreProjectileCount)) { priority = Priority.First });
        }

        private static void RestoreProjectileCount(Attack __instance)
        {
            if (_secondaryAttacksCompatActive() || _scaledAttack() != __instance) return;
            var multiplier = _shotProjectiles();
            if (multiplier > 1) __instance.m_projectiles /= multiplier;
        }
    }
}
