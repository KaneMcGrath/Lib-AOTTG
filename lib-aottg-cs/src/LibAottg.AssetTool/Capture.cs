// SPDX-License-Identifier: MIT
using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace LibAottg.AssetTool;

internal static class JsonValues
{
    public static float[] Floats(this JsonElement value) => value.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    public static int[] Ints(this JsonElement value) => value.EnumerateArray().Select(x => x.GetInt32()).ToArray();
    public static string Text(this JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw new InvalidDataException("Null " + name);
    public static bool Flag(this JsonElement value, string name, bool fallback = true) => value.TryGetProperty(name, out var flag) ? flag.GetBoolean() : fallback;
}

internal sealed class Capture : IDisposable
{
    private readonly JsonDocument document;
    private readonly byte[] geometry;
    public string DirectoryPath { get; }
    public JsonElement Manifest => document.RootElement;
    public JsonElement[] Nodes { get; }
    public JsonElement[] Meshes { get; }
    public JsonElement[] Clips { get; }
    public Matrix4x4[] Globals { get; }

    public Capture(string path)
    {
        DirectoryPath = Path.GetFullPath(path);
        document = JsonDocument.Parse(File.ReadAllBytes(FilePath("manifest.json")));
        var m = Manifest;
        Require(m.Text("schema") == "libaottg.source-capture" && m.GetProperty("version").GetInt32() == 1, "Unsupported capture schema/version.");
        Require(m.Flag("complete", false), "Capture is incomplete.");
        Require(m.Text("binary") == "little-endian; float32 and int32; matrix column-major", "Unsupported binary layout.");
        geometry = File.ReadAllBytes(FilePath(m.Text("geometryFile")));
        Nodes = m.GetProperty("nodes").EnumerateArray().ToArray();
        Meshes = m.GetProperty("meshes").EnumerateArray().ToArray();
        Clips = m.GetProperty("clips").EnumerateArray().ToArray();
        Require(Nodes.Length > 0, "Capture has no nodes.");
        Require(Clips.Length == m.GetProperty("expectedClipCount").GetInt32(), "Clip inventory is incomplete.");
        Require(Clips.Sum(x => (long)x.GetProperty("frameCount").GetInt32()) == m.GetProperty("sampleCount").GetInt64(), "Sample count mismatch.");
        Globals = new Matrix4x4[Nodes.Length];
        for (int i = 0; i < Nodes.Length; i++)
        {
            var node = Nodes[i];
            int parent = node.GetProperty("parent").GetInt32();
            Require(node.GetProperty("id").GetInt32() == i && parent >= -1 && parent < i, "Node IDs/parents must be in hierarchy order.");
            var local = CoordinateMath.Trs(node.GetProperty("position").Floats(), node.GetProperty("rotation").Floats(), node.GetProperty("scale").Floats());
            Globals[i] = parent < 0 ? local : local * Globals[parent];
        }
    }

    public string FilePath(string relative)
    {
        Require(!Path.IsPathRooted(relative), "Capture paths must be relative.");
        string resolved = Path.GetFullPath(Path.Combine(DirectoryPath, relative));
        Require(resolved.StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Capture path escapes its directory.");
        return resolved;
    }

    private ReadOnlySpan<byte> Buffer(JsonElement descriptor, int components, string type)
    {
        int count = descriptor.GetProperty("count").GetInt32();
        int offset = descriptor.GetProperty("byteOffset").GetInt32();
        int length = descriptor.GetProperty("byteLength").GetInt32();
        Require(count >= 0 && descriptor.GetProperty("components").GetInt32() == components && descriptor.Text("componentType") == type, "Invalid buffer descriptor.");
        Require(offset >= 0 && offset % 4 == 0 && length == checked(count * components * 4) && (long)offset + length <= geometry.Length, "Invalid geometry buffer range.");
        return geometry.AsSpan(offset, length);
    }

    public float[] ReadFloats(JsonElement descriptor, int components)
    {
        var bytes = Buffer(descriptor, components, "float32");
        var values = new float[bytes.Length / 4];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(i * 4, 4));
            Require(float.IsFinite(values[i]), "Geometry contains a non-finite value.");
        }
        return values;
    }

    public int[] ReadInts(JsonElement descriptor, int components)
    {
        var bytes = Buffer(descriptor, components, "int32");
        var values = new int[bytes.Length / 4];
        for (int i = 0; i < values.Length; i++) values[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i * 4, 4));
        return values;
    }

    public float[] ReadAnimation(JsonElement clip)
    {
        Require(clip.GetProperty("nodeCount").GetInt32() == Nodes.Length, "Animation node count mismatch.");
        int frames = clip.GetProperty("frameCount").GetInt32();
        Require(frames > 0, "Empty animation.");
        byte[] bytes = File.ReadAllBytes(FilePath(clip.Text("file")));
        Require(bytes.Length == checked(frames * (1 + Nodes.Length * 10) * 4), "Animation byte length mismatch: " + clip.Text("name"));
        float[] samples = new float[bytes.Length / 4];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * 4, 4));
            Require(float.IsFinite(samples[i]), "Animation contains a non-finite sample.");
        }
        int stride = 1 + Nodes.Length * 10;
        float duration = clip.GetProperty("duration").GetSingle();
        Require(duration >= 0 && float.IsFinite(duration) && samples[0] == 0 && Math.Abs(samples[(frames - 1) * stride] - duration) < 1e-5f, "Invalid animation endpoint.");
        for (int i = 1; i < frames; i++) Require(samples[i * stride] > samples[(i - 1) * stride], "Animation times must increase.");
        return samples;
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    public void Dispose() => document.Dispose();
}
