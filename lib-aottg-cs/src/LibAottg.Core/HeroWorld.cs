// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace LibAottg
{
    public enum HeroQuery { Ground, Wall, HookAim, HookProjectile, Camera, Gun }
    public struct HeroWeaponSweep
    {
        public HookSide Side;
        public Vec3 PreviousStart, PreviousEnd, Start, End;
        public float Radius;
        public bool Gun;
    }
    /// <summary>Destination-game hit identity and point. The host applies its own damage policy.</summary>
    public struct HeroHit
    {
        public int TargetId;
        public Vec3 Point;
        public HookSide Side;
        public bool Gun;
        /// <summary>Attacker speed in host-independent character units per second.</summary>
        public float Speed;
        /// <summary>Target velocity in host world units per second.</summary>
        public Vec3 TargetVelocity;
        /// <summary>AoTTG speed-based damage, calculated by LibAottg when the hit is accepted.</summary>
        public int Damage;
        /// <summary>AoTTG weapon score multiplier; zero selects the default multiplier of one.</summary>
        public float ScoreMultiplier;
    }
    public interface IHeroCombatWorld
    {
        void CollectWeaponHits(HeroWeaponSweep sweep, IList<HeroHit> hits);
        void OnHeroHit(HeroHit hit);
    }
    public interface IHeroAttackTargetWorld
    {
        /// <summary>Return an enemy weak point for source attack selection, rather than ordinary scenery.</summary>
        bool TryAttackTarget(CharacterMotor motor, CharacterInput input, out Vec3 point);
    }
    public interface IHeroWorld : IHookGeometryWorld
    {
        CharacterContacts GetContacts(CharacterMotor motor, HeroPose pose);
        CollisionResult Move(CharacterMotor motor, Vec3 start, HeroPose proposedPose, float dt);
    }
    /// <summary>Small optional host boundary. Override Raycast and Move for a solid world;
    /// defaults support free flight without collision or damage. All spatial values use host units.</summary>
    public class HeroWorld : IHeroWorld, IHeroCameraWorld, IHeroCombatWorld
    {
        public virtual bool Raycast(Vec3 origin, Vec3 direction, float distance, HeroQuery query, out HookHit hit)
        { hit = default(HookHit); return false; }
        public virtual CollisionResult Move(CharacterMotor motor, Vec3 start, HeroPose proposedPose, float dt)
        { return new CollisionResult { Position = motor.Position, Velocity = motor.Velocity }; }
        public virtual CharacterContacts GetContacts(CharacterMotor motor, HeroPose pose)
        {
            HookHit hit;
            float scale = motor.Scale;
            return new CharacterContacts {
                Grounded = Raycast(motor.Position + Vec3.Up * (0.1f * scale), -Vec3.Up, 0.3f * scale, HeroQuery.Ground, out hit),
                GroundNormal = Vec3.Up,
                FrontWall = Raycast(motor.Position + pose.Up * scale, pose.Forward, scale, HeroQuery.Wall, out hit),
                UpperFrontClear = !Raycast(motor.Position + pose.Up * (3 * scale), pose.Forward, 1.2f * scale, HeroQuery.Wall, out hit)
            };
        }
        public virtual Vec3 HookOrigin(CharacterMotor motor, HookSide side)
        { return motor.Position + Vec3.Up * (0.85f * motor.Scale); }
        public virtual bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit)
        { return Raycast(origin, direction, distance, HeroQuery.HookAim, out hit); }
        public virtual bool SweepHook(Vec3 start, Vec3 end, HookSide side, out HookHit hit)
        { Vec3 d = end - start; return Raycast(start, d.Normalized, d.Length, HeroQuery.HookProjectile, out hit); }
        public virtual bool ResolveHookAnchor(int targetId, Vec3 localPoint, out Vec3 worldPoint)
        { worldPoint = Vec3.Zero; return false; }
        public virtual void OnCharacterEvent(CharacterEvent kind) { }
        public virtual bool SweepCamera(Vec3 pivot, Vec3 position, float radius, out Vec3 safePosition)
        {
            Vec3 d = position - pivot; HookHit hit;
            bool found = Raycast(pivot, d.Normalized, d.Length + radius, HeroQuery.Camera, out hit);
            safePosition = found ? pivot + d.Normalized * Math.Max(0, (hit.Point - pivot).Length - radius) : position;
            return found;
        }
        public virtual void CollectWeaponHits(HeroWeaponSweep sweep, IList<HeroHit> hits)
        {
            if (!sweep.Gun) return;
            Vec3 d = sweep.End - sweep.Start; HookHit hit;
            if (Raycast(sweep.Start, d.Normalized, d.Length, HeroQuery.Gun, out hit) && hit.TargetId != 0)
                hits.Add(new HeroHit { TargetId = hit.TargetId, Point = hit.Point });
        }
        public virtual void OnHeroHit(HeroHit hit) { }
    }

    /// <summary>Ready-to-use minimal static box/floor host. Replace with native engine queries as needed.</summary>
    public class CalibrationHeroWorld : HeroWorld
    {
        public KinematicCollisionWorld Collision { get; private set; }
        public CalibrationHeroWorld(float radius = 0.5f, float height = 1.7f, float worldScale = 1)
        { Collision = new KinematicCollisionWorld(radius, height, worldScale); }
        public override bool Raycast(Vec3 origin, Vec3 direction, float distance, HeroQuery query, out HookHit hit)
        { return Collision.TryHook(origin, direction, distance, HookSide.Left, out hit); }
        public override CollisionResult Move(CharacterMotor motor, Vec3 start, HeroPose proposedPose, float dt)
        { return Collision.Move(start, motor.Position, motor.Velocity); }
    }

    /// <summary>Optional animated rig service. A host using its own rig can implement this without GLB.</summary>
    public interface IHeroPresentation
    {
        void Sample(CharacterMotor motor, HeroPose pose, CharacterInput input, float blendDelta);
        Vec3 Head { get; }
        Vec3 HookOrigin(CharacterMotor motor, HookSide side);
        bool WeaponSegment(HookSide side, bool gun, out Vec3 start, out Vec3 end);
    }
}
