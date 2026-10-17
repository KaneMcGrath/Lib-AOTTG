# Third-party material

The MIT license applies to original LibAottg code and documentation. It does
not relicense upstream dependencies or extracted game resources.

## Game assets and reference material

Both libraries include optional resources under `assets/`: captured character
models, skeletons, animations, embedded textures, audio, smoke shader/texture
data, UI sprites and bitmap fonts, with extraction manifests and metadata.
These originate from AoTTG/RCMod/StinkMod captures. Their original ownership
and licensing continue to apply; inclusion or conversion grants no new rights
to the underlying game content. The MIT grant does not cover these assets.
See the manifests and each library's `docs/Assets.md` for capture provenance.

Recorded test fixtures are reference observations used to validate selected
behavior. They do not confer rights to the original game or its assets.

## External packages

`StrideAOTTG/` is the optional Windows demo. Its original source is covered
by this repository's MIT license. Stride 4.3.0.2507 is restored through NuGet
under its upstream license. The extracted City geometry, collision data and
textures in `StrideAOTTG/Assets/City/` retain their original game ownership
and licensing and are excluded from the MIT grant, as are the shared game
assets consumed from `lib-aottg-cs/assets/`. The bundled map is ready to use;
no extraction tools or original game installation are required.

The optional C# assets runtime references `SharpGLTF.Core` 1.0.7 through NuGet.
Its upstream license and package notices continue to apply. Dependencies are
restored by the build and are not vendored in this repository.

The C++ core uses the C++ standard library and does not bundle an engine SDK.
Host engines and SDKs, including Unity for the optional C# adapter, remain
subject to their own terms.
