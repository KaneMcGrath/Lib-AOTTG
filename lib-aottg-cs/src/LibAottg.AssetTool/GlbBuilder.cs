// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Json;

namespace LibAottg.AssetTool;

internal sealed class GlbBuilder : IDisposable
{
    private readonly MemoryStream binary = new();
    private readonly BinaryWriter writer;
    public List<Dictionary<string, object>> Views { get; } = [];
    public List<Dictionary<string, object>> Accessors { get; } = [];
    public GlbBuilder() => writer = new(binary, Encoding.UTF8, true);

    private void Align() { while (binary.Position % 4 != 0) writer.Write((byte)0); }
    public int View(byte[] bytes, int? target = null, int? stride = null)
    {
        Align();
        var view = new Dictionary<string, object> { ["buffer"] = 0, ["byteOffset"] = binary.Position, ["byteLength"] = bytes.Length };
        if (target.HasValue) view["target"] = target.Value;
        if (stride.HasValue) view["byteStride"] = stride.Value;
        int id = Views.Count;
        Views.Add(view);
        writer.Write(bytes);
        return id;
    }

    public int Floats(float[] values, int components, string type, int? target = null, bool bounds = false)
    {
        Capture.Require(values.Length > 0 && values.Length % components == 0 && values.All(float.IsFinite), "Invalid output float accessor.");
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Encoding.UTF8, true)) foreach (float value in values) w.Write(value);
        var accessor = new Dictionary<string, object> { ["bufferView"] = View(stream.ToArray(), target), ["componentType"] = 5126, ["count"] = values.Length / components, ["type"] = type };
        if (bounds)
        {
            float[] min = new float[components], max = new float[components];
            for (int c = 0; c < components; c++)
            {
                min[c] = float.PositiveInfinity; max[c] = float.NegativeInfinity;
                for (int i = c; i < values.Length; i += components) { min[c] = Math.Min(min[c], values[i]); max[c] = Math.Max(max[c], values[i]); }
            }
            accessor["min"] = min; accessor["max"] = max;
        }
        Accessors.Add(accessor);
        return Accessors.Count - 1;
    }

    public int Integers(int[] values, int components, bool joints)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            foreach (int value in values)
            {
                Capture.Require(value >= 0 && (!joints || value <= ushort.MaxValue), "Invalid output integer accessor.");
                if (joints) w.Write((ushort)value); else w.Write((uint)value);
            }
        Accessors.Add(new() { ["bufferView"] = View(stream.ToArray(), joints ? 34962 : 34963), ["componentType"] = joints ? 5123 : 5125, ["count"] = values.Length / components, ["type"] = joints ? "VEC4" : "SCALAR" });
        return Accessors.Count - 1;
    }

    public byte[] Build(Dictionary<string, object> json)
    {
        int byteLength = checked((int)binary.Length);
        json["buffers"] = new[] { new { byteLength } };
        json["bufferViews"] = Views;
        json["accessors"] = Accessors;
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(json);
        int jsonLength = (metadata.Length + 3) & ~3;
        Align();
        using var output = new MemoryStream();
        using var w = new BinaryWriter(output);
        w.Write(0x46546C67); w.Write(2); w.Write(checked(12 + 8 + jsonLength + 8 + (int)binary.Length));
        w.Write(jsonLength); w.Write(0x4E4F534A); w.Write(metadata);
        for (int i = metadata.Length; i < jsonLength; i++) w.Write((byte)' ');
        w.Write((int)binary.Length); w.Write(0x004E4942); w.Write(binary.ToArray());
        return output.ToArray();
    }

    public void Dispose() { writer.Dispose(); binary.Dispose(); }
}
