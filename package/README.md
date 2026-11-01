# PoVPlus Compat

One plugin that fixes the conflicts found in the PoVPlus Valheim pack. Each fix turns itself off with a log warning if its target mod is missing or has changed, and the other fixes keep working. It adds no settings and stores no saved data.

**What it fixes**

- **Feed Barrels and ground stacking:** lets FeedLikeGrandma and FineDining keep working alongside EpicLoot and ItemDataManager.
- **Health regen gear:** EpicLoot `AddHealthRegen` works again under FineDining.
- **Tankards and rod bags:** `E` opens and closes them with EpicLoot 0.15. `Alt+E` opens shard slots on a socketed item. Tankard storage docks under SeneaL UI's inventory.
- **Burst crossbows:** a `TripleBowShot` proc affects only the first shot of a SecondaryAttacks burst.
- **Sleep HUD:** BetterSleepBruh's HUD and its boost protections work on clients under SeneaL UI, and the strip stays clear of SeneaL's info card.
- **Boosted waves:** physical waves run about 1.16× during a sleep boost instead of about 3.3×. They are not fully normalized, and waves still step when the boost ends.
- **Map filters:** SeneaL UI's Bounties and Treasure Maps chips work with EpicLoot's map pins.
- **Cooking conversions:** ValheimCuisine no longer drops Monstrum, Warfare and Wizardry cooking and fermenting recipes when it rebuilds its stations.
- **Log noise:** quiet lookups for specific optional mods that are absent. Other HarmonyX warnings still appear.

**Tested with** Valheim 1.0.21, BepInExPack 5.4.2360, EpicLoot 0.15.4, FeedLikeGrandma 1.2.1, FineDining 1.1.12, BetterSleepBruh 2.1.2, SeneaL UI 1.2.8, UsefulTankards 1.0.9 and TrollingFishing 1.1.5, ValheimCuisine 2.3.4. Those mods are not dependencies, because each fix is optional. The item stacking fix only accepts EpicLoot 0.15.4's exact code, so a different EpicLoot build makes it stand down.

**Install on the client and the server.** If your server uses a hash allow-list, every client needs the same DLL.

**Replaces** these standalone plugins, which must be removed: HealthRegenCompat, ItemDataStackCompat, RodBagInputBridge, BurstMultiShotCompat, BetterSleepHudBridge, QuietLookups and SeneaLUI UsefulTankards Bridge.

**Tested on one client on a dedicated server.** Sleep with other players was simulated, and play with two real players has not been tested.

**To remove it:** close the game and uninstall. It stores nothing, so saves are unaffected, and the original conflicts return.
