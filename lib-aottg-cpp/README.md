# lib-aottg-cpp

An engine-independent C++20 port of **lib-aottg-cs** for implementing an AoTTG HERO in another game. The controller follows RCMod mechanics and reference data, with movement, hooks, resources, weapons, action timing and optional camera, rope, smoke and HUD systems. No managed runtime is required.

## Build and link

Use CMake 3.25 or newer and a C++20 compiler. From this directory:

```sh
cmake -S . -B build -DLIBAOTTG_BUILD_TESTS=OFF
cmake --build build --config Release
```

Set `LIBAOTTG_BUILD_EXAMPLES=ON` to build the `LibAottgConsoleHost` minimal host example.

In your host's CMake project:

```cmake
add_subdirectory(path/to/lib-aottg-cpp)
target_link_libraries(your_game PRIVATE lib-aottg-cpp)
```

Headers and the `LibAottg` namespace retain their API names. The CMake project, target and output library are named `lib-aottg-cpp`.

```cpp
#include <LibAottg/Stats.hpp>

auto config = LibAottg::HeroStats::RcModPresets().at("mikasa").ToConfig();
config.WorldScale = 1;
LibAottg::CalibrationHeroWorld world(0.5f, 1.7f, config.WorldScale);
LibAottg::HeroController hero(&config, &world);
hero.Respawn({}, true);
LibAottg::HeroInputFrame input;
input.MoveY = 1;
hero.Advance(0.016f, input);
// Render hero.RenderPose; use hero.Motor->Animation() for clip playback.
```

Read [host integration](docs/Integration.md) for coordinates, input, collision, hooks, rendering and lifecycle, and [C++ API and checks](docs/Cpp.md) for ownership and optional reference comparisons.

The port includes the controller and HUD package reader, but does not port the C# GLB character loader, animation sampler or skinning runtime. Use your engine's rig/animation system and implement presentation sockets. `assets/character/` includes the exported GLB character, 68 named animation clips, embedded textures, sockets and physics metadata. Import it through your engine's glTF pipeline; see [character assets](docs/Assets.md). `assets/` also contains optional audio, smoke and UI resources. HUD package composition requires a compatible `AOHUD001` binary package, which is not bundled; raw UI manifests can instead drive your own renderer.

The calibration collider supports static boxes and a floor. General scene collision, game-specific skill effects, damage authority and rendering belong to your adapter. Existing comparisons cover selected scripted behavior and do not establish full parity for a new host.

## License

Original library code is licensed under the [MIT License](LICENSE). Extracted game assets retain their original ownership and licensing; see [third-party notices](THIRD_PARTY.md).
