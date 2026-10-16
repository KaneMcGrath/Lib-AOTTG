# Host integration

The host owns input devices, scene collision, rendering, audio, networking and game-specific damage. The library owns HERO movement, hook flight and pull, resources, action timing and animation selection. Start with calibration geometry, then replace each host service with your engine implementation.

## Coordinates and units

The simulation uses Y-up and +Z-forward. Convert axes consistently at your engine boundary, including camera rays, normals, velocities, rotations and moving anchors. Position is the character's feet/root. `WorldScale` is host units per original AoTTG unit: use 100 for centimetres. Set it before construction and scale your scene geometry to match. Do not also scale movement tuning. Time, angles and resource counts keep their original meaning.

## Input and timing

Call `HeroController.Advance` once per host frame with elapsed seconds and a `HeroInputFrame`. The controller schedules 0.02-second physics steps, consumes input edges once and interpolates the render pose. Its default maximum frame delta is 0.1 seconds; larger stalls discard excess time. For an engine-owned physics schedule use `CharacterMotor.AdvanceFrame` and `StepPhysics` instead; choose one scheduling approach.

Provide movement axes and held actions. Explicit `Pressed`/`Released` masks preserve taps between sampled frames. Set `HasView`, `ViewForward`, `AimOrigin` and `AimDirection` for your own camera. `Reel` is negative inward and positive outward; give inward input priority when both are pressed. Reset input mapping when focus or input ownership changes.

The optional `HeroCamera` consumes mouse deltas in pixels, positive Y downward, once per frame without another delta-time multiplier. Pointer coordinates are normalized from the top-left. The host applies the resulting camera pose, cursor capture and obstruction queries.

## Collision and hooks

Subclass `HeroWorld`. Implement `Raycast` for ground/wall probes, hook aim, projectile travel, camera and gun queries. Use `HeroQuery` to choose masks and exclude the character's own collider. Implement `Move` to sweep the proposed pose and return resolved position and velocity in host units. The motor already integrated gravity and forces; avoid integrating them a second time.

For an engine dynamic rigidbody, use the lower-level motor API: feed in the resolved body pose, step the motor, assign its resulting velocity, and let engine physics integrate once. Disable duplicate gravity/drag. The C# Unity adapter demonstrates this approach.

`CalibrationHeroWorld` and `KinematicCollisionWorld` provide an upright capsule against static boxes and a floor. They are useful for bring-up, but arbitrary meshes, slopes, stairs and moving bodies need engine collision services. The default source capsule has radius 0.5 and height 1.7, with its centre 0.85 above the root.

For moving hook targets supply stable IDs and target-local anchor points through `ResolveHookAnchor`. Return failure when the target is removed. Local anchor data belongs to the host; do not rescale it inside the motor. Supply hook muzzle and blade positions from the current animated rig.

## Rendering and combat

Render `RenderPose` rather than the uninterpolated physics root. Follow the motor animation's clip, authoritative time, speed, revision and fade. Handle zero-speed held poses and same-clip restarts. Apply procedural body/aim pose after animation and update sockets afterward. The library does not require the original character model; map named clips and equipment sockets to your own rig.

Implement combat queries and apply damage through your game's rules. Accepted `HeroHit` data includes speed-based damage; target velocity must use host units. Post damage UI only after applying a hit and kill UI only after confirming a kill. Game-specific enemies, skill effects and network authority remain host responsibilities.

`HeroRope`, `HeroSmokeTrail`, camera, sound callbacks and HUD models are optional. Render ropes/particles and play `SoundRequested` cues using engine APIs. Bundled audio, smoke textures and UI manifests are under `assets/`; the exported character package is included under `assets/character/` (22 mesh primitives, 68 clips and seven embedded textures). HUD models can drive your own UI.

## Lifecycle and validation

Advance a controller on one simulation thread. Synchronize or copy render data for other threads. Stop advancing while paused; clear input, hooks, effects and transient UI on a new session. Restore camera, cursor and character visibility when your host relinquishes control.

Test walking, jumping, gas thrust, single/dual hooks, moving-anchor removal, reel, attacks, reload, death, pause and reactivation. Repeat at another world scale and at different render rates. Recorded checks cover selected behavior; they do not establish complete gameplay, camera, collision or visual parity for a new engine.
