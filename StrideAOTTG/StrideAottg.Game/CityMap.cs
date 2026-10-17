// SPDX-License-Identifier: MIT
using System.Text.Json;
using LibAottg;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Buffer = Stride.Graphics.Buffer;

namespace StrideAottg;

/// <summary>Original City scene assets. This importer belongs only to the reference demo.</summary>
public sealed class CityMap : IDisposable
{
    public CityCollision Collision { get; }
    public Vec3[] Spawns { get; }
    public int MeshCount { get; }
    public int TextureCount => textures.Count;
    private readonly List<IDisposable> resources = new();
    private readonly Dictionary<string, Texture> textures = new();

    public CityMap(GraphicsDevice device, Scene scene, string directory, float scale)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        var root = manifest.RootElement;
        if (root.GetProperty("schema").GetString() != "strideaottg.city") throw new InvalidDataException("Not a City package.");
        static Vec3 V(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());
        Spawns = root.GetProperty("spawns").EnumerateArray().Select(p => V(p) * scale + Vec3.Up * (0.01f * scale)).ToArray();
        Collision = new CityCollision(Path.Combine(directory, "collision.bin"), scale);
        var model = new Model();
        foreach (var row in root.GetProperty("materials").EnumerateArray())
        {
            var c = row.GetProperty("color");
            IComputeColor color = new ComputeColor(new Color4(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle(), c[3].GetSingle()));
            string textureName = row.GetProperty("texture").GetString();
            if (textureName != null)
            {
                if (!textures.TryGetValue(textureName, out var texture))
                {
                    using var stream = File.OpenRead(Path.Combine(directory, textureName));
                    texture = Texture.Load(device, stream, loadAsSRGB: true);
                    textures.Add(textureName, texture); resources.Add(texture);
                }
                var map = new ComputeTextureColor(texture) { AddressModeU = TextureAddressMode.Wrap, AddressModeV = TextureAddressMode.Wrap };
                color = new ComputeBinaryColor { LeftChild = map, RightChild = color, Operator = BinaryOperator.Multiply };
            }
            var attributes = new MaterialAttributes { Diffuse = new MaterialDiffuseMapFeature(color),
                DiffuseModel = new MaterialDiffuseLambertModelFeature(), CullMode = row.GetProperty("doubleSided").GetBoolean() ? CullMode.None : CullMode.Back };
            if (row.GetProperty("alphaTest").GetBoolean())
                attributes.Transparency = new MaterialTransparencyCutoffFeature { Alpha = new ComputeFloat(row.GetProperty("cutoff").GetSingle()) };
            model.Materials.Add(new MaterialInstance(Material.New(device, new MaterialDescriptor { Attributes = attributes })));
        }
        using var reader = new BinaryReader(File.OpenRead(Path.Combine(directory, "render.bin")));
        if (reader.ReadUInt32() != 0x31595443) throw new InvalidDataException("City geometry header.");
        MeshCount = reader.ReadInt32();
        for (int m = 0; m < MeshCount; m++)
        {
            int material = reader.ReadInt32(), count = reader.ReadInt32(), indexCount = reader.ReadInt32();
            var vertices = new VertexPositionNormalTexture[count];
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            for (int v = 0; v < count; v++)
            {
                var p = StrideWorld.ToStride(new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()) * scale);
                var n = StrideWorld.ToStride(new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                vertices[v] = new VertexPositionNormalTexture(p, n, new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            var indices = new uint[indexCount];
            for (int i = 0; i < indexCount; i++) indices[i] = reader.ReadUInt32();
            // Reflecting Unity Z also reflects triangle winding into Stride's clockwise fronts.
            var vb = Buffer.Vertex.New(device, vertices); var ib = Buffer.Index.New(device, indices);
            resources.Add(vb); resources.Add(ib);
            var bounds = new BoundingBox(min, max);
            model.Meshes.Add(new Mesh { MaterialIndex = material, BoundingBox = bounds, BoundingSphere = BoundingSphere.FromBox(bounds),
                Draw = new MeshDraw { PrimitiveType = PrimitiveType.TriangleList, DrawCount = indexCount,
                    VertexBuffers = [new VertexBufferBinding(vb, VertexPositionNormalTexture.Layout, count)],
                    IndexBuffer = new IndexBufferBinding(ib, true, indexCount) } });
        }
        // Derive reflected bounds from the actual uploaded vertices.
        Vector3 totalMin = new(float.MaxValue), totalMax = new(float.MinValue);
        foreach (var mesh in model.Meshes) { totalMin = Vector3.Min(totalMin, mesh.BoundingBox.Minimum); totalMax = Vector3.Max(totalMax, mesh.BoundingBox.Maximum); }
        model.BoundingBox = new BoundingBox(totalMin, totalMax); model.BoundingSphere = BoundingSphere.FromBox(model.BoundingBox);
        var entity = new Entity("The City - original map"); entity.Add(new ModelComponent(model)); scene.Entities.Add(entity);
    }
    public void Dispose() { foreach (var resource in resources) resource.Dispose(); resources.Clear(); }
}
