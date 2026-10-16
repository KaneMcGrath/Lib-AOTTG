// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using LibAottg;

internal sealed class TestWorld : IHookGeometryWorld
{
    public readonly List<CharacterEvent> Events = new List<CharacterEvent>();
    public Vec3 Target = new Vec3(0, 10, 20), Anchor, Muzzle;
    public Vec3? RightTarget;
    public bool UseMuzzle;
    public bool AimHit = true, ProjectileHit = true, Moving, Exists = true;
    public float Scale = 1;
    public Vec3 LastOrigin;
    public float LastRange;
    public bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit)
    { LastOrigin = origin; LastRange = distance; hit = new HookHit((side == HookSide.Right && RightTarget.HasValue ? RightTarget.Value : Target) * Scale); return AimHit; }
    public Vec3 HookOrigin(CharacterMotor m, HookSide side) { return UseMuzzle ? Muzzle : m.Position; }
    public bool SweepHook(Vec3 start, Vec3 end, HookSide side, out HookHit hit)
    {
        Vec3 target = (side == HookSide.Right && RightTarget.HasValue ? RightTarget.Value : Target) * Scale;
        Vec3 d = end - start;
        float t = d.LengthSquared > 0 ? Vec3.Dot(target - start, d) / d.LengthSquared : -1;
        hit = new HookHit(target) { TargetId = Moving ? 7 : 0, LocalPoint = new Vec3(1, 2, 3) };
        return ProjectileHit && t >= 0 && t <= 1 && (start + d * t - target).Length < 0.001f * Scale;
    }
    public bool ResolveHookAnchor(int targetId, Vec3 localPoint, out Vec3 point)
    { point = Anchor * Scale; return Exists; }
    public void OnCharacterEvent(CharacterEvent kind) { Events.Add(kind); }
}

internal static class Program
{
    private static int checks;
    private const float Dt = 0.02f;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Near(float value, float expected, string name, float tolerance = 0.0001f)
    { Check(Math.Abs(value - expected) <= tolerance, name + ": " + value + " expected " + expected); }
    private static void Near(Vec3 value, Vec3 expected, string name, float tolerance = 0.0001f)
    { Check((value - expected).Length <= tolerance, name + ": " + value + " expected " + expected); }
    private static CharacterInput Input() { return new CharacterInput { LookDirection = new Vec3(0, 0, 1), AimDirection = new Vec3(0, 1, 1) }; }
    private static void Tick(CharacterMotor motor, TestWorld world, CharacterInput input, bool ground = false)
    {
        if (ground) motor.SetPose(new Vec3(motor.Position.X, 0, motor.Position.Z), new Vec3(motor.Velocity.X, 0, motor.Velocity.Z), motor.FacingDegrees);
        motor.Step(Dt, input, new CharacterContacts { Grounded = ground }, world);
    }
    private static CharacterMotor Attached(TestWorld world)
    {
        var motor = new CharacterMotor();
        var input = Input(); input.AimDirection = world.Target; input.HookLeft = new Button(true, true, false);
        motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
        for (int n = 0; n < 100 && !motor.LeftHookAttached; n++)
        {
            // Keep the muzzle/hero stationary while testing projectile travel.
            motor.SetPose(Vec3.Zero, Vec3.Zero, 0);
            motor.StepPhysics(Dt, input, new CharacterContacts(), world);
        }
        Check(motor.LeftHookAttached, "projectile reached reference anchor");
        return motor;
    }
    private static Vec3 ReadVector(JsonElement array)
    { var a = array.EnumerateArray().Select(v => v.GetSingle()).ToArray(); return new Vec3(a[0], a[1], a[2]); }
    private static void UnityReference()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/unity-4.6-physics.json")));
        var velocities = fixture.RootElement.GetProperty("scenarios").EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString(), r => ReadVector(r.GetProperty("velocity")));
        var world = new TestWorld();
        var input = Input(); input.MoveY = 1;
        var motor = new CharacterMotor(); motor.Respawn(Vec3.Zero, true); motor.SetPose(Vec3.Zero, new Vec3(0, -2, -15), 0);
        motor.StepPhysics(Dt, input, new CharacterContacts { Grounded = true }, world);
        Near(motor.Velocity, velocities["ground"], "Unity ground force");
        motor = new CharacterMotor(); motor.Respawn(Vec3.Zero, true); motor.SetPose(Vec3.Zero, new Vec3(0, 0, 10), 0);
        input.Jump = new Button(true, true, false);
        motor.AdvanceFrame(Dt, input, new CharacterContacts { Grounded = true }, world);
        input.Jump = new Button(true, false, false);
        for (int n = 0; n < 5; n++) motor.AdvanceFrame(Dt, input, new CharacterContacts { Grounded = true }, world);
        motor.StepPhysics(Dt, input, new CharacterContacts { Grounded = true }, world);
        Near(motor.Velocity, velocities["jump"], "Unity delayed jump");
        motor = new CharacterMotor(); motor.SetPose(Vec3.Zero, new Vec3(0, 0, 10), 0);
        input = Input(); input.MoveY = 1; input.Dash = new Button(true, true, false);
        motor.Step(Dt, input, new CharacterContacts(), world);
        Near(motor.Velocity, velocities["dash"], "Unity additive dash");
        Near(motor.Gas, 96, "dash gas");
        world.Target = new Vec3(0, 0, -20);
        motor = Attached(world); motor.SetPose(Vec3.Zero, new Vec3(0, 0, 10), 0);
        input = Input(); input.HookLeft = new Button(true, false, false);
        motor.StepPhysics(Dt, input, new CharacterContacts(), world);
        Near(motor.Velocity, velocities["pull"], "Unity pull plus away damping");
        foreach (var entry in new[] { ("spin_in", -0.8f), ("spin_out", 0.8f), ("spin_diagonal", 0.8f), ("spin_vertical", 0.8f) })
        {
            world = new TestWorld { Target = entry.Item1 == "spin_diagonal" ? new Vec3(-10, 0, -10) : entry.Item1 == "spin_vertical" ? new Vec3(0, -20, 0) : new Vec3(0, 0, -20) };
            motor = Attached(world);
            Vec3 v = entry.Item1 == "spin_diagonal" ? new Vec3(10, 0, 10) : entry.Item1 == "spin_vertical" ? new Vec3(0, 10, 0) : new Vec3(0, 0, 10);
            motor.SetPose(Vec3.Zero, v, 0);
            input = Input(); input.HookLeft = new Button(true, false, false); input.Jump = new Button(true, false, false); input.Reel = entry.Item2;
            motor.StepPhysics(Dt, input, new CharacterContacts(), world);
            Check(motor.Spinning, entry.Item1 + " spin activated");
            Near(motor.Velocity, velocities[entry.Item1], "Unity " + entry.Item1, 0.0002f);
        }
        world = new TestWorld(); motor = new CharacterMotor(); input = Input(); input.MoveY = 1;
        motor.StepPhysics(Dt, input, new CharacterContacts { FrontWall = true }, world);
        motor.SetPose(Vec3.Zero, new Vec3(0, 0, 10), 0);
        input.MoveY = 0;
        motor.StepPhysics(Dt, input, new CharacterContacts { FrontWall = true }, world);
        Near(motor.Velocity, velocities["walljump"], "Unity wall run and wall jump impulse");
        motor = new CharacterMotor(); input.MoveY = 1;
        motor.StepPhysics(Dt, input, new CharacterContacts { FrontWall = true }, world);
        motor.StepPhysics(Dt, input, new CharacterContacts { FrontWall = true, UpperFrontClear = true }, world);
        motor.StepPhysics(Dt, input, new CharacterContacts(), world);
        Near(motor.Velocity, velocities["roof"], "Unity roof hold cancels gravity");
    }
    private static void Mechanics()
    {
        var world = new TestWorld(); var motor = new CharacterMotor(); var input = Input();
        input.Jump = new Button(true, true, false);
        motor.Respawn(Vec3.Zero, true);
        Tick(motor, world, input, true);
        Check(!world.Events.Contains(CharacterEvent.Jumped), "jump waits for clip threshold");
        input.Jump = new Button(true, false, false);
        for (int n = 0; n < 5; n++) Tick(motor, world, input, true);
        Check(world.Events.Count(e => e == CharacterEvent.Jumped) == 1, "jump edge emits once");
        motor = new CharacterMotor(); motor.SetPose(new Vec3(0, 10, 0), new Vec3(100, 0, 200), 0);
        input = Input(); input.MoveX = -1;
        Tick(motor, world, input); Near(motor.Velocity.X, 100, "unboosted air momentum"); Near(motor.Velocity.Z, 200, "no airborne cap");
        input.Jump = new Button(true, false, false); Tick(motor, world, input);
        Near(motor.Velocity.X, 99.6f, "ACL air acceleration");
        motor = new CharacterMotor(); input = Input(); input.Forward = new Button(true, true, false); input.MoveY = 1;
        motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
        input.Forward = new Button(true, false, false);
        motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
        input.Forward = new Button(true, true, false);
        motor.AdvanceFrame(Dt, input, new CharacterContacts(), world); Check(motor.IsDashing, "double tap dash");
        world = new TestWorld(); motor = new CharacterMotor(); input = Input(); input.AimDirection = world.Target; input.HookLeft = new Button(true, true, false);
        Tick(motor, world, input); Check(motor.LeftHookPhase == HookPhase.Flying, "hook flight is not immediate");
        Near(motor.Gas, 99.8f, "launch costs gas before attachment");
        world.ProjectileHit = false;
        for (int n = 0; n < 45; n++) { input.HookLeft = new Button(false, false, false); Tick(motor, world, input); }
        Check(world.Events.Contains(CharacterEvent.HookMissedLeft), "traveling hook times out");
        world = new TestWorld { AimHit = false }; motor = new CharacterMotor(); input.HookLeft = new Button(true, true, false);
        Tick(motor, world, input); Check(!motor.LeftHookVisible && world.Events.Count == 0, "no aim hit does not launch");
        world = new TestWorld { Target = new Vec3(0, 0, 20) }; motor = Attached(world);
        input = Input(); input.HookLeft = new Button(true, false, false);
        for (int n = 0; n < 20; n++) Tick(motor, world, input);
        input.HookLeft = default(Button); Tick(motor, world, input);
        Check(!motor.LeftHookAttached, "release grace starts on attachment, not key release");
        world = new TestWorld { Target = new Vec3(0, 10, 20), Anchor = new Vec3(0, 10, 20), Moving = true };
        motor = Attached(world); world.Anchor = new Vec3(4, 12, 20); input.HookLeft = new Button(true, false, false);
        Tick(motor, world, input); Near(motor.LeftHookAnchor, world.Anchor, "moving anchor");
        world.Exists = false; Tick(motor, world, input); Check(!motor.LeftHookAttached, "destroyed anchor releases");
        world = new TestWorld(); motor = new CharacterMotor(); input = Input(); input.Attack = new Button(true, true, false);
        Tick(motor, world, input); float blade = motor.Blade;
        input.Attack = new Button(true, false, false);
        for (int n = 0; n < 20; n++) Tick(motor, world, input);
        Check(motor.Animation.Speed == 0 && !motor.AttackHitboxActive, "aerial held attack pauses windup");
        float time = motor.Animation.Time;
        for (int n = 0; n < 10; n++) Tick(motor, world, input);
        Near(motor.Animation.Time, time, "held attack remains paused"); Near(motor.Blade, blade, "swings do not spend blades");
        input.Attack = new Button(false, false, true);
        for (int n = 0; n < 15; n++) Tick(motor, world, input);
        Check(world.Events.Contains(CharacterEvent.AttackWindowOpened) && world.Events.Contains(CharacterEvent.AttackWindowClosed), "blade attack windows");
        Near(motor.Blade, 100, "missed swing retains durability"); motor.BladeHit(world); Near(motor.Blade, 98, "confirmed contact consumes two");
        motor = new CharacterMotor(); input = Input(); input.Reload = new Button(true, true, false); Tick(motor, world, input, true);
        input = Input();
        for (int n = 0; n < 9; n++) Tick(motor, world, input, true);
        Check(motor.BladesEquipped && motor.SpareBladePairs == 5, "reload before throw threshold");
        Tick(motor, world, input, true); Check(!motor.BladesEquipped && motor.SpareBladePairs == 4, "ground reload throws at 13 percent");
        for (int n = 0; n < 18; n++) Tick(motor, world, input, true);
        Check(motor.BladesEquipped && motor.Blade == 100, "ground reload equips at 37 percent");
        for (int n = 0; n < 50; n++) Tick(motor, world, input, true);
        motor.BladeHit(world, 100); Check(!motor.BladesEquipped && motor.Blade == 0 && motor.SpareBladePairs == 3, "broken blades throw once");
        motor.BladeHit(world, 100); Check(motor.SpareBladePairs == 3, "no duplicate break throw");
        motor.SetEquipment(EquipmentMode.Guns); input.Skill = new Button(false, false, true); Tick(motor, world, input);
        Check(motor.LeftGunLoaded && motor.RightGunLoaded, "shot waits for animation");
        input = Input(); for (int n = 0; n < 10; n++) Tick(motor, world, input);
        Check(!motor.LeftGunLoaded && !motor.RightGunLoaded && world.Events.Contains(CharacterEvent.GunFiredLeft), "shot timing");
        for (int n = 0; n < 40; n++) Tick(motor, world, input);
        for (int n = 0; n < 50; n++) Tick(motor, world, input, true);
        input.Reload = new Button(true, true, false); Tick(motor, world, input, true);
        Check(motor.Animation.Clip == "AHSS_gun_reload_both", "source gun reload clip");
        input = Input(); for (int n = 0; n < 70; n++) Tick(motor, world, input, true);
        Check(motor.LeftGunLoaded && motor.LeftGunReserve == 6, "gun reload at 62 percent");
        motor.Die(world); Vec3 dead = motor.Position; Tick(motor, world, input); Near(motor.Position, dead, "dead motor inert");
        motor.Respawn(new Vec3(1, 2, 3)); Check(motor.State == CharacterState.Idle && motor.Gas == 100, "respawn");
        motor.Grab(world); Tick(motor, world, input); Check(motor.State == CharacterState.Grabbed, "grabbed motor inert");
        motor.EscapeGrab(Vec3.Zero, world); Check(motor.State == CharacterState.Idle, "escape");
        var levi = HeroStats.RcModPresets()["Levi"].ToConfig(); Near(levi.Mass, 0.45f, "ACL force mass"); Near(levi.AirAcceleration, 30, "ACL air thrust");
        bool rejected = false; try { new CharacterMotor(new CharacterConfig { WorldScale = 0 }); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected, "zero scale rejected");
        rejected = false; try { motor.Step(float.NaN, input, new CharacterContacts(), world); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected, "nonfinite dt rejected");
    }
    private static void ScaleChecks()
    {
        foreach (float scale in new[] { 0.001f, 0.1f, 10f, 1000f })
        {
            var a = new CharacterMotor(); var b = new CharacterMotor(new CharacterConfig { WorldScale = scale });
            var wa = new KinematicCollisionWorld(); var wb = new KinematicCollisionWorld(worldScale: scale);
            wa.Boxes.Add(new WorldBox(new Vec3(-20, 0, 40), new Vec3(20, 40, 42)));
            wb.Boxes.Add(new WorldBox(new Vec3(-20, 0, 40) * scale, new Vec3(20, 40, 42) * scale));
            var ea = new List<CharacterEvent>(); var eb = new List<CharacterEvent>(); wa.Event = ea.Add; wb.Event = eb.Add;
            for (int tick = 0; tick < 400; tick++)
            {
                var input = Input(); input.MoveY = tick < 130 ? 1 : 0; input.AimDirection = new Vec3(0, 0.3f, 1);
                input.HasAimOrigin = true; input.AimOrigin = a.Position + Vec3.Up;
                input.Jump = new Button(tick >= 15 && tick < 40, tick == 15, tick == 40);
                input.Dash = new Button(false, tick == 30, false);
                input.HookLeft = new Button(tick >= 55 && tick < 115, tick == 55, tick == 115);
                input.HookRight = new Button(tick >= 65 && tick < 115, tick == 65, tick == 115);
                input.Attack = new Button(tick >= 155 && tick < 190, tick == 155, tick == 190);
                input.Reload = new Button(false, tick == 230, false);
                input.Reel = tick % 2 == 0 ? -0.6f : 0.6f;
                Vec3 pa = a.Position, pb = b.Position;
                var ca = wa.Contacts(pa, new Vec3(0, 0, 1)); var cb = wb.Contacts(pb, new Vec3(0, 0, 1));
                ca.Grounded &= a.Velocity.Y <= 0; cb.Grounded &= b.Velocity.Y <= 0;
                a.Step(Dt, input, ca, wa);
                input.AimOrigin *= scale; b.Step(Dt, input, cb, wb);
                var ra = wa.Move(pa, a.Position, a.Velocity); var rb = wb.Move(pb, b.Position, b.Velocity);
                a.SetPose(ra.Position, ra.Velocity, a.FacingDegrees); b.SetPose(rb.Position, rb.Velocity, b.FacingDegrees);
                Near(b.Position / scale, a.Position, "scaled trajectory " + scale, 0.005f);
                Near(b.Velocity / scale, a.Velocity, "scaled velocity " + scale, 0.005f);
                Near(b.Gas, a.Gas, "scale preserves gas");
                Check(b.State == a.State && b.Animation.Clip == a.Animation.Clip, "scale preserves states and animation");
                Check(ea.SequenceEqual(eb), "scale preserves ordered events");
            }
        }
        var world = new TestWorld { Scale = 10, Target = new Vec3(0, 10, 20), Anchor = new Vec3(0, 10, 20), Moving = true };
        var motor = new CharacterMotor(new CharacterConfig { WorldScale = 10 }); var i = Input(); i.HasAimOrigin = true; i.AimOrigin = new Vec3(3, 4, 5); i.AimDirection = world.Target; i.HookLeft = new Button(true, true, false);
        motor.AdvanceFrame(Dt, i, new CharacterContacts(), world);
        Near(world.LastOrigin, i.AimOrigin, "aim origin crosses scale boundary once"); Near(world.LastRange, 100000, "scaled aim range");
        for (int n = 0; n < 10 && !motor.LeftHookAttached; n++) { motor.SetPose(Vec3.Zero, Vec3.Zero, 0); motor.StepPhysics(Dt, i, new CharacterContacts(), world); }
        Check(motor.LeftHookAttached, "scaled moving anchor attaches"); world.Anchor = new Vec3(4, 12, 20);
        motor.StepPhysics(Dt, i, new CharacterContacts(), world); Near(motor.LeftHookAnchor, world.Anchor * 10, "scaled moving anchor is host-owned");
    }
    private static void ReplayLiveHero(string path)
    {
        CharacterMotor motor = null;
        var world = new TestWorld { AimHit = false };
        int samples = 0, expectedSamples = 0;
        float positionError = 0, velocityError = 0;
        bool floor = false; var collision = new KinematicCollisionWorld();
        foreach (string line in TraceLines(path))
        {
            if (line.Length == 0) continue;
            using var json = JsonDocument.Parse(line);
            var row = json.RootElement;
            if (!row.TryGetProperty("phase", out var phaseValue)) continue;
            string phase = phaseValue.GetString();
            if (phase == "reset")
            {
                floor = row.TryGetProperty("floor", out var f) && f.GetBoolean();
                motor = new CharacterMotor(HeroStats.RcModPresets()["Mikasa"].ToConfig());
                motor.Respawn(ReadVector(row.GetProperty("position")), row.TryGetProperty("groundedBefore", out var groundedBefore) && groundedBefore.GetBoolean());
                motor.SetPose(ReadVector(row.GetProperty("position")), ReadVector(row.GetProperty("velocity")), 0);
                if (row.TryGetProperty("gas", out var gas)) motor.ConsumeGas(motor.Config.GasCapacity - gas.GetSingle());
            }
            else if (motor != null && phase == "command" && row.GetProperty("name").GetString() == "dash")
            {
                var input = Input(); input.MoveY = 1; input.Dash = new Button(true, true, false);
                motor.AdvanceFrame(0.00000001f, input, new CharacterContacts(), world);
            }
            else if (motor != null && phase == "command" && row.GetProperty("name").GetString() == "jump")
            {
                var input = Input(); input.Jump = new Button(false, true, false);
                motor.AdvanceFrame(0.00000001f, input, new CharacterContacts { Grounded = true }, world);
            }
            else if (motor != null && phase == "set-velocity")
                motor.SetPose(motor.Position, ReadVector(row.GetProperty("velocity")), motor.FacingDegrees);
            else if (motor != null && phase == "hook-command")
            {
                world.AimHit = true; world.Target = ReadVector(row.GetProperty("target"));
                world.UseMuzzle = true; world.Muzzle = ReadVector(row.GetProperty("muzzle"));
                var input = Input(); input.HookLeft = new Button(true, false, false); input.AimDirection = world.Target - motor.Position;
                motor.AdvanceFrame(0.00000001f, input, new CharacterContacts(), world);
            }
            else if (motor != null && (phase == "frame.before" || phase == "fixed.before"))
            {
                var input = Input();
                input.LookDirection = ReadVector(row.GetProperty("look"));
                if (row.TryGetProperty("driverMoveY", out var y)) input.MoveY = y.GetSingle();
                if (row.TryGetProperty("driverJump", out var jump)) input.Jump = new Button(jump.GetBoolean(), false, false);
                if (row.TryGetProperty("driverLeft", out var left)) input.HookLeft = new Button(left.GetBoolean(), false, false);
                if (phase == "fixed.before" && row.TryGetProperty("leftHeld", out var held)) input.HookLeft = new Button(held.GetBoolean(), false, false);
                var contacts = new CharacterContacts {
                    Grounded = row.TryGetProperty("groundProbe", out var g) && g.GetBoolean(),
                    FrontWall = row.TryGetProperty("frontProbe", out var front) && front.GetBoolean(),
                    UpperFrontClear = row.TryGetProperty("upperClear", out var upper) && upper.GetBoolean()
                };
                if (phase == "frame.before") motor.AdvanceFrame(row.GetProperty("dt").GetSingle(), input, contacts, world);
                else
                {
                    var start = motor.Position;
                    motor.StepPhysics(row.GetProperty("dt").GetSingle(), input, contacts, world);
                    if (floor)
                    {
                        Vec3 offset = Vec3.Up * 1000;
                        var result = collision.Move(start - offset, motor.Position - offset, motor.Velocity);
                        motor.SetPose(result.Position + offset, result.Velocity, motor.FacingDegrees);
                    }
                }
            }
            else if (motor != null && phase == "observed")
            {
                Vec3 p = ReadVector(row.GetProperty("position")), v = ReadVector(row.GetProperty("velocity"));
                float pe = (p - motor.Position).Length, ve = (v - motor.Velocity).Length;
                positionError = Math.Max(positionError, pe); velocityError = Math.Max(velocityError, ve);
                Near(motor.Position, p, "live HERO trajectory scenario " + row.GetProperty("scenario") + " tick " + row.GetProperty("tick"), floor ? 0.025f : 0.0002f);
                Near(motor.Velocity, v, "live HERO velocity scenario " + row.GetProperty("scenario") + " tick " + row.GetProperty("tick"), floor ? 0.003f : 0.0002f);
                if (row.TryGetProperty("gas", out var gas)) Near(motor.Gas, gas.GetSingle(), "live HERO gas", 0.0002f);
                samples++;
            }
            else if (phase == "complete") { expectedSamples = row.TryGetProperty("observations", out var observations) ? observations.GetInt32() : row.GetProperty("scenarios").GetInt32() * 30; break; }
            else if (phase == "error") throw new Exception("Live HERO reference reported an error.");
        }
        Check(expectedSamples > 0 && samples == expectedSamples, "live HERO scenario completeness");
        Console.WriteLine("Live HERO replay: " + samples + " ticks, max position error " + positionError + ", velocity error " + velocityError);
    }
    private static IEnumerable<string> TraceLines(string path)
    {
        using var input = File.OpenRead(path);
        using Stream stream = path.EndsWith(".gz") ? new GZipStream(input, CompressionMode.Decompress) : input;
        using var reader = new StreamReader(stream);
        string line;
        while ((line = reader.ReadLine()) != null) yield return line;
    }
    private static void Main(string[] args)
    {
        UnityReference(); Mechanics(); GameplayIssues(); GameplayFollowup(); WallBurst(); checks += HookSpecialChecks.Run(); ScaleChecks();
        ReplayLiveHero(args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "fixtures/live-hero.jsonl.gz"));
        Console.WriteLine("Passed " + checks + " HERO physics, action and scale checks.");
    }

    private static void WallBurst()
    {
        var runningWorld = new TestWorld();
        var runningMotor = new CharacterMotor();
        var runningInput = Input(); runningInput.MoveY = 1;
        runningMotor.StepPhysics(Dt, runningInput, new CharacterContacts { FrontWall = true }, runningWorld);
        runningInput.Dash = new Button(true, true, false);
        runningMotor.AdvanceFrame(Dt, runningInput, new CharacterContacts { FrontWall = true }, runningWorld);
        Check(runningMotor.IsDashing, "burst during wallrun before wall jump is ordinary");
        var native = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "fixtures/wall-burst.jsonl"))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .Where(row => row.TryGetProperty("phase", out var phase) && phase.GetString() == "wall-burst").ToArray();
        foreach (string scenario in new[] { "ordinary", "release", "timeout", "expired" })
        {
            var rows = native.Where(row => row.GetProperty("scenario").GetString() == scenario).ToArray();
            var before = rows.Single(row => row.GetProperty("stage").GetString() == "before");
            var command = rows.Single(row => row.GetProperty("stage").GetString() == "command");
            var ticks = rows.Where(row => row.GetProperty("stage").GetString().StartsWith("tick-")).ToArray();
            bool canceled = scenario == "release" || scenario == "timeout";
            Check((before.GetProperty("state").GetString() == "GroundDodge") == canceled, "native cancellation window");
            Near(command.GetProperty("dashTime").GetSingle(), canceled ? 0 : 0.5f, "native timer after state setter");
            Near(command.GetProperty("gas").GetSingle(), before.GetProperty("gas").GetSingle() * 0.96f, "native burst gas cost");
            Near(ReadVector(ticks[0].GetProperty("velocity")).Z, ReadVector(before.GetProperty("velocity")).Z - 40, "native additive boost");
            if (canceled)
                foreach (var tick in ticks) Near(ReadVector(tick.GetProperty("velocity")).Z, ReadVector(ticks[0].GetProperty("velocity")).Z, "native canceled burst retains momentum");
            else Check(ReadVector(ticks[11].GetProperty("velocity")).Z > ReadVector(ticks[0].GetProperty("velocity")).Z, "native ordinary burst damps");
        }
        foreach (float scale in new[] { 0.001f, 1f, 10f, 1000f })
        foreach (bool timeout in new[] { false, true })
        foreach (bool expired in new[] { false, true })
        foreach (bool doubleTap in new[] { false, true })
        {
            var config = new CharacterConfig { WorldScale = scale };
            var world = new TestWorld { Scale = scale };
            var motor = new CharacterMotor(config);
            var input = Input(); input.MoveY = 1;
            var wall = new CharacterContacts { FrontWall = true };
            motor.StepPhysics(Dt, input, wall, world);
            Check(motor.State == CharacterState.WallRun, "burst setup enters wallrun");
            if (timeout)
                for (int n = 0; n < 51 && motor.State == CharacterState.WallRun; n++) motor.Step(Dt, input, wall, world);
            else { input.MoveY = 0; motor.Step(Dt, input, wall, world); }
            Check(motor.State == CharacterState.GroundDodge && world.Events.Contains(CharacterEvent.WallJumped), "wall release/timeout enters airborne dodge");
            if (expired)
                for (int n = 0; n < 25; n++) { input = Input(); motor.Step(Dt, input, new CharacterContacts(), world); }
            input = Input(); input.MoveY = -1;
            if (doubleTap)
            {
                input.Back = new Button(true, true, false);
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                input.Back = new Button(false, false, true);
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                input.Back = new Button(true, true, false);
            }
            else input.Dash = new Button(true, true, false);
            Vec3 before = motor.Velocity;
            float gas = motor.Gas;
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            Check(motor.IsDashing == expired, "only active airborne dodge clears burst timer");
            Near(motor.Gas, gas - 4, "canceled burst still costs gas");
            Check(world.Events.Count(e => e == CharacterEvent.Dashed) == 1, "burst event emitted once");
            motor.StepPhysics(Dt, input, new CharacterContacts(), world);
            Near(motor.Velocity / scale, before / scale + new Vec3(0, -config.Gravity * Dt, -config.DashSpeed), "wall burst keeps full impulse", 0.002f);
            input = Input(); input.MoveY = -1;
            float boosted = motor.Velocity.Z / scale;
            for (int n = 0; n < 6; n++) motor.Step(Dt, input, new CharacterContacts(), world);
            if (!expired) Near(motor.Velocity.Z / scale, boosted, "wall burst skips all damping", 0.002f);
            else Check(motor.Velocity.Z / scale > boosted, "burst after dodge expires damps normally");
            input.Dash = new Button(true, true, false);
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            Check(world.Events.Count(e => e == CharacterEvent.Dashed) == 1, "canceled burst preserves cooldown");
            motor.Respawn(Vec3.Zero);
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            Check(motor.IsDashing, "respawn clears wall burst state");
        }
    }
    private static void GameplayFollowup()
    {
        foreach (float scale in new[] { 0.001f, 1f, 10f, 1000f })
        {
            foreach (bool frameFirst in new[] { false, true })
            foreach (float verticalSpeed in new[] { -5f, 5f })
            {
                var world = new TestWorld { Scale = scale, Target = new Vec3(0, 10, 20) };
                var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale });
                var input = Input(); input.HookLeft = new Button(true, true, false);
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                for (int n = 0; n < 100 && !motor.LeftHookAttached; n++)
                { motor.SetPose(Vec3.Zero, Vec3.Zero, 0); motor.StepPhysics(Dt, input, new CharacterContacts(), world); }
                Check(motor.LeftHookAttached, "slide regression starts with an attached hook");
                input.HookLeft = new Button(true, false, false);
                motor.SetPose(Vec3.Zero, new Vec3(0, 0, 60) * scale, 0);
                motor.StepPhysics(Dt, input, new CharacterContacts { Grounded = true }, world);
                Check(motor.State == CharacterState.Slide && motor.Animation.Clip == "slide", "swing ground contact starts slide");
                motor.SetPose(Vec3.Up * scale, new Vec3(0, verticalSpeed, 60) * scale, 0);
                if (frameFirst)
                {
                    motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                    Check(motor.State != CharacterState.Slide, "frame clears slide when contact is lost");
                }
                motor.StepPhysics(Dt, input, new CharacterContacts(), world);
                Check(motor.Animation.Clip == "air_hook_l" && motor.State != CharacterState.Slide, "swing resumes hooked aerial clip immediately");
                Check(motor.LeftHookAttached, "leaving slide preserves the swing hook");
                motor.ReleaseHook(HookSide.Left, world);
                motor.SetPose(Vec3.Up * scale, new Vec3(0, verticalSpeed, 0) * scale, 0);
                motor.StepPhysics(Dt, input, new CharacterContacts(), world);
                Check(motor.Animation.Clip == (verticalSpeed < 0 ? "air_fall" : "air_rise"), "unhooked aerial selection still follows velocity");
            }
            foreach (bool empty in new[] { false, true })
            {
                var world = new TestWorld { Scale = scale };
                var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale });
                if (empty) motor.ConsumeGas(motor.Gas);
                motor.SetPose(Vec3.Up * 10 * scale, new Vec3(4, 0, 6) * scale, 0);
                var input = Input(); input.MoveY = 1; input.Dodge = new Button(true, true, false);
                motor.Step(Dt, input, new CharacterContacts(), world);
                Check(!world.Events.Contains(CharacterEvent.Dodged) && motor.Animation.Clip != "dodge", "dodge cannot start in air with or without gas");
                Near(motor.Velocity / scale, new Vec3(4, -20 * Dt, 6), "rejected aerial dodge preserves momentum");
                motor.Respawn(Vec3.Zero, true); world.Events.Clear();
                if (empty) motor.ConsumeGas(motor.Gas);
                float gasBefore = motor.Gas;
                motor.Step(Dt, input, new CharacterContacts { Grounded = true }, world);
                Check(world.Events.Contains(CharacterEvent.Dodged) && motor.State == CharacterState.GroundDodge, "ground dodge works with or without gas");
                Near(motor.Gas, gasBefore, "ground dodge has no burst gas cost");
                input.Dodge = default; input.Dash = new Button(true, true, false);
                motor.Respawn(Vec3.Zero, true); world.Events.Clear();
                motor.Step(Dt, input, new CharacterContacts { Grounded = true }, world);
                Check(!world.Events.Contains(CharacterEvent.Dodged) && !motor.IsDashing, "burst input does not ground dodge");
                motor.Respawn(Vec3.Up * 10 * scale); world.Events.Clear();
                if (empty) motor.ConsumeGas(motor.Gas);
                motor.Step(Dt, input, new CharacterContacts(), world);
                Check(motor.IsDashing == !empty && world.Events.Contains(CharacterEvent.Dashed) == !empty, "air burst requires gas");
                Check(!world.Events.Contains(CharacterEvent.Dodged), "empty burst never falls back to aerial dodge");
            }
        }
        foreach (int direction in Enumerable.Range(0, 4))
        {
            var config = new CharacterConfig { DoubleTapDash = false };
            var motor = new CharacterMotor(config); var world = new TestWorld(); var input = Input();
            input.MoveY = direction < 2 ? (direction == 0 ? -1 : 1) : 0;
            input.MoveX = direction >= 2 ? (direction == 2 ? -1 : 1) : 0;
            if (direction == 0) input.Back = new Button(true, true, false);
            if (direction == 1) input.Forward = new Button(true, true, false);
            if (direction == 2) input.Left = new Button(true, true, false);
            if (direction == 3) input.Right = new Button(true, true, false);
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            Check(!motor.IsDashing, "disabled double tap does not burst in any direction");
            config.DoubleTapDash = true;
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            Check(!motor.IsDashing, "enabling double tap does not reuse disabled taps");
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            Check(motor.IsDashing, "enabled double tap bursts in each direction");
            motor.Respawn(Vec3.Zero); config.DoubleTapDash = false;
            input.Dash = new Button(true, true, false);
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            Check(motor.IsDashing, "explicit burst works with double tap disabled");
        }
    }

    private static void GameplayIssues()
    {
        foreach (float scale in new[] { 0.001f, 1f, 10f, 1000f })
        {
            foreach (bool noStop in new[] { false, true })
            {
                var world = new TestWorld { Scale = scale };
                var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale, NoStopJump = noStop });
                motor.SetPose(Vec3.Zero, new Vec3(0, 0, 60) * scale, 0);
                var input = Input(); input.MoveY = 1;
                motor.StepPhysics(Dt, input, new CharacterContacts { Grounded = true }, world);
                Check(motor.State == CharacterState.Slide, "fast landing begins slide");
                input.MoveY = 0; input.Jump = new Button(true, true, false);
                motor.AdvanceFrame(Dt, input, new CharacterContacts { Grounded = true }, world);
                Check(motor.State == (noStop ? CharacterState.Slide : CharacterState.Idle), "jump exits slide unless NoStopJump is enabled");
                input.Jump = new Button(true, false, false);
                for (int n = 0; n < 7; n++) Tick(motor, world, input, true);
                Check(motor.Velocity.Y > 0, "jump impulse occurs after windup");
                if (noStop) Check(motor.Velocity.Z / scale > 50, "NoStopJump retains sliding momentum");
                else Near(motor.Velocity.Z / scale, 0, "vanilla slide stops before jumping");
            }
            {
                var world = new TestWorld { Scale = scale };
                var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale });
                motor.SetPose(new Vec3(0, 50, 0) * scale, new Vec3(9, 5, 8) * scale, 0);
                var input = Input(); input.Skill = new Button(true, true, false);
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                Near(motor.Velocity / scale, Vec3.Up * 10, "Mikasa source startup velocity");
                input.Skill = default;
                while (motor.Animation.NormalizedTime < 0.8f)
                {
                    float before = motor.Animation.NormalizedTime;
                    motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                    if (before < 0.8f && motor.Animation.NormalizedTime >= 0.8f)
                        Near(motor.Velocity / scale, -Vec3.Up * 30, "Mikasa strike starts downward at 80 percent");
                    else Check(!motor.AttackHitboxActive, "Mikasa hitbox stays closed during startup");
                }
                Check(motor.AttackHitboxActive, "Mikasa downward strike opens hitbox");
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                Near(motor.Velocity.Y / scale, -30, "strike velocity is not reset on every frame");
                while (motor.Animation.NormalizedTime < 1) motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                float previous = motor.Velocity.Y / scale;
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                Near(motor.Velocity.Y / scale, previous - 30 * Dt, "Mikasa post-clip downward acceleration uses frame time");
                motor.StepPhysics(Dt, input, new CharacterContacts { Grounded = true }, world);
                Check(motor.Animation.Clip == "attack3_2" && !motor.AttackHitboxActive, "Mikasa landing closes strike and starts recovery");
                Near(motor.Velocity.Z / scale, 0, "Mikasa landing stops horizontal velocity");
            }
        }
        foreach (bool right in new[] { false, true })
        foreach (bool guns in new[] { false, true })
        {
            var world = new TestWorld { Target = new Vec3(0, 10, 20) };
            var motor = new CharacterMotor();
            if (guns) motor.SetEquipment(EquipmentMode.Guns);
            var input = Input(); input.AimDirection = world.Target;
            if (right) input.HookRight = new Button(true, true, false); else input.HookLeft = new Button(true, true, false);
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            for (int n = 0; n < 100 && !(right ? motor.RightHookAttached : motor.LeftHookAttached); n++)
            { motor.SetPose(Vec3.Zero, Vec3.Zero, 0); motor.StepPhysics(Dt, input, new CharacterContacts(), world); }
            Check(right ? motor.RightHookAttached : motor.LeftHookAttached, "facing check attached hook");
            // Forward camera/input points west, hook points north and velocity points east.
            // The source single-hook yaw is -9 degrees for blades and +90 for guns.
            motor.SetPose(Vec3.Zero, new Vec3(20, 0, 0), 0);
            input.LookDirection = new Vec3(-1, 0, 0); input.MoveY = 1;
            motor.StepPhysics(Dt, input, new CharacterContacts(), world);
            Near(motor.FacingTargetDegrees, guns ? 90 : right ? -9 : 9, "hook facing overrides camera direction");
            if (right) motor.ReleaseHook(HookSide.Right, world); else motor.ReleaseHook(HookSide.Left, world);
            motor.StepPhysics(Dt, input, new CharacterContacts(), world);
            Near(motor.FacingTargetDegrees, -90, "camera-relative facing returns after release");
        }
        foreach (float scale in new[] { 1f, 10f })
        foreach (bool guns in new[] { false, true })
        foreach (int layout in new[] { 0, 1, 2 })
        {
            var world = new TestWorld { Scale = scale,
                Target = layout == 0 ? new Vec3(-0.5f, 4, 20) : layout == 1 ? new Vec3(-3, 4, 20) : new Vec3(-10, 4, 0),
                RightTarget = layout == 0 ? new Vec3(0.5f, 4, 20) : layout == 1 ? new Vec3(3, 4, 20) : new Vec3(0, 4, 10) };
            var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale });
            if (guns) motor.SetEquipment(EquipmentMode.Guns);
            var input = Input(); input.HookLeft = input.HookRight = new Button(true, true, false);
            motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
            for (int n = 0; n < 100 && !(motor.LeftHookAttached && motor.RightHookAttached); n++)
            { motor.SetPose(Vec3.Zero, Vec3.Zero, 0); motor.StepPhysics(Dt, input, new CharacterContacts(), world); }
            Check(motor.LeftHookAttached && motor.RightHookAttached, "dual facing check attaches both anchors");
            motor.SetPose(Vec3.Zero, new Vec3(20, 0, 0) * scale, 0);
            input.LookDirection = new Vec3(-1, 0, 0); input.MoveY = 1;
            motor.StepPhysics(Dt, input, new CharacterContacts(), world);
            Near(motor.FacingTargetDegrees, layout == 2 ? -45 : layout == 0 && guns ? 90 : 0, "source close, parallel and wide dual-hook facing", 0.001f);
        }
    }
}
