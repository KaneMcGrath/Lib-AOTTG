// SPDX-License-Identifier: MIT
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using SharpGLTF.Schema2;

namespace LibAottg.Assets;

/// <summary>A validated package shared by any number of independent animated instances.</summary>
public sealed class CharacterPackage
{
    internal ModelRoot Model { get; }
    internal int[] NodeOrder { get; }
    internal Dictionary<string, NodeCurveSamplers[]> Samplers { get; } = new(StringComparer.Ordinal);
    public string DirectoryPath { get; }
    public string AssetSha256 { get; }
    public float MetersPerSourceUnit { get; }
    public bool ScaleCalibrated { get; }
    public IReadOnlyList<PackagePrimitive> Primitives { get; }
    public IReadOnlyList<PackageMaterial> Materials { get; }
    public IReadOnlyList<PackageClip> Clips { get; }
    public IReadOnlyList<PackageSocket> Sockets { get; }
    public PackageCapsule Capsule { get; }

    private CharacterPackage(string directory)
    {
        DirectoryPath = Path.GetFullPath(directory);
        using var manifest = ReadSidecar("manifest.json", "libaottg.character-package");
        var root = manifest.RootElement;
        if (!root.GetProperty("complete").GetBoolean()) throw new InvalidDataException("Character package is incomplete.");
        var asset = root.GetProperty("asset").GetString() ?? throw new InvalidDataException("Missing asset path.");
        string assetPath = Path.GetFullPath(Path.Combine(DirectoryPath, asset));
        if (!assetPath.StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Asset must be inside the package directory.");
        using (var file = File.OpenRead(assetPath))
        using (var hash = SHA256.Create()) AssetSha256 = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        if (!string.Equals(AssetSha256, root.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Character GLB hash does not match its manifest.");
        MetersPerSourceUnit = root.GetProperty("metersPerSourceUnit").GetSingle();
        if (!RuntimeMath.Finite(MetersPerSourceUnit) || MetersPerSourceUnit <= 0) throw new InvalidDataException("Invalid package scale.");
        ScaleCalibrated = root.GetProperty("scaleCalibrated").GetBoolean();
        Model = ModelRoot.Load(assetPath);
        if (Model.LogicalMeshes.Count != root.GetProperty("meshCount").GetInt32() ||
            Model.LogicalAnimations.Count != root.GetProperty("clipCount").GetInt32())
            throw new InvalidDataException("Package inventory does not match the GLB.");
        // Explicit parent-before-child traversal; logical indices need not follow hierarchy order.
        NodeOrder = Node.Flatten(Model.DefaultScene).Select(n => n.LogicalIndex).ToArray();
        if (NodeOrder.Distinct().Count() != NodeOrder.Length) throw new InvalidDataException("Repeated scene node.");

        Materials = Model.LogicalMaterials.Select(m =>
        {
            var channel = m.FindChannel("BaseColor");
            var color = channel?.Parameters.FirstOrDefault(p => p.Value is Vector4)?.Value is Vector4 factor ? factor : Vector4.One;
            return new PackageMaterial(m.Name ?? "Material", color,
                channel?.Texture?.PrimaryImage.Content.Content.ToArray(), m.Alpha.ToString(), m.AlphaCutoff,
                m.DoubleSided, channel?.Texture?.Sampler?.WrapS.ToString() ?? "REPEAT",
                channel?.Texture?.Sampler?.WrapT.ToString() ?? "REPEAT", m.Unlit,
                channel?.Texture?.Sampler?.MagFilter.ToString() ?? "LINEAR");
        }).ToArray();
        var primitives = new List<PackagePrimitive>();
        foreach (var node in Node.Flatten(Model.DefaultScene).Where(n => n.Mesh != null))
        foreach (var p in node.Mesh.Primitives)
        {
            if (p.DrawPrimitiveType != PrimitiveType.TRIANGLES || p.MorphTargetsCount != 0)
                throw new NotSupportedException("Character runtime supports triangle meshes without morph targets.");
            var positions = p.GetVertexAccessor("POSITION").AsVector3Array().ToArray();
            var normals = p.GetVertexAccessor("NORMAL")?.AsVector3Array().ToArray() ?? Enumerable.Repeat(Vector3.UnitY, positions.Length).ToArray();
            var uv = p.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array().ToArray() ?? new Vector2[positions.Length];
            var indices = p.GetIndices()?.ToArray() ?? Enumerable.Range(0, positions.Length).Select(i => (uint)i).ToArray();
            var joints = p.GetVertexAccessor("JOINTS_0")?.AsVector4Array().ToArray();
            var weights = p.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array().ToArray();
            int[] jointNodes = []; Matrix4x4[] binds = [];
            if (node.Skin != null)
            {
                jointNodes = new int[node.Skin.JointsCount]; binds = new Matrix4x4[jointNodes.Length];
                for (int j = 0; j < jointNodes.Length; j++)
                {
                    var joint = node.Skin.GetJoint(j);
                    jointNodes[j] = joint.Joint.LogicalIndex; binds[j] = joint.InverseBindMatrix;
                }
                if (joints == null || weights == null) throw new InvalidDataException("Skinned primitive has no joint weights.");
            }
            primitives.Add(new PackagePrimitive(node.Name ?? node.Mesh.Name ?? "Mesh", node.LogicalIndex,
                p.Material?.LogicalIndex ?? -1, positions, normals, uv, indices, joints, weights, jointNodes, binds));
        }
        Primitives = primitives;
        using var rules = ReadSidecar("animation-rules.json", "libaottg.animation-rules");
        var metadata = rules.RootElement.GetProperty("clips").EnumerateArray().ToDictionary(c => c.GetProperty("name").GetString()!);
        Clips = Model.LogicalAnimations.Select(a =>
        {
            if (!metadata.TryGetValue(a.Name, out var m)) throw new InvalidDataException($"Missing animation metadata: {a.Name}");
            Samplers.Add(a.Name, Model.LogicalNodes.Select(n => n.GetCurveSamplers(a)).ToArray());
            float speed = m.GetProperty("capturedPlaybackSpeed").GetSingle();
            if (!RuntimeMath.Finite(speed) || speed < 0) throw new InvalidDataException($"Unsupported animation speed: {a.Name}");
            return new PackageClip(a.Name, a.Duration, speed);
        }).ToArray();
        using var sockets = ReadSidecar("sockets.json", "libaottg.sockets");
        Sockets = sockets.RootElement.GetProperty("sockets").EnumerateArray().Select(s =>
            new PackageSocket(s.GetProperty("name").GetString()!, s.GetProperty("path").GetString()!, s.GetProperty("node").GetInt32())).ToArray();
        if (Sockets.Any(s => s.Node < 0 || s.Node >= Model.LogicalNodes.Count)) throw new InvalidDataException("Invalid socket node.");
        if (Sockets.Select(s => s.Path).Distinct(StringComparer.Ordinal).Count() != Sockets.Count)
            throw new InvalidDataException("Socket paths must be unique.");
        using var physics = ReadSidecar("physics.json", "libaottg.physics");
        var capsule = physics.RootElement.GetProperty("colliders").EnumerateArray().First(c =>
            c.GetProperty("type").GetString() == "CapsuleCollider" && c.GetProperty("enabled").GetBoolean() && !c.GetProperty("isTrigger").GetBoolean());
        if (capsule.GetProperty("direction").GetInt32() != 1) throw new NotSupportedException("Only vertical character capsules are supported.");
        int capsuleNode = capsule.GetProperty("node").GetInt32();
        if (!Matrix4x4.Decompose(Model.LogicalNodes[capsuleNode].WorldMatrix, out var scale, out _, out _))
            throw new InvalidDataException("Cannot determine collider scale.");
        var center = capsule.GetProperty("center").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        Capsule = new PackageCapsule(Vector3.Transform(new Vector3(center[0], center[1], center[2]), Model.LogicalNodes[capsuleNode].WorldMatrix),
            capsule.GetProperty("radius").GetSingle() * Math.Max(Math.Abs(scale.X), Math.Abs(scale.Z)),
            capsule.GetProperty("height").GetSingle() * Math.Abs(scale.Y));
        if (!RuntimeMath.Finite(Capsule.Radius) || !RuntimeMath.Finite(Capsule.Height) || Capsule.Radius <= 0 || Capsule.Height < 2 * Capsule.Radius ||
            !RuntimeMath.Finite(Capsule.Center.X) || !RuntimeMath.Finite(Capsule.Center.Y) || !RuntimeMath.Finite(Capsule.Center.Z))
            throw new InvalidDataException("Invalid package capsule.");
    }

    private JsonDocument ReadSidecar(string name, string schema)
    {
        var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(DirectoryPath, name)));
        if (doc.RootElement.GetProperty("schema").GetString() != schema || doc.RootElement.GetProperty("version").GetInt32() != 1)
        { doc.Dispose(); throw new InvalidDataException($"Unsupported {name} schema/version."); }
        return doc;
    }
    public static CharacterPackage Load(string directory) => new(directory);
    public CharacterInstance CreateInstance() => new(this);
}

public sealed record PackageClip(string Name, float Duration, float CapturedSpeed);
public sealed record PackageSocket(string Name, string Path, int Node);
public sealed record PackageCapsule(Vector3 Center, float Radius, float Height);
public sealed record PackageMaterial(string Name, Vector4 BaseColor, byte[]? Png, string AlphaMode, float AlphaCutoff,
    bool DoubleSided, string WrapS, string WrapT, bool Unlit, string MagnificationFilter);
public sealed record PackagePrimitive(string Name, int Node, int Material, Vector3[] Positions, Vector3[] Normals,
    Vector2[] TexCoords, uint[] Indices, Vector4[]? Joints, Vector4[]? Weights, int[] JointNodes, Matrix4x4[] InverseBinds);
