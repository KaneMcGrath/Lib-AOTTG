// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    public sealed partial class CharacterMotor
    {
        private bool _attackReleased, _bladesThrown, _reloadEquipped, _attackMove;
        private int _attackLoops;
        private float _speedBuff;
        private uint _attackRandom = 0x6d2b79f5;
        private string SelectBladeAttack(CharacterInput input)
        {
            if (input.SourceAttackSelection)
            {
                var pose = HeroBodyPose.Evaluate(this);
                if (pose.Leaning)
                {
                    bool left = input.MoveX < 0 || (input.MoveX == 0 && pose.LeanLeft);
                    _attackRandom = unchecked(_attackRandom * 1664525u + 1013904223u);
                    return "attack1_hook_" + (left ? "l" : "r") + (((_attackRandom >> 16) & 1) + 1);
                }
                if (input.MoveX == 0 && input.HasAttackTarget)
                {
                    Vec3 delta = input.AttackTarget / Scale - _position;
                    float angle = DeltaAngle(FacingDegrees, Yaw(delta));
                    if (Math.Abs(angle) < 90 && delta.Length < 6 && delta.Y <= 2 && delta.Y >= -5) return "attack4";
                    return angle > 0 ? "attack1" : "attack2";
                }
            }
            return input.MoveX < 0 || (input.MoveX == 0 && input.AttackLeft) ? "attack2" : "attack1";
        }
        private bool IsAction { get { return _actionState == CharacterState.Attack || _actionState == CharacterState.Reload || _actionState == CharacterState.Supply; } }

        private void BeginAction(CharacterState state, string clip, float speed, ICharacterWorld world)
        {
            _actionState = State = state;
            _bladesThrown = _reloadEquipped = _attackMove = false;
            Play(clip, false, speed, state == CharacterState.Reload ? 0.1f : 0, 0, true);
            if (state == CharacterState.Attack)
            {
                if (Grounded && Equipment == EquipmentMode.Blades) _pendingForce += Math3.Direction(FacingDegrees) * 200;
                _attackReleased = Grounded || clip == "attack3_1" || clip == "attack5" || clip == "special_petra" || clip.StartsWith("special_");
                world.OnCharacterEvent(CharacterEvent.AttackStarted);
            }
        }
        private void SetAttackWindow(bool active, ICharacterWorld world)
        {
            if (AttackHitboxActive == active) return;
            AttackHitboxActive = active;
            world.OnCharacterEvent(active ? CharacterEvent.AttackWindowOpened : CharacterEvent.AttackWindowClosed);
        }
        private void CancelAction(ICharacterWorld world)
        {
            SetAttackWindow(false, world);
            if (_actionState == CharacterState.Attack) world.OnCharacterEvent(CharacterEvent.AttackEnded);
            _actionState = CharacterState.Idle;
            _attackLoops = 0;
            _animation.Speed = HeroAnimations.Speed(_animation.Clip);
        }
        private bool CanReload() { return Equipment == EquipmentMode.Blades || LeftGunReserve > 0 || RightGunReserve > 0; }
        private void BeginReload(ICharacterWorld world)
        {
            CancelAction(world);
            string clip;
            if (Equipment == EquipmentMode.Blades) clip = Grounded ? "changeBlade" : "changeBlade_air";
            else
            {
                if (LeftGunLoaded && RightGunLoaded) LeftGunLoaded = RightGunLoaded = false;
                clip = "AHSS_gun_reload_" + (!LeftGunLoaded && !RightGunLoaded ? "both" : !LeftGunLoaded ? "l" : "r") + (Grounded ? "" : "_air");
            }
            BeginAction(CharacterState.Reload, clip, HeroAnimations.Speed(clip), world);
        }
        private void ThrowBlades(ICharacterWorld world)
        {
            if (!BladesEquipped) return;
            BladesEquipped = false;
            SpareBladePairs = Math.Max(0, SpareBladePairs - 1);
            if (SpareBladePairs == 0) Blade = 0;
            SetAttackWindow(false, world);
            world.OnCharacterEvent(CharacterEvent.BladesThrown);
        }
        /// <summary>Call on a confirmed weapon contact. HERO uses 2 durability per unit, never per swing.</summary>
        public void BladeHit(ICharacterWorld world, int amount = 1)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            ConsumeBlade(2f * (amount == 0 ? 1 : amount));
            if (Blade <= 0) ThrowBlades(world);
        }
        private void FireGuns(bool both, CharacterInput input, ICharacterWorld world)
        {
            if (!LeftGunLoaded && !RightGunLoaded) { BeginReload(world); return; }
            string side = both && LeftGunLoaded && RightGunLoaded ? "both" :
                LeftHookAttached && LeftGunLoaded && RightGunLoaded ? "r" : LeftGunLoaded ? "l" : "r";
            Vec3 aim = input.HasAimTarget ? input.AimTarget - _position : input.AimDirection;
            if (Math3.Horizontal(aim).LengthSquared > 0) FacingTargetDegrees = Yaw(aim);
            BeginAction(CharacterState.Attack, "AHSS_shoot_" + side + (Grounded ? "" : "_air"), 1, world);
            _attackReleased = false;
        }
        private void UpdateAction(float dt, CharacterInput input, ICharacterWorld world)
        {
            float t = _animation.NormalizedTime;
            if (IsAction)
            {
                if (_actionState == CharacterState.Attack && Equipment == EquipmentMode.Blades)
                {
                    if (!_attackReleased)
                    {
                        if (!input.Attack.Held) { _attackReleased = true; _animation.Speed = HeroAnimations.Speed(_animation.Clip); }
                        else if (t >= 0.32f) _animation.Speed = 0;
                    }
                    float start = _config.AttackWindowStart, end = _config.AttackWindowEnd;
                    switch (_animation.Clip)
                    {
                        case "attack3_1": start = 0.8f; end = float.MaxValue; break;
                        case "attack3_2": start = end = -1; break;
                        case "attack5": start = 0.35f; end = 0.5f; break;
                        case "special_petra": start = 0.35f; end = 0.48f; break;
                        case "special_armin": start = 0.25f; end = 0.35f; break;
                        case "attack4": start = 0.6f; end = 0.9f; break;
                        case "special_sasha": case "special_marco_0": case "special_marco_1": start = end = -1; break;
                    }
                    bool active = BladesEquipped && Blade > 0 && (_animation.Clip == "attack3_1" ? t >= start : t > start) && t < end;
                    // HERO begins Mikasa's downward strike when its blade hitbox opens.
                    if (_animation.Clip == "attack3_1" && active && !AttackHitboxActive)
                        SetReferenceVelocity(-Vec3.Up * 30);
                    SetAttackWindow(active, world);
                    // Once the first clip ends, HERO accelerates down on every render frame
                    // until the fixed update observes the landing and starts attack3_2.
                    if (_animation.Clip == "attack3_1" && t >= 1)
                        SetReferenceVelocity(_velocity - Vec3.Up * (30 * dt));
                    if (_attackLoops > 0 && t > end)
                    { _attackLoops--; _animation.Time = start * _animation.Duration; }
                    else if (t >= 1 && _animation.Clip != "attack3_1") FinishAction(world);
                }
                else if (_actionState == CharacterState.Attack)
                {
                    if (!_attackReleased && t > 0.167f)
                    {
                        _attackReleased = true;
                        bool both = _animation.Clip.Contains("both");
                        if (both || _animation.Clip.Contains("shoot_l")) { LeftGunLoaded = false; world.OnCharacterEvent(CharacterEvent.GunFiredLeft); }
                        if (both || _animation.Clip.Contains("shoot_r")) { RightGunLoaded = false; world.OnCharacterEvent(CharacterEvent.GunFiredRight); }
                        // Update2 uses ForceMode.Acceleration: force persists for one fixed step.
                        _pendingForce += (-Math3.Direction(FacingDegrees) * (both ? 1000 : 600) + Vec3.Up * 200) * _config.Mass;
                    }
                    if (t >= 1) FinishAction(world);
                }
                else if (_actionState == CharacterState.Reload)
                {
                    if (Equipment == EquipmentMode.Blades)
                    {
                        if (!_bladesThrown && t >= (Grounded ? 0.13f : 0.2f))
                        { _bladesThrown = true; ThrowBlades(world); }
                        if (!_reloadEquipped && t >= (Grounded ? 0.37f : 0.56f) && SpareBladePairs > 0)
                        { _reloadEquipped = BladesEquipped = true; Blade = _config.BladeCapacity; world.OnCharacterEvent(CharacterEvent.Reloaded); }
                    }
                    else if (!_reloadEquipped && t > 0.62f)
                    {
                        _reloadEquipped = true;
                        if (!LeftGunLoaded && LeftGunReserve > 0) { LeftGunLoaded = true; if (!_config.InfiniteGas) LeftGunReserve--; }
                        if (!RightGunLoaded && RightGunReserve > 0) { RightGunLoaded = true; if (!_config.InfiniteGas) RightGunReserve--; }
                        world.OnCharacterEvent(CharacterEvent.Reloaded);
                    }
                    if (t >= 1) FinishAction(world);
                }
                else if (_actionState == CharacterState.Supply && t >= 1)
                {
                    Gas = _config.GasCapacity; Blade = _config.BladeCapacity;
                    SpareBladePairs = _config.SpareBladePairs; BladesEquipped = SpareBladePairs > 0;
                    LeftGunLoaded = RightGunLoaded = true;
                    LeftGunReserve = RightGunReserve = _config.GunReservePerSide;
                    world.OnCharacterEvent(CharacterEvent.Supplied); FinishAction(world);
                }
            }
            else if ((State == CharacterState.Slide && !Grounded) ||
                (State == CharacterState.GroundDodge && (t >= 1 || (!Grounded && t > 0.6f))) ||
                (State == CharacterState.Land && t >= 1)) State = CharacterState.Idle;
        }
        private void FinishAction(ICharacterWorld world)
        { CancelAction(world); State = CharacterState.Idle; Play(Equipment == EquipmentMode.Guns ? "AHSS_stand_gun" : _config.StandAnimation, true); }

        private void SkillPhysics(ICharacterWorld world)
        {
            if (_actionState != CharacterState.Attack) return;
            string clip = _animation.Clip;
            float t = _animation.NormalizedTime;
            if ((clip == "attack5" || clip == "special_petra") && t > 0.4f && !_attackMove)
            {
                _attackMove = true;
                if (_launchPointRight.LengthSquared > 0) _pendingImpulse += (_launchPointRight - _position).Normalized * 13;
                if (clip == "special_petra" && _launchPointLeft.LengthSquared > 0)
                {
                    _pendingImpulse += (_launchPointLeft - _position).Normalized * 13;
                    Disable(ref _left, HookSide.Left, world); Disable(ref _right, HookSide.Right, world);
                }
                _pendingImpulse += Vec3.Up * 2;
            }
            if (clip == "attack3_1" && t >= 1 && Grounded)
            {
                SetReferenceVelocity(Vec3.Zero);
                Play("attack3_2", false, 1, 0, 0, true);
                SetAttackWindow(false, world);
            }
        }

        private void UseSkill(CharacterInput input, ICharacterWorld world)
        {
            string skill = _config.SkillId.ToLowerInvariant();
            if ((skill == "armin" || skill == "marco" || skill == "sasha") && !Grounded) return;
            string clip;
            switch (skill)
            {
                case "mikasa":
                    clip = "attack3_1";
                    SetReferenceVelocity(Vec3.Up * 10);
                    break;
                case "levi": clip = "attack5"; SetReferenceVelocity(_velocity + Vec3.Up * 5); break;
                case "petra": clip = "special_petra"; SetReferenceVelocity(_velocity + Vec3.Up * 5); break;
                case "armin": clip = "special_armin"; break;
                case "marco": clip = "special_marco_0"; break;
                case "sasha": clip = "special_sasha"; _speedBuff = 10; break;
                // Transformation, titan taunts, laughter and combat effects belong to the destination game.
                default: world.OnCharacterEvent(CharacterEvent.SkillUsed); _skillCooldown = _config.SkillCooldown; return;
            }
            BeginAction(CharacterState.Attack, clip, HeroAnimations.Speed(clip), world);
            _skillCooldown = _config.SkillCooldown;
            if (skill == "levi" || skill == "petra")
            {
                Vec3 origin = input.HasAimOrigin ? input.AimOrigin : _position;
                HookHit target;
                if (input.AimDirection.LengthSquared > 0 && TryHookAim(origin, input.AimDirection.Normalized, 1e7f, HookSide.Right, world, out target))
                {
                    // HERO disables/replaces these projectiles only when the special's ray hits.
                    // launch flags/elapsed timers survive replacement until a new landing.
                    if (skill == "petra")
                    {
                        Disable(ref _left, HookSide.Left, world);
                        LaunchAt(ref _left, HookSide.Left, origin, target, false, world, 150);
                    }
                    Disable(ref _right, HookSide.Right, world);
                    LaunchAt(ref _right, HookSide.Right, origin, target, false, world, skill == "levi" ? 250 : 150, skill == "levi");
                    FacingTargetDegrees = Yaw(target.Point - _position);
                }
                _attackLoops = skill == "levi" ? 3 : 0;
            }
            world.OnCharacterEvent(CharacterEvent.SkillUsed);
        }
    }
}
