# The City in the Stride demo

The City is the default interactive map. It uses geometry, UVs, textures, material colors, collider geometry and five spawn positions extracted from the dedicated AOTTG game copy's `level0` scene. Streets, buildings, roofs, gates and the surrounding walls are available for movement and hook queries. Respawn starts at the original street spawn near the gate.

Build and run from the repository root as described in the [demo README](../README.md).
Use `--sandbox` to select the calibration map instead.

The bundled package in `Assets/City/` includes render meshes, textures, collider
geometry and a provenance manifest. `CityMap.cs` renders the map and
`CityCollision.cs` implements triangle raycasts and upright capsule movement.
No extraction step or original game installation is needed.

Lighting and sky are Stride approximations. Titans and original scene gameplay
scripts are not imported. Collision uses the source surfaces with the demo's
capsule solver rather than Unity PhysX, so this is not a physics parity claim.

The standalone City checks cover source spawns, ground and rooftop contacts,
high-speed falls, ray queries and world scales 0.001, 1 and 10. See the README
for the command. Map assets retain their original ownership and licensing.
