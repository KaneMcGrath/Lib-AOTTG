// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using LibAottg;

internal static class HookSpecialChecks
{
    private static int checks;
    private const float Dt = 0.02f;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static void Near(Vec3 value, Vec3 expected, string name)
    { Check((value - expected).Length < 0.001f, name + ": " + value + " expected " + expected); }
    private static Vec3 Vector(JsonElement row, string property)
    { var v = row.GetProperty(property).EnumerateArray().Select(n => n.GetSingle()).ToArray(); return new Vec3(v[0], v[1], v[2]); }
    private static CharacterInput Held(TestWorld world) => new CharacterInput {
        LookDirection = new Vec3(0, 0, 1), AimDirection = world.Target,
        HookLeft = new Button(true, false, false), HookRight = new Button(true, false, false)
    };
    private static CharacterMotor Fire(TestWorld world, string skill, float scale)
    {
        var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale, SkillId = skill });
        motor.AdvanceFrame(Dt, Held(world), new CharacterContacts(), world);
        Check(motor.LeftHookPhase == HookPhase.Flying && motor.RightHookPhase == HookPhase.Flying, "both ordinary hooks start flying");
        return motor;
    }
    private static void Attach(CharacterMotor motor, TestWorld world)
    {
        for (int n = 0; n < 100 && !(motor.LeftHookAttached && motor.RightHookAttached); n++)
        {
            motor.SetPose(Vec3.Zero, Vec3.Zero, 0);
            motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
        }
        Check(motor.LeftHookAttached && motor.RightHookAttached, "both reference hooks attach");
    }
    private static void Activate(CharacterMotor motor, TestWorld world)
    {
        var input = Held(world); input.Skill = new Button(true, true, false);
        motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
        Check(motor.State == CharacterState.Attack, "special activates through public input");
    }
    public static int Run()
    {
        // Values and transition expectations below were checked in the original Unity player.
        using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/hook-specials.json")));
        var scenarios = native.RootElement.GetProperty("observations").EnumerateArray()
            .GroupBy(r => r.GetProperty("name").GetString()).ToDictionary(g => g.Key, g => g.ToArray());
        var flying = scenarios["mikasa_flying"];
        var activated = flying.Single(r => r.GetProperty("stage").GetString() == "activated");
        var initialVelocity = activated.GetProperty("velocity").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        Vec3 startup = new Vec3(initialVelocity[0], initialVelocity[1], initialVelocity[2]);
        Check(activated.GetProperty("leftPhase").GetInt32() == 0 && activated.GetProperty("rightPhase").GetInt32() == 0,
            "native Mikasa activation leaves travelling projectiles intact");
        Check(flying.Any(r => r.GetProperty("leftPhase").GetInt32() == 1 && r.GetProperty("state").GetString() == "Idle"),
            "native hook landing cancels Mikasa");
        foreach (float scale in new[] { 0.001f, 1f, 10f, 1000f })
        {
            foreach (bool fresh in new[] { false, true })
            foreach (string skill in new[] { "mikasa", "levi", "petra" })
            {
                var world = new TestWorld { Scale = scale };
                var motor = Fire(world, skill, scale); Attach(motor, world);
                if (!fresh) for (int n = 0; n < 20; n++) motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                world.AimHit = false; // No replacement shot: exercise the shared QHold/EHold release path.
                motor.SetPose(motor.Position, new Vec3(12, -8, 24) * scale, 0);
                Activate(motor, world);
                var sourceActivation = scenarios[skill == "mikasa" ? "mikasa_attached_old" : skill + "_attached_miss"]
                    .Single(r => r.GetProperty("stage").GetString() == "activated");
                Near(motor.Velocity / scale, Vector(sourceActivation, "velocity"), "source special velocity assignment");
                Check(sourceActivation.GetProperty("leftPhase").GetInt32() == 1 && sourceActivation.GetProperty("rightPhase").GetInt32() == 1,
                    "native special with no aim replacement initially retains attachments");
                Check(motor.LeftHookAttached && motor.RightHookAttached, "activation does not directly remove attachments");
                float gas = motor.Gas;
                int ticks = fresh ? 15 : 0;
                for (int n = 0; n < ticks; n++) motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                if (fresh) Check(motor.LeftHookAttached && motor.RightHookAttached, "fresh attachments retain the strict 0.3 second grace");
                motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                Check(!motor.LeftHookAttached && !motor.RightHookAttached, "special ignores physically held hook keys when grace expires");
                Check(motor.Gas == gas, "suppressed special hook holds spend no sustain gas");
                Check(motor.State == CharacterState.Attack, "attachment release does not cancel the special");
                if (!fresh)
                {
                    var firstTick = scenarios[skill == "mikasa" ? "mikasa_attached_old" : skill + "_attached_miss"]
                        .Single(r => r.GetProperty("stage").GetString() == "tick_0");
                    Check(firstTick.GetProperty("leftPhase").GetInt32() == -1 && firstTick.GetProperty("rightPhase").GetInt32() == -1,
                        "old attachment expiry matches the native first fixed tick");
                }
            }
            {
                var world = new TestWorld { Scale = scale, Target = new Vec3(0, 0, 100) };
                var motor = Fire(world, "mikasa", scale);
                motor.SetPose(Vec3.Zero, new Vec3(12, -8, 24) * scale, 0);
                Activate(motor, world);
                Near(motor.Velocity / scale, startup, "Mikasa cancels previous momentum while shots travel");
                Check(motor.LeftHookPhase == HookPhase.Flying && motor.RightHookPhase == HookPhase.Flying, "Mikasa preserves both travelling hooks");
                bool sawWindow = false;
                for (int n = 0; n < 100 && !motor.LeftHookAttached; n++)
                {
                    // A stationary hero isolates projectile flight; its velocity still feeds forces/action timing.
                    motor.SetPose(Vec3.Zero, motor.Velocity, motor.FacingDegrees);
                    motor.Step(Dt, Held(world), new CharacterContacts(), world);
                    sawWindow |= motor.AttackHitboxActive;
                }
                Check(sawWindow, "late travelling hook reaches Mikasa after its hitbox opens");
                Check(motor.LeftHookAttached && motor.RightHookAttached && motor.State == CharacterState.Idle,
                    "landing cancels attack and immediately restores idle state");
                Check(!motor.AttackHitboxActive && world.Events.Contains(CharacterEvent.AttackWindowClosed), "landing closes an active special hitbox");
                Check(motor.Animation.Clip == "air_hook_r_just", "simultaneous hook landings use previous physics hand flags");
                Check(world.Events.Count(e => e == CharacterEvent.AttackEnded) == 1, "two simultaneous landings cancel the action only once");
                Check(!world.Events.Contains(CharacterEvent.HookReleasedLeft), "travelling-hook technique does not release the projectile");
            }
            foreach (bool hit in new[] { false, true })
            foreach (string skill in new[] { "levi", "petra" })
            {
                var world = new TestWorld { Scale = scale, Target = new Vec3(0, 0, 40) };
                var motor = Fire(world, skill, scale);
                world.AimHit = hit;
                Activate(motor, world);
                int releases = world.Events.Count(e => e == CharacterEvent.HookReleasedLeft || e == CharacterEvent.HookReleasedRight);
                var source = scenarios[skill + (hit ? "_aim_hit" : "_flying_miss")];
                var sourceBefore = source.Single(r => r.GetProperty("stage").GetString() == "before");
                var sourceActivated = source.Single(r => r.GetProperty("stage").GetString() == "activated");
                int replacements = new[] { "leftId", "rightId" }.Count(p => sourceBefore.GetProperty(p).GetInt32() != sourceActivated.GetProperty(p).GetInt32());
                Check(releases == replacements, "aim hit replaces exactly the native projectile sides");
                Check(motor.LeftHookPhase == HookPhase.Flying && motor.RightHookPhase == HookPhase.Flying, "special does not erase other travelling hooks");
                for (int n = 0; n < 30 && !(motor.RightHookAttached || motor.LeftHookPhase == HookPhase.Retracting); n++)
                {
                    motor.SetPose(Vec3.Zero, motor.Velocity, motor.FacingDegrees);
                    motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                }
                Check(motor.State == CharacterState.Attack, "landing during Levi/Petra preserves the attack state");
                Check(source.All(r => r.GetProperty("state").GetString() != "Idle" || r.GetProperty("stage").GetString() == "before"),
                    "native Levi/Petra landing also preserves the attack");
                if (skill == "petra")
                {
                    Check(!motor.LeftHookAttached && !motor.RightHookAttached && motor.LeftHookPhase == HookPhase.Retracting && motor.RightHookPhase == HookPhase.Retracting,
                        "Petra landing disables both projectiles before either becomes attached");
                    Check(!world.Events.Contains(CharacterEvent.HookAttachedLeft) && !world.Events.Contains(CharacterEvent.HookAttachedRight), "disabled Petra shots never report attachments");
                    while (motor.Animation.NormalizedTime <= 0.4f) motor.AdvanceFrame(Dt, Held(world), new CharacterContacts(), world);
                    motor.SetPose(Vec3.Zero, Vec3.Zero, 0);
                    motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                    Near(motor.Velocity / scale, new Vec3(0, 4 - 20 * Dt, 26), "Petra keeps the landing point for its later impulse");
                }
                else
                {
                    Check(motor.RightHookAttached, "Levi permits its travelling hook to attach without cancelling");
                    // Suppress frame animation until the source projectile timeout; launch grace itself is fixed-clock state.
                    for (int n = 0; n < 20; n++) motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                    Check(motor.RightHookAttached, "Levi landing grants the right hook -100 seconds of grace even for a normal shot");
                    motor.AdvanceFrame(0.41f, Held(world), new CharacterContacts(), world);
                    Check(motor.RightHookAttached == !hit, "only a Levi-mode projectile has the 0.4 second frame lifetime");
                    var final = source.Single(r => r.GetProperty("stage").GetString() == "tick_34");
                    Check(motor.RightHookAttached == (final.GetProperty("rightPhase").GetInt32() == 1), "Levi projectile lifetime matches native observation");
                }
            }
            {
                var world = new TestWorld { Scale = scale, RightTarget = new Vec3(0, 0, 80) };
                var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale });
                var input = Held(world); input.HookRight = default;
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                for (int n = 0; n < 100 && !motor.LeftHookAttached; n++)
                { motor.SetPose(Vec3.Zero, Vec3.Zero, 0); motor.StepPhysics(Dt, input, new CharacterContacts(), world); }
                Check(motor.LeftHookAttached, "mixed phase technique attaches only the first hook");
                for (int n = 0; n < 20; n++) motor.StepPhysics(Dt, input, new CharacterContacts(), world);
                input = Held(world);
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                Check(motor.RightHookPhase == HookPhase.Flying, "mixed phase technique fires a second hook");
                Activate(motor, world);
                motor.StepPhysics(Dt, input, new CharacterContacts(), world);
                Check(!motor.LeftHookVisible && motor.RightHookPhase == HookPhase.Flying, "Mikasa drops only the old attached hook and retains the travelling side");
                for (int n = 0; n < 100 && !motor.RightHookAttached; n++)
                { motor.SetPose(Vec3.Zero, motor.Velocity, 0); motor.Step(Dt, input, new CharacterContacts(), world); }
                Check(motor.RightHookAttached && motor.State == CharacterState.Idle, "the surviving travelling side cancels Mikasa on landing");
            }
            {
                var world = new TestWorld { Scale = scale };
                var motor = Fire(world, "mikasa", scale); Attach(motor, world);
                for (int n = 0; n < 20; n++) motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                motor.Config.SkillId = "levi"; world.Target = new Vec3(0, 0, 100);
                Activate(motor, world);
                Check(motor.RightHookPhase == HookPhase.Flying, "aimed replacement launches before the fixed step");
                motor.StepPhysics(Dt, Held(world), new CharacterContacts(), world);
                Check(!motor.LeftHookVisible && !motor.RightHookVisible, "replacement inherits old launch flags and may expire before landing");
            }
            foreach (string action in new[] { "attack", "reload", "burst" })
            {
                var world = new TestWorld { Scale = scale, Target = new Vec3(0, 0, action == "attack" ? 100 : 20) };
                var motor = Fire(world, "mikasa", scale);
                var input = Held(world);
                if (action == "attack") input.Attack = new Button(true, true, false);
                if (action == "reload") input.Reload = new Button(true, true, false);
                if (action == "burst") { input.MoveY = 1; input.Dash = new Button(true, true, false); }
                motor.AdvanceFrame(Dt, input, new CharacterContacts(), world);
                input.Attack.Pressed = input.Reload.Pressed = input.Dash.Pressed = false;
                bool pausedAttack = false;
                for (int n = 0; n < 100 && !motor.LeftHookAttached; n++)
                {
                    motor.SetPose(Vec3.Zero, motor.Velocity, 0);
                    motor.Step(Dt, input, new CharacterContacts(), world);
                    pausedAttack |= motor.Animation.Speed == 0;
                }
                if (action == "attack") Check(pausedAttack, "landing cancels an attack held at its paused windup");
                Check(motor.LeftHookAttached && motor.State == CharacterState.Idle && !motor.IsDashing,
                    "ordinary landing exits attack, reload or burst state immediately");
                Check(motor.Animation.Clip == "air_hook_r_just" && motor.Animation.Speed == 1, "ordinary landing restores hook animation and speed");
            }
        }
        Console.WriteLine("Hook/special transitions: " + checks + " checks against nine native source scenarios and scaled regressions.");
        return checks;
    }
}
