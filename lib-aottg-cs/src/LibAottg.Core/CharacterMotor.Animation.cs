// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    public sealed partial class CharacterMotor
    {
        private void Play(string clip, bool loop, float speed = -1, float fade = 0.1f, float time = 0, bool restart = false)
        {
            if (!restart && _animation.Clip == clip) return;
            float duration = HeroAnimations.Duration(clip);
            if (clip == "attack1" || clip == "attack2" || clip.StartsWith("attack1_hook_")) duration = _config.AttackDuration;
            if (clip == "changeBlade") duration = _config.ReloadDuration * 1.2f;
            if (clip == "dodge") duration = _config.GroundDodgeDuration;
            if (clip == "supply") duration = _config.SupplyDuration;
            _animation = new CharacterAnimation { Clip = clip, Loop = loop, Duration = duration,
                Speed = speed < 0 ? HeroAnimations.Speed(clip) : speed,
                CrossFade = fade, Time = time, Revision = _animation.Revision + 1 };
        }
        private void AdvanceAnimation(float dt)
        {
            _animation.Time += dt * _animation.Speed;
            if (_animation.Loop && _animation.Duration > 0) _animation.Time %= _animation.Duration;
        }
        private void SelectLocomotion(Vec3 desired)
        {
            // A brief floor contact must not leave Slide blocking airborne clip selection.
            if (State == CharacterState.Slide && !Grounded) State = CharacterState.Idle;
            if (IsAction || State == CharacterState.GroundDodge || State == CharacterState.Land ||
                State == CharacterState.Slide || State == CharacterState.WallRun || State == CharacterState.RoofVault) return;
            if (_dashTime > 0) return;
            if (Grounded)
            {
                State = desired.LengthSquared > 0 ? CharacterState.Run : CharacterState.Idle;
                if (_animation.Clip == "jump" && _animation.NormalizedTime < 1) return;
                Play(desired.LengthSquared > 0 ? (_speedBuff > 0 ? "run_sasha" : _config.RunAnimation) :
                    Equipment == EquipmentMode.Guns ? "AHSS_stand_gun" : _config.StandAnimation, true);
                return;
            }
            State = _velocity.Y >= 0 ? CharacterState.Jump : CharacterState.Fall;
            string clip = _animation.Clip;
            if ((clip == "jump" || clip == "air_release" || clip == "air_hook_l_just" || clip == "air_hook_r_just") && _animation.NormalizedTime < 1) return;
            if (clip == "dash" && _animation.NormalizedTime < 0.99f) return;
            if (!LeftHookAttached && !RightHookAttached && _velocity.Y > 20 &&
                (clip == "air_hook_l" || clip == "air_hook_r" || clip == "air_hook"))
            { Play("air_release", false); return; }
            if (Math.Abs(_velocity.X) + Math.Abs(_velocity.Z) <= 25)
                Play(_velocity.Y < 0 ? "air_fall" : "air_rise", true, -1, 0.2f);
            else if (LeftHookAttached || RightHookAttached)
                Play(Equipment == EquipmentMode.Guns ?
                    LeftHookAttached && RightHookAttached ? "AHSS_hook_forward_both" : LeftHookAttached ? "AHSS_hook_forward_l" : "AHSS_hook_forward_r" :
                    LeftHookAttached && RightHookAttached ? "air_hook" : LeftHookAttached ? "air_hook_l" : "air_hook_r", true);
            else
            {
                float angle = DeltaAngle(FacingDegrees, Yaw(_velocity));
                // The short forward-flight transition holds its last pose (ClampForever).
                // The sideways and backward flight clips are authored loops.
                Play(Math.Abs(angle) < 45 ? "air2" : angle > 0 && angle < 135 ? "air2_right" :
                    angle < 0 && angle > -135 ? "air2_left" : "air2_backward", Math.Abs(angle) >= 45, -1, 0.2f);
            }
        }
    }
}
