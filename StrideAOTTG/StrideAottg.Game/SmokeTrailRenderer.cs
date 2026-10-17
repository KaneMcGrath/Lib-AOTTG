// SPDX-License-Identifier: MIT
using LibAottg;
using LibAottg.Assets;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Buffer = Stride.Graphics.Buffer;

namespace StrideAottg;

/// <summary>Draws the optional portable smoke as world-space, alpha-blended billboards.</summary>
internal sealed class SmokeTrailRenderer : IDisposable
{
    public HeroSmokeTrail Trail { get; }
    public int MaxObservedParticles { get; private set; }
    private readonly Entity entity;
    private readonly Texture texture;
    private readonly Buffer<VertexPositionColorTexture> vertexBuffer;
    private readonly Buffer<uint> indexBuffer;
    private readonly VertexPositionColorTexture[] vertices;
    private readonly Mesh mesh;
    private readonly Model model;
    private readonly ModelComponent component;
    private readonly List<HeroSmokeParticle> sorted = new();

    public SmokeTrailRenderer(GraphicsDevice device, Scene scene, SmokePackage package)
    {
        Trail = new HeroSmokeTrail(package.Settings);
        using var stream = new MemoryStream(package.TexturePng, false);
        texture = Texture.Load(device, stream, loadAsSRGB: true);
        IComputeColor color = new ComputeBinaryColor {
            LeftChild = new ComputeTextureColor(texture) { AddressModeU = TextureAddressMode.Clamp, AddressModeV = TextureAddressMode.Clamp },
            RightChild = new ComputeVertexStreamColor(), Operator = BinaryOperator.Multiply };
        var material = Material.New(device, new MaterialDescriptor { Attributes = new MaterialAttributes {
            Diffuse = new MaterialDiffuseMapFeature(color), DiffuseModel = null,
            Emissive = new MaterialEmissiveMapFeature(color) { UseAlpha = true },
            Transparency = new MaterialTransparencyBlendFeature { DitheredShadows = false }, CullMode = CullMode.None } });
        // The original shader outputs straight RGB/alpha. Stride's blend feature defaults
        // to premultiplied blending, which exposes RGB in the PNG's transparent border.
        foreach (var pass in material.Passes) pass.BlendState = BlendStates.NonPremultiplied;
        vertices = new VertexPositionColorTexture[package.Settings.MaxParticles * 4];
        var indices = new uint[package.Settings.MaxParticles * 6];
        for (uint n = 0; n < package.Settings.MaxParticles; n++)
        { int i = (int)n * 6; uint v = n * 4; indices[i] = v; indices[i + 1] = v + 1; indices[i + 2] = v + 2; indices[i + 3] = v; indices[i + 4] = v + 2; indices[i + 5] = v + 3; }
        vertexBuffer = Buffer.Vertex.New(device, vertices, GraphicsResourceUsage.Dynamic);
        indexBuffer = Buffer.Index.New(device, indices);
        mesh = new Mesh { Draw = new MeshDraw { PrimitiveType = PrimitiveType.TriangleList, DrawCount = 0,
            VertexBuffers = [new VertexBufferBinding(vertexBuffer, VertexPositionColorTexture.Layout, vertices.Length)],
            IndexBuffer = new IndexBufferBinding(indexBuffer, true, indices.Length) } };
        model = new Model(); model.Meshes.Add(mesh); model.Materials.Add(new MaterialInstance(material));
        entity = new Entity("Original HERO smoke trail");
        component = new ModelComponent(model) { IsShadowCaster = false, Enabled = false };
        entity.Add(component); scene.Entities.Add(entity);
    }
    public void Reset() { Trail.Reset(); component.Enabled = false; mesh.Draw.DrawCount = 0; }
    public void Hide() { component.Enabled = false; }
    public void Advance(float dt, CharacterMotor motor, HeroPose pose, HeroCameraPose camera, CommandList commands)
    {
        Trail.Advance(dt, motor, pose);
        MaxObservedParticles = Math.Max(MaxObservedParticles, Trail.Particles.Count);
        component.Enabled = Trail.Particles.Count > 0;
        mesh.Draw.DrawCount = Trail.Particles.Count * 6;
        if (Trail.Particles.Count == 0) return;
        sorted.Clear(); sorted.AddRange(Trail.Particles);
        sorted.Sort((a, b) => Vec3.Dot(b.Position - camera.Position, camera.Forward).CompareTo(Vec3.Dot(a.Position - camera.Position, camera.Forward)));
        Vec3 right = Vec3.Cross(camera.Forward, camera.Up).Normalized, up = camera.Up;
        var bounds = new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue));
        for (int n = 0; n < sorted.Count; n++)
        {
            var p = sorted[n];
            float c = MathF.Cos(p.Rotation), s = MathF.Sin(p.Rotation);
            Vec3 r = (right * c + up * s) * (p.Size * 0.5f), u = (up * c - right * s) * (p.Size * 0.5f);
            var tint = new Color(p.Color.X, p.Color.Y, p.Color.Z, p.Alpha);
            var corners = new[] { p.Position - r - u, p.Position + r - u, p.Position + r + u, p.Position - r + u };
            for (int k = 0; k < 4; k++)
            {
                var position = StrideWorld.ToStride(corners[k]);
                vertices[n * 4 + k] = new VertexPositionColorTexture(position, tint, new Vector2(k is 1 or 2 ? 1 : 0, k < 2 ? 1 : 0));
                bounds.Minimum = Vector3.Min(bounds.Minimum, position); bounds.Maximum = Vector3.Max(bounds.Maximum, position);
            }
        }
        vertexBuffer.SetData(commands, vertices);
        model.BoundingBox = mesh.BoundingBox = bounds;
        model.BoundingSphere = mesh.BoundingSphere = BoundingSphere.FromBox(bounds);
    }
    public void Dispose() { entity.Scene?.Entities.Remove(entity); vertexBuffer.Dispose(); indexBuffer.Dispose(); texture.Dispose(); }
}
