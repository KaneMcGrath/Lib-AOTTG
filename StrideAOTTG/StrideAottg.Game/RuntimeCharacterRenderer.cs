// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using LibAottg.Assets;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Buffer = Stride.Graphics.Buffer;

namespace StrideAottg;

/// <summary>Runtime GLB bridge. CPU deformation is intentional for this small reference character.</summary>
public sealed class RuntimeCharacterRenderer : IDisposable
{
    public CharacterInstance Character { get; }
    public HeroCharacter Hero { get; }
    public int TextureCount => textures.Count;
    public int UploadedFrames { get; private set; }
    private readonly GraphicsDevice device;
    private readonly Model model = new();
    private readonly Dictionary<string, Texture> textures = new();
    private readonly List<Buffer> resources = new();
    private readonly Buffer<VertexPositionNormalTexture>[] vertexBuffers;
    private readonly VertexPositionNormalTexture[][] vertices;

    public RuntimeCharacterRenderer(GraphicsDevice device, Entity entity, CharacterPackage package)
    {
        this.device = device;
        Hero = new HeroCharacter(package);
        Character = Hero.Character;
        Character.Sample("stand", 0);
        foreach (var material in package.Materials) model.Materials.Add(new MaterialInstance(CreateMaterial(material)));
        vertexBuffers = new Buffer<VertexPositionNormalTexture>[package.Primitives.Count];
        vertices = new VertexPositionNormalTexture[package.Primitives.Count][];
        for (int i = 0; i < package.Primitives.Count; i++)
        {
            var source = package.Primitives[i];
            vertices[i] = new VertexPositionNormalTexture[source.Positions.Length];
            FillVertices(i);
            vertexBuffers[i] = Buffer.Vertex.New(device, vertices[i], GraphicsResourceUsage.Dynamic);
            // glTF fronts are counterclockwise; Stride's default rasterizer uses clockwise fronts.
            var clockwise = (uint[])source.Indices.Clone();
            for (int triangle = 0; triangle < clockwise.Length; triangle += 3)
                (clockwise[triangle + 1], clockwise[triangle + 2]) = (clockwise[triangle + 2], clockwise[triangle + 1]);
            var indices = Buffer.Index.New(device, clockwise);
            resources.Add(vertexBuffers[i]); resources.Add(indices);
            var mesh = new Mesh
            {
                MaterialIndex = source.Material,
                Draw = new MeshDraw
                {
                    PrimitiveType = PrimitiveType.TriangleList,
                    DrawCount = source.Indices.Length,
                    VertexBuffers = [new VertexBufferBinding(vertexBuffers[i], VertexPositionNormalTexture.Layout, vertices[i].Length)],
                    IndexBuffer = new IndexBufferBinding(indices, true, source.Indices.Length)
                }
            };
            model.Meshes.Add(mesh);
        }
        SetBounds();
        entity.Add(new ModelComponent(model));
    }

    private Material CreateMaterial(PackageMaterial source)
    {
        IComputeColor color = new ComputeColor(new Color4(source.BaseColor.X, source.BaseColor.Y, source.BaseColor.Z, source.BaseColor.W));
        if (source.Png != null)
        {
            string key = Convert.ToHexString(SHA256.HashData(source.Png));
            if (!textures.TryGetValue(key, out var texture))
            {
                using var stream = new MemoryStream(source.Png, writable: false);
                // glTF base-color PNGs encode sRGB, while the Stride scene shades in linear space.
                texture = Texture.Load(device, stream, loadAsSRGB: true);
                textures.Add(key, texture);
            }
            var map = new ComputeTextureColor(texture);
            map.AddressModeU = Wrap(source.WrapS);
            map.AddressModeV = Wrap(source.WrapT);
            map.Filtering = source.MagnificationFilter == "NEAREST" ? TextureFilter.Point : TextureFilter.Linear;
            color = new ComputeBinaryColor { LeftChild = map, RightChild = color, Operator = BinaryOperator.Multiply };
        }
        var attributes = new MaterialAttributes
        {
            Diffuse = new MaterialDiffuseMapFeature(color),
            DiffuseModel = new MaterialDiffuseLambertModelFeature(),
            CullMode = source.DoubleSided ? CullMode.None : CullMode.Back
        };
        if (source.Unlit)
        {
            attributes.DiffuseModel = null;
            attributes.Emissive = new MaterialEmissiveMapFeature(color) { UseAlpha = true };
        }
        if (source.AlphaMode == "MASK") attributes.Transparency = new MaterialTransparencyCutoffFeature { Alpha = new ComputeFloat(source.AlphaCutoff) };
        if (source.AlphaMode == "BLEND") attributes.Transparency = new MaterialTransparencyBlendFeature { DitheredShadows = false };
        return Material.New(device, new MaterialDescriptor { Attributes = attributes });
    }

    private static TextureAddressMode Wrap(string mode) => mode switch
    {
        "CLAMP_TO_EDGE" => TextureAddressMode.Clamp,
        "MIRRORED_REPEAT" => TextureAddressMode.Mirror,
        _ => TextureAddressMode.Wrap
    };

    private void FillVertices(int i)
    {
        var source = Character.Package.Primitives[i]; var pose = Character.Primitives[i];
        for (int v = 0; v < vertices[i].Length; v++)
            vertices[i][v] = new VertexPositionNormalTexture(ToStride(pose.Positions[v]), ToStride(pose.Normals[v]),
                new Vector2(source.TexCoords[v].X, source.TexCoords[v].Y));
    }
    private static Vector3 ToStride(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private void SetBounds()
    {
        var bounds = new BoundingBox(ToStride(Character.BoundsMin), ToStride(Character.BoundsMax));
        model.BoundingBox = bounds;
        model.BoundingSphere = BoundingSphere.FromBox(bounds);
        foreach (var mesh in model.Meshes) { mesh.BoundingBox = bounds; mesh.BoundingSphere = model.BoundingSphere; }
    }

    public void Upload(CommandList commands)
    {
        for (int i = 0; i < vertexBuffers.Length; i++)
        { FillVertices(i); vertexBuffers[i].SetData(commands, vertices[i]); }
        SetBounds(); UploadedFrames++;
    }

    public void ApplyEquipment()
    {
        for (int i = 0; i < model.Meshes.Count; i++)
            model.Meshes[i].Draw.DrawCount = Hero.PrimitiveVisible[i] ? Character.Package.Primitives[i].Indices.Length : 0;
    }

    public void ShowAllPrimitives()
    {
        for (int i = 0; i < model.Meshes.Count; i++) model.Meshes[i].Draw.DrawCount = Character.Package.Primitives[i].Indices.Length;
    }

    public void Dispose()
    {
        foreach (var buffer in resources) buffer.Dispose();
        foreach (var texture in textures.Values) texture.Dispose();
        resources.Clear(); textures.Clear();
    }
}
