// SPDX-License-Identifier: MIT
using System.Numerics;
using System.Text.Json;

namespace LibAottg.AssetTool;

internal sealed class BindSkeleton
{
    public Matrix4x4[] RestGlobals { get; }
    public Dictionary<int, Matrix4x4> MeshBindTransforms { get; } = [];
    public HashSet<int> Joints { get; } = [];
    public List<object> Checks { get; } = [];
    public int? AnchorNode { get; }
    public int? AnchorJoint { get; }

    public BindSkeleton(Capture capture)
    {
        RestGlobals = (Matrix4x4[])capture.Globals.Clone();
        var skins = capture.Meshes.Where(m => m.Flag("skinned", false)).ToArray();
        if (skins.Length == 0) return;
        // Bind space has a global-frame ambiguity. Preserve the captured highest
        // joint's global scale/frame, not a renderer transform: assembled costume
        // renderer transforms need not be the original mesh bind transforms.
        var anchor = skins.OrderByDescending(m => m.GetProperty("bones").GetArrayLength()).First();
        AnchorNode = anchor.GetProperty("node").GetInt32();
        var known = new Dictionary<int, Matrix4x4>();
        int[] anchorBones = Bones(capture, anchor);
        var anchorBinds = Matrices(capture, anchor, anchorBones.Length);
        int rootSlot = Enumerable.Range(0, anchorBones.Length).MinBy(i => Depth(anchorBones[i]));
        AnchorJoint = anchorBones[rootSlot];
        Matrix4x4 anchorMesh = anchorBinds[rootSlot] * capture.Globals[AnchorJoint.Value];
        foreach (var (bone, i) in anchorBones.Select((bone, i) => (bone, i))) known[bone] = CoordinateMath.Inverse(anchorBinds[i]) * anchorMesh;
        var pending = new List<JsonElement>(skins);
        while (pending.Count > 0)
        {
            bool progress = false;
            foreach (var mesh in pending.ToArray())
            {
                int node = mesh.GetProperty("node").GetInt32();
                int[] bones = Bones(capture, mesh);
                Matrix4x4[] binds = Matrices(capture, mesh, bones.Length);
                int shared = Array.FindIndex(bones, known.ContainsKey);
                if (shared < 0) continue;
                Matrix4x4 meshBind = binds[shared] * known[bones[shared]];
                float error = 0;
                for (int i = 0; i < bones.Length; i++)
                {
                    Matrix4x4 joint = CoordinateMath.Inverse(binds[i]) * meshBind;
                    if (known.TryGetValue(bones[i], out var expected)) error = Math.Max(error, CoordinateMath.Error(joint, expected));
                    else known[bones[i]] = joint;
                    Joints.Add(bones[i]);
                }
                Capture.Require(error < 2e-4f, $"Inconsistent shared bind skeleton for '{mesh.Text("name")}' (relative matrix error {error}).");
                Capture.Require(MeshBindTransforms.TryAdd(node, meshBind), "Multiple skinned renderers on one source node are unsupported.");
                Checks.Add(new { sourceNode = node, mesh = mesh.Text("name"), sharedJointRelativeError = error, bindTransform = CoordinateMath.Elements(meshBind), capturedTransformRelativeDifference = CoordinateMath.Error(meshBind, capture.Globals[node]) });
                pending.Remove(mesh); progress = true;
            }
            Capture.Require(progress, "Disconnected skin joint sets have no shared bind-frame anchor.");
        }
        // Keep captured local transforms on non-joints, while their joint ancestors
        // move to the reconstructed rest pose. Rigid attachments follow naturally.
        for (int i = 0; i < RestGlobals.Length; i++)
        {
            if (known.TryGetValue(i, out var rest)) RestGlobals[i] = rest;
            else
            {
                int parent = capture.Nodes[i].GetProperty("parent").GetInt32();
                if (parent >= 0)
                {
                    Matrix4x4 local = capture.Globals[i] * CoordinateMath.Inverse(capture.Globals[parent]);
                    RestGlobals[i] = local * RestGlobals[parent];
                }
            }
        }

        int Depth(int node)
        {
            int depth = 0;
            for (int n = node; n >= 0; n = capture.Nodes[n].GetProperty("parent").GetInt32()) depth++;
            return depth;
        }
    }

    public static int[] Bones(Capture capture, JsonElement mesh)
    {
        int[] bones = mesh.GetProperty("bones").Ints();
        Capture.Require(bones.Length > 0 && bones.Length <= ushort.MaxValue && bones.Distinct().Count() == bones.Length && bones.All(b => b >= 0 && b < capture.Nodes.Length), "Invalid skin joint references.");
        return bones;
    }

    public static Matrix4x4[] Matrices(Capture capture, JsonElement mesh, int count)
    {
        float[] values = capture.ReadFloats(mesh.GetProperty("bindposes"), 16);
        Capture.Require(values.Length == count * 16, "Bind matrix count mismatch.");
        Matrix4x4[] matrices = new Matrix4x4[count];
        for (int i = 0; i < count; i++)
        {
            matrices[i] = CoordinateMath.Matrix(values[(i * 16)..(i * 16 + 16)]);
            var m = matrices[i];
            Capture.Require(Math.Abs(m.M14) < 1e-6f && Math.Abs(m.M24) < 1e-6f && Math.Abs(m.M34) < 1e-6f && Math.Abs(m.M44 - 1) < 1e-6f, "Non-affine bind matrix.");
            _ = CoordinateMath.Inverse(m);
        }
        return matrices;
    }
}
