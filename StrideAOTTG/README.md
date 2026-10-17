# StrideAOTTG demo

A playable Windows x64 demo of LibAottg using Stride 4.3.0.2507 and Direct3D 11.
It references the sibling `lib-aottg-cs` projects and bundled assets directly.
No original game installation, private captures or extraction tools are required.

## Build and run

Install the .NET 10 SDK on Windows. From the repository root:

```powershell
dotnet build StrideAOTTG/StrideAottg.sln -c Release
dotnet run --project StrideAOTTG/StrideAottg.Windows -c Release --no-build
```

Alternatively, open `StrideAottg.sln` in Visual Studio and select
`StrideAottg.Windows` as the startup project. Close the demo before rebuilding.
The build restores Stride packages through NuGet.

The demo starts in [The City](docs/City.md). For the calibration sandbox:

```powershell
dotnet run --project StrideAOTTG/StrideAottg.Windows -c Release --no-build -- --sandbox
```

Optional arguments are `--scale <number>` (host units per AoTTG unit, default 1)
and `--package <directory>` (an alternative completed character package).

## Features

Both maps support HERO movement, jumps, gas, hooks and reeling, blades and guns,
source stat/skill presets, supply, flares, grab/escape, death and respawn. The
sandbox includes training targets and moving hook anchors. The City provides
textured scenery, rooftops, collision and original spawn positions.

Presentation includes the animated character, procedural posing, three camera
modes, ropes, native audio, smoke trails, damage/style HUD, KillInfo feed,
resource HUD, crosshairs and speed readouts. Options provide saved bindings,
gameplay/camera/effects settings and fullscreen controls. The clip inspector
remains available for exploring the 68 bundled animation clips.

## Controls

Press **Escape** for the options menu. It pauses gameplay and releases the cursor. Click a primary or alternate binding, then press a keyboard key or mouse button. Backspace/Delete clears the selected slot; Escape cancels capture. Shared bindings are allowed, with a notice identifying other assigned actions. Escape remains reserved so the menu is always accessible. Restore Default Bindings resets controls; Resume or Escape returns to gameplay after held inputs are released.

The fullscreen button switches between fullscreen and windowed play; **F11** is the default shortcut and can be rebound. Controls and fullscreen preference save automatically to `%LOCALAPPDATA%/StrideAottg/options.json`. The menu includes actions, movement/camera, demo controls and clip inspector controls. Mouse movement and wheel reeling remain available alongside the configurable buttons.

The default movement/hook/weapon bindings follow AoTTG controls, with separate keys for air burst and ground dodge. Hosts remain free to map different devices to the library's abstract actions.

Reel-in takes priority when reel-in and reel-out are held together, including mixed button/wheel input. Hold reel-out and press/release reel-in to switch quickly between inward and outward motion. Wheel directions supply full-strength reel input, matching AOTTG.

The **Gameplay** tab lets you disable double-tap movement to air burst. The setting saves automatically, applies on resume and survives respawns and preset changes. The explicit burst binding remains available when double tapping is disabled. Ground dodge requires ground contact and uses no gas; air burst requires gas.

[Wall-jump bursts](../lib-aottg-cs/docs/Integration.md) preserve the original speed exploit: release W during a wall run, then hold S and press Left Ctrl while jumping backward. The boost keeps its full impulse and skips burst slowdown; gas cost and cooldown still apply. Double-tap S also works when enabled.

The **Camera** tab exposes AOTTG's camera distance (0–1), mouse speed (0.01–1), invert mouse, and camera tilt. Drag the sliders or use +/- for steps of 0.01; the displayed values use AOTTG's menu units. Preferences save automatically and apply on resume, surviving respawns and preset changes. Restore AOTTG Camera Defaults selects distance 1, mouse speed 0.5, inversion off, and tilt on. F6 also saves the tilt preference. Original mode uses cursor-height pitch and ignores invert mouse, matching the source. LibAottg owns the settings conversion and camera behavior through `HeroCameraConfig`; see [camera integration](../lib-aottg-cs/docs/Integration.md).

| Control | Action |
| --- | --- |
| WASD | Move relative to the camera; optional directional double taps air burst |
| Left Shift | Jump; hold in air for gas thrust/spin |
| Left Ctrl | Air burst |
| Left Alt | Ground dodge |
| Q / E | Hold left / right hook; release to detach |
| Space / B | Hold both hooks |
| Wheel down / up | Reel in / out; Z / X are continuous alternatives |
| Mouse | Camera/aim according to the selected camera mode |
| Left mouse / F | Blade attack; gun aim/hold and release to fire |
| Right mouse / C | Named blade skill; gun aim/hold and release to fire both |
| R / T / V | Reload / supply / flare |
| F2 | Cycle TPS, Original and WOW camera modes |
| F3 | Cycle eight source stat/skill presets and respawn |
| F4 | Switch blades/guns while no action is active |
| F5 / F6 | Toggle procedural body/rig pose / camera tilt |
| F7 | Toggle smoke trail |
| G / K / Home | Demonstrate grab/escape / death / respawn |
| Tab | Release/capture cursor; gameplay input pauses while cursor is free |
| Left/right/up/down arrows | Keyboard camera turn/elevation fallback |
| F1 | Enter/leave the captured clip inspector |
| Escape | Open options / resume; cancel binding capture |
| F11 | Toggle fullscreen |

TPS captures the cursor and aims at screen center. Original uses a free cursor with edge turning and cursor-height pitch. WOW uses right mouse for camera orbit; use **C** for its skill input. Focus loss releases the cursor and pauses simulation. The yellow marker shows the resolved aim point; a cursor/center crosshair shows the chosen aim ray. Targets block movement and accept hook/weapon queries; the smaller floating target moves, and the small nearby box is useful for blade contacts.

In the clip inspector, **PageUp/PageDown** select a clip, **P** pauses playback, and arrows orbit. Use `--package <directory>` to load another completed package; the bundled character is copied beside the executable during build.

## Integration and limitations

`SandboxController` translates Stride input into `HeroInputFrame`, advances
`HeroController`, and applies the character and camera poses. `StrideWorld` and
`CityCollision` implement host collision, hook and combat queries. Rendering,
audio and UI stay in the demo.

The bundled character is one captured blade appearance. Presets change stats
and skills; gun attachments and effects are demo approximations. Complete titan
skills, enemy behavior, networking and full visual/physics parity remain host
work. See [library integration](../lib-aottg-cs/docs/Integration.md) and
[character assets](../lib-aottg-cs/docs/Assets.md).

## Checks

Portable City collision checks use the bundled map:

```powershell
dotnet run --project StrideAOTTG/tests/CityChecks -c Release -- StrideAOTTG/Assets/City
```

Optional automated render/gameplay checks also use the bundled assets. To run
the main sequence, create an output directory and pass an absolute report path:

```powershell
New-Item -ItemType Directory -Force artifacts/stride-demo | Out-Null
$reportPath = Join-Path $PWD 'artifacts/stride-demo/gameplay.json'
dotnet run --project StrideAOTTG/StrideAottg.Windows -c Release --no-build -- --smoke-test --report $reportPath
```

Other modes are `--city-smoke-test`, `--asset-smoke-test`, `--options-smoke-test`,
`--damage-hud-smoke-test`, `--kill-info-smoke-test` and `--hero-hud-smoke-test`.
Choose one mode per run; each exits after writing its report and images. Options
checks use isolated settings. Reports and build outputs are excluded from Git.

## License

Original demo code is covered by the repository's [MIT license](../LICENSE).
Stride dependencies and extracted City, character, audio and UI assets retain
their own terms. See [third-party notices](../THIRD_PARTY.md).
