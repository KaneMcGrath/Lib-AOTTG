// SPDX-License-Identifier: MIT
using System.Numerics;
using LibAottg;

namespace LibAottg.Assets;

/// <summary>Optional captured HERO rig adapter: authoritative clip playback, sockets, equipment
/// visibility and source-based gun head/arm poses. Hosts upload the resulting primitives.</summary>
public sealed class HeroCharacter : IHeroPresentation
{
    public CharacterInstance Character { get; }
    public bool ProceduralAim { get; set; } = true;
    public IReadOnlyList<bool> PrimitiveVisible => visible;
    /// <summary>The supplied capture contains blades. A host can attach gun meshes to hand sockets.</summary>
    public bool HasGunGeometry { get; }
    public Vec3 Head => Position(head);
    private readonly bool[] visible;
    private readonly Dictionary<string, int> bones;
    private readonly int head, neck;
    private readonly int[] hands, upperArms, forearms, bladeStarts, bladeEnds, hookMuzzles, gunMuzzles;
    private readonly bool[] blades;
    private CharacterMotor? motor;
    private CharacterInput input;
    private HeroPose pose;
    private int revision = -1;
    private float blendDelta;
    private Quaternion oldHead;
    private bool headAimed;
    private float sampledTime = -1;

    public HeroCharacter(CharacterPackage package)
    {
        Character = package.CreateInstance();
        bones = package.Model.LogicalNodes.GroupBy(n => n.Name ?? "").ToDictionary(g => g.Key, g => g.First().LogicalIndex);
        head = Bone("head"); neck = Bone("neck");
        hands = [Bone("hand_L"), Bone("hand_R")];
        upperArms = [Bone("upper_arm_L"), Bone("upper_arm_R")];
        forearms = [Bone("forearm_L"), Bone("forearm_R")];
        bladeStarts = [Socket("bladeStart", "shoulder_L"), Socket("bladeStart", "shoulder_R")];
        bladeEnds = [Socket("bladeEnd", "shoulder_L"), Socket("bladeEnd", "shoulder_R")];
        hookMuzzles = [Bone("hookRefL1"), Bone("hookRefR1")];
        gunMuzzles = [Bone("hookRefL2"), Bone("hookRefR2")];
        blades = package.Primitives.Select(p => p.Name.IndexOf("character_blade", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        HasGunGeometry = package.Primitives.Any(p => p.Name.IndexOf("gun", StringComparison.OrdinalIgnoreCase) >= 0);
        visible = Enumerable.Repeat(true, package.Primitives.Count).ToArray();
        Character.PoseModifier = ApplyAim;
    }
    private int Bone(string name) => bones.TryGetValue(name, out int index) ? index : -1;
    private int Socket(string name, string side) => Character.Package.Sockets.FirstOrDefault(s => s.Name == name && s.Path.IndexOf(side, StringComparison.Ordinal) >= 0)?.Node ?? -1;

    public void Sample(CharacterMotor motor, HeroPose pose, CharacterInput input, float blendDelta)
    {
        if (!RuntimeMath.Finite(blendDelta) || blendDelta < 0) throw new ArgumentOutOfRangeException(nameof(blendDelta));
        if (!ReferenceEquals(this.motor, motor)) { revision = -1; headAimed = false; sampledTime = -1; }
        this.motor = motor; this.pose = pose; this.input = input; this.blendDelta = 0;
        var animation = motor.Animation;
        Character.Play(animation.Clip, animation.Loop, animation.Speed, animation.CrossFade, revision != animation.Revision);
        this.blendDelta = blendDelta;
        // Root-only updates need fresh socket coordinates, but no repeat vertex deformation for blade rigs.
        if (blendDelta > 0 || revision != animation.Revision || sampledTime != animation.Time ||
            (ProceduralAim && motor.Equipment == EquipmentMode.Guns)) Character.Seek(animation.Time, blendDelta);
        sampledTime = animation.Time;
        revision = animation.Revision;
        for (int i = 0; i < visible.Length; i++) visible[i] = !blades[i] || (motor.Equipment == EquipmentMode.Blades && motor.BladesEquipped);
    }
    /// <summary>World pose of a named rig node in the core coordinate convention.</summary>
    public bool TryNode(string name, out Vec3 position)
    { int node = Bone(name); position = node >= 0 ? Position(node) : default; return node >= 0; }
    /// <summary>Node pose in core world coordinates, including character scale and procedural rotation.</summary>
    public bool TryNodeMatrix(string name, out Matrix4x4 matrix)
    {
        int node = Bone(name);
        if (node < 0) { matrix = default; return false; }
        var mirror = Matrix4x4.CreateScale(1, 1, -1);
        matrix = mirror * Character.WorldMatrices[node] * mirror * Matrix4x4.CreateScale((motor?.Scale ?? 1) / Character.Package.MetersPerSourceUnit) *
            Matrix4x4.CreateFromQuaternion(Euler(pose.Pitch, pose.Yaw, pose.Roll)) * Matrix4x4.CreateTranslation(pose.Position.X, pose.Position.Y, pose.Position.Z);
        return true;
    }
    public Vec3 HookOrigin(CharacterMotor motor, HookSide side)
    {
        int n = (int)side;
        int node = motor.Equipment == EquipmentMode.Guns ? gunMuzzles[n] : hookMuzzles[n];
        if (node < 0) node = hands[n];
        return node < 0 ? motor.Position + Vec3.Up * motor.Scale : Position(node);
    }
    public bool WeaponSegment(HookSide side, bool gun, out Vec3 start, out Vec3 end)
    {
        int n = (int)side;
        int a = gun ? hands[n] : bladeStarts[n], b = gun ? hands[n] : bladeEnds[n];
        start = a >= 0 ? Position(a) : default; end = b >= 0 ? Position(b) : default;
        if (gun) end = start + pose.Forward * ((motor?.Scale ?? 1) * 0.5f);
        return a >= 0 && b >= 0;
    }
    private Vec3 Position(int node)
    {
        float scale = motor?.Scale ?? 1;
        if (node < 0) return pose.Position + Vec3.Up * (1.5f * scale);
        var p = Character.WorldMatrices[node].Translation / Character.Package.MetersPerSourceUnit;
        return pose.Position + pose.Rotate(new Vec3(p.X, p.Y, -p.Z) * scale);
    }
    private void ApplyAim(CharacterPoseEditor editor)
    {
        if (!ProceduralAim || motor == null || motor.Equipment != EquipmentMode.Guns || motor.State == CharacterState.Dead || motor.State == CharacterState.Grabbed)
        { headAimed = false; return; }
        var animation = motor.Animation;
        bool shooting = animation.Clip.StartsWith("AHSS_shoot", StringComparison.Ordinal);
        bool both = input.Skill.Held || (shooting && animation.Clip.IndexOf("both", StringComparison.Ordinal) >= 0);
        bool left = both || (input.Attack.Held && motor.LeftGunLoaded) || (shooting && animation.Clip.IndexOf("shoot_l", StringComparison.Ordinal) >= 0);
        bool right = both || (input.Attack.Held && !motor.LeftGunLoaded && motor.RightGunLoaded) || (shooting && animation.Clip.IndexOf("shoot_r", StringComparison.Ordinal) >= 0);
        Vec3 target = input.HasAimTarget ? input.AimTarget :
            (input.HasAimOrigin ? input.AimOrigin : pose.Position) + input.AimDirection.Normalized * (10000 * motor.Scale);
        Vec3 delta = target - pose.Position;
        float angle = Delta(pose.Yaw, Yaw(delta));
        if ((left || right) && head >= 0 && neck >= 0)
        {
            var baseRotation = ToCore(editor.WorldRotation(head));
            var worldRotation = Euler(pose.Pitch, pose.Yaw, pose.Roll) * baseRotation;
            Vec3 euler = Angles(worldRotation);
            float elevation = RuntimeMath.Clamp((float)Math.Atan2(Position(neck).Y - target.Y, Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z)) * 180 / RuntimeMath.PI, -40, 30);
            var desired = Euler(euler.X + elevation, euler.Y + RuntimeMath.Clamp(angle, -40, 40), euler.Z);
            if (!headAimed) oldHead = worldRotation;
            oldHead = Quaternion.Normalize(Quaternion.Lerp(oldHead, desired, RuntimeMath.Clamp(blendDelta * 60, 0, 1)));
            editor.SetWorldRotation(head, ToGltf(Quaternion.Inverse(Euler(pose.Pitch, pose.Yaw, pose.Roll)) * oldHead));
            headAimed = true;
        }
        else headAimed = false;
        for (int n = 0; n < 2; n++)
        {
            bool attached = n == 0 ? motor.LeftHookAttached : motor.RightHookAttached;
            bool aiming = attached || (n == 0 ? left && angle < 40 && angle > -90 : right && angle > -40 && angle < 90);
            if (hands[n] < 0 || forearms[n] < 0 || upperArms[n] < 0) continue;
            if (!aiming)
            {
                if (!motor.Grounded && !left && !right) editor.SetLocalRotation(hands[n], ToGltf(Euler(n == 0 ? 90 : -90, 0, 0)));
                continue;
            }
            Vec3 aim = attached ? (n == 0 ? motor.LeftHookAnchor : motor.RightHookAnchor) : target;
            Vec3 d = aim - Position(upperArms[n]);
            float elevation = (float)Math.Atan2(d.Y, Math.Sqrt(d.X * d.X + d.Z * d.Z)) * 180 / RuntimeMath.PI;
            editor.SetLocalRotation(hands[n], ToGltf(Euler(n == 0 ? 90 : -90, 0, 0)));
            editor.SetLocalRotation(forearms[n], ToGltf(Euler(n == 0 ? -90 : 90, 0, 0)));
            var world = Euler(n == 0 ? 0 : 180, 90 + Yaw(d), n == 0 ? -elevation : elevation);
            editor.SetWorldRotation(upperArms[n], ToGltf(Quaternion.Inverse(Euler(pose.Pitch, pose.Yaw, pose.Roll)) * world));
        }
    }
    private static float Yaw(Vec3 v) => RuntimeMath.Atan2(v.X, v.Z) * 180 / RuntimeMath.PI;
    private static float Delta(float from, float to) => ((to - from) % 360 + 540) % 360 - 180;
    private static Quaternion ToCore(Quaternion q) => new(-q.X, -q.Y, q.Z, q.W);
    private static Quaternion ToGltf(Quaternion q) => ToCore(q);
    private static Quaternion Euler(float x, float y, float z) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, y * RuntimeMath.PI / 180) *
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, x * RuntimeMath.PI / 180) * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z * RuntimeMath.PI / 180);
    private static Vec3 Angles(Quaternion q)
    {
        var m = Matrix4x4.CreateFromQuaternion(q);
        return new Vec3(RuntimeMath.Asin(RuntimeMath.Clamp(-m.M32, -1, 1)), RuntimeMath.Atan2(m.M31, m.M33), RuntimeMath.Atan2(m.M12, m.M22)) * (180 / RuntimeMath.PI);
    }
}
