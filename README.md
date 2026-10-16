# Lib-AOTTG

Engine-independent libraries for bringing Attack on Titan Tribute Game (AoTTG)
HERO movement, hooks, resources, weapons and optional presentation systems into
other games. Hosts supply input, collision, rendering and combat integration.

| Directory | Implementation |
| --- | --- |
| [lib-aottg-cs](lib-aottg-cs/README.md) | C# controller targeting .NET Standard 2.0, with optional character assets runtime and Unity adapter |
| [lib-aottg-cpp](lib-aottg-cpp/README.md) | C++20 port for native hosts, built with CMake |

Both implementations preserve the `LibAottg` API namespace. They build
independently; neither requires the original game to build the controller.
Each directory contains integration documentation, examples and checks.

## Build

For C#, install the .NET 10 SDK and run from this repository:

```sh
dotnet build lib-aottg-cs/lib-aottg-cs.sln -c Release
dotnet run --project lib-aottg-cs/tests/LibAottg.Smoke -c Release
```

For C++, install CMake 3.25+ and a C++20 compiler:

```sh
cmake -S lib-aottg-cpp -B lib-aottg-cpp/build -DLIBAOTTG_BUILD_EXAMPLES=ON
cmake --build lib-aottg-cpp/build --config Release
```

These are development libraries with bounded reference comparisons, not a claim
of full gameplay or visual parity. See each implementation's documentation for
host responsibilities and limitations.

## License

Original library source, examples, tests and documentation are licensed under
the [MIT License](LICENSE). Each library also includes a copy of the license
so it can be distributed independently.

Extracted game assets retain their original ownership and licensing and are
excluded from the MIT license grant. External dependencies retain their own
licenses. See [THIRD_PARTY.md](THIRD_PARTY.md).

## Slop Warning

This entire project was mostly AI generated with codex/gpt-6.1-sol
I expect this repo will only be useful to other clankers
