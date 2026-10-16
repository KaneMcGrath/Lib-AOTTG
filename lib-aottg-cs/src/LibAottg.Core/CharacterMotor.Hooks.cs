// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    public sealed partial class CharacterMotor
    {
        private struct Rope
        {
            public HookPhase Phase;
            public Vec3 Position, PreviousPosition, FlightVelocity, LocalPoint, RetractStart;
            public bool Special;
            public int TargetId;
            public float Age, SpecialTime;
            public int SpiralTicks;
        }
        private Rope _left, _right;
        private bool _skipGasThisTick;
        // HERO's launch flags/timers and launch points belong to the hero, not the projectile.
        // Replacing a projectile does not reset these until its landing calls HERO.launch.
        private bool _launchLeft, _launchRight, _leftHookedAtPhysicsStart, _rightHookedAtPhysicsStart;
        private float _elapsedLeft, _elapsedRight;
        private Vec3 _launchPointLeft, _launchPointRight;
        public bool LeftHookAttached { get { return _left.Phase == HookPhase.Attached; } }
        public bool RightHookAttached { get { return _right.Phase == HookPhase.Attached; } }
        public HookPhase LeftHookPhase { get { return _left.Phase; } }
        public HookPhase RightHookPhase { get { return _right.Phase; } }
        public Vec3 LeftHookAnchor { get { return _left.Position * Scale; } }
        public Vec3 RightHookAnchor { get { return _right.Position * Scale; } }
        public bool LeftHookVisible { get { return _left.Phase != HookPhase.Inactive; } }
        public bool RightHookVisible { get { return _right.Phase != HookPhase.Inactive; } }

        // HERO.setHookedPplDirection runs after movement and overrides camera-relative facing.
        // Keep the target separate from the interpolated body rotation, as in the source.
        private void UpdateHookFacing()
        {
            bool left = LeftHookAttached, right = RightHookAttached;
            if ((!left && !right) || (Equipment == EquipmentMode.Guns && _actionState == CharacterState.Attack)) return;
            bool attack = _actionState == CharacterState.Attack;
            float facing;
            if (left && right)
            {
                Vec3 normal = _left.Position - _right.Position;
                Vec3 midpoint = (_left.Position + _right.Position) * 0.5f;
                Vec3 delta = midpoint - _position;
                if (normal.LengthSquared < 4)
                {
                    facing = Yaw(delta);
                    if (Equipment == EquipmentMode.Guns && !attack)
                        facing += HookVelocityAngle(delta);
                }
                else if (HeroCamera.Angle(_position - midpoint, _position - _left.Position) < 30 &&
                         HeroCamera.Angle(_position - midpoint, _position - _right.Position) < 30)
                    facing = Yaw(delta);
                else
                {
                    normal = normal.Normalized;
                    Vec3 forward = HeroBodyPose.Evaluate(this).Forward;
                    forward -= normal * Vec3.Dot(normal, forward);
                    if (forward.LengthSquared < 0.000001f)
                        forward = Math.Abs(normal.Z) > 0.70710678f ? new Vec3(0, -normal.Z, normal.Y) : new Vec3(-normal.Y, normal.X, 0);
                    facing = Yaw(forward.Normalized);
                    if (DeltaAngle(Yaw(_position - _left.Position), facing) > 0) facing += 180;
                }
            }
            else
            {
                Vec3 delta = (right ? _right.Position : _left.Position) - _position;
                facing = Yaw(delta);
                if (!attack)
                {
                    float angle = HookVelocityAngle(delta);
                    facing += angle * (Equipment == EquipmentMode.Guns ? 1 :
                        (left && angle < 0) || (right && angle > 0) ? -0.1f : 0.1f);
                }
            }
            FacingTargetDegrees = facing;
            // bodyLean snaps ordinary blade attacks to their hook-facing rotation.
            if (attack && Equipment == EquipmentMode.Blades && _animation.Clip != "attack3_1" && _animation.Clip != "attack3_2")
                FacingDegrees = facing;
        }
        private float HookVelocityAngle(Vec3 delta)
        {
            float current = -(float)Math.Atan2(_velocity.Z, _velocity.X) * 180 / (float)Math.PI;
            float target = -(float)Math.Atan2(delta.Z, delta.X) * 180 / (float)Math.PI;
            return -DeltaAngle(current, target);
        }

        private bool HookInputAllowed
        {
            get { return State != CharacterState.Grabbed && State != CharacterState.Dead &&
                (_animation.Clip != "attack3_1" && _animation.Clip != "attack5" && _animation.Clip != "special_petra"); }
        }
        private void LaunchHooks(CharacterInput input, ICharacterWorld world)
        {
            if (input.AimDirection.LengthSquared < 0.001f || Gas <= 0) return;
            if (!HookInputAllowed) return;
            Vec3 origin = input.HasAimOrigin ? input.AimOrigin : _position;
            Vec3 direction = input.AimDirection.Normalized;
            if (input.HookLeft.Held || input.HookLeft.Pressed)
                TryLaunch(ref _left, HookSide.Left, origin, direction, false, world);
            if (input.HookRight.Held || input.HookRight.Pressed)
                TryLaunch(ref _right, HookSide.Right, origin, direction, false, world);
            if ((input.HookBoth.Held || input.HookBoth.Pressed) && _left.Phase == HookPhase.Inactive && _right.Phase == HookPhase.Inactive)
            {
                TryLaunch(ref _left, HookSide.Left, origin, direction, true, world);
                TryLaunch(ref _right, HookSide.Right, origin, direction, true, world);
            }
        }
        private void TryLaunch(ref Rope rope, HookSide side, Vec3 rayOrigin, Vec3 rayDirection, bool spread, ICharacterWorld world, float projectileSpeed = 0, bool special = false)
        {
            if (rope.Phase != HookPhase.Inactive || Gas <= 0) return;
            HookHit target;
            if (rayDirection.LengthSquared == 0 || !TryHookAim(rayOrigin, rayDirection, special ? 1e7f : spread ? 1e6f : _config.HookRange, side, world, out target)) return;
            LaunchAt(ref rope, side, rayOrigin, target, spread, world, projectileSpeed, special);
        }
        // Inputs here use reference units, after the motor's world-scale boundary.
        private bool TryHookAim(Vec3 origin, Vec3 direction, float distance, HookSide side,
            ICharacterWorld world, out HookHit target)
        {
            if (!_config.ImprovedRaycasts) return world.TryHook(origin, direction, distance, side, out target);
            direction = direction.Normalized;
            float skip = Math.Min((origin - _position).Length * 0.5f, distance);
            if (skip < distance && world.TryHook(origin + direction * skip, direction, distance - skip, side, out target)) return true;
            // This is an aim point only. Projectile sweeps still require real collision to attach.
            target = new HookHit(origin + direction * distance);
            return direction.LengthSquared > 0;
        }
        private void LaunchAt(ref Rope rope, HookSide side, Vec3 rayOrigin, HookHit target, bool spread,
            ICharacterWorld world, float projectileSpeed = 0, bool special = false)
        {
            if (Gas <= 0) return;
            var geometry = world as IHookGeometryWorld;
            Vec3 muzzle = geometry != null ? geometry.HookOrigin(this, side) : _position + Vec3.Up * 0.85f;
            Vec3 point = target.Point;
            if (spread)
            {
                float distance = (point - rayOrigin).Length;
                float offset = distance * (distance <= 50 ? 0.05f : 0.3f);
                Vec3 f = Math3.Direction(FacingDegrees);
                point += new Vec3(f.Z, 0, -f.X) * (side == HookSide.Left ? -offset : offset);
            }
            Vec3 direction = (point - muzzle).Normalized;
            if (direction.LengthSquared <= 0) return;
            // Bullet.launch inherits only the positive component parallel to the shot.
            float inherited = Math.Max(0, Vec3.Dot(_velocity, direction));
            rope = new Rope { Phase = HookPhase.Flying, Position = muzzle, PreviousPosition = muzzle, Special = special,
                FlightVelocity = direction * ((projectileSpeed > 0 ? projectileSpeed : _config.HookProjectileSpeed) + inherited) };
            if (side == HookSide.Left) _launchPointLeft = Vec3.Zero; else _launchPointRight = Vec3.Zero;
            ConsumeGas(_config.HookLaunchGasCost);
            world.OnCharacterEvent(side == HookSide.Left ? CharacterEvent.HookLaunchedLeft : CharacterEvent.HookLaunchedRight);
        }
        private void AdvanceHooks(float dt, ICharacterWorld world)
        {
            AdvanceHook(ref _left, HookSide.Left, dt, world);
            AdvanceHook(ref _right, HookSide.Right, dt, world);
        }
        private void AdvanceHook(ref Rope rope, HookSide side, float dt, ICharacterWorld world)
        {
            if (rope.Special && (rope.Phase == HookPhase.Attached || rope.Phase == HookPhase.Retracting) && ++rope.SpiralTicks >= 60)
            { rope = new Rope(); return; }
            var geometry = world as IHookGeometryWorld;
            if (rope.Phase == HookPhase.Flying)
            {
                Vec3 end = rope.Position + rope.FlightVelocity * dt;
                HookHit hit;
                Vec3 segment = end - rope.PreviousPosition;
                bool collided = geometry != null ? geometry.SweepHook(rope.PreviousPosition, end, side, out hit) :
                    world.TryHook(rope.PreviousPosition, segment.Normalized, segment.Length, side, out hit);
                // An adapter must return a hit on this segment, never a distant aim target.
                if (collided && (hit.Point - rope.PreviousPosition).Length <= segment.Length + 0.001f)
                {
                    rope.Position = hit.Point; rope.TargetId = hit.TargetId; rope.LocalPoint = hit.LocalPoint; rope.Age = 0;
                    if (side == HookSide.Left) { _launchLeft = true; _elapsedLeft = 0; _launchPointLeft = hit.Point; }
                    else { _launchRight = true; _elapsedRight = 0; _launchPointRight = hit.Point; }
                    bool special = _animation.Clip == "attack5" || _animation.Clip == "special_petra";
                    if (!special) { CancelAction(world); State = CharacterState.Idle; }
                    _dashTime = 0; _jumpPending = false;
                    _wallRunTime = 0;
                    Vec3 launch = (hit.Point - _position).Normalized * 20;
                    // Bullet sets its phase AFTER HERO.launch, so this newly landing side is not hooked yet.
                    launch.Y = Math.Max(launch.Y, 30);
                    if (hit.Point.Y >= _position.Y) launch.Y += (hit.Point.Y - _position.Y) * 10;
                    if (!special) _pendingForce += launch;
                    _skipGasThisTick = true;
                    FacingDegrees = FacingTargetDegrees = Yaw(launch);
                    if (!special) Play(Equipment == EquipmentMode.Guns ? "AHSS_hook_forward_both" :
                        side == HookSide.Left ? (_rightHookedAtPhysicsStart ? "dash" : "air_hook_l_just") :
                        (_leftHookedAtPhysicsStart ? "dash" : "air_hook_r_just"), false, 1, 0.1f, 0, true);
                    // This exception is selected by the current clip, not Bullet.leviMode.
                    // Even a normal hook landing during Levi grants the RIGHT timer a long grace.
                    if (special) _elapsedRight = -100;
                    if (_animation.Clip == "special_petra")
                    {
                        _elapsedLeft = _elapsedRight = -100;
                        Disable(ref _left, HookSide.Left, world); Disable(ref _right, HookSide.Right, world);
                        return; // Bullet sees phase 2 after HERO.launch and never enters phase 1.
                    }
                    rope.Phase = HookPhase.Attached;
                    world.OnCharacterEvent(side == HookSide.Left ? CharacterEvent.HookAttachedLeft : CharacterEvent.HookAttachedRight);
                }
                else
                {
                    rope.PreviousPosition = rope.Position; rope.Position = end;
                    rope.Age += dt;
                    if (rope.Age > _config.HookMissTimeout)
                    {
                        rope.Phase = HookPhase.Retracting; rope.Age = 0; rope.RetractStart = rope.Position;
                        world.OnCharacterEvent(side == HookSide.Left ? CharacterEvent.HookMissedLeft : CharacterEvent.HookMissedRight);
                    }
                }
            }
        }
        private void AdvanceRetraction(ref Rope rope, HookSide side, float dt, ICharacterWorld world)
        {
            if (rope.Special && rope.Phase != HookPhase.Inactive)
            {
                rope.SpecialTime += dt;
                if (rope.SpecialTime > 0.4f && (rope.Phase == HookPhase.Flying || rope.Phase == HookPhase.Attached))
                    Disable(ref rope, side, world);
                if (rope.Phase == HookPhase.Retracting) return; // Spiral lifetime uses fixed ticks.
            }
            if (rope.Phase != HookPhase.Retracting) return;
            rope.Age += dt;
            var geometry = (IHookGeometryWorld)world;
            rope.Position = Vec3.Lerp(rope.RetractStart, geometry.HookOrigin(this, side), _config.HookRetractDuration > 0 ? rope.Age / _config.HookRetractDuration : 1);
            if (rope.Age > _config.HookRetractDuration) rope = new Rope();
        }
        private void UpdateHookAnchor(ref Rope rope, HookSide side, ICharacterWorld world)
        {
            if (rope.Phase == HookPhase.Attached && rope.TargetId != 0)
            {
                var geometry = world as IHookGeometryWorld;
                Vec3 anchor;
                if (geometry == null || !geometry.ResolveHookAnchor(rope.TargetId, rope.LocalPoint, out anchor))
                { Release(ref rope, side, world); return; }
                rope.Position = anchor;
            }
        }
        private void TickLaunchedHook(ref Rope rope, HookSide side, bool held, float dt, ICharacterWorld world)
        {
            if (side == HookSide.Left ? _launchLeft : _launchRight)
            {
                float elapsed = side == HookSide.Left ? (_elapsedLeft += dt) : (_elapsedRight += dt);
                // The special blocks QHold/EHold even if the physical buttons remain held.
                if (HookInputAllowed && held && Gas > 0) ConsumeGas(_config.HookGasUsePerSecond * dt);
                else if (elapsed > _config.HookReleaseGrace) Release(ref rope, side, world);
            }
        }
        private bool RopeForce(Rope rope, bool otherLaunched, bool boost, ref Vec3 acceleration)
        {
            if (rope.Phase != HookPhase.Attached) return false;
            Vec3 direction = (rope.Position - _position).Normalized;
            bool away = Vec3.Dot(_velocity, direction) < 0;
            if (away && boost) return true;
            // HERO halves each pull when both sides are launched. There is no rope-length constraint.
            float pull = otherLaunched ? _config.HookPullAcceleration * 0.5f : _config.HookPullAcceleration;
            acceleration += direction * (pull / _config.Mass);
            if (away) acceleration -= _velocity * 2;
            return false;
        }
        private void Release(ref Rope rope, HookSide side, ICharacterWorld world)
        {
            if (side == HookSide.Left) _launchLeft = false; else _launchRight = false;
            if (rope.Phase == HookPhase.Inactive) return;
            rope = new Rope();
            world.OnCharacterEvent(side == HookSide.Left ? CharacterEvent.HookReleasedLeft : CharacterEvent.HookReleasedRight);
        }
        private void Disable(ref Rope rope, HookSide side, ICharacterWorld world)
        {
            if (rope.Phase == HookPhase.Inactive || rope.Phase == HookPhase.Retracting) return;
            rope.Phase = HookPhase.Retracting; rope.Age = 0; rope.RetractStart = rope.Position;
            world.OnCharacterEvent(side == HookSide.Left ? CharacterEvent.HookReleasedLeft : CharacterEvent.HookReleasedRight);
        }
        public void ReleaseHook(HookSide side, ICharacterWorld world)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (side == HookSide.Left) Release(ref _left, side, world); else Release(ref _right, side, world);
        }
        private static Vec3 RotateTowards(Vec3 from, Vec3 to, float radians)
        {
            float a = from.Length, b = to.Length;
            if (a < 0.00001f) return to;
            if (b < 0.00001f) return from;
            Vec3 u = from / a, v = to / b;
            float angle = (float)Math.Acos(Math3.Clamp(Vec3.Dot(u, v), -1, 1));
            if (angle <= radians) return to;
            Vec3 tangent = (v - u * Vec3.Dot(u, v)).Normalized;
            if (tangent.LengthSquared < 0.001f)
            {
                // Unity's OrthoNormalVectorFast branch, checked against the 4.6 player.
                Vec3 axis = Math.Abs(u.Z) > 0.70710678f ? new Vec3(0, -u.Z, u.Y).Normalized : new Vec3(-u.Y, u.X, 0).Normalized;
                tangent = Vec3.Cross(axis, u);
            }
            return (u * (float)Math.Cos(radians) + tangent * (float)Math.Sin(radians)) * a;
        }
    }
}
