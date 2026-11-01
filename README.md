# PoVPlus.Compat

One BepInEx plugin holding the compatibility fixes for the PoVPlus Valheim pack. The player-facing description is in `package/README.md`, and changes are in `package/CHANGELOG.md`.

No game files or third-party mods are included.

## Build

Requires the .NET SDK and an installed pack. References resolve from `GameManaged` and `Profile`, which default to a local Valheim install and a Gale `PoVPlus` profile; override them with `-p:` if yours differ.

```
dotnet build PoVPlus.Compat.csproj -c Release -o build
```

## Package

`pack.py` checks `package/` against Hexium's packaging rules and writes the release zip. It expects the built DLL at `../dist/PoVPlus.Compat.dll` and writes to `../dist/`.

Raise `version_number` in `package/manifest.json` and `Version` in `Plugin.cs` together.

## License

MIT, see `package/LICENSE`.
