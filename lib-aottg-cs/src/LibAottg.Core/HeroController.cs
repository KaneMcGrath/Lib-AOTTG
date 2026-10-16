// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace LibAottg
{
    /// <summary>Complete optional local runtime: edges, frame/fixed clocks, collision feedback,
    /// camera, procedural root pose, animated muzzles and host combat queries.</summary>
    public sealed class HeroController
    {
        public CharacterMotor Motor { get; private set; }
        public HeroInputMapper Input { get; private set; }
        public HeroCamera Camera { get; set; }
        private IHeroPresentation presentation;
        public IHeroPresentation Presentation
        {
            get { return presentation; }
            set { if (!ReferenceEquals(presentation, value)) ClearWeaponHistory(); presentation = value; }
        }
        public bool ProceduralPose = true, Combat = true, SourceAttackSelection = true;
        public float FixedStep { get; private set; }
        /// <summary>Bound catch-up work without passing a huge frame delta through source rules.</summary>
        public float MaxFrameDelta = 0.1f;
        public int PhysicsTicks { get; private set; }
        public HeroPose Pose { get; private set; }
        public HeroPose RenderPose { get; private set; }
        public CharacterInput LastInput { get; private set; }
        public event Action<CharacterEvent> Event;
        public event Action<HeroHit> Hit;
        /// <summary>Optional engine audio boundary for hooks, swing windows, shots, blade hits and death.
        /// Successful hook launches and paired gun shots each share one cue per frame.</summary>
        public event Action<HeroSoundCue> SoundRequested;
        private bool hookSoundPending;
        private readonly IHeroWorld world;
        private readonly Services services;
        private double accumulator;
        private HeroPose previous;
        private readonly HashSet<int> struck = new HashSet<int>();
        private readonly HashSet<int> shotTargets = new HashSet<int>();
        private readonly List<HeroHit> hits = new List<HeroHit>();
        private readonly List<HookSide> gunShots = new List<HookSide>();
        private readonly Vec3[] weaponStart = new Vec3[2], weaponEnd = new Vec3[2];
        private readonly bool[] weaponHistory = new bool[2];
        private bool attackWindow;

        public HeroController(CharacterConfig config = null, IHeroWorld world = null, float fixedStep = 0.02f)
        {
            Motor = new CharacterMotor(config);
            if (fixedStep <= 0 || !WorldBox.Finite(new Vec3(fixedStep, 0, 0)) || fixedStep > Motor.Config.MaxStep)
                throw new ArgumentOutOfRangeException("fixedStep");
            FixedStep = fixedStep; this.world = world ?? new HeroWorld();
            Input = new HeroInputMapper(); services = new Services(this);
            Respawn(Vec3.Zero);
        }
        public void Respawn(Vec3 position, bool grounded = false)
        {
            Motor.Respawn(position, grounded); Input.Reset(); accumulator = 0; PhysicsTicks = 0;
            hookSoundPending = false;
            ClearWeaponHistory(); attackWindow = false; struck.Clear(); gunShots.Clear();
            Pose = previous = RenderPose = BodyPose();
            LastInput = default(CharacterInput);
            if (Camera != null) Camera.Reset();
            Sample(Pose, 0);
        }
        public void Grab() { Motor.Grab(services); Synchronize(); }
        public void EscapeGrab(Vec3 position) { Motor.EscapeGrab(position, services); Synchronize(); }
        public void Die() { Motor.Die(services); Synchronize(); }
        public void BladeHit(int amount = 1) { Motor.BladeHit(services, amount); }
        public void SetEquipment(EquipmentMode equipment) { Motor.SetEquipment(equipment); ClearWeaponHistory(); }
        public bool TrySetEquipment(EquipmentMode equipment)
        {
            if (Motor.State == CharacterState.Attack || Motor.State == CharacterState.Reload || Motor.State == CharacterState.Supply) return false;
            SetEquipment(equipment); return true;
        }
        /// <summary>Call after an external teleport or host-owned lifecycle adjustment.</summary>
        public void Synchronize() { Pose = previous = RenderPose = BodyPose(); accumulator = 0; ClearWeaponHistory(); Sample(Pose, 0); }
        private void ClearWeaponHistory() { weaponHistory[0] = weaponHistory[1] = false; }

        public void Advance(float frameDelta, HeroInputFrame raw)
        {
            ValidateDelta(frameDelta);
            float dt = Math.Min(frameDelta, MaxFrameDelta);
            if (Camera != null)
            {
                Camera.Look(dt, raw);
                Camera.Follow(0, Motor, Pose.Position, Head(), raw, world as IHeroCameraWorld);
            }
            Vec3 forward = Camera != null ? Camera.Pose.Forward : Math3.Direction(Motor.FacingDegrees);
            Vec3 origin = Camera != null ? Camera.Pose.Position : Head();
            Vec3 aim = Camera != null ? Camera.Pose.AimDirection : forward;
            var input = Input.Sample(raw, forward, origin, aim);
            Advance(dt, input, raw);
        }
        /// <summary>Direct input/AI/replay route; bypasses device interpretation and camera look.</summary>
        public void Advance(float frameDelta, CharacterInput input)
        { ValidateDelta(frameDelta); Advance(Math.Min(frameDelta, MaxFrameDelta), input, default(HeroInputFrame)); }
        private void ValidateDelta(float dt)
        {
            if (!WorldBox.Finite(new Vec3(dt, MaxFrameDelta, 0)) || dt <= 0 || MaxFrameDelta <= 0)
                throw new ArgumentOutOfRangeException("frameDelta");
        }
        private void Advance(float dt, CharacterInput input, HeroInputFrame raw)
        {
            input.SourceAttackSelection |= SourceAttackSelection;
            var targets = world as IHeroAttackTargetWorld;
            Vec3 target;
            if (!input.HasAttackTarget && targets != null && targets.TryAttackTarget(Motor, input, out target))
            { input.HasAttackTarget = true; input.AttackTarget = target; }
            HookHit aimHit;
            if (!input.HasAimTarget && input.AimDirection.LengthSquared > 0)
            {
                Vec3 origin = input.HasAimOrigin ? input.AimOrigin : Motor.Position;
                Vec3 direction = input.AimDirection.Normalized;
                float distance = Motor.Config.HookRange * Motor.Scale;
                float skip = Motor.Config.ImprovedRaycasts ? Math.Min((origin - Motor.Position).Length * 0.5f, distance) : 0;
                if (world.TryHook(origin + direction * skip, direction, distance - skip, HookSide.Left, out aimHit))
                { input.HasAimTarget = true; input.AimTarget = aimHit.Point; }
                else if (Motor.Config.ImprovedRaycasts)
                { input.HasAimTarget = true; input.AimTarget = origin + direction * distance; }
            }
            LastInput = input;
            Sample(Pose, 0);
            Motor.AdvanceFrame(dt, input, world.GetContacts(Motor, Pose), services);
            if (hookSoundPending)
            {
                hookSoundPending = false;
                if (SoundRequested != null) SoundRequested(new HeroSoundCue(HeroSound.HookLaunch, Motor.Position));
            }
            Pose = BodyPose();
            Sample(Pose, dt);
            // Source actions fire on the frame clock even when this frame has no physics tick.
            ProcessGuns(input);
            accumulator += dt;
            while (accumulator + 1e-9 >= FixedStep)
            {
                previous = Pose;
                Vec3 start = Motor.Position;
                Motor.StepPhysics(FixedStep, input, world.GetContacts(Motor, Pose), services);
                var motion = world.Move(Motor, start, BodyPose(), FixedStep);
                Motor.SetPose(motion.Position, motion.Velocity, Motor.FacingDegrees);
                Pose = BodyPose(); Sample(Pose, 0);
                ProcessBlades();
                PhysicsTicks++; accumulator -= FixedStep;
            }
            float alpha = Math3.Clamp((float)(accumulator / FixedStep), 0, 1);
            RenderPose = new HeroPose { Position = Vec3.Lerp(previous.Position, Pose.Position, alpha),
                Yaw = previous.Yaw + HeroBodyPose.DeltaAngle(previous.Yaw, Pose.Yaw) * alpha,
                Pitch = Motor.Grounded ? 0 : previous.Pitch + HeroBodyPose.DeltaAngle(previous.Pitch, Pose.Pitch) * alpha,
                Roll = Motor.Grounded ? 0 : previous.Roll + HeroBodyPose.DeltaAngle(previous.Roll, Pose.Roll) * alpha,
                Leaning = Pose.Leaning, LeanLeft = Pose.LeanLeft };
            Sample(RenderPose, 0);
            if (Camera != null) Camera.Follow(dt, Motor, RenderPose.Position, Head(), raw, world as IHeroCameraWorld);
        }
        private HeroPose BodyPose()
        { return ProceduralPose ? HeroBodyPose.Evaluate(Motor) : new HeroPose { Position = Motor.Position, Yaw = Motor.FacingDegrees }; }
        private Vec3 Head() { return Presentation != null ? Presentation.Head : RenderPose.Position + Vec3.Up * (1.5f * Motor.Scale); }
        private void Sample(HeroPose pose, float dt) { if (Presentation != null) Presentation.Sample(Motor, pose, LastInput, dt); }
        private void Raise(CharacterEvent kind)
        {
            if (kind == CharacterEvent.HookLaunchedLeft || kind == CharacterEvent.HookLaunchedRight) hookSoundPending = true;
            if (kind == CharacterEvent.AttackWindowOpened) { struck.Clear(); attackWindow = true; RequestSound(HeroSound.BladeSwing); }
            if (kind == CharacterEvent.Died) RequestSound(HeroSound.Death);
            if (kind == CharacterEvent.AttackWindowClosed) attackWindow = false;
            if (kind == CharacterEvent.GunFiredLeft) gunShots.Add(HookSide.Left);
            if (kind == CharacterEvent.GunFiredRight) gunShots.Add(HookSide.Right);
            world.OnCharacterEvent(kind);
            if (Event != null) Event(kind);
        }
        private void ProcessBlades()
        {
            var combat = world as IHeroCombatWorld;
            if (Presentation == null) return;
            for (int n = 0; n < 2; n++)
            {
                Vec3 start, end;
                if (!Presentation.WeaponSegment((HookSide)n, false, out start, out end)) { weaponHistory[n] = false; continue; }
                if (Combat && combat != null && attackWindow && Motor.AttackHitboxActive)
                {
                    var sweep = new HeroWeaponSweep { Side = (HookSide)n, Start = start, End = end,
                        PreviousStart = weaponHistory[n] ? weaponStart[n] : start, PreviousEnd = weaponHistory[n] ? weaponEnd[n] : end,
                        Radius = 0.1f * Motor.Scale };
                    AcceptHits(combat, sweep, true);
                }
                weaponStart[n] = start; weaponEnd[n] = end;
                weaponHistory[n] = true;
            }
        }
        private void RequestSound(HeroSound sound)
        { if (SoundRequested != null) SoundRequested(new HeroSoundCue(sound, Motor.Position)); }
        private void ProcessGuns(CharacterInput input)
        {
            if (gunShots.Count > 0) RequestSound(HeroSound.AhssFire);
            var combat = world as IHeroCombatWorld;
            foreach (var side in gunShots)
            {
                Vec3 start, end;
                if (Presentation == null || !Presentation.WeaponSegment(side, true, out start, out end))
                    start = Motor.Position + Vec3.Up * Motor.Scale;
                Vec3 target = input.HasAimTarget ? input.AimTarget : input.HasAimOrigin ? input.AimOrigin + input.AimDirection.Normalized * (10000 * Motor.Scale) : start + input.AimDirection.Normalized * (10000 * Motor.Scale);
                HookHit aimed;
                if (world.TryHook(input.HasAimOrigin ? input.AimOrigin : start, input.AimDirection.Normalized, 10000 * Motor.Scale, side, out aimed)) target = aimed.Point;
                if (Combat && combat != null) AcceptHits(combat, new HeroWeaponSweep { Gun = true, Side = side, Start = start, End = target }, false);
            }
            gunShots.Clear();
        }
        private void AcceptHits(IHeroCombatWorld combat, HeroWeaponSweep sweep, bool blade)
        {
            hits.Clear(); combat.CollectWeaponHits(sweep, hits);
            if (!blade) shotTargets.Clear();
            foreach (var candidate in hits)
            {
                if (candidate.TargetId == 0 || (blade ? !struck.Add(candidate.TargetId) : !shotTargets.Add(candidate.TargetId))) continue;
                var hit = candidate; hit.Side = sweep.Side; hit.Gun = sweep.Gun; hit.Speed = Motor.Velocity.Length / Motor.Scale;
                float multiplier = hit.ScoreMultiplier > 0 ? hit.ScoreMultiplier : 1f;
                hit.Damage = HeroDamage.Calculate(Motor.Velocity, hit.TargetVelocity, multiplier, Motor.Scale);
                if (blade) { Motor.BladeHit(services); RequestSound(HeroSound.BladeHit); }
                combat.OnHeroHit(hit); if (Hit != null) Hit(hit);
                if (blade && !Motor.AttackHitboxActive) break;
            }
        }
        private sealed class Services : IHookGeometryWorld
        {
            private readonly HeroController owner;
            public Services(HeroController owner) { this.owner = owner; }
            public Vec3 HookOrigin(CharacterMotor motor, HookSide side)
            { return owner.Presentation != null ? owner.Presentation.HookOrigin(motor, side) : owner.world.HookOrigin(motor, side); }
            public bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit)
            { return owner.world.TryHook(origin, direction, distance, side, out hit); }
            public bool SweepHook(Vec3 start, Vec3 end, HookSide side, out HookHit hit)
            { return owner.world.SweepHook(start, end, side, out hit); }
            public bool ResolveHookAnchor(int id, Vec3 localPoint, out Vec3 point)
            { return owner.world.ResolveHookAnchor(id, localPoint, out point); }
            public void OnCharacterEvent(CharacterEvent kind) { owner.Raise(kind); }
        }
    }
}
