// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    public sealed partial class CharacterMotor
    {
        private void GroundMovement(Vec3 desired, CharacterInput input, float dt, ICharacterWorld world)
        {
            if (!_wasGrounded && !IsAction)
            {
                _dashTime = _wallRunTime = 0;
                world.OnCharacterEvent(CharacterEvent.Landed);
                if (desired.LengthSquared == 0 && !LeftHookVisible && !RightHookVisible)
                { State = CharacterState.Land; Play("dash_land", false, 1, 0.01f); }
                else if (Math3.Horizontal(_velocity).LengthSquared > _config.GroundSpeed * _config.GroundSpeed * 1.5f)
                {
                    State = CharacterState.Slide; FacingTargetDegrees = Yaw(_velocity);
                    Play("slide", true, 1, 0.05f); world.OnCharacterEvent(CharacterEvent.SlideStarted);
                }
            }
            Vec3 target = Vec3.Zero;
            if (IsAction)
            {
                if (_actionState == CharacterState.Attack && (_animation.Clip == "attack1" || _animation.Clip == "attack2" ||
                    (_animation.Clip == "attack5" && _animation.NormalizedTime > 0.4f && _animation.NormalizedTime < 0.61f) ||
                    (_animation.Clip == "special_petra" && _animation.NormalizedTime > 0.35f && _animation.NormalizedTime < 0.48f)))
                    _pendingForce += Math3.Direction(FacingDegrees) * 200;
            }
            else if (State == CharacterState.GroundDodge && _animation.Clip == "dodge")
            {
                float t = _animation.NormalizedTime;
                if (t >= 0.2f && t < 0.8f) target = -Math3.Direction(FacingDegrees) * _config.GroundDodgeSpeed;
                else if (t > 0.8f) target = Math3.Horizontal(_velocity) * 0.9f;
            }
            else if (State == CharacterState.Land && _animation.NormalizedTime < 1)
                target = Math3.Horizontal(_velocity) * 0.96f;
            else if (State == CharacterState.Slide)
            {
                target = Math3.Horizontal(_velocity) * 0.99f;
                if (_velocity.Length < _config.GroundSpeed * 1.2f) State = CharacterState.Idle;
            }
            else
            {
                target = desired * (_config.GroundSpeed * (_speedBuff > 0 ? 4 : 1));
                if (desired.LengthSquared > 0) FacingTargetDegrees = Yaw(desired);
            }
            // Unity queues AddForce; hook angle tests and spin still see the pre-force velocity.
            if (!(IsAction && Equipment == EquipmentMode.Guns && _actionState == CharacterState.Attack))
                _pendingVelocityChange += new Vec3(Math3.Clamp(target.X - _velocity.X, -_config.MaxGroundVelocityChange, _config.MaxGroundVelocityChange),
                    0, Math3.Clamp(target.Z - _velocity.Z, -_config.MaxGroundVelocityChange, _config.MaxGroundVelocityChange));
            TurnTowards(FacingTargetDegrees, dt * 10);
            if (_animation.Clip == "jump" && _animation.NormalizedTime > 0.18f)
            {
                _pendingVelocityChange += Vec3.Up * _config.JumpSpeed;
                if (_jumpPending) { _jumpPending = false; world.OnCharacterEvent(CharacterEvent.Jumped); }
            }
        }
        private void AirMovement(Vec3 desired, CharacterInput input, CharacterContacts contacts, float dt, bool spin,
            ref Vec3 acceleration, ICharacterWorld world)
        {
            if (State == CharacterState.RoofVault && _animation.Clip == "toRoof")
            {
                if (_config.NoForcedToRoof && (desired.LengthSquared == 0 || (_config.AutoWallJump && input.Dodge.Held)))
                { WallJump(world); return; }
                if (_animation.NormalizedTime < 0.22f) SetReferenceVelocity(Vec3.Zero);
                else
                {
                    if (!_roofImpulse) { _pendingImpulse += Vec3.Up * _config.RoofVaultSpeed; _roofImpulse = true; }
                    _pendingImpulse += Math3.Direction(FacingDegrees) * 0.05f;
                }
                if (_animation.NormalizedTime >= 1) State = CharacterState.Fall;
                return;
            }
            if (State == CharacterState.WallRun)
            {
                _pendingVelocityChange += Vec3.Up * _config.WallRunSpeed - _velocity;
                _wallRunTime += dt;
                if (_wallRunTime > _config.WallRunMaxDuration || desired.LengthSquared == 0 || (_config.AutoWallJump && input.Dodge.Held))
                    WallJump(world);
                else if (contacts.UpperFrontClear)
                {
                    State = CharacterState.RoofVault; _roofImpulse = false;
                    Play("toRoof", false); world.OnCharacterEvent(CharacterEvent.RoofVaulted);
                }
                else if (!contacts.FrontWall)
                { State = CharacterState.Fall; world.OnCharacterEvent(CharacterEvent.WallRunEnded); }
                return;
            }
            float angle = Math.Abs(DeltaAngle(FacingDegrees, Yaw(desired)));
            if (!IsAction && _dashTime <= 0 && State != CharacterState.GroundDodge && desired.LengthSquared > 0 && angle < 45 &&
                !input.Jump.Held && !input.HookLeft.Held && !input.HookRight.Held && !input.HookBoth.Held && contacts.FrontWall)
            {
                State = CharacterState.WallRun; _wallRunTime = 0;
                Play("wallrun", true); world.OnCharacterEvent(CharacterEvent.WallRunStarted);
                return;
            }
            // Direction input alone never brakes or caps aerial momentum.
            if (_dashTime <= 0 && _animation.Clip != "jump" && _animation.Clip != "dash" &&
                _animation.Clip != "attack5" && _animation.Clip != "special_petra" && !spin && input.Jump.Held && Gas > 0)
            {
                acceleration += desired * _config.AirAcceleration;
                GasThrustActive = true;
                if (!_skipGasThisTick) ConsumeGas(_config.GasUsePerSecond * dt);
            }
            if (desired.LengthSquared > 0 && _dashTime <= 0) FacingTargetDegrees = Yaw(desired);
        }
        private void WallJump(ICharacterWorld world)
        {
            _pendingImpulse -= Math3.Direction(FacingDegrees) * (_config.GroundSpeed * 0.75f);
            State = CharacterState.GroundDodge;
            Play("dodge", false, 1, 0.1f, _config.GroundDodgeDuration * 0.2f, true);
            _wallRunTime = 0;
            world.OnCharacterEvent(CharacterEvent.WallRunEnded);
            world.OnCharacterEvent(CharacterEvent.WallJumped);
        }
        private static float DeltaAngle(float a, float b) { return ((b - a + 540) % 360 + 360) % 360 - 180; }
        private void TurnTowards(float target, float factor)
        {
            // Quaternion.Lerp for a pure yaw, rather than a linear interpolation of Euler degrees.
            double half = DeltaAngle(FacingDegrees, target) * Math.PI / 360;
            float t = Math3.Clamp(factor, 0, 1);
            FacingDegrees += (float)(2 * Math.Atan2(t * Math.Sin(half), 1 - t + t * Math.Cos(half)) * 180 / Math.PI);
        }
    }
}
