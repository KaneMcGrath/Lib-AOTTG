// SPDX-License-Identifier: MIT
using System.Numerics;
using SharpGLTF.Transforms;

namespace LibAottg.Assets;

/// <summary>Scoped post-animation bone edits in package coordinates. Each edit propagates to children/sockets.</summary>
public sealed class CharacterPoseEditor
{
    private readonly CharacterPackage package;
    private readonly AffineTransform[] locals;
    private readonly Matrix4x4[] worlds;
    internal CharacterPoseEditor(CharacterPackage package, AffineTransform[] locals, Matrix4x4[] worlds)
    { this.package = package; this.locals = locals; this.worlds = worlds; }
    public Matrix4x4 World(int node) => worlds[node];
    public Quaternion WorldRotation(int node)
    {
        if (!Matrix4x4.Decompose(worlds[node], out _, out var rotation, out _)) throw new InvalidDataException("Invalid bone matrix.");
        return rotation;
    }
    public void SetLocalRotation(int node, Quaternion rotation)
    {
        if (rotation.LengthSquared() < 1e-12f || !RuntimeMath.Finite(rotation.LengthSquared())) throw new ArgumentException("Invalid bone rotation.");
        var local = locals[node].GetDecomposed();
        locals[node] = new AffineTransform(local.Scale, Quaternion.Normalize(rotation), local.Translation);
        Rebuild();
    }
    public void SetWorldRotation(int node, Quaternion rotation)
    {
        var parent = package.Model.LogicalNodes[node].VisualParent;
        SetLocalRotation(node, parent == null ? rotation : Quaternion.Inverse(WorldRotation(parent.LogicalIndex)) * rotation);
    }
    internal void Rebuild()
    {
        foreach (int i in package.NodeOrder)
        {
            var parent = package.Model.LogicalNodes[i].VisualParent;
            worlds[i] = parent == null ? locals[i].Matrix : locals[i].Matrix * worlds[parent.LogicalIndex];
        }
    }
}
