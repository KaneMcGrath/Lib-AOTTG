# lib-aottg-cs

An engine-independent C# library for implementing an Attack on Titan Tribute Game (AoTTG) HERO in another game. Movement, hooks, resources, weapons and action timing follow RCMod mechanics and reference data. Your game supplies collision, input, rendering and combat integration.

`LibAottg.Core` targets .NET Standard 2.0 without external packages. The optional `LibAottg.Assets` runtime targets .NET Standard 2.0 and .NET 10 and uses SharpGLTF for character packages. The C++20 port is distributed separately as **lib-aottg-cpp**. Public C# namespaces and assembly names remain `LibAottg` for API compatibility.

## Build and try

Install the .NET 10 SDK to build the full solution and examples. From this directory:

```sh
dotnet build lib-aottg-cs.sln -c Release
dotnet run --project examples/ConsoleHost -c Release
dotnet run --project tests/LibAottg.Smoke -c Release
```

Reference `src/LibAottg.Core/LibAottg.Core.csproj` from your host project, or build and reference its assembly in an engine supporting .NET Standard 2.0.

```csharp
using LibAottg;

var config = HeroStats.RcModPresets()["Mikasa"].ToConfig();
config.WorldScale = 1; // host units per AoTTG unit
var world = new CalibrationHeroWorld(worldScale: config.WorldScale);
var hero = new HeroController(config, world);
hero.Respawn(Vec3.Zero, grounded: true);

// Each host frame:
hero.Advance(frameSeconds, new HeroInputFrame {
    MoveY = 1,
    HasView = true,
    ViewForward = new Vec3(0, 0, 1),
    AimOrigin = hero.Motor.Position + Vec3.Up,
    AimDirection = new Vec3(0, 0, 1)
});
// Apply hero.RenderPose to your rendered character.
```

Read [host integration](docs/Integration.md) for timing, units, engine collision, animation, combat and lifecycle. Read [character assets](docs/Assets.md) for optional GLB playback. `unity/LibAottgUnityMotor.cs` demonstrates a Unity Rigidbody adapter; `examples/ConsoleHost` runs without an engine or installed game.

## Layout

- `src/LibAottg.Core`: controller, motor and optional presentation systems.
- `src/LibAottg.Assets`: character packages, animation, skinning and sockets.
- `src/LibAottg.AssetTool`: optional capture-to-package converter.
- `assets`: exported character package, audio, smoke and UI resources and manifests.
- `tests`: standalone regressions and recorded reference fixtures; asset checks use the bundled `assets/character` package.

The static calibration collider is limited to boxes and a floor. Engine collision, complete skill effects, enemy interactions and rendered fidelity require host work and validation. The momentum-preserving `NoStopJump` option is an extension and is disabled by default.

## License

Original library code is licensed under the [MIT License](LICENSE). Extracted game assets retain their original ownership and licensing; see [third-party notices](THIRD_PARTY.md).
