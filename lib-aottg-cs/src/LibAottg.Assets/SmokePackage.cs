// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text.Json;

namespace LibAottg.Assets;

/// <summary>Optional extracted HERO smoke assets, independent of the character GLB.</summary>
public sealed class SmokePackage
{
    public HeroSmokeSettings Settings { get; }
    public byte[] TexturePng { get; }
    public string TextureSha256 { get; }

    private SmokePackage(string directory)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        var root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "libaottg.smoke" || root.GetProperty("version").GetInt32() != 1)
            throw new InvalidDataException("Unsupported smoke manifest.");
        string texture = root.GetProperty("texture").GetString() ?? throw new InvalidDataException("Missing smoke texture.");
        string folder = Path.GetFullPath(directory), path = Path.GetFullPath(Path.Combine(folder, texture));
        if (!path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Texture must be inside the smoke package.");
        TexturePng = File.ReadAllBytes(path);
        using (var sha = SHA256.Create()) TextureSha256 = BitConverter.ToString(sha.ComputeHash(TexturePng)).Replace("-", "").ToLowerInvariant();
        if (TextureSha256 != root.GetProperty("textureSha256").GetString()) throw new InvalidDataException("Smoke texture hash mismatch.");
        var s = root.GetProperty("settings");
        float[] Values(string name) => s.GetProperty(name).EnumerateArray().Select(x => x.GetSingle()).ToArray();
        var life = Values("lifetime"); var speed = Values("speed"); var size = Values("size"); var rotation = Values("rotation");
        var position = Values("localPosition"); var q = Values("localRotation"); var color = Values("color");
        // Quaternion rotation of the emitter's orthogonal local axes.
        var quaternion = new System.Numerics.Quaternion(q[0], q[1], q[2], q[3]);
        Vec3 Rotate(System.Numerics.Vector3 axis)
        { var v = System.Numerics.Vector3.Transform(axis, quaternion); return new Vec3(v.X, v.Y, v.Z); }
        Settings = new HeroSmokeSettings {
            Rate = s.GetProperty("rate").GetSingle(), LifetimeMin = life[0], LifetimeMax = life[1],
            SpeedMin = speed[0], SpeedMax = speed[1], SizeMin = size[0], SizeMax = size[1],
            RotationMin = rotation[0], RotationMax = rotation[1],
            LocalPosition = new Vec3(position[0], position[1], position[2]),
            LocalDirection = Rotate(System.Numerics.Vector3.UnitZ), LocalRight = Rotate(System.Numerics.Vector3.UnitX), LocalUp = Rotate(System.Numerics.Vector3.UnitY),
            Radius = s.GetProperty("radius").GetSingle(), ConeAngle = s.GetProperty("coneAngle").GetSingle(),
            Color = new Vec3(color[0], color[1], color[2]), Alpha = color[3], MaxParticles = s.GetProperty("maxParticles").GetInt32(),
            SizeCurve = s.GetProperty("sizeCurve").EnumerateArray().Select(k => new SmokeCurveKey(k.GetProperty("time").GetSingle(),
                k.GetProperty("value").GetSingle(), k.GetProperty("inSlope").GetSingle(), k.GetProperty("outSlope").GetSingle())).ToArray() };
        Settings.Validate();
    }
    public static SmokePackage Load(string directory) => new(directory);
}
