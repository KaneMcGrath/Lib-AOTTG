// SPDX-License-Identifier: MIT
using LibAottg;

namespace StrideAottg;

/// <summary>Demo-owned static triangle queries and continuous upright capsule collision.</summary>
public sealed class CityCollision
{
    private struct Triangle
    {
        public Vec3 A, B, C, Min, Max, Normal;
        public Triangle(Vec3 a, Vec3 b, Vec3 c)
        { A = a; B = b; C = c; Min = Minimum(a, Minimum(b, c)); Max = Maximum(a, Maximum(b, c)); Normal = Vec3.Cross(b - a, c - a).Normalized; }
    }
    private sealed class Node
    {
        public Vec3 Min, Max;
        public int Start, Count;
        public Node Left, Right;
    }
    private readonly Triangle[] triangles;
    private readonly Node root;
    private readonly List<int> candidates = new();
    public float Scale { get; }
    public int TriangleCount => triangles.Length;
    public Vec3 LastNormal { get; private set; }
    public float Friction = 0.6f;
    private const float Skin = 0.001f;

    public CityCollision(string file, float scale)
    {
        Scale = scale;
        using var r = new BinaryReader(File.OpenRead(file));
        if (r.ReadUInt32() != 0x314C4F43) throw new InvalidDataException("City collider header.");
        triangles = new Triangle[r.ReadInt32()];
        Vec3 V() => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        for (int i = 0; i < triangles.Length; i++) triangles[i] = new Triangle(V(), V(), V());
        root = Build(0, triangles.Length);
    }
    private static Vec3 Minimum(Vec3 a, Vec3 b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
    private static Vec3 Maximum(Vec3 a, Vec3 b) => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
    private static float Axis(Vec3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
    private Node Build(int start, int count)
    {
        var n = new Node { Start = start, Count = count, Min = new(float.MaxValue, float.MaxValue, float.MaxValue), Max = new(float.MinValue, float.MinValue, float.MinValue) };
        for (int i = start; i < start + count; i++) { n.Min = Minimum(n.Min, triangles[i].Min); n.Max = Maximum(n.Max, triangles[i].Max); }
        if (count > 8)
        {
            Vec3 size = n.Max - n.Min; int axis = size.X > size.Y && size.X > size.Z ? 0 : size.Y > size.Z ? 1 : 2;
            Array.Sort(triangles, start, count, Comparer<Triangle>.Create((a, b) => Axis(a.Min + a.Max, axis).CompareTo(Axis(b.Min + b.Max, axis))));
            int half = count / 2; n.Left = Build(start, half); n.Right = Build(start + half, count - half);
        }
        return n;
    }
    private void Query(Node n, Vec3 min, Vec3 max)
    {
        if (n.Max.X < min.X || n.Min.X > max.X || n.Max.Y < min.Y || n.Min.Y > max.Y || n.Max.Z < min.Z || n.Min.Z > max.Z) return;
        if (n.Left != null) { Query(n.Left, min, max); Query(n.Right, min, max); }
        else for (int i = n.Start; i < n.Start + n.Count; i++) candidates.Add(i);
    }
    private static bool RayBounds(Node n, Vec3 p, Vec3 d, float limit)
    {
        float near = 0, far = limit;
        for (int a = 0; a < 3; a++)
        {
            float v = Axis(d, a), x = Axis(p, a), min = Axis(n.Min, a), max = Axis(n.Max, a);
            if (Math.Abs(v) < 1e-10f) { if (x < min || x > max) return false; continue; }
            float lo = (min - x) / v, hi = (max - x) / v;
            near = Math.Max(near, Math.Min(lo, hi)); far = Math.Min(far, Math.Max(lo, hi));
            if (near > far) return false;
        }
        return true;
    }
    private static bool RayTriangle(Triangle t, Vec3 p, Vec3 d, out float distance)
    {
        Vec3 e1 = t.B - t.A, e2 = t.C - t.A, h = Vec3.Cross(d, e2);
        float det = Vec3.Dot(e1, h); distance = 0;
        if (Math.Abs(det) < 1e-8f) return false;
        float inv = 1 / det; Vec3 s = p - t.A;
        float u = inv * Vec3.Dot(s, h);
        if (u < -1e-6f || u > 1.000001f) return false;
        Vec3 q = Vec3.Cross(s, e1); float v = inv * Vec3.Dot(d, q);
        if (v < -1e-6f || u + v > 1.000001f) return false;
        distance = inv * Vec3.Dot(e2, q); return distance >= 0;
    }
    private void Ray(Node n, Vec3 p, Vec3 d, ref float nearest, ref bool found, ref Vec3 normal)
    {
        if (!RayBounds(n, p, d, nearest)) return;
        if (n.Left != null) { Ray(n.Left, p, d, ref nearest, ref found, ref normal); Ray(n.Right, p, d, ref nearest, ref found, ref normal); }
        else for (int i = n.Start; i < n.Start + n.Count; i++)
            if (RayTriangle(triangles[i], p, d, out float distance) && distance <= nearest)
            { nearest = distance; found = true; normal = triangles[i].Normal; if (Vec3.Dot(normal, d) > 0) normal = -normal; }
    }
    public bool Raycast(Vec3 origin, Vec3 direction, float distance, out HookHit hit)
    {
        Vec3 p = origin / Scale, d = direction.Normalized, normal = Vec3.Zero;
        float nearest = distance / Scale; bool found = false;
        Ray(root, p, d, ref nearest, ref found, ref normal); LastNormal = normal;
        hit = found ? new HookHit((p + d * nearest) * Scale) : default;
        return found;
    }

    private static Vec3 PointTriangle(Vec3 p, Triangle t)
    {
        Vec3 ab = t.B - t.A, ac = t.C - t.A, ap = p - t.A;
        float d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return t.A;
        Vec3 bp = p - t.B; float d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return t.B;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return t.A + ab * (d1 / (d1 - d3));
        Vec3 cp = p - t.C; float d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return t.C;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return t.A + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return t.B + (t.C - t.B) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float inv = 1 / (va + vb + vc); return t.A + ab * (vb * inv) + ac * (vc * inv);
    }
    private static void Segments(Vec3 p, Vec3 q, Vec3 a, Vec3 b, out Vec3 x, out Vec3 y)
    {
        Vec3 d1 = q - p, d2 = b - a, r = p - a;
        float aa = Vec3.Dot(d1, d1), ee = Vec3.Dot(d2, d2), ff = Vec3.Dot(d2, r), s, t;
        if (aa < 1e-12f) { s = 0; t = ee < 1e-12f ? 0 : Math.Clamp(ff / ee, 0, 1); }
        else
        {
            float cc = Vec3.Dot(d1, r);
            if (ee < 1e-12f) { t = 0; s = Math.Clamp(-cc / aa, 0, 1); }
            else
            {
                float bb = Vec3.Dot(d1, d2), denominator = aa * ee - bb * bb;
                s = denominator > 1e-12f ? Math.Clamp((bb * ff - cc * ee) / denominator, 0, 1) : 0;
                t = (bb * s + ff) / ee;
                if (t < 0) { t = 0; s = Math.Clamp(-cc / aa, 0, 1); }
                else if (t > 1) { t = 1; s = Math.Clamp((bb - cc) / aa, 0, 1); }
            }
        }
        x = p + d1 * s; y = a + d2 * t;
    }
    private static float CapsuleDistance(Triangle triangle, Vec3 feet, float radius, float height, out Vec3 normal)
    {
        Vec3 p = feet + Vec3.Up * radius, q = feet + Vec3.Up * (height - radius);
        Vec3 best = p - PointTriangle(p, triangle);
        void Consider(Vec3 delta) { if (delta.LengthSquared < best.LengthSquared) best = delta; }
        Consider(q - PointTriangle(q, triangle));
        foreach (var edge in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
        { Segments(p, q, edge.Item1, edge.Item2, out Vec3 x, out Vec3 y); Consider(x - y); }
        if (RayTriangle(triangle, p, q - p, out float along) && along <= 1) best = Vec3.Zero;
        float distance = best.Length;
        normal = distance > 1e-6f ? best / distance : Vec3.Dot((p + q) * .5f - triangle.A, triangle.Normal) >= 0 ? triangle.Normal : -triangle.Normal;
        return distance;
    }
    private void Candidates(Vec3 start, Vec3 end, float radius, float height)
    {
        candidates.Clear(); Vec3 pad = new(radius + Skin, Skin, radius + Skin);
        Query(root, Minimum(start, end) - pad, Maximum(start, end) + pad + Vec3.Up * height);
    }
    public CollisionResult Move(Vec3 start, Vec3 target, Vec3 velocity, float hostRadius, float hostHeight)
    {
        start /= Scale; target /= Scale; velocity /= Scale;
        float radius = hostRadius / Scale, height = hostHeight / Scale;
        Vec3 position = start, remaining = target - start; bool collided = false, grounded = false;
        // Resolve an initial overlap, including an external respawn/teleport.
        for (int pass = 0; pass < 8; pass++)
        {
            Candidates(position, position, radius, height); bool moved = false;
            foreach (int index in candidates)
            {
                float distance = CapsuleDistance(triangles[index], position, radius, height, out Vec3 n);
                if (distance < radius - 1e-5f) { position += n * (radius - distance + Skin); moved = collided = true; }
            }
            if (!moved) break;
        }
        for (int iteration = 0; iteration < 8 && remaining.LengthSquared > 1e-12f; iteration++)
        {
            Candidates(position, position + remaining, radius, height);
            float nearest = 1; Vec3 normal = Vec3.Zero; bool hit = false;
            foreach (int index in candidates)
            {
                float time = 0;
                for (int step = 0; step < 48 && time <= nearest; step++)
                {
                    float distance = CapsuleDistance(triangles[index], position + remaining * time, radius, height, out Vec3 n);
                    float closing = -Vec3.Dot(remaining, n);
                    if (closing <= 1e-7f) break;
                    float gap = distance - radius;
                    if (gap <= Skin + 1e-5f) { nearest = time; normal = n; hit = true; break; }
                    time += Math.Max(1e-7f, (gap - Skin) / closing);
                }
            }
            if (!hit) { position += remaining; break; }
            collided = true; grounded |= normal.Y > 0.5f;
            position += remaining * nearest; remaining *= 1 - nearest;
            float into = Vec3.Dot(remaining, normal); if (into < 0) remaining -= normal * into;
            into = Vec3.Dot(velocity, normal);
            if (into < 0)
            {
                velocity -= normal * into;
                float tangent = velocity.Length;
                if (tangent > 0) { float factor = Math.Max(0, 1 - Friction * -into / tangent); velocity *= factor; remaining *= factor; }
            }
        }
        return new CollisionResult { Position = position * Scale, Velocity = velocity * Scale, Collided = collided, Grounded = grounded };
    }
}
