// SPDX-License-Identifier: MIT
using LibAottg;
using LibAottg.Assets;
using System.Numerics;
using System.Text.Json;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static void Near(float value, float expected, string name, float tolerance = 0.0001f) => Check(Math.Abs(value - expected) <= tolerance, $"{name}: {value}, expected {expected}");
    private static void Near(Vec3 value, Vec3 expected, string name, float tolerance = 0.0001f) => Check((value - expected).Length <= tolerance, $"{name}: {value}, expected {expected}");
    private const float Dt = 0.02f;
    private static HeroInputFrame View(HeroButtons held = HeroButtons.None) => new() { Held = held, HasView = true, ViewForward = new Vec3(0, 0, 1), AimDirection = new Vec3(0, 0, 1), AimOrigin = new Vec3(0, 1, 0) };

    private sealed class CombatWorld : CalibrationHeroWorld
    {
        public readonly List<CharacterEvent> Events = new();
        public readonly List<HeroHit> Hits = new();
        public readonly List<HeroWeaponSweep> Sweeps = new();
        public int Queries;
        public override void OnCharacterEvent(CharacterEvent kind) => Events.Add(kind);
        public override void CollectWeaponHits(HeroWeaponSweep sweep, IList<HeroHit> hits)
        {
            Queries++;
            Sweeps.Add(sweep);
            // Two destinations and a duplicate collider on target 7, returned by either blade.
            hits.Add(new HeroHit { TargetId = 7, Point = sweep.End });
            hits.Add(new HeroHit { TargetId = 7, Point = sweep.End });
            hits.Add(new HeroHit { TargetId = 8, Point = sweep.End });
        }
        public override void OnHeroHit(HeroHit hit) => Hits.Add(hit);
    }
    private sealed class Rig : IHeroPresentation
    {
        private HeroPose pose;
        public Vec3 Head => pose.Position + Vec3.Up * 1.5f;
        public int Samples;
        public bool RightAvailable = true;
        public void Sample(CharacterMotor motor, HeroPose pose, CharacterInput input, float blendDelta) { this.pose = pose; Samples++; }
        public Vec3 HookOrigin(CharacterMotor motor, HookSide side) => pose.Position + new Vec3(side == HookSide.Left ? -0.5f : 0.5f, 1, 0);
        public bool WeaponSegment(HookSide side, bool gun, out Vec3 start, out Vec3 end) { start = pose.Position; end = start + new Vec3(0, 1, 1); return side == HookSide.Left || RightAvailable; }
    }
    private sealed class AnchorWorld : HeroWorld
    {
        public Vec3 Left, Right;
        public bool Grounded;
        public override CharacterContacts GetContacts(CharacterMotor motor, HeroPose pose) => new() { Grounded = Grounded };
        public override bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit)
        { hit = new HookHit(side == HookSide.Left ? Left : Right); return true; }
        public override bool SweepHook(Vec3 start, Vec3 end, HookSide side, out HookHit hit)
        { hit = new HookHit(side == HookSide.Left ? Left : Right); return true; }
    }
    private static void Audio()
    {
        foreach (float scale in new[] { 1f, 10f })
        {
            var world = new AnchorWorld { Left = new Vec3(-2, 4, 30) * scale, Right = new Vec3(2, 4, 30) * scale };
            var hero = new HeroController(new CharacterConfig { WorldScale = scale }, world);
            var cues = new List<HeroSoundCue>();
            var events = new List<CharacterEvent>();
            hero.SoundRequested += cues.Add;
            hero.Event += events.Add;
            var spawn = new Vec3(2, 3, 4) * scale;
            hero.Respawn(spawn);
            hero.Advance(0.001f, View(HeroButtons.HookBoth));
            Check(hero.PhysicsTicks == 0 && cues.Count == 1, "Double hook sounds once on the frame clock before physics");
            Check(events.Contains(CharacterEvent.HookLaunchedLeft) && events.Contains(CharacterEvent.HookLaunchedRight), "Audio retains both side events");
            Check(cues[0].Sound == HeroSound.HookLaunch, "Hook sound identity");
            Near(cues[0].Position, spawn, "Sound location uses host scale");
            Near(cues[0].Volume, 1, "Source rope volume"); Near(cues[0].Pitch, 1, "Source rope pitch");
            hero.Advance(0.001f, View(HeroButtons.HookBoth));
            Check(cues.Count == 1, "Holding already flying hooks does not repeat audio");
            hero.Respawn(spawn);
            hero.Advance(0.001f, View(HeroButtons.HookLeft));
            hero.Advance(0.001f, View(HeroButtons.HookLeft | HeroButtons.HookRight));
            Check(cues.Count == 3, "Left and subsequent right shots each restart audio");
            hero.Respawn(spawn); hero.Motor.ConsumeGas(100);
            hero.Advance(0.001f, View(HeroButtons.HookBoth));
            Check(cues.Count == 3, "No audio without gas");
            hero.Respawn(spawn); hero.Die();
            hero.Advance(0.001f, View(HeroButtons.HookBoth));
            Check(cues.Count == 4 && cues[3].Sound == HeroSound.Death, "Death sounds once; dead hero cannot launch hooks");
            // Removal is an engine-independent mute; no player or device is required.
            hero.SoundRequested -= cues.Add;
            hero.Respawn(spawn); hero.Advance(0.001f, View(HeroButtons.HookBoth));
            Check(cues.Count == 4 && hero.Motor.LeftHookVisible, "Audio is optional and does not gate gameplay");
        }
        var miss = new HeroController(world: new HeroWorld());
        int missedCues = 0; miss.SoundRequested += _ => missedCues++;
        miss.Advance(0.001f, View(HeroButtons.HookBoth));
        Check(missedCues == 0, "Aim miss does not fire hook sound");
        foreach (string skill in new[] { "levi", "petra" })
        {
            var hero = new HeroController(new CharacterConfig { SkillId = skill },
                new AnchorWorld { Left = new Vec3(-2, 4, 30), Right = new Vec3(2, 4, 30) });
            int cues = 0; hero.SoundRequested += _ => cues++;
            hero.Advance(0.001f, View(HeroButtons.Skill));
            Check(cues == 1, skill + " skill hook launch sounds once");
        }
    }
    private static void WeaponAudio()
    {
        var world = new CombatWorld();
        var hero = new HeroController(world: world) { Presentation = new Rig(), SourceAttackSelection = false };
        var cues = new List<HeroSoundCue>();
        var events = new List<CharacterEvent>();
        hero.SoundRequested += cues.Add; hero.Event += events.Add;
        hero.Respawn(Vec3.Zero, true);
        hero.Advance(Dt, View(HeroButtons.Attack));
        Check(cues.Count == 0, "Starting attack does not sound before swing window");
        for (int n = 0; n < 35; n++) hero.Advance(Dt, View());
        Check(cues.Count(c => c.Sound == HeroSound.BladeSwing) == events.Count(e => e == CharacterEvent.AttackWindowOpened), "One swing per attack window");
        Check(cues.Any(c => c.Sound == HeroSound.BladeSwing), "Blade swing played");
        Check(cues.Count(c => c.Sound == HeroSound.BladeHit) == world.Hits.Count && world.Hits.Count > 0, "Accepted blade hits sound; duplicates do not");
        foreach (var button in new[] { HeroButtons.Attack, HeroButtons.Skill })
        {
            hero.Respawn(Vec3.Zero, true); hero.SetEquipment(EquipmentMode.Guns); cues.Clear(); events.Clear();
            hero.Advance(Dt, View(button));
            Check(cues.Count == 0, "Gun aim has no firing sound");
            for (int n = 0; n < 40; n++) hero.Advance(Dt, View());
            Check(cues.Count == 1 && cues[0].Sound == HeroSound.AhssFire, "Single/double AHSS shot sounds once");
            Check(events.Count(e => e == CharacterEvent.GunFiredLeft || e == CharacterEvent.GunFiredRight) == (button == HeroButtons.Skill ? 2 : 1), "Both side events retained");
            for (int n = 0; n < 10; n++) hero.Advance(Dt, View());
            Check(cues.Count == 1, "Idle guns do not repeat sound");
        }
    }
    private static void InputAndClocks()
    {
        var mapper = new HeroInputMapper();
        var raw = View(HeroButtons.Jump); raw.MoveY = 1;
        var first = mapper.Sample(raw, Vec3.Up, Vec3.Zero, Vec3.Up);
        Check(first.Jump.Pressed && first.Jump.Held && first.Forward.Pressed, "Held states derive action and movement edges");
        var next = mapper.Sample(raw, Vec3.Up, Vec3.Zero, Vec3.Up);
        Check(!next.Jump.Pressed && !next.Forward.Pressed, "Repeated held values do not repeat edges");
        raw = View(); raw.Pressed = raw.Released = HeroButtons.Attack;
        next = mapper.Sample(raw, Vec3.Up, Vec3.Zero, Vec3.Up);
        Check(next.Jump.Released && next.Forward.Released && next.Attack.Pressed && next.Attack.Released && !next.Attack.Held, "Between-frame taps are preserved");
        Near(next.LookDirection, new Vec3(0, 0, 1), "Host view overrides internal interpretation");

        var world = new CombatWorld();
        var hero = new HeroController(world: world) { Presentation = new Rig(), SourceAttackSelection = false };
        hero.Respawn(Vec3.Zero, true);
        raw = View(HeroButtons.Jump);
        hero.Advance(0.01f, raw);
        Check(hero.PhysicsTicks == 0 && hero.Motor.Animation.Clip == "jump", "Frame edge consumed when no physics step is due");
        for (int i = 0; i < 11; i++) hero.Advance(0.01f, raw);
        Check(hero.PhysicsTicks == 6 && world.Events.Count(e => e == CharacterEvent.Jumped) == 1, "One jump across multiple render/physics frames");
        int before = hero.PhysicsTicks;
        hero.Advance(5, View());
        Check(hero.PhysicsTicks - before <= 5, "Long stalls have bounded catch-up");
        hero.Grab(); Check(hero.Motor.State == CharacterState.Grabbed, "Facade grab");
        hero.EscapeGrab(new Vec3(2, 3, 4)); Near(hero.RenderPose.Position, new Vec3(2, 3, 4), "Escape synchronizes render pose");
        hero.Die(); Check(hero.Motor.State == CharacterState.Dead, "Facade death");
        hero.Respawn(Vec3.Zero, true); Near(hero.Motor.Gas, 100, "Respawn resets resources");
        Check(hero.Camera == null, "A minimal host needs no camera, devices or rendering");

        // The convenience runtime must preserve existing core integration for constant frame input.
        var runtime = new HeroController() { ProceduralPose = false, SourceAttackSelection = false };
        var direct = new CharacterMotor();
        var input = new CharacterInput { MoveX = 0.5f, MoveY = 1, LookDirection = new Vec3(0, 0, 1), Jump = new Button(true, false, false) };
        for (int i = 0; i < 100; i++)
        {
            runtime.Advance(Dt, input); direct.Step(Dt, input, default, new HeroWorld());
            Near(runtime.Motor.Position, direct.Position, "Runtime integrates gravity and forces once");
            Near(runtime.Motor.Velocity, direct.Velocity, "Runtime preserves motor velocity");
        }
    }
    private static void Cameras()
    {
        var motor = new CharacterMotor(); motor.Respawn(Vec3.Zero, true);
        var camera = new HeroCamera();
        var raw = new HeroInputFrame { MouseDeltaX = 100, MouseDeltaY = 20, HasPointer = true, PointerX = 0.8f, PointerY = 0.3f, AspectRatio = 16f / 9 };
        camera.Look(Dt, raw); var view = camera.Follow(Dt, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), raw);
        Near(view.Yaw, 15, "Mouse deltas interpreted once without multiplying by frame duration");
        Near(view.Pitch, -3, "Top-left device delta convention");
        Near(view.Forward.Length, 1, "Camera forward unit length"); Near(Vec3.Dot(view.Forward, view.Up), 0, "Camera basis orthogonal");
        Near(view.AimDirection, view.Forward, "TPS always aims at viewport center");
        Check(view.CapturePointer, "TPS asks the host to capture pointer");
        camera.Config.Mode = HeroCameraMode.Wow; camera.Reset(); camera.Look(Dt, raw);
        view = camera.Follow(Dt, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), raw);
        Near(view.Yaw, 0, "WOW ignores relative motion unless orbit is held");
        Check(!view.CapturePointer && (view.AimDirection - view.Forward).Length > 0.1f, "WOW uses free cursor aim");
        raw.OrbitHeld = true; camera.Look(Dt, raw); view = camera.Follow(Dt, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), raw);
        Near(view.Yaw, 15, "WOW orbit held");
        camera.Config.Mode = HeroCameraMode.Original; camera.Reset(); camera.Look(Dt, raw);
        view = camera.Follow(Dt, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), raw);
        Near(view.Yaw, 2.232f, "Original edge turn from source 62*dt rule and mouse speed");
        Near(view.Pitch, 7, "Original absolute cursor height rule");
        raw.PointerX = 0.5f; camera.Reset(); camera.Look(Dt, raw); view = camera.Follow(Dt, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), raw);
        Near(view.Yaw, 0, "Original central horizontal dead zone");
        camera.Config.Mode = HeroCameraMode.Tps; camera.Reset();
        view = camera.Follow(0, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), default);
        Near(view.Position, new Vec3(0.65f, 2.14f, -3.84f), "Source human camera offsets at 50 FOV");
        var collision = new CalibrationHeroWorld(); collision.Collision.Boxes.Add(new WorldBox(new Vec3(-2, 0, -2), new Vec3(2, 4, -1.8f)));
        view = camera.Follow(0, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), default, collision);
        Check(view.Position.Z > -1.8f, "Camera cannot sit beyond an obstructing wall");
        var scaledMotor = new CharacterMotor(new CharacterConfig { WorldScale = 10 });
        var scaled = new HeroCamera().Follow(0, scaledMotor, Vec3.Zero, new Vec3(0, 15, 0), default);
        Near(scaled.Position / 10, new Vec3(0.65f, 2.14f, -3.84f), "Camera offsets scale exactly once");
        foreach (float distance in new[] { 0f, 0.3f, 0.35f, 1f })
        {
            camera.Config.CameraDistance = distance;
            view = camera.Follow(0, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), default);
            float multiplier = distance + 0.3f;
            Near(view.Position.Y, 1.5f + 0.64f - (0.6f - multiplier) * 2, "AOTTG distance height");
            Near(view.Position.Z, -6.4f * multiplier, "AOTTG distance at 50 FOV");
            Near(view.Position.X, multiplier < 0.65f ? Math.Max((0.6f - multiplier) * 2, 0.65f) : 0, "AOTTG shoulder threshold");
        }
        foreach (var mode in new[] { HeroCameraMode.Tps, HeroCameraMode.Wow, HeroCameraMode.Original })
        foreach (float speed in new[] { 0.01f, 0.5f, 1f })
        foreach (bool invert in new[] { false, true })
        {
            camera.Config.Mode = mode; camera.Config.MouseSpeed = speed; camera.Config.InvertMouse = invert;
            camera.Reset();
            var sample = new HeroInputFrame { MouseDeltaX = 10, MouseDeltaY = 4, OrbitHeld = true,
                HasPointer = true, PointerX = 0.8f, PointerY = 0.3f };
            camera.Look(Dt, sample);
            view = camera.Follow(0, motor, Vec3.Zero, new Vec3(0, 1.5f, 0), sample);
            Near(view.Yaw, mode == HeroCameraMode.Original ? 0.2f * 0.4f * speed * Dt * 62 * 150 : 10 * speed, "AOTTG mouse speed across modes");
            Near(view.Pitch, mode == HeroCameraMode.Original ? 7 : -4 * speed * (invert ? -1 : 1), "AOTTG invert across modes");
        }
        foreach (float invalid in new[] { -0.01f, 1.01f, float.NaN, float.PositiveInfinity })
        {
            bool rejected = false;
            try { camera.Config.CameraDistance = invalid; } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid AOTTG distance rejected");
            rejected = false;
            try { camera.Config.MouseSpeed = invalid; } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid AOTTG mouse speed rejected");
        }
        var hooked = new CharacterMotor();
        var anchorWorld = new AnchorWorld { Left = new Vec3(-10, 5, 10) };
        var hook = new CharacterInput { HookLeft = new Button(true, true, false), AimDirection = new Vec3(-10, 5, 10).Normalized };
        hooked.AdvanceFrame(Dt, hook, default, anchorWorld);
        for (int i = 0; i < 20 && !hooked.LeftHookAttached; i++) hooked.StepPhysics(Dt, hook, default, anchorWorld);
        Check(hooked.LeftHookAttached, "Camera tilt test has an attached hook");
        hooked.SetPose(Vec3.Zero, new Vec3(0, 0, 10), 0);
        camera.Config.CameraTilt = true; camera.Reset();
        view = camera.Follow(Dt, hooked, Vec3.Zero, Vec3.Up, default);
        Check(Math.Abs(view.Roll) > 0.01f, "Camera tilt banks toward hook");
        camera.Config.CameraTilt = false; camera.Reset();
        view = camera.Follow(Dt, hooked, Vec3.Zero, Vec3.Up, default);
        Near(view.Roll, 0, "Disabled camera tilt leaves horizon level");
    }
    private static void Combat()
    {
        var world = new CombatWorld();
        var hero = new HeroController(world: world) { Presentation = new Rig(), SourceAttackSelection = false };
        hero.Respawn(Vec3.Zero, true);
        for (int i = 0; i < 35; i++)
        {
            var raw = View(); if (i == 0) raw.Pressed = HeroButtons.Attack;
            hero.Advance(Dt, raw);
            if (i < 10) Check(world.Hits.Count == 0, "Blade hits wait for the animation window");
        }
        Check(world.Hits.Count == 2 && world.Hits.Select(h => h.TargetId).Distinct().Count() == 2, "Deduplicate target colliders and both blades per window");
        Near(hero.Motor.Blade, 96, "Confirmed contacts spend durability; repeated queries do not");
        Check(hero.TrySetEquipment(EquipmentMode.Guns), "Equipment changes after actions finish");
        var gunWorld = new CombatWorld();
        hero = new HeroController(world: gunWorld) { Presentation = new Rig() }; hero.Respawn(Vec3.Zero, true); hero.SetEquipment(EquipmentMode.Guns);
        for (int i = 0; i < 50; i++)
        {
            var raw = View(i < 2 ? HeroButtons.Skill : HeroButtons.None);
            hero.Advance(i == 8 ? 0.005f : Dt, raw);
        }
        Check(gunWorld.Events.Count(e => e == CharacterEvent.GunFiredLeft) == 1 && gunWorld.Events.Count(e => e == CharacterEvent.GunFiredRight) == 1, "Both guns fire once on skill release");
        Check(gunWorld.Hits.Count > 0 && gunWorld.Hits.All(h => h.Gun), "Frame-clock gun events dispatch destination hits");
        Check(!hero.Motor.LeftGunLoaded && !hero.Motor.RightGunLoaded, "Both chambers consumed");
        Check(gunWorld.Hits.Count == 4, "Duplicate colliders produce one hit per target per gun shot");
        hero.Advance(Dt, new HeroInputFrame { Pressed = HeroButtons.Reload });
        Check(!hero.TrySetEquipment(EquipmentMode.Blades), "Busy equipment toggle does not throw or interrupt reload");
        for (int i = 0; i < 120; i++) hero.Advance(Dt, View());
        Check(hero.Motor.LeftGunLoaded && hero.Motor.RightGunLoaded, "Reload completes using source speed and duration");
        var delayedRig = new Rig { RightAvailable = false };
        var delayedWorld = new CombatWorld();
        hero = new HeroController(world: delayedWorld) { Presentation = delayedRig, SourceAttackSelection = false };
        hero.Respawn(new Vec3(7, 0, 0), true);
        for (int tick = 0; tick < 18; tick++)
        {
            if (tick == 15) delayedRig.RightAvailable = true;
            var raw = View(); if (tick == 0) raw.Pressed = HeroButtons.Attack;
            hero.Advance(Dt, raw);
        }
        var firstRight = delayedWorld.Sweeps.First(s => s.Side == HookSide.Right);
        Near(firstRight.PreviousStart, firstRight.Start, "An appearing weapon cannot sweep from stale/default geometry");
    }
    private static void RigAndScale(CharacterPackage package)
    {
        var rig = new HeroCharacter(package);
        var motor = new CharacterMotor(); motor.Respawn(Vec3.Zero, true);
        var pose = new HeroPose { Position = new Vec3(3, 4, 5), Yaw = 35, Pitch = -20, Roll = 15 };
        rig.Sample(motor, pose, default, Dt);
        Check(rig.TryNode("head", out var h), "Captured head available without host bone lookup");
        var baseVertices = rig.Character.Primitives.SelectMany(p => p.Positions).ToArray();
        var scaledMotor = new CharacterMotor(new CharacterConfig { WorldScale = 10 }); scaledMotor.Respawn(Vec3.Zero, true);
        var scaledRig = new HeroCharacter(package); var scaledPose = pose; scaledPose.Position *= 10;
        scaledRig.Sample(scaledMotor, scaledPose, default, Dt);
        Near(scaledRig.Head / 10, rig.Head, "Tilted rig head scales exactly once");
        Near(scaledRig.HookOrigin(scaledMotor, HookSide.Left) / 10, rig.HookOrigin(motor, HookSide.Left), "Animated muzzle scales exactly once");
        foreach (var side in new[] { HookSide.Left, HookSide.Right })
        {
            Check(rig.WeaponSegment(side, false, out var a, out var b) && (a - b).Length > 0.1f, "Distinct blade start/end sockets");
        }
        motor.SetEquipment(EquipmentMode.Guns);
        var input = new CharacterInput { Attack = new Button(true, true, false), HasAimOrigin = true, AimOrigin = pose.Position + Vec3.Up, AimDirection = new Vec3(0.3f, 0.2f, 1).Normalized };
        rig.Sample(motor, pose, input, Dt);
        Check(rig.PrimitiveVisible.Count(v => !v) == 2, "Gun equipment hides both captured blade meshes");
        Check(!baseVertices.SequenceEqual(rig.Character.Primitives.SelectMany(p => p.Positions)), "Gun arm/head pose changes the rig");
        Check(rig.Character.Primitives.All(p => p.Positions.All(v => !float.IsNaN(v.Length()) && !float.IsInfinity(v.Length())) && p.Normals.All(v => Math.Abs(v.Length() - 1) < 0.001f)), "Procedural skinning yields finite vertices and normalized normals");
        var gunVertices = rig.Character.Primitives.SelectMany(p => p.Positions).ToArray();
        for (int i = 0; i < 20; i++) rig.Sample(motor, pose, input, 0);
        Check(gunVertices.SequenceEqual(rig.Character.Primitives.SelectMany(p => p.Positions)), "Repeated zero-time sampling does not accumulate bone edits");
        motor.SetEquipment(EquipmentMode.Blades); rig.Sample(motor, pose, default, 0.1f);
        Check(rig.PrimitiveVisible.All(v => v), "Returning to blades restores visibility");
        var solo = package.CreateInstance(); solo.Sample(motor.Animation.Clip, motor.Animation.Time);
        Check(solo.Primitives.SelectMany(p => p.Positions).SequenceEqual(rig.Character.Primitives.SelectMany(p => p.Positions)), "Disabling gun aim restores original animation without polluted crossfades");

    }
    private static Vec3 Vector(JsonElement array)
    { var a = array.EnumerateArray().Select(v => v.GetSingle()).ToArray(); return new Vec3(a[0], a[1], a[2]); }
    private static Quaternion Rotation(JsonElement array)
    { var a = array.EnumerateArray().Select(v => v.GetSingle()).ToArray(); return new Quaternion(a[0], a[1], a[2], a[3]); }
    private static void SourcePresentation(CharacterPackage package, string path)
    {
        float maxRotationError = 0, maxPositionError = 0;
        int observations = 0, expectedObservations = 0;
        foreach (var line in File.ReadLines(path))
        {
            using var document = JsonDocument.Parse(line); var row = document.RootElement;
            if (row.TryGetProperty("complete", out var complete) && complete.GetBoolean()) expectedObservations = row.GetProperty("observations").GetInt32();
            if (!row.TryGetProperty("kind", out var kind)) continue;
            if (kind.GetString() == "body-lean")
            {
                var world = new AnchorWorld { Left = Vector(row.GetProperty("leftAnchor")), Right = Vector(row.GetProperty("rightAnchor")) };
                var observedMotor = new CharacterMotor();
                if (row.GetProperty("guns").GetBoolean()) observedMotor.SetEquipment(EquipmentMode.Guns);
                var hookInput = new CharacterInput { LookDirection = new Vec3(0, 0, 1), AimDirection = new Vec3(0, 0, 1),
                    HookLeft = new Button(row.GetProperty("left").GetBoolean(), false, false), HookRight = new Button(row.GetProperty("right").GetBoolean(), false, false) };
                observedMotor.AdvanceFrame(Dt, hookInput, default, world);
                for (int tick = 0; tick < 20 &&
                    (observedMotor.LeftHookPhase == HookPhase.Flying || observedMotor.RightHookPhase == HookPhase.Flying); tick++)
                {
                    observedMotor.SetPose(Vec3.Zero, Vec3.Zero, 0);
                    observedMotor.StepPhysics(Dt, hookInput, default, world);
                }
                observedMotor.SetPose(Vec3.Zero, Vector(row.GetProperty("velocity")), row.GetProperty("yaw").GetSingle());
                if (row.GetProperty("attack").GetBoolean())
                {
                    hookInput.Attack = new Button(false, true, false);
                    observedMotor.AdvanceFrame(Dt, hookInput, default, world);
                }
                var body = HeroBodyPose.Evaluate(observedMotor);
                var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, body.Yaw * (float)Math.PI / 180) *
                    Quaternion.CreateFromAxisAngle(Vector3.UnitX, body.Pitch * (float)Math.PI / 180) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, body.Roll * (float)Math.PI / 180);
                float error = QuaternionError(rotation, Rotation(row.GetProperty("rotation")));
                Check(error < 0.00001f, $"Original bodyLean quaternion error {error}; core pitch={body.Pitch}, roll={body.Roll}, hooks={observedMotor.LeftHookPhase}/{observedMotor.RightHookPhase}");
                observations++;
                continue;
            }
            if (kind.GetString() != "gun-pose") continue;
            observations++;
            var euler = Vector(row.GetProperty("euler")); var target = Vector(row.GetProperty("target"));
            var motor = new CharacterMotor(); motor.Respawn(Vec3.Zero, true); motor.SetEquipment(EquipmentMode.Guns);
            motor.AdvanceFrame(0.2f, default, new CharacterContacts { Grounded = true }, new HeroWorld());
            var rig = new HeroCharacter(package) { ProceduralAim = false };
            var pose = new HeroPose { Pitch = euler.X, Yaw = euler.Y, Roll = euler.Z };
            rig.Sample(motor, pose, default, 0.1f);
            rig.ProceduralAim = true;
            rig.Sample(motor, pose, new CharacterInput { Skill = new Button(true, false, false), HasAimTarget = true, AimTarget = target, AimDirection = target.Normalized }, row.GetProperty("dt").GetSingle());
            foreach (var bone in row.GetProperty("bones").EnumerateArray())
            {
                string name = bone.GetProperty("name").GetString()!;
                Check(rig.TryNodeMatrix(name, out var matrix) && Matrix4x4.Decompose(matrix, out _, out var actual, out _), "Observed bone available " + name);
                Matrix4x4.Decompose(matrix, out _, out var actualRotation, out _);
                var expected = Rotation(bone.GetProperty("rotation"));
                float error = QuaternionError(actualRotation, expected);
                float positionError = (new Vec3(matrix.Translation.X, matrix.Translation.Y, matrix.Translation.Z) - Vector(bone.GetProperty("position"))).Length;
                maxRotationError = Math.Max(maxRotationError, error); maxPositionError = Math.Max(maxPositionError, positionError);
                Check(error < 0.0001f, $"Original {name} quaternion error {error}");
                Check(positionError < 0.0002f, $"Original {name} socket position error {positionError}");
            }
        }
        Check(observations == expectedObservations && observations >= 8, "Complete original body/head/arm observations");
        Console.WriteLine($"Original pose comparison: {observations} observations; gun quaternion error {maxRotationError}, position {maxPositionError} units.");
    }
    private static float QuaternionError(Quaternion actual, Quaternion expected)
    {
        var a = new Vector4(actual.X, actual.Y, actual.Z, actual.W); var b = new Vector4(expected.X, expected.Y, expected.Z, expected.W);
        return Math.Min((a - b).Length(), (a + b).Length());
    }
    private static void Main(string[] args)
    {
        GroundedBodyPose(); Audio(); WeaponAudio(); Smoke(); InputAndClocks(); Cameras(); Combat();
        var package = CharacterPackage.Load(args.Length > 0 ? args[0] : "character-package");
        RigAndScale(package);
        SourcePresentation(package, args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "fixtures/unity-4.6-presentation.jsonl"));
        Console.WriteLine("Assets target: " + typeof(HeroCharacter).Assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
            .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName);
        Console.WriteLine($"Passed {checks} host runtime, input, camera, combat and procedural rig checks.");
    }

    private static void GroundedBodyPose()
    {
        foreach (float scale in new[] { 1f, 10f })
        foreach (var equipment in new[] { EquipmentMode.Blades, EquipmentMode.Guns })
        foreach (var hooks in new[] { HeroButtons.HookLeft, HeroButtons.HookRight, HeroButtons.HookBoth })
        {
            var world = new AnchorWorld { Left = new Vec3(-2, 10, 20) * scale, Right = new Vec3(-1, 10, 20) * scale };
            var hero = new HeroController(new CharacterConfig { WorldScale = scale }, world);
            hero.Motor.SetEquipment(equipment);
            for (int i = 0; i < 10; i++) hero.Advance(Dt, View(hooks));
            Check(hero.Motor.LeftHookAttached || hero.Motor.RightHookAttached, "Body pose test attaches hooks");
            hero.Motor.SetPose(Vec3.Zero, new Vec3(15, 5, 0) * scale, 35);
            hero.Synchronize();
            Check(hero.Pose.Leaning && Math.Abs(hero.Pose.Roll) > 0.01f, "Airborne hooks retain body lean");
            world.Grounded = true;
            // Leave part of a render interval so landing would otherwise interpolate the airborne tilt.
            hero.Advance(Dt * 1.25f, View(hooks));
            Check(hero.Motor.Grounded, "Body pose test lands with hooks held");
            Near(hero.Pose.Up, Vec3.Up, "Grounded body is upright");
            Near(hero.RenderPose.Up, Vec3.Up, "Landing render interpolation is upright");
            Check(!hero.RenderPose.Leaning && !hero.RenderPose.LeanLeft, "Grounded pose clears lean flags");
            world.Grounded = false;
            hero.Advance(Dt, View(hooks));
            Check(hero.Pose.Leaning, "Hook lean resumes on leaving ground");
        }
    }
    private static void Smoke()
    {
        var package = SmokePackage.Load(Path.Combine(AppContext.BaseDirectory, "SmokePackage"));
        var settings = package.Settings;
        Near(settings.Rate, 60, "extracted smoke rate");
        Near(settings.LifetimeMin, 3, "extracted minimum lifetime"); Near(settings.LifetimeMax, 4, "extracted maximum lifetime");
        Near(settings.LocalDirection, Vec3.Up, "original emitter points upward");
        Check(package.TexturePng.Length > 100 && package.TextureSha256.Length == 64, "original smoke PNG loads with verified hash");
        var trails = new List<HeroSmokeTrail>();
        foreach (float scale in new[] { 1f, 10f })
        {
            var hero = new HeroController(new CharacterConfig { WorldScale = scale });
            hero.Respawn(new Vec3(0, 30, 0) * scale);
            var trail = new HeroSmokeTrail(settings, 42);
            trails.Add(trail);
            trail.Advance(Dt, hero.Motor, hero.RenderPose);
            Check(trail.Particles.Count == 0, "unpowered fall emits no smoke");
            for (int n = 0; n < 25; n++)
            {
                hero.Advance(Dt, new HeroInputFrame { MoveY = 1, Held = HeroButtons.Jump });
                var velocity = hero.Motor.Velocity; float gas = hero.Motor.Gas;
                trail.Advance(Dt, hero.Motor, hero.RenderPose);
                Near(hero.Motor.Velocity, velocity, "optional smoke does not alter movement");
                Near(hero.Motor.Gas, gas, "optional smoke does not consume gas");
            }
            // float frame steps represent slightly less than 0.5 seconds; emission keeps the fractional remainder.
            Check(trail.Particles.Count >= 29 && trail.Particles.Count <= 30, "extracted emission rate on frame clock");
        }
        Check(trails[0].Particles.Count == trails[1].Particles.Count, "smoke count is scale-independent");
        for (int n = 0; n < trails[0].Particles.Count; n++)
        {
            var a = trails[0].Particles[n]; var b = trails[1].Particles[n];
            Near(b.Position / 10, a.Position, "world-space smoke scales with hero", 0.001f);
            Near(b.Velocity / 10, a.Velocity, "smoke drift scales with hero");
            Near(b.Size / 10, a.Size, "smoke size scales with hero");
            Near(b.Age, a.Age, "particle age does not scale");
        }
        var motor = new CharacterMotor();
        var first = trails[0].Particles[0];
        var distant = new HeroPose { Position = new Vec3(1000, 1000, 1000), Yaw = 90 };
        trails[0].Advance(Dt, motor, distant);
        var after = trails[0].Particles[0];
        Near(after.Position, first.Position + first.Velocity * Dt, "existing smoke stays in world space when hero moves");
        Check(after.Alpha < first.Alpha, "smoke fades with particle age");
        trails[0].Enabled = false;
        for (int n = 0; n < 205; n++) trails[0].Advance(Dt, motor, distant);
        Check(trails[0].Particles.Count == 0, "smoke expires after source lifetime with emission disabled");
        trails[1].Reset(); Check(trails[1].Particles.Count == 0, "respawn clears smoke and emitter history");
        var thrust = new HeroController();
        thrust.Advance(Dt, new HeroInputFrame { Held = HeroButtons.Jump });
        Check(thrust.Motor.GasThrustActive, "disabled smoke check has active thrust");
        var disabled = new HeroSmokeTrail(settings) { Enabled = false };
        disabled.Advance(Dt, thrust.Motor, thrust.RenderPose);
        Check(disabled.Particles.Count == 0, "disabled optional system emits nothing during thrust");
        disabled.Enabled = true; disabled.Advance(Dt, thrust.Motor, thrust.RenderPose);
        Check(disabled.Particles.Count == 1, "enabling smoke starts emission during thrust");
    }
}
