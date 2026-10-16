# Optional character assets

Use your engine's own rig and animation pipeline, or reference `LibAottg.Assets` for the independent character-package runtime. The exported character is bundled in `assets/character/`, including its GLB, manifest, animation rules, sockets, physics metadata and bind-pose data. It contains 22 mesh primitives, 68 named clips and seven embedded textures.

```csharp
using LibAottg.Assets;

var package = CharacterPackage.Load("assets/character");
var rig = new HeroCharacter(package);
hero.Presentation = rig;
```

A completed package supplies a manifest, GLB, skeleton, named clips and sockets. Loading validates completion, hashes and inventory. The optional `src/LibAottg.AssetTool` converts the exporter schema, rather than arbitrary model files; run it without arguments for its command usage. A game installation and capture tooling are not required to build or run the controller.

For standalone playback use `package.CreateInstance()`, `Play`, `Advance` or `Sample`. For controller playback let `HeroCharacter` follow the authoritative animation clock and procedural pose. Upload its deformed primitive positions/normals alongside package UVs, indices and materials; respect equipment visibility. Socket matrices provide attachments and hook origins.

Package coordinates are right-handed, Y-up and -Z-forward. The controller is +Z-forward. Apply the coordinate conversion and root transform once, with scale `WorldScale / package.MetersPerSourceUnit`. Preserve triangle winding and normal transforms when reflecting axes. Visual root animation does not replace motor displacement.

The host supplies materials, texture upload, audio playback and particle/UI rendering. Audio cue names are HookLaunch, BladeSwing, AhssFire, BladeHit and Death. Raw resources and manifests live under `assets/`. HUD checks can use those manifests independently of a character package.

Run the asset and host checks from the release root:

```sh
dotnet run --project tests/LibAottg.RuntimeChecks -c Release -- assets/character
dotnet run --project tests/LibAottg.HostChecks -c Release -- assets/character
```

The bundled character is one captured appearance, not a costume collection. Gun meshes are not included. Meter scale is a conversion setting rather than a calibrated measurement, and materials approximate the legacy shaders. Package metadata retains the original capture identity and warnings.
