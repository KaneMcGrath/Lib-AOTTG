// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace LibAottg
{
    /// <summary>Static axis-aligned calibration geometry in core coordinates.</summary>
    public struct WorldBox
    {
        public Vec3 Min, Max;
        public WorldBox(Vec3 min, Vec3 max)
        {
            if (!Finite(min) || !Finite(max) || min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z)
                throw new ArgumentException("Box bounds must be finite and ordered.");
            Min = min; Max = max;
        }
        internal static bool Finite(Vec3 v) { return Finite(v.X) && Finite(v.Y) && Finite(v.Z); }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }

    public struct CollisionResult
    {
        public Vec3 Position, Velocity;
        public bool Collided, Grounded;
    }

    /// <summary>
    /// Static calibration world: continuous upright capsule sweeps, sliding and ground/wall probes.
    /// Gravity remains motor-owned. Hosts with arbitrary meshes or moving geometry supply their own solver.
    /// </summary>
    public sealed class KinematicCollisionWorld : ICharacterWorld
    {
        public readonly List<WorldBox> Boxes = new List<WorldBox>();
        public Action<CharacterEvent> Event;
        /// <summary>Dimensionless Coulomb friction for this calibration geometry. Unity's default is 0.6.</summary>
        public float Friction = 0.6f;
        private float _radius, _height;
        public float WorldScale { get; private set; }
        public float Radius { get { return _radius * WorldScale; } }
        public float Height { get { return _height * WorldScale; } }
        public const float Skin = 0.001f;
        public KinematicCollisionWorld(float radius = 0.5f, float height = 1.7f, float worldScale = 1f)
        {
            if (radius <= 0 || height < 2 * radius || !WorldBox.Finite(new Vec3(radius, height, 0)))
                throw new ArgumentOutOfRangeException("radius", "Require finite positive radius and height >= diameter.");
            if (worldScale <= 0 || !WorldBox.Finite(new Vec3(worldScale, 0, 0))) throw new ArgumentOutOfRangeException("worldScale");
            WorldScale = worldScale; _radius = radius; _height = height;
        }
        public void OnCharacterEvent(CharacterEvent kind) { if (Event != null) Event(kind); }

        public bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit)
        {
            origin /= WorldScale; distance /= WorldScale;
            direction = direction.Normalized;
            float nearest = distance; bool found = false;
            foreach (var hostBox in Boxes)
            {
                WorldBox box = ReferenceBox(hostBox);
                float t;
                if (RayBox(origin, direction, box, out t) && t >= 0 && t <= nearest)
                { nearest = t; found = true; }
            }
            if (direction.Y < -0.00001f && origin.Y >= 0)
            {
                float t = -origin.Y / direction.Y;
                if (t <= nearest) { nearest = t; found = true; }
            }
            hit = found ? new HookHit((origin + direction * nearest) * WorldScale) : default(HookHit);
            return found;
        }

        public CharacterContacts Contacts(Vec3 feet, Vec3 forward)
        {
            HookHit hit;
            Vec3 direction = new Vec3(forward.X, 0, forward.Z).Normalized;
            bool ground = TryHook(feet + Vec3.Up * (0.1f * WorldScale), -Vec3.Up, 0.3f * WorldScale, HookSide.Left, out hit);
            bool front = TryHook(feet + Vec3.Up * WorldScale, direction, WorldScale, HookSide.Left, out hit);
            bool upper = !TryHook(feet + Vec3.Up * (3f * WorldScale), direction, 1.2f * WorldScale, HookSide.Left, out hit);
            return new CharacterContacts { Grounded = ground, GroundNormal = Vec3.Up, FrontWall = front, UpperFrontClear = upper };
        }
        private WorldBox ReferenceBox(WorldBox box) { return new WorldBox(box.Min / WorldScale, box.Max / WorldScale); }

        public CollisionResult Move(Vec3 start, Vec3 target, Vec3 velocity)
        {
            if (!WorldBox.Finite(start) || !WorldBox.Finite(target) || !WorldBox.Finite(velocity))
                throw new ArgumentException("Collision inputs must be finite.");
            if (Friction < 0 || !WorldBox.Finite(new Vec3(Friction, 0, 0))) throw new ArgumentOutOfRangeException("Friction");
            start /= WorldScale; target /= WorldScale; velocity /= WorldScale;
            Vec3 position = Depenetrate(start);
            bool collided = (position - start).LengthSquared > 0;
            Vec3 remaining = target - start;
            float step = velocity.LengthSquared > 0 ? Math.Max(0, Vec3.Dot(remaining, velocity) / velocity.LengthSquared) : 0;
            bool ground = false;
            for (int iteration = 0; iteration < 8 && remaining.LengthSquared > 1e-12f; iteration++)
            {
                float t; Vec3 normal;
                if (!Sweep(position, remaining, out t, out normal)) { position += remaining; break; }
                bool restingContact = Math.Abs(Vec3.Dot(remaining, normal)) * t <= Skin * 1.5f;
                collided = true; ground |= normal.Y > 0.5f;
                position += remaining * t + normal * Skin;
                remaining *= 1 - t;
                float into = Vec3.Dot(remaining, normal);
                if (into < 0) remaining -= normal * into;
                into = Vec3.Dot(velocity, normal);
                if (into < 0)
                {
                    velocity -= normal * into;
                    float tangentSpeed = velocity.Length;
                    if (tangentSpeed > 0)
                    {
                        Vec3 beforeFriction = velocity;
                        velocity *= Math.Max(0, 1 - Friction * -into / tangentSpeed);
                        Vec3 change = velocity - beforeFriction;
                        remaining += change * (step * (1 - t));
                        if (restingContact) position += change * (step * t);
                    }
                }
            }
            return new CollisionResult { Position = position * WorldScale, Velocity = velocity * WorldScale, Collided = collided, Grounded = ground };
        }

        private Vec3 Depenetrate(Vec3 feet)
        {
            for (int pass = 0; pass < 12; pass++)
            {
                bool corrected = false;
                if (feet.Y < 0) { feet.Y = Skin; corrected = true; }
                foreach (var hostBox in Boxes)
                {
                    WorldBox box = ReferenceBox(hostBox);
                    var bounds = ExpandedSegment(box);
                    Vec3 center = feet + Vec3.Up * (_height / 2);
                    Vec3 delta = center - Closest(center, bounds);
                    float length = delta.Length;
                    if (length >= _radius) continue;
                    Vec3 normal; float depth;
                    if (length > 0.000001f) { normal = delta / length; depth = _radius - length; }
                    else
                    {
                        float best = center.X - bounds.Min.X; normal = new Vec3(-1, 0, 0);
                        Choose(bounds.Max.X - center.X, new Vec3(1, 0, 0), ref best, ref normal);
                        Choose(center.Y - bounds.Min.Y, new Vec3(0, -1, 0), ref best, ref normal);
                        Choose(bounds.Max.Y - center.Y, Vec3.Up, ref best, ref normal);
                        Choose(center.Z - bounds.Min.Z, new Vec3(0, 0, -1), ref best, ref normal);
                        Choose(bounds.Max.Z - center.Z, new Vec3(0, 0, 1), ref best, ref normal);
                        depth = best + _radius;
                    }
                    feet += normal * (depth + Skin); corrected = true;
                }
                if (!corrected) break;
            }
            return feet;
        }
        private static void Choose(float value, Vec3 candidate, ref float best, ref Vec3 normal)
        { if (value < best) { best = value; normal = candidate; } }

        private bool Sweep(Vec3 feet, Vec3 motion, out float time, out Vec3 normal)
        {
            time = 1; normal = Vec3.Zero; bool found = false;
            if (motion.Y < 0 && feet.Y + motion.Y <= 0)
            { time = Math.Max(0, -feet.Y / motion.Y); normal = Vec3.Up; found = true; }
            Vec3 center = feet + Vec3.Up * (_height / 2);
            foreach (var hostBox in Boxes)
            {
                WorldBox box = ReferenceBox(hostBox);
                float t; Vec3 n;
                if (RoundedBoxSweep(center, motion, ExpandedSegment(box), _radius, out t, out n) && t <= time)
                { time = t; normal = n; found = true; }
            }
            return found;
        }
        private WorldBox ExpandedSegment(WorldBox box)
        {
            float halfSegment = _height / 2 - _radius;
            return new WorldBox(box.Min - Vec3.Up * halfSegment, box.Max + Vec3.Up * halfSegment);
        }
        private static Vec3 Closest(Vec3 p, WorldBox b)
        { return new Vec3(Math3.Clamp(p.X, b.Min.X, b.Max.X), Math3.Clamp(p.Y, b.Min.Y, b.Max.Y), Math3.Clamp(p.Z, b.Min.Z, b.Max.Z)); }
        private static float Axis(Vec3 v, int i) { return i == 0 ? v.X : i == 1 ? v.Y : v.Z; }
        private static Vec3 Unit(int i) { return i == 0 ? new Vec3(1, 0, 0) : i == 1 ? Vec3.Up : new Vec3(0, 0, 1); }

        // Capsule-box Minkowski sum = box expanded by vertical capsule segment, rounded by radius.
        // Test its six faces, twelve edge cylinders and eight corner spheres analytically.
        private static bool RoundedBoxSweep(Vec3 origin, Vec3 motion, WorldBox box, float radius, out float time, out Vec3 normal)
        {
            float best = 2; Vec3 bestNormal = Vec3.Zero;
            for (int axis = 0; axis < 3; axis++)
            for (int side = 0; side < 2; side++)
            {
                float velocity = Axis(motion, axis); if (Math.Abs(velocity) < 1e-10f) continue;
                float boundary = Axis(side == 0 ? box.Min : box.Max, axis) + (side == 0 ? -radius : radius);
                Accept((boundary - Axis(origin, axis)) / velocity, origin, motion, box, radius, ref best, ref bestNormal);
            }
            for (int axis = 0; axis < 3; axis++)
            for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                Vec3 edge = Unit(u) * Axis(a == 0 ? box.Min : box.Max, u) + Unit(v) * Axis(b == 0 ? box.Min : box.Max, v);
                Vec3 d = origin - edge; d -= Unit(axis) * Axis(d, axis);
                Vec3 m = motion - Unit(axis) * Axis(motion, axis);
                RootCandidate(d, m, origin, motion, box, radius, ref best, ref bestNormal);
            }
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                var corner = new Vec3(x == 0 ? box.Min.X : box.Max.X, y == 0 ? box.Min.Y : box.Max.Y, z == 0 ? box.Min.Z : box.Max.Z);
                RootCandidate(origin - corner, motion, origin, motion, box, radius, ref best, ref bestNormal);
            }
            time = best; normal = bestNormal; return best <= 1;
        }
        private static void RootCandidate(Vec3 d, Vec3 m, Vec3 origin, Vec3 motion, WorldBox box, float radius, ref float best, ref Vec3 normal)
        {
            double a = Vec3.Dot(m, m), b = 2.0 * Vec3.Dot(d, m), c = Vec3.Dot(d, d) - radius * radius;
            if (a < 1e-16) return;
            double discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return;
            float t = (float)((-b - Math.Sqrt(discriminant)) / (2 * a));
            Accept(t, origin, motion, box, radius, ref best, ref normal);
        }
        private static void Accept(float t, Vec3 origin, Vec3 motion, WorldBox box, float radius, ref float best, ref Vec3 normal)
        {
            if (t < -0.000001f || t > 1 || t >= best) return;
            t = Math.Max(0, t);
            Vec3 point = origin + motion * t, delta = point - Closest(point, box);
            float distance = delta.Length;
            if (Math.Abs(distance - radius) > 0.0001f || distance < 1e-8f) return;
            Vec3 n = delta / distance;
            if (Vec3.Dot(motion, n) >= -1e-8f) return;
            best = t; normal = n;
        }
        private static bool RayBox(Vec3 origin, Vec3 direction, WorldBox box, out float time)
        {
            float near = 0, far = float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
            {
                float o = Axis(origin, i), d = Axis(direction, i), min = Axis(box.Min, i), max = Axis(box.Max, i);
                if (Math.Abs(d) < 1e-8f) { if (o < min || o > max) { time = 0; return false; } continue; }
                float a = (min - o) / d, b = (max - o) / d;
                near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
                if (near > far) { time = 0; return false; }
            }
            time = near; return far >= 0;
        }
    }
}
