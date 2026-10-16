// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    /// <summary>A small engine-independent, right-handed 3D vector. Y is up.</summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vec3 Zero { get { return new Vec3(); } }
        public static Vec3 Up { get { return new Vec3(0, 1, 0); } }
        public float LengthSquared { get { return X * X + Y * Y + Z * Z; } }
        public float Length { get { return (float)Math.Sqrt(LengthSquared); } }
        public Vec3 Normalized { get { float n = Length; return n > 0.00001f ? this / n : Zero; } }
        public static float Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static Vec3 Cross(Vec3 a, Vec3 b) { return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) { return a + (b - a) * Math3.Clamp(t, 0, 1); }
        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator -(Vec3 a) { return new Vec3(-a.X, -a.Y, -a.Z); }
        public static Vec3 operator *(Vec3 a, float n) { return new Vec3(a.X * n, a.Y * n, a.Z * n); }
        public static Vec3 operator *(float n, Vec3 a) { return a * n; }
        public static Vec3 operator /(Vec3 a, float n) { return new Vec3(a.X / n, a.Y / n, a.Z / n); }
        public bool Equals(Vec3 other) { return X == other.X && Y == other.Y && Z == other.Z; }
        public override bool Equals(object obj) { return obj is Vec3 && Equals((Vec3)obj); }
        public override int GetHashCode() { unchecked { return ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode(); } }
        public override string ToString() { return string.Format("({0}, {1}, {2})", X, Y, Z); }
    }

    internal static class Math3
    {
        public static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            return Math.Abs(target - current) <= maxDelta ? target : current + Math.Sign(target - current) * maxDelta;
        }
        public static Vec3 Horizontal(Vec3 v) { return new Vec3(v.X, 0, v.Z); }
        public static Vec3 Direction(float degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return new Vec3((float)Math.Sin(radians), 0, (float)Math.Cos(radians));
        }
    }
}
