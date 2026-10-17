// SPDX-License-Identifier: MIT
using LibAottg;
using Stride.Core.Mathematics;

namespace StrideAottg;

/// <summary>Example host queries and training-target damage. Controller rules live in LibAottg.</summary>
public sealed class StrideWorld : CalibrationHeroWorld, IHeroAttackTargetWorld
{
    public CityCollision City { get; set; }
    public readonly HashSet<CharacterEvent> Events = new();
    public readonly List<HeroHit> Hits = new();
    public CharacterEvent? LastEvent { get; private set; }
    public int CollisionCount { get; private set; }
    public int MovingAnchorQueries { get; private set; }
    public float CurrentTime { get; private set; }
    private readonly List<Target> targets = new();
    public IReadOnlyList<Target> Targets => targets;
    public event Action<Target, HeroHit> TargetKilled;
    public sealed class Target
    {
        public int Id, BoxIndex;
        public string Name = "Titan";
        public Vec3 Center, Origin, Half, Velocity;
        public bool Moving;
        public bool InfiniteHealth;
        public bool OneHit;
        public float Health = 100;
        public float RespawnAt = -1;
        public bool Active => Health > 0;
    }
    public StrideWorld(float radius, float height, float scale = 1) : base(radius, height, scale) { }
    public Target AddTarget(Vec3 center, Vec3 size, bool moving = false, bool infiniteHealth = false, bool oneHit = false)
    {
        var target = new Target { Id = targets.Count + 1, BoxIndex = Collision.Boxes.Count,
            Origin = center, Center = center, Half = size * 0.5f, Moving = moving,
            InfiniteHealth = infiniteHealth, OneHit = oneHit };
        targets.Add(target); Collision.Boxes.Add(new WorldBox(center - target.Half, center + target.Half));
        return target;
    }
    public void UpdateTargets(float time)
    {
        CurrentTime = time;
        foreach (var t in targets.Where(t => t.Moving))
        {
            t.Center = t.Origin + new Vec3(MathF.Sin(time * 0.6f) * 8 * Collision.WorldScale, 0, 0);
            t.Velocity = new Vec3(MathF.Cos(time * 0.6f) * 4.8f * Collision.WorldScale, 0, 0);
            Collision.Boxes[t.BoxIndex] = new WorldBox(t.Center - t.Half, t.Center + t.Half);
        }
        foreach (var t in targets.Where(t => !t.Active && t.RespawnAt >= 0 && time >= t.RespawnAt))
        {
            t.Health = 100;
            t.RespawnAt = -1;
            Collision.Boxes[t.BoxIndex] = new WorldBox(t.Center - t.Half, t.Center + t.Half);
        }
    }
    public void ResetTargets() { foreach (var t in targets) { t.Health = 100; t.RespawnAt = -1; } UpdateTargets(0); Hits.Clear(); }
    public override bool Raycast(Vec3 origin, Vec3 direction, float distance, HeroQuery query, out HookHit hit)
    {
        if (City != null)
        {
            bool found = City.Raycast(origin, direction, distance, out hit);
            float nearest = found ? (hit.Point - origin).Length : distance;
            Vec3 ray = direction.Normalized;
            foreach (var target in targets.Where(t => t.Health > 0))
            {
                if (RayBox(origin, ray, nearest, target.Center - target.Half, target.Center + target.Half, out float along) && along < nearest)
                {
                    nearest = along;
                    hit = new HookHit(origin + ray * along) { TargetId = target.Id, LocalPoint = origin + ray * along - target.Center };
                    found = true;
                }
            }
            return found;
        }
        if (!base.Raycast(origin, direction, distance, query, out hit)) return false;
        float tolerance = 0.002f * Collision.WorldScale;
        foreach (var t in targets)
        {
            Vec3 p = hit.Point - t.Center;
            if (Math.Abs(p.X) <= t.Half.X + tolerance && Math.Abs(p.Y) <= t.Half.Y + tolerance && Math.Abs(p.Z) <= t.Half.Z + tolerance)
            { hit.TargetId = t.Id; hit.LocalPoint = p; break; }
        }
        return true;
    }
    private static bool RayBox(Vec3 origin, Vec3 direction, float limit, Vec3 min, Vec3 max, out float along)
    {
        float near = 0, far = limit;
        for (int axis = 0; axis < 3; axis++)
        {
            float p = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            float d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            float lo = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            float hi = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;
            if (Math.Abs(d) < 1e-10f) { if (p < lo || p > hi) { along = 0; return false; } continue; }
            float a = (lo - p) / d, b = (hi - p) / d;
            near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
            if (near > far) { along = 0; return false; }
        }
        along = near;
        return near <= limit && far >= 0;
    }
    public override bool ResolveHookAnchor(int id, Vec3 localPoint, out Vec3 point)
    {
        var t = targets.FirstOrDefault(t => t.Id == id);
        point = t != null ? t.Center + localPoint : default;
        if (t?.Moving == true) MovingAnchorQueries++;
        return t != null;
    }
    public override void OnCharacterEvent(CharacterEvent kind) { LastEvent = kind; Events.Add(kind); }
    public override CollisionResult Move(CharacterMotor motor, Vec3 start, HeroPose proposedPose, float dt)
    {
        var result = City != null ? City.Move(start, motor.Position, motor.Velocity, Collision.Radius, Collision.Height) : base.Move(motor, start, proposedPose, dt);
        if (result.Collided) CollisionCount++;
        return result;
    }
    public override CharacterContacts GetContacts(CharacterMotor motor, HeroPose pose)
    {
        if (City == null) return base.GetContacts(motor, pose);
        float scale = motor.Scale;
        bool ground = Raycast(motor.Position + Vec3.Up * (0.1f * scale), -Vec3.Up, 0.3f * scale, HeroQuery.Ground, out _);
        Vec3 normal = City.LastNormal;
        bool front = Raycast(motor.Position + Vec3.Up * scale, pose.Forward, scale, HeroQuery.Wall, out _);
        bool upper = !Raycast(motor.Position + Vec3.Up * (3 * scale), pose.Forward, 1.2f * scale, HeroQuery.Wall, out _);
        return new CharacterContacts { Grounded = ground, GroundNormal = normal, FrontWall = front, UpperFrontClear = upper };
    }
    public bool TryAttackTarget(CharacterMotor motor, CharacterInput input, out Vec3 point)
    {
        var nearest = targets.Where(t => t.Health > 0).OrderBy(t => (t.Center - motor.Position).LengthSquared).FirstOrDefault();
        point = nearest?.Center ?? default;
        return nearest != null && (point - motor.Position).Length / motor.Scale < 8;
    }
    public override void CollectWeaponHits(HeroWeaponSweep sweep, IList<HeroHit> hits)
    {
        if (sweep.Gun) { base.CollectWeaponHits(sweep, hits); return; }
        foreach (var t in targets.Where(t => t.Health > 0))
        {
            var half = t.Half + new Vec3(sweep.Radius, sweep.Radius, sweep.Radius);
            if (SegmentBox(sweep.Start, sweep.End, t.Center - half, t.Center + half) ||
                SegmentBox(sweep.PreviousEnd, sweep.End, t.Center - half, t.Center + half) ||
                SegmentBox(sweep.PreviousStart, sweep.Start, t.Center - half, t.Center + half))
                hits.Add(new HeroHit { TargetId = t.Id, Point = sweep.End, TargetVelocity = t.Velocity });
        }
    }
    private static bool SegmentBox(Vec3 start, Vec3 end, Vec3 min, Vec3 max)
    {
        var d = end - start;
        float near = 0, far = 1;
        for (int i = 0; i < 3; i++)
        {
            float p = i == 0 ? start.X : i == 1 ? start.Y : start.Z;
            float v = i == 0 ? d.X : i == 1 ? d.Y : d.Z;
            float lo = i == 0 ? min.X : i == 1 ? min.Y : min.Z, hi = i == 0 ? max.X : i == 1 ? max.Y : max.Z;
            if (Math.Abs(v) < 1e-12f) { if (p < lo || p > hi) return false; continue; }
            float a = (lo - p) / v, b = (hi - p) / v;
            near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
            if (near > far) return false;
        }
        return true;
    }
    public override void OnHeroHit(HeroHit hit)
    {
        var target = targets.FirstOrDefault(t => t.Id == hit.TargetId);
        if (target == null || target.Health <= 0) return;
        if (!target.InfiniteHealth) target.Health = target.OneHit ? 0 : Math.Max(0, target.Health - hit.Damage);
        Hits.Add(hit);
        if (target.Health == 0)
        {
            target.RespawnAt = CurrentTime + 5;
            TargetKilled?.Invoke(target, hit);
        }
    }
    public static Vector3 ToStride(Vec3 value) => new(value.X, value.Y, -value.Z);
    public static Vec3 ToCore(Vector3 value) => new(value.X, value.Y, -value.Z);
    public static Quaternion Rotation(HeroPose pose) => Quaternion.RotationMatrix(
        Matrix.RotationZ(MathUtil.DegreesToRadians(pose.Roll)) * Matrix.RotationX(-MathUtil.DegreesToRadians(pose.Pitch)) *
        Matrix.RotationY(-MathUtil.DegreesToRadians(pose.Yaw)));
}
