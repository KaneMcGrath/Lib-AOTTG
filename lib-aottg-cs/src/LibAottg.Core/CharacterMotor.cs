// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    /// <summary>Portable AoTTG human motor. Host owns collision, target damage and presentation.</summary>
    public sealed partial class CharacterMotor
    {
        private readonly CharacterConfig _config;
        private CharacterState _actionState;
        private float _dashTime, _dashCooldown, _dashOriginSpeed, _flareCooldown, _skillCooldown;
        private bool _wasGrounded, _hasPhysicsTick, _jumpPending, _roofImpulse;
        private float _wallRunTime;
        private CharacterAnimation _animation;
        private readonly float[] _taps = { -1, -1, -1, -1 };
        private float _frameClock;
        private float _currentSpeed;
        private Vec3 _pendingForce, _pendingImpulse, _pendingVelocityChange;

        public CharacterMotor(CharacterConfig config = null)
        {
            _config = config ?? new CharacterConfig();
            _config.Validate();
            Scale = _config.WorldScale;
            _world = new WorldBoundary(this);
            Respawn(Vec3.Zero);
        }

        public CharacterConfig Config { get { return _config; } }
        private Vec3 _position, _velocity;
        public Vec3 Position { get { return _position * Scale; } }
        public Vec3 Velocity { get { return _velocity * Scale; } }
        public float Scale { get; private set; }
        public float FacingDegrees { get; private set; }
        public float FacingTargetDegrees { get; private set; }
        public CharacterState State { get; private set; }
        public bool Grounded { get; private set; }
        public bool IsDashing { get { return _dashTime > 0; } }
        public bool Spinning { get; private set; }
        public bool GasThrustActive { get; private set; }
        public float Gas { get; private set; }
        public float Blade { get; private set; }
        /// <summary>Reference currentBladeNum: remaining pairs including the equipped pair.</summary>
        public int SpareBladePairs { get; private set; }
        public bool BladesEquipped { get; private set; }
        public bool AttackHitboxActive { get; private set; }
        public EquipmentMode Equipment { get; private set; }
        public bool LeftGunLoaded { get; private set; }
        public bool RightGunLoaded { get; private set; }
        public int LeftGunReserve { get; private set; }
        public int RightGunReserve { get; private set; }
        public float FlareCooldownRemaining { get { return _flareCooldown; } }
        public float SkillCooldownRemaining { get { return _skillCooldown; } }
        public CharacterAnimation Animation { get { return _animation; } }
        public float FieldOfView { get; private set; }

        public void SetEquipment(EquipmentMode mode)
        {
            if (IsAction) throw new InvalidOperationException("Wait for the current action to finish before changing equipment.");
            Equipment = mode;
            Play(mode == EquipmentMode.Guns ? "AHSS_stand_gun" : _config.StandAnimation, true);
        }

        public void SetPose(Vec3 position, Vec3 velocity, float facingDegrees)
        { if (!WorldBox.Finite(position) || !WorldBox.Finite(velocity) || float.IsNaN(facingDegrees) || float.IsInfinity(facingDegrees)) throw new ArgumentException("Pose must be finite.");
            _position = position / Scale; _velocity = velocity / Scale; FacingDegrees = facingDegrees;
            if (!_hasPhysicsTick) FacingTargetDegrees = facingDegrees; }

        /// <summary>Convenience frame + physics tick. Use separate clocks when render timing matters.</summary>
        public void Step(float deltaTime, CharacterInput input, CharacterContacts contacts, ICharacterWorld world)
        {
            ValidateStep(deltaTime, world);
            AdvanceFrame(deltaTime, input, contacts, world);
            StepPhysics(deltaTime, input, contacts, world);
        }

        /// <summary>Call once per render update. Consumes edges once, advances animation/actions and dash damping.</summary>
        public void AdvanceFrame(float dt, CharacterInput input, CharacterContacts contacts, ICharacterWorld world)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) throw new ArgumentOutOfRangeException("dt");
            if (State == CharacterState.Dead || State == CharacterState.Grabbed) return;
            world = BindWorld(world);
            if (input.HasAimOrigin) input.AimOrigin /= Scale;
            if (input.HasAimTarget) input.AimTarget /= Scale;
            Grounded = contacts.Grounded;
            _frameClock += dt;
            _dashCooldown = Math.Max(0, _dashCooldown - dt);
            _flareCooldown = Math.Max(0, _flareCooldown - dt);
            _skillCooldown = Math.Max(0, _skillCooldown - dt);
            _speedBuff = Math.Max(0, _speedBuff - dt);
            AdvanceAnimation(dt);
            UpdateAction(dt, input, world);
            AdvanceRetraction(ref _left, HookSide.Left, dt, world);
            AdvanceRetraction(ref _right, HookSide.Right, dt, world);
            if (_dashTime > 0)
            {
                _dashTime = Math.Max(0, _dashTime - dt);
                if (_currentSpeed > _dashOriginSpeed)
                    _pendingVelocityChange -= _velocity * (dt * _config.DashDamping);
            }

            Vec3 desired = Desired(input);
            Vec3 f = Forward(input), right = new Vec3(f.Z, 0, -f.X);
            Vec3 dash = input.MoveY != 0 ? f * Math.Sign(input.MoveY) : input.MoveX != 0 ? right * Math.Sign(input.MoveX) : Vec3.Zero;
            bool doubleTap = DoubleTap(input, ref dash);
            if (!Grounded && _dashTime <= 0 && _dashCooldown <= 0 && Gas > 0 && ((input.Dash.Pressed && dash.LengthSquared > 0) || doubleTap))
            {
                CancelAction(world);
                _jumpPending = false;
                _wallRunTime = 0;
                _dashOriginSpeed = _currentSpeed;
                _pendingVelocityChange += dash * _config.DashSpeed;
                FacingDegrees = FacingTargetDegrees = Yaw(dash);
                // HERO.dash assigns dashTime before setting state. Its state setter clears
                // that timer when leaving GroundDodge (including the airborne wall jump).
                // Keep the additive impulse, gas cost and cooldown, but skip burst damping.
                _dashTime = State == CharacterState.GroundDodge ? 0 : _config.DashDuration;
                _dashCooldown = _config.DashCooldown;
                ConsumeGas(_config.GasCapacity * _config.DashGasFraction);
                Play("dash", false, 1, 0.1f, 0.1f, true);
                State = CharacterState.AirDodge;
                world.OnCharacterEvent(CharacterEvent.Dashed);
                if (_dashTime <= 0)
                {
                    State = CharacterState.Idle;
                    Play(_config.StandAnimation, true);
                }
            }
            if (Grounded && (State == CharacterState.Idle || State == CharacterState.Run || State == CharacterState.Slide) &&
                !IsAction && _dashTime <= 0 && input.Dodge.Pressed && _animation.Clip != "jump")
            {
                if (desired.LengthSquared > 0) FacingTargetDegrees = Yaw(desired) + 180;
                Play("dodge", false, 1, 0.1f, 0, true);
                State = CharacterState.GroundDodge;
                world.OnCharacterEvent(CharacterEvent.Dodged);
            }
            if (Grounded && !IsAction && input.Jump.Pressed && _animation.Clip != "jump")
            {
                if (!_config.NoStopJump) State = CharacterState.Idle;
                _jumpPending = true;
                Play("jump", false, 1, 0.1f, 0, true);
            }
            if (input.Supply.Pressed && Grounded && !IsAction &&
                (Gas < _config.GasCapacity || Blade < _config.BladeCapacity || SpareBladePairs < _config.SpareBladePairs ||
                 LeftGunReserve < _config.GunReservePerSide || RightGunReserve < _config.GunReservePerSide))
                BeginAction(CharacterState.Supply, "supply", 1, world);
            else if (input.Reload.Pressed && !IsAction && _dashTime <= 0 &&
                (!Grounded || _animation.Clip == _config.StandAnimation || _animation.Clip == "AHSS_stand_gun") && CanReload())
                BeginReload(world);
            else if (input.Skill.Pressed && !IsAction && _dashTime <= 0 && _skillCooldown <= 0 && Equipment == EquipmentMode.Blades)
                UseSkill(input, world);
            else if (Equipment == EquipmentMode.Guns && !IsAction && _dashTime <= 0 && (input.Attack.Released || input.Skill.Released))
                FireGuns(input.Skill.Released, input, world);
            else if (input.Attack.Pressed && !IsAction && _dashTime <= 0 && Equipment == EquipmentMode.Blades)
            {
                if (BladesEquipped && Blade > 0)
                {
                    BeginAction(CharacterState.Attack, SelectBladeAttack(input), 1, world);
                }
            }
            if (input.Flare.Pressed && _flareCooldown <= 0)
            { _flareCooldown = _config.FlareCooldown; world.OnCharacterEvent(CharacterEvent.FlareFired); }
            LaunchHooks(input, world);
        }

        /// <summary>One physics tick. Held input only; frame edges belong in AdvanceFrame.</summary>
        public void StepPhysics(float dt, CharacterInput input, CharacterContacts contacts, ICharacterWorld world)
        {
            ValidateStep(dt, world);
            Grounded = contacts.Grounded;
            GasThrustActive = false;
            if (State == CharacterState.Dead || State == CharacterState.Grabbed) return;
            world = BindWorld(world);
            float speed = _velocity.Length;
            _currentSpeed = speed;
            if (_animation.Clip != "attack3_2" && _animation.Clip != "attack5" && _animation.Clip != "special_petra")
                TurnTowards(FacingTargetDegrees, dt * 6);
            Spinning = false;
            UpdateHookAnchor(ref _left, HookSide.Left, world);
            UpdateHookAnchor(ref _right, HookSide.Right, world);
            Vec3 force = Vec3.Zero;
            _leftHookedAtPhysicsStart = _launchLeft && LeftHookAttached;
            _rightHookedAtPhysicsStart = _launchRight && RightHookAttached;
            bool spinL = RopeForce(_left, _launchRight, input.Jump.Held, ref force);
            TickLaunchedHook(ref _left, HookSide.Left, input.HookLeft.Held || input.HookBoth.Held, dt, world);
            spinL &= LeftHookAttached;
            bool spinR = RopeForce(_right, _launchLeft, input.Jump.Held, ref force);
            TickLaunchedHook(ref _right, HookSide.Right, input.HookRight.Held || input.HookBoth.Held, dt, world);
            spinR &= RightHookAttached;
            Vec3 desired = Desired(input);
            SelectLocomotion(desired);
            if (Grounded) GroundMovement(desired, input, dt, world);
            else AirMovement(desired, input, contacts, dt, spinL || spinR, ref force, world);
            if (spinL || spinR)
            {
                Vec3 anchor = spinL && spinR ? (_left.Position + _right.Position) * 0.5f : spinL ? _left.Position : _right.Position;
                // AddForce(VelocityChange) is queued in Unity; RotateTowards sees the pre-force velocity.
                Vec3 direction = RotateTowards(anchor - _position, _velocity, 1.53938f * (1 + Math3.Clamp(input.Reel, -0.8f, 0.8f)));
                SetReferenceVelocity(direction.Normalized * (speed + 0.1f));
                Spinning = true;
                GasThrustActive = true;
                if (!_skipGasThisTick) ConsumeGas(_config.GasUsePerSecond * dt);
            }
            SkillPhysics(world);
            bool above = (_left.Phase == HookPhase.Attached && _left.Position.Y > _position.Y) ||
                         (_right.Phase == HookPhase.Attached && _right.Position.Y > _position.Y);
            float gravity = above ? _config.HookedGravity : _config.Gravity;
            if (State == CharacterState.RoofVault && _animation.NormalizedTime < 0.22f) force += Vec3.Up * _config.Gravity;
            UpdateHookFacing();
            _skipGasThisTick = false;
            // In the reference player HERO.FixedUpdate runs before Bullet.FixedUpdate.
            // A newly landing bullet adds its launch force now; sustained pull starts next tick.
            AdvanceHooks(dt, world);
            _velocity += (force - Vec3.Up * gravity + _pendingForce / _config.Mass) * dt + _pendingImpulse / _config.Mass + _pendingVelocityChange;
            _pendingForce = _pendingImpulse = _pendingVelocityChange = Vec3.Zero;
            _position += _velocity * dt;
            float targetFov = speed > 10 ? Math.Min(100, speed + 40) : 50;
            FieldOfView += (targetFov - FieldOfView) * 0.1f;
            _wasGrounded = Grounded;
            _hasPhysicsTick = true;
        }

        private void ValidateStep(float dt, ICharacterWorld world)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0 || dt > _config.MaxStep)
                throw new ArgumentOutOfRangeException("dt", "Use a positive step no greater than Config.MaxStep.");
        }
        // Observed in the original Unity 4.6 player: a velocity assignment clears accumulated
        // Impulse/VelocityChange, but preserves Force/Acceleration for the next simulation.
        private void SetReferenceVelocity(Vec3 value)
        { _velocity = value; _pendingImpulse = _pendingVelocityChange = Vec3.Zero; }
        private Vec3 Forward(CharacterInput input)
        {
            Vec3 forward = Math3.Horizontal(input.LookDirection).Normalized;
            return forward.LengthSquared > 0 ? forward : Math3.Direction(FacingDegrees);
        }
        private Vec3 Desired(CharacterInput input)
        {
            Vec3 f = Forward(input);
            Vec3 v = f * Math3.Clamp(input.MoveY, -1, 1) + new Vec3(f.Z, 0, -f.X) * Math3.Clamp(input.MoveX, -1, 1);
            float n = v.Length;
            return n < 0.25f ? Vec3.Zero : v.Normalized * (n > 0.95f ? 1 : n);
        }
        private static float Yaw(Vec3 v) { return (float)(Math.Atan2(v.X, v.Z) * 180 / Math.PI); }
        private bool DoubleTap(CharacterInput input, ref Vec3 direction)
        {
            if (!_config.DoubleTapDash)
            {
                for (int n = 0; n < _taps.Length; n++) _taps[n] = -1;
                return false;
            }
            Vec3 f = Forward(input), r = new Vec3(f.Z, 0, -f.X);
            bool result = false;
            for (int n = 0; n < 4; n++)
                if ((n == 0 ? input.Back : n == 1 ? input.Forward : n == 2 ? input.Left : input.Right).Pressed)
                {
                    if (!result && !Grounded && _taps[n] >= 0 && _frameClock - _taps[n] < _config.DoubleTapWindow)
                    { direction = n == 0 ? -f : n == 1 ? f : n == 2 ? -r : r; result = true; _taps[n] = -1; }
                    else _taps[n] = _frameClock;
                }
            return result;
        }

        public void ConsumeGas(float amount)
        { if (!_config.InfiniteGas) Gas = Math3.Clamp(Gas - Math.Max(0, amount), 0, _config.GasCapacity); }
        public void ConsumeBlade(float amount)
        { if (!_config.InfiniteGas) Blade = Math3.Clamp(Blade - Math.Max(0, amount), 0, _config.BladeCapacity); }
        public void RefillGas() { Gas = _config.GasCapacity; }
        public void Die(ICharacterWorld world)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (State == CharacterState.Dead) return;
            Stop(world); State = CharacterState.Dead; Play("T-pose", false);
            world.OnCharacterEvent(CharacterEvent.Died);
        }
        public void Grab(ICharacterWorld world)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (State == CharacterState.Dead || State == CharacterState.Grabbed) return;
            Stop(world); _velocity = Vec3.Zero; State = CharacterState.Grabbed; Play("grabbed", false);
            world.OnCharacterEvent(CharacterEvent.Grabbed);
        }
        private void Stop(ICharacterWorld world)
        {
            ReleaseHook(HookSide.Left, world); ReleaseHook(HookSide.Right, world); CancelAction(world);
            _dashTime = _wallRunTime = 0; _jumpPending = false; Spinning = false;
            GasThrustActive = false;
            _pendingForce = _pendingImpulse = _pendingVelocityChange = Vec3.Zero;
        }
        public void EscapeGrab(Vec3 position, ICharacterWorld world)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (State != CharacterState.Grabbed) return;
            _position = position / Scale; _velocity = Vec3.Zero; State = CharacterState.Idle;
            Play(_config.StandAnimation, true); world.OnCharacterEvent(CharacterEvent.EscapedGrab);
        }
        public void Respawn(Vec3 position, bool grounded = false)
        {
            _left = _right = new Rope();
            _launchLeft = _launchRight = _leftHookedAtPhysicsStart = _rightHookedAtPhysicsStart = false;
            _elapsedLeft = _elapsedRight = 0; _launchPointLeft = _launchPointRight = Vec3.Zero;
            _dashTime = _dashCooldown = _wallRunTime = _flareCooldown = _skillCooldown = _frameClock = 0;
            _jumpPending = _roofImpulse = _skipGasThisTick = false;
            _wasGrounded = _hasPhysicsTick = grounded;
            _pendingForce = _pendingImpulse = _pendingVelocityChange = Vec3.Zero;
            _speedBuff = 0; _attackLoops = 0;
            _currentSpeed = 0;
            _actionState = CharacterState.Idle;
            AttackHitboxActive = Spinning = GasThrustActive = false;
            for (int n = 0; n < 4; n++) _taps[n] = -1;
            Gas = _config.GasCapacity; Blade = _config.BladeCapacity; SpareBladePairs = _config.SpareBladePairs;
            BladesEquipped = SpareBladePairs > 0;
            LeftGunLoaded = RightGunLoaded = true;
            LeftGunReserve = RightGunReserve = _config.GunReservePerSide;
            _position = position / Scale; _velocity = Vec3.Zero; FacingDegrees = FacingTargetDegrees = 0; Grounded = grounded;
            State = CharacterState.Idle; FieldOfView = 50;
            Play(Equipment == EquipmentMode.Guns ? "AHSS_stand_gun" : _config.StandAnimation, true, 1, 0, 0, true);
        }
    }
}
