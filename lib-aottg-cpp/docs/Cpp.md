# C++ API and reference checks

Configuration is copied on construction. Configure before creating the controller; editing the original config afterward does not update it. For live tuning use `Motor->Config()` and preserve its construction-time world scale. Controller/motor copying and moving are disabled because internal services refer to their owner.

World, rig and camera pointers are borrowed and must outlive their use. Omitted worlds receive an owned default. Animation, rope and particle accessors return references whose contents may change on the next update. Use one simulation thread and synchronize renderer access.

Calculated C# properties become methods such as `Position()` and `Animation()`. Preset keys are lowercase ASCII. Events are optional `std::function` callbacks; install your own dispatcher for multiple subscribers. World, combat and presentation interfaces are virtual extension points. The API does not promise a stable binary ABI.

## Optional checks

The default `LIBAOTTG_BUILD_TESTS=ON` builds standalone check, parity, reference, HUD and benchmark executables. The C++ library builds independently; C# oracle generation additionally needs the .NET 10 SDK and the sibling `../lib-aottg-cs` source tree. Comparison scripts need Python 3.

From this directory, generate commands and C# results, run the C++ trace and compare:

```sh
dotnet run --project tests/Oracle -c Release -- build/comparison
```

Run the built `LibAottgParity` executable with `build/comparison/commands.txt` and `build/comparison/cpp.tsv`, then run:

```sh
python tests/compare.py build/comparison
```

Executables are under `build/Release` for multi-configuration generators or `build` for single-configuration generators. Generate collision inputs with `tests/CollisionOracle` and pass that output path plus a report path to `LibAottgChecks`. The `tests/BenchmarkOracle` project and `LibAottgBenchmark` generate optional timing results. Reference and HUD executables require their corresponding input fixtures/packages; building them alone does not run validation.

Comparisons use scripted host services and selected recorded observations. A passing trace does not certify all branches, visual fidelity or your engine adapter. `LIBAOTTG_ENABLE_IPO=ON` optionally enables cross-translation-unit optimization when the compiler supports it.
