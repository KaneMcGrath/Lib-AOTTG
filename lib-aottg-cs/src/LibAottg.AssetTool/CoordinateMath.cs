// SPDX-License-Identifier: MIT
using System.Numerics;

namespace LibAottg.AssetTool;

// System.Numerics uses row vectors. Its row-major element sequence is exactly
// the column-major sequence of the equivalent glTF/Unity column-vector matrix.
public static class CoordinateMath
{
    public static Matrix4x4 Matrix(float[] a)
    {
        Capture.Require(a.Length == 16 && a.All(float.IsFinite), "Invalid matrix.");
        return new(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9], a[10], a[11], a[12], a[13], a[14], a[15]);
    }

    public static float[] Elements(Matrix4x4 m) => [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
    public static Matrix4x4 Inverse(Matrix4x4 m)
    {
        Capture.Require(Matrix4x4.Invert(m, out var inverse) && Elements(inverse).All(float.IsFinite), "Singular transform/bind matrix.");
        return inverse;
    }

    public static Quaternion Rotation(float[] q)
    {
        Capture.Require(q.Length == 4 && q.All(float.IsFinite), "Invalid quaternion.");
        var rotation = new Quaternion(q[0], q[1], q[2], q[3]);
        Capture.Require(rotation.LengthSquared() > 1e-12f, "Zero quaternion.");
        return Quaternion.Normalize(rotation);
    }

    public static Matrix4x4 Trs(float[] p, float[] q, float[] s)
    {
        Capture.Require(p.Length == 3 && s.Length == 3 && p.All(float.IsFinite) && s.All(float.IsFinite) && s.All(x => Math.Abs(x) > 1e-12f), "Invalid TRS.");
        return Matrix4x4.CreateScale(s[0], s[1], s[2]) * Matrix4x4.CreateFromQuaternion(Rotation(q)) * Matrix4x4.CreateTranslation(p[0], p[1], p[2]);
    }

    public static Matrix4x4 ConvertMatrix(Matrix4x4 source, float metersPerUnit)
    {
        var c = Matrix4x4.CreateScale(metersPerUnit, metersPerUnit, -metersPerUnit);
        var converted = Inverse(c) * source * c;
        // Inverting affine float32 matrices introduces tiny noise in the last
        // row. glTF inverse bind matrices require these values to be exact.
        Capture.Require(Math.Abs(converted.M14) < 1e-5f && Math.Abs(converted.M24) < 1e-5f && Math.Abs(converted.M34) < 1e-5f && Math.Abs(converted.M44 - 1) < 1e-5f, "Non-affine conversion matrix.");
        converted.M14 = converted.M24 = converted.M34 = 0;
        converted.M44 = 1;
        return converted;
    }

    public static float[] Position(float[] p, float unit) => [p[0] * unit, p[1] * unit, -p[2] * unit];
    public static float[] ConvertRotation(float[] q)
    {
        var r = Rotation(q);
        return [-r.X, -r.Y, r.Z, r.W];
    }

    public static (float[] Position, float[] Rotation, float[] Scale) Decompose(Matrix4x4 m)
    {
        Capture.Require(Matrix4x4.Decompose(m, out var s, out var q, out var p), "Rest transform cannot be decomposed.");
        q = Quaternion.Normalize(q);
        float[] position = [p.X, p.Y, p.Z], rotation = [q.X, q.Y, q.Z, q.W], scale = [s.X, s.Y, s.Z];
        Capture.Require(Error(m, Trs(position, rotation, scale)) < 2e-4f, "Rest transform contains unsupported shear.");
        return (position, rotation, scale);
    }

    public static float Error(Matrix4x4 a, Matrix4x4 b)
    {
        float[] av = Elements(a), bv = Elements(b);
        return av.Zip(bv, (x, y) => Math.Abs(x - y)).Max() / Math.Max(1, bv.Select(Math.Abs).Max());
    }
}
