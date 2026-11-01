# PoVPlus.Compat

One BepInEx plugin holding the compatibility fixes for the PoVPlus Valheim pack. The player-facing description is in `package/README.md`, and changes are in `package/CHANGELOG.md`.

No game files or third-party mods are included.

## Build

Requires the .NET SDK and an installed pack. References resolve from `GameManaged` and `Profile`, which default to a local Valheim install and a Gale `PoVPlus` profile; override them with `-p:` if yours differ.

```
dotnet build PoVPlus.Compat.csproj -c Release -o build
```

## Package

`pack.py` checks `package/` against Hexium's packaging rules and writes the release zip to `../dist/`, outside this repository. It reads the DLL from there, so copy the build first:

```
mkdir ../dist
cp build/PoVPlus.Compat.dll ../dist/
python pack.py
```

## Reproducibility

The source builds to the same method bodies as the release, but the file hash depends on the build path, which is embedded with the debug information. Use `SHA256SUMS.txt` from the release to identify the shipped DLL, and do not expect a clone at another path to match it.

Raise `version_number` in `package/manifest.json` and `Version` in `Plugin.cs` together.

## License

MIT, see `package/LICENSE`.
