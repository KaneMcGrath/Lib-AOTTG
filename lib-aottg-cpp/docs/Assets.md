# Exported character

`assets/character/character.glb` contains the exported character: 22 mesh primitives, a skeleton, 68 named animation clips and seven embedded textures. Import it with your engine's glTF 2.0 loader. The adjacent manifest records the GLB hash and inventory; animation rules, sockets, physics, bind-pose metadata and a profile are included alongside it.

The C++ controller does not include a GLB loader, animation sampler or skinning runtime. Use your engine's implementation to sample `hero.Motor->Animation()` at its authoritative clip time, respecting speed, revision, fade and held poses. Implement `IHeroPresentation` for animated hook muzzles and combat sockets.

The GLB is right-handed, Y-up and -Z-forward; the controller is Y-up and +Z-forward. Convert coordinates once and apply the root pose and `WorldScale / metersPerSourceUnit` scale. `metersPerSourceUnit` is in the manifest. Preserve winding and normal transforms across any reflection. Keep visual root animation separate from controller movement.

This is one captured appearance, not a costume collection; gun meshes are not included. The scale is a conversion setting rather than a calibrated measurement. Materials approximate legacy shaders. Original capture metadata and warnings are retained in the package.
