// SPDX-License-Identifier: MIT
using System.Numerics;
using SharpGLTF.Transforms;

namespace LibAottg.Assets;

/// <summary>Independent playback, local TRS crossfades, world sockets and CPU skinning in glTF coordinates.</summary>
public sealed class CharacterInstance
{
    public CharacterPackage Package { get; }
    public Matrix4x4[] WorldMatrices { get; }
    public IReadOnlyList<DeformedPrimitive> Primitives { get; }
    public string? ClipName { get; private set; }
    public float Time { get; private set; }
    public float Speed { get; private set; } = 1;
    public bool Loop { get; private set; }
    public bool Finished => !Loop && ClipName != null && Time >= Duration;
    public float Duration => ClipName == null ? 0 : Package.Clips.First(c => c.Name == ClipName).Duration;
    public Vector3 BoundsMin { get; private set; }
    public Vector3 BoundsMax { get; private set; }
    /// <summary>Optional post-animation pose modifier; base crossfades remain unaffected.</summary>
    public Action<CharacterPoseEditor>? PoseModifier { get; set; }
    private readonly CharacterPoseEditor editor;
    private readonly AffineTransform[] locals, fadeSource, posedLocals;
    private readonly Matrix4x4[][] skinMatrices, normalMatrices;
    private float fadeDuration, fadeTime;

    internal CharacterInstance(CharacterPackage package)
    {
        Package = package;
        WorldMatrices = new Matrix4x4[package.Model.LogicalNodes.Count];
        locals = new AffineTransform[WorldMatrices.Length]; fadeSource = new AffineTransform[locals.Length];
        posedLocals = new AffineTransform[locals.Length];
        editor = new CharacterPoseEditor(package, posedLocals, WorldMatrices);
        Primitives = package.Primitives.Select(p => new DeformedPrimitive(new Vector3[p.Positions.Length], new Vector3[p.Positions.Length])).ToArray();
        skinMatrices = package.Primitives.Select(p => new Matrix4x4[p.JointNodes.Length]).ToArray();
        normalMatrices = package.Primitives.Select(p => new Matrix4x4[Math.Max(1, p.JointNodes.Length)]).ToArray();
        Sample(null, 0);
    }

    public void Play(string name, bool loop = true, float speed = 1, float crossFadeSeconds = 0.1f, bool restart = false)
    {
        if (!Package.Samplers.ContainsKey(name)) throw new ArgumentException($"Unknown animation '{name}'.", nameof(name));
        if (!RuntimeMath.Finite(speed) || speed < 0 || !RuntimeMath.Finite(crossFadeSeconds) || crossFadeSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (!restart && ClipName == name) { Loop = loop; Speed = speed; return; }
        Array.Copy(locals, fadeSource, locals.Length);
        fadeDuration = crossFadeSeconds; fadeTime = 0;
        ClipName = name; Time = 0; Loop = loop; Speed = speed;
        Evaluate();
    }

    public void Advance(float dt)
    {
        if (!RuntimeMath.Finite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (ClipName == null) return;
        Time += dt * Speed;
        Time = Loop && Duration > 0 ? Time % Duration : Math.Min(Time, Duration);
        fadeTime = Math.Min(fadeDuration, fadeTime + dt);
        Evaluate();
    }

    /// <summary>Follow an external controller clock while advancing the existing crossfade in real time.</summary>
    public void Seek(float seconds, float blendDelta = 0)
    {
        if (!RuntimeMath.Finite(seconds) || seconds < 0 || !RuntimeMath.Finite(blendDelta) || blendDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (ClipName == null) return;
        Time = Loop && Duration > 0 ? seconds % Duration : Math.Min(seconds, Duration);
        fadeTime = Math.Min(fadeDuration, fadeTime + blendDelta);
        Evaluate();
    }

    /// <summary>Exact solo pose. Null selects reconstructed rest; no controller/root motion is extracted.</summary>
    public void Sample(string? name, float seconds, bool loop = false)
    {
        if (name != null && !Package.Samplers.ContainsKey(name)) throw new ArgumentException($"Unknown animation '{name}'.", nameof(name));
        if (!RuntimeMath.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        ClipName = name; Loop = loop; Speed = 1; fadeDuration = fadeTime = 0;
        Time = name == null ? 0 : loop && Duration > 0 ? seconds % Duration : Math.Min(seconds, Duration);
        Evaluate();
    }

    public Matrix4x4 SocketMatrix(string path)
    {
        var socket = Package.Sockets.FirstOrDefault(s => s.Path == path) ?? throw new ArgumentException("Unknown socket path.", nameof(path));
        return WorldMatrices[socket.Node];
    }

    private void Evaluate()
    {
        var samplers = ClipName == null ? null : Package.Samplers[ClipName];
        float blend = fadeDuration <= 0 ? 1 : RuntimeMath.Clamp(fadeTime / fadeDuration, 0, 1);
        foreach (int i in Package.NodeOrder)
        {
            var local = samplers == null ? Package.Model.LogicalNodes[i].LocalTransform : samplers[i].GetLocalTransform(Time);
            if (blend < 1)
            {
                var a = fadeSource[i].GetDecomposed(); var b = local.GetDecomposed();
                local = new AffineTransform(Vector3.Lerp(a.Scale, b.Scale, blend),
                    Quaternion.Slerp(a.Rotation, b.Rotation, blend), Vector3.Lerp(a.Translation, b.Translation, blend));
            }
            locals[i] = local;
            posedLocals[i] = local;
        }
        editor.Rebuild();
        PoseModifier?.Invoke(editor);
        BoundsMin = new Vector3(float.PositiveInfinity); BoundsMax = new Vector3(float.NegativeInfinity);
        for (int p = 0; p < Package.Primitives.Count; p++)
        {
            var source = Package.Primitives[p]; var target = Primitives[p];
            bool skinned = source.JointNodes.Length != 0;
            for (int j = 0; j < skinMatrices[p].Length; j++)
            {
                skinMatrices[p][j] = source.InverseBinds[j] * WorldMatrices[source.JointNodes[j]];
                normalMatrices[p][j] = NormalMatrix(skinMatrices[p][j]);
            }
            if (!skinned) normalMatrices[p][0] = NormalMatrix(WorldMatrices[source.Node]);
            for (int v = 0; v < source.Positions.Length; v++)
            {
                Vector3 pos = Vector3.Zero, normal = Vector3.Zero;
                if (skinned)
                {
                    var joint = source.Joints![v]; var weight = source.Weights![v];
                    for (int k = 0; k < 4; k++)
                    {
                        float w = RuntimeMath.Component(weight, k); if (w == 0) continue;
                        int j = (int)RuntimeMath.Component(joint, k);
                        pos += Vector3.Transform(source.Positions[v], skinMatrices[p][j]) * w;
                        normal += Vector3.TransformNormal(source.Normals[v], normalMatrices[p][j]) * w;
                    }
                }
                else
                {
                    pos = Vector3.Transform(source.Positions[v], WorldMatrices[source.Node]);
                    normal = Vector3.TransformNormal(source.Normals[v], normalMatrices[p][0]);
                }
                target.Positions[v] = pos;
                target.Normals[v] = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.UnitY;
                BoundsMin = Vector3.Min(BoundsMin, pos); BoundsMax = Vector3.Max(BoundsMax, pos);
            }
        }
    }

    private static Matrix4x4 NormalMatrix(Matrix4x4 matrix)
    {
        if (!Matrix4x4.Invert(matrix, out var inverse)) throw new InvalidDataException("Singular animated transform.");
        return Matrix4x4.Transpose(inverse);
    }
}

public sealed record DeformedPrimitive(Vector3[] Positions, Vector3[] Normals);
