# PoVPlus.Compat

One BepInEx plugin with compatibility fixes for the PoVPlus Valheim pack. No game files or third-party mods are included. Player-facing notes are in [`package/README.md`](package/README.md), and changes are in [`package/CHANGELOG.md`](package/CHANGELOG.md).

This fixes specific demonstrated conflicts. It does not make the pack fully compatible.

**Evidence and audit guide:** [AUDIT.md at the pinned commit](https://github.com/ave-satanas/PoVPlus.Review/blob/cb378b8c0cbe138d2af3a712563e669bffc1f23a/AUDIT.md). The latest version, which may move, is on [`main`](https://github.com/ave-satanas/PoVPlus.Review).

**Tested source:** the 1.0.1 release was built from the source at commit [`eed8d74`](https://github.com/ave-satanas/PoVPlus.Compat/tree/eed8d74b3700a044eb9dbce7053fe0ec2018ca69). Later commits change documentation and tools only. Check with `git diff eed8d74 HEAD -- "*.cs" "*.csproj"`.

## Modules

Each runs independently. If its target is missing or changed, it logs and stands down, and the rest continue.

| Module | Corrects | Guarded contract |
|---|---|---|
| `ItemDataStack` | ItemDataManager's dead transpiler blocks FeedLikeGrandma and FineDining's EpicLoot integration | Exact EpicLoot prefix, by IL SHA-256 |
| `HealthRegen` | FineDining's `UpdateFood` loses EpicLoot `AddHealthRegen` | IL shape around `HealFood`; refuses if already present |
| `BurstMultiShot` | SecondaryAttacks bursts inherit multiplied projectiles | EpicLoot `MultiShot` fields; stands down if upstream binds |
| `RodBag` | EpicLoot 0.15 broke Use on fishing rods | TrollingFishing gate; acts only while its bind is failed |
| `Tankards` | Same for tankards, plus docking under SeneaL UI | UsefulTankards gate and storage members |
| `SleepHud` | BetterSleepBruh's HUD and RPCs fail under SeneaL UI; boosted waves ran about 3.3× | BetterSleepBruh view and RPC members; foreign handlers left alone |
| `QuietLookups` | Quiets exact, traced optional-mod probes | Listed names only; never fabricates a result |
| `EpicMapChips` | SeneaL UI's bounty and treasure chips were dead | SeneaL `EpicMap`, EpicLoot pin properties |
| `CuisineConversions` | ValheimCuisine's rebuild dropped Monstrum, Warfare and Wizardry conversions | Cuisine's private snapshot fields and `ReapplyConversions` |

## Tested versions

Valheim 1.0.21, BepInExPack 5.4.2360, EpicLoot 0.15.4, FeedLikeGrandma 1.2.1, FineDining 1.1.12, BetterSleepBruh 2.1.2, SeneaL UI 1.2.8, SeneaL API 1.2.0, UsefulTankards 1.0.9, TrollingFishing 1.1.5, SecondaryAttacks 1.2.21, ValheimCuisine 2.3.4, Monstrum 1.6.1, Warfare 1.9.6, Wizardry 1.2.3.

Tested with one client on a dedicated server. Sleep was simulated. Most feature checks predate 1.0.1; the audit guide says which.

## Build

Needs the .NET SDK and your own legally obtained copies. SDK 10.0.100 was used.

| Reference | Property | Default |
|---|---|---|
| Valheim `valheim_Data/Managed` | `GameManaged` | The original machine's Steam library, so set it |
| BepInEx folder of a mod profile | `Profile` | `%APPDATA%\com.kesomannen.gale\valheim\profiles\PoVPlus\BepInEx` |

`Profile` must contain `core/BepInEx.dll`, `core/0Harmony.dll`, `plugins/RandyKnapp-EpicLoot/EpicLoot.dll` and `plugins/seneaL-SeneaL_API/SeneaL-API/SeneaL-API.dll`.

```
dotnet build PoVPlus.Compat.csproj -c Release -o build -p:GameManaged=<path> -p:Profile=<path>
```

Reading those paths is the only access the build needs. It does not install anything.

## Package

`pack.py` needs Python 3, and reads and writes `../dist/`, outside this repository:

```
mkdir ../dist
cp build/PoVPlus.Compat.dll ../dist/
python pack.py
```

If `../dist` exists, skip the `mkdir`. Raise `version_number` in `package/manifest.json` and `Version` in `Plugin.cs` together.

## Comparing a build

The file hash embeds the build path, so a rebuild elsewhere will not match the release hash. Compare the managed image with PowerShell 7. In a git clone, the SDK stamps the commit into the version string, so turn that off:

```
dotnet build PoVPlus.Compat.csproj -c Release -o build -p:GameManaged=<path> -p:Profile=<path> -p:IncludeSourceRevisionInInformationalVersion=false
pwsh -File tools/il-digest.ps1 -Path build/PoVPlus.Compat.dll
```

The tested release gives 124 method bodies, 37 types and Image SHA-256 `F2B5F7F0ECC30338D23C091EBFC1ADC4106119FC8470324C4154F4F02016CC29`. That digest covers IL, exception handlers, strings, signatures, attributes and references. It excludes the module ID, PE headers and debug data, which vary between builds.

IL SHA-256 `1DC63390417AF3FA3B0626A8DBE6BFDA221EE0CB70BB65AAC29195374B20DF1D` is method instructions only, and does not see a changed string. Do not treat it as proof of equivalence. `tools/il-digest.test.ps1` shows the difference.

A match needs the same referenced assemblies and a similar compiler, so a mismatch needs investigating but does not by itself prove different source. A match shows the same managed content, not that the rebuild is byte-identical. The exact tested DLL is in the review package linked above, not in this repository.

## Known limitations

- Several modules depend on private members of other mods, so updates need rechecking.
- `ItemDataStack` accepts one exact EpicLoot build. Others decline, and feeding stops by design.
- Boosted waves run about 1.16×, not 1×, and still step when a boost ends.
- A finished cook of the restored foods has not been run on 1.0.1.
- Two-player behaviour is untested.

Agents: read [AGENTS.md](AGENTS.md).

## License

MIT, see [`package/LICENSE`](package/LICENSE).
