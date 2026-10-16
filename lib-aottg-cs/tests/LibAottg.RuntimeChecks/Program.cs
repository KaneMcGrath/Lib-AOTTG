// SPDX-License-Identifier: MIT
using System.Numerics;
using System.Text.Json;
using LibAottg;
using LibAottg.Assets;

int count = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; }
var path = args.Length > 0 ? args[0] : "character-package";
var package = CharacterPackage.Load(path);
Check(package.Primitives.Count == 22 && package.Clips.Count == 68 && package.Materials.Count == 8, "Package inventory");
Check(package.Materials.Where(m => m.Png != null).Select(m => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(m.Png!))).Distinct().Count() == 7, "Embedded textures");
Check(Math.Abs(package.Capsule.Radius - 0.5f) < 1e-6f && Math.Abs(package.Capsule.Height - 1.7f) < 1e-6f, "Collider dimensions");
var instance = package.CreateInstance();
float maxSize = 0;
foreach (var clip in package.Clips)
foreach (float time in new[] { 0, clip.Duration * 0.5f, clip.Duration })
{
    instance.Sample(clip.Name, time);
    foreach (var p in instance.Primitives)
    {
        Check(p.Positions.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)), clip.Name + " finite vertices");
        Check(p.Normals.All(v => Math.Abs(v.Length() - 1) < 1e-4f), clip.Name + " unit normals");
    }
    maxSize = Math.Max(maxSize, (instance.BoundsMax - instance.BoundsMin).Length());
}
instance.Sample("stand", 0.2f);
var standing = instance.Primitives.SelectMany(p => p.Positions).ToArray();
Console.WriteLine($"Standing bounds {instance.BoundsMin} .. {instance.BoundsMax}");
instance.Sample("attack1", 0.3f); instance.Sample("stand", 0.2f);
Check(standing.SequenceEqual(instance.Primitives.SelectMany(p => p.Positions)), "Clip switch restores default channels");
var second = package.CreateInstance(); second.Sample("run", 0.2f);
Check(standing.SequenceEqual(instance.Primitives.SelectMany(p => p.Positions)), "Instances have independent poses");
instance.Play("run", crossFadeSeconds: 0); instance.Advance(package.Clips.First(c => c.Name == "run").Duration + 0.25f);
Check(Math.Abs(instance.Time - 0.25f) < 1e-5f, "Loop wraps");
instance.Play("attack1", loop: false, crossFadeSeconds: 0); instance.Advance(100);
Check(instance.Finished && instance.Time == instance.Duration, "One-shot clamps");
// Fly for several clip durations through the controller and rig adapter. Forward air2
// must retain its authored final pose; directional flight clips must keep animating.
foreach (float scale in new[] { 1f, 10f })
foreach (float yaw in new[] { 0f, 30f, 44f, 45f, 90f, -90f, 180f })
{
    var motor = new CharacterMotor(new CharacterConfig { WorldScale = scale });
    float radians = yaw * MathF.PI / 180;
    motor.SetPose(new Vec3(0, 100, 0) * scale,
        new Vec3(MathF.Sin(radians), 0, MathF.Cos(radians)) * (100 * scale), 0);
    var rig = new HeroCharacter(package) { ProceduralAim = false };
    var flightWorld = new KinematicCollisionWorld();
    var flightInput = new CharacterInput { LookDirection = new Vec3(0, 0, 1) };
    motor.Step(0.02f, flightInput, default, flightWorld);
    string expected = yaw < 45 && yaw > -45 ? "air2" : yaw == -90 ? "air2_left" : yaw < 135 ? "air2_right" : "air2_backward";
    Check(motor.Animation.Clip == expected, "High-speed flight at " + yaw + " selects " + expected + ", got " + motor.Animation.Clip);
    int revision = motor.Animation.Revision;
    for (int frame = 0; frame < 150; frame++)
    {
        motor.Step(0.02f, flightInput, default, flightWorld);
        rig.Sample(motor, default, flightInput, 0.02f);
        Check(motor.Animation.Revision == revision, "Sustained flight does not restart " + expected);
        if (expected == "air2" && frame > 20)
        {
            Check(rig.Character.Finished && rig.Character.Time == rig.Character.Duration, "Forward flight holds at clip end");
            second.Sample("air2", second.Package.Clips.First(c => c.Name == "air2").Duration);
            Check(rig.Character.Primitives.SelectMany(p => p.Positions).Zip(second.Primitives.SelectMany(p => p.Positions), Vector3.Distance).Max() < 1e-5f,
                "Forward flight keeps final skinned pose across repeated updates");
        }
    }
    Check(motor.Animation.Loop == (expected != "air2"), "Flight wrapping matches authored clip " + expected);
    if (expected != "air2") Check(!rig.Character.Finished && motor.Animation.Time < motor.Animation.Duration, "Directional flight still loops");
}
instance.Sample("stand", 0.2f);
instance.Play("attack1", crossFadeSeconds: 0.1f);
Check(standing.Zip(instance.Primitives.SelectMany(p => p.Positions), (a,b) => Vector3.Distance(a,b)).Max() < 1e-5f, "Crossfade begins at previous pose");
instance.Advance(0.1f); second.Sample("attack1", 0.1f);
Check(instance.Primitives.SelectMany(p => p.Positions).Zip(second.Primitives.SelectMany(p => p.Positions), Vector3.Distance).Max() < 1e-5f, "Crossfade reaches target pose");
instance.Sample("stand", 0.2f); instance.Play("run", crossFadeSeconds: 0.2f); instance.Advance(0.08f);
var visibleTransition = instance.Primitives.SelectMany(p => p.Positions).ToArray();
instance.Play("attack1", loop: false, crossFadeSeconds: 0.1f);
Check(visibleTransition.Zip(instance.Primitives.SelectMany(p => p.Positions), Vector3.Distance).Max() < 1e-5f, "Interrupted crossfade begins at current visible pose");
instance.Advance(0.1f); second.Sample("attack1", 0.1f);
Check(instance.Primitives.SelectMany(p => p.Positions).Zip(second.Primitives.SelectMany(p => p.Positions), Vector3.Distance).Max() < 1e-5f, "Interrupted crossfade reaches new target");
instance.Play("run", speed: 0, crossFadeSeconds: 0); instance.Advance(1);
Check(instance.Time == 0, "Zero playback speed pauses");
instance.Sample("stand", 0.2f);
instance.Play("attack1", loop: false, speed: 0, crossFadeSeconds: 0.1f);
instance.Seek(0.32f, 0);
Check(standing.Zip(instance.Primitives.SelectMany(p => p.Positions), Vector3.Distance).Max() < 1e-5f, "External clock seek preserves starting crossfade pose");
instance.Seek(0.32f, 0.1f); second.Sample("attack1", 0.32f);
Check(Math.Abs(instance.Time - 0.32f) < 1e-6f && instance.Primitives.SelectMany(p => p.Positions).Zip(second.Primitives.SelectMany(p => p.Positions), Vector3.Distance).Max() < 1e-5f, "Paused controller clock completes fade without advancing clip");
foreach (var socket in package.Sockets)
    Check(float.IsFinite(instance.SocketMatrix(socket.Path).Translation.Length()), "Finite socket " + socket.Path);
bool rejected = false; try { instance.Sample("missing", 0); } catch (ArgumentException) { rejected = true; }
Check(rejected, "Unknown clips fail explicitly");

var world = new KinematicCollisionWorld { Friction = 0 };
world.Boxes.Add(new WorldBox(new Vec3(-2, 0, 5), new Vec3(2, 8, 5.05f)));
var result = world.Move(new Vec3(0, 1, 0), new Vec3(0, 1, 20), new Vec3(0, 0, 1000));
Check(result.Collided && result.Position.Z <= 4.501f && Math.Abs(result.Velocity.Z) < 1e-5f, "High-speed sweep prevents thin-wall tunneling");
result = world.Move(new Vec3(0, 1, 0), new Vec3(1, 1, 10), new Vec3(5, 0, 50));
Check(result.Position.X > 0.99f && result.Position.Z < 4.501f && Math.Abs(result.Velocity.X - 5) < 1e-5f, "Wall sliding preserves tangent velocity");
result = world.Move(new Vec3(0, 20, 0), new Vec3(0, -30, 0), new Vec3(0, -2500, 0));
Check(result.Grounded && result.Position.Y >= 0 && result.Velocity.Y == 0, "High-speed floor landing");
result = world.Move(new Vec3(0, 9, 5), new Vec3(0, 3, 5), new Vec3(0, -100, 0));
Check(result.Grounded && Math.Abs(result.Position.Y - 8.001f) < 0.001f, "Landing on obstacle top");
result = world.Move(new Vec3(0, 1, 5.02f), new Vec3(0, 1, 5.02f), Vec3.Zero);
Check(result.Collided && (result.Position.Z < 4.501f || result.Position.Z > 5.549f), "Spawn overlap depenetrates");
Check(world.Contacts(new Vec3(0, 0.001f, 0), new Vec3(0, 0, 1)).Grounded, "Ground probe");
Check(world.Contacts(new Vec3(0, 2, 4.499f), new Vec3(0, 0, 1)).FrontWall, "Wall probe");
Check(world.TryHook(new Vec3(0, 1, 0), new Vec3(0, 0, 1), 10, HookSide.Left, out var hit) && Math.Abs(hit.Point.Z - 5) < 1e-5f, "Hook ray hits nearest actual surface");
// A capsule rounds the box corner; its AABB would incorrectly block this path.
var cornerWorld = new KinematicCollisionWorld { Friction = 0 };
cornerWorld.Boxes.Add(new WorldBox(new Vec3(0, 0, 0), new Vec3(2, 3, 2)));
result = cornerWorld.Move(new Vec3(-0.4f, 0, -2), new Vec3(-0.4f, 0, -0.4f), new Vec3(0, 0, 2));
Check(!result.Collided && (result.Position - new Vec3(-0.4f, 0, -0.4f)).Length < 1e-5f, "Rounded capsule corner clearance");
result = cornerWorld.Move(new Vec3(-0.3f, 0, -2), new Vec3(-0.3f, 0, 1), new Vec3(0, 0, 3));
Check(result.Collided && result.Position.X < -0.3f, "Rounded corner collision produces diagonal slide");
var random = new Random(71238);
for (int i = 0; i < 500; i++)
{
    float x = (float)(random.NextDouble() * 6 - 2), z = (float)(random.NextDouble() * 6 - 2);
    var start = new Vec3(x, 1, -3);
    var end = new Vec3(z, 1, 5);
    result = cornerWorld.Move(start, end, (end - start) * 50);
    float dx = result.Position.X - Math.Clamp(result.Position.X, 0, 2);
    float dz = result.Position.Z - Math.Clamp(result.Position.Z, 0, 2);
    Check(dx * dx + dz * dz >= 0.2498f, "Randomized capsule sweep/slide stays outside box");
    // Independently sample the original straight trajectory. Any intersection must trigger a collision.
    bool crosses = false;
    for (int j = 0; j <= 1000; j++)
    {
        var sample = start + (end - start) * (j / 1000f);
        dx = sample.X - Math.Clamp(sample.X, 0, 2); dz = sample.Z - Math.Clamp(sample.Z, 0, 2);
        if (dx * dx + dz * dz < 0.249f) { crosses = true; break; }
    }
    Check(!crosses || result.Collided, "Randomized sweep detects sampled intersection");
}

// Package loading rejects tampered assets and incomplete/unknown contracts before creating a scene.
string temporary = Path.Combine(Path.GetTempPath(), "libaottg-runtime-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    foreach (string file in Directory.EnumerateFiles(package.DirectoryPath, "*.json")) File.Copy(file, Path.Combine(temporary, Path.GetFileName(file)));
    File.Copy(Path.Combine(package.DirectoryPath, "character.glb"), Path.Combine(temporary, "character.glb"));
    string manifestPath = Path.Combine(temporary, "manifest.json");
    var manifestNode = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
    manifestNode["complete"] = false; File.WriteAllText(manifestPath, manifestNode.ToJsonString());
    rejected = false; try { CharacterPackage.Load(temporary); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Incomplete package rejected");
    manifestNode["complete"] = true; manifestNode["version"] = 999; File.WriteAllText(manifestPath, manifestNode.ToJsonString());
    rejected = false; try { CharacterPackage.Load(temporary); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Unknown schema version rejected");
    manifestNode["version"] = 1; File.WriteAllText(manifestPath, manifestNode.ToJsonString());
    using (var tamper = new FileStream(Path.Combine(temporary, "character.glb"), FileMode.Open, FileAccess.Write))
    { tamper.Position = 100; tamper.WriteByte(0); }
    rejected = false; try { CharacterPackage.Load(temporary); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Tampered GLB rejected");
}
finally
{
    string resolved = Path.GetFullPath(temporary);
    if (Path.GetDirectoryName(resolved) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) ||
        !Path.GetFileName(resolved).StartsWith("libaottg-runtime-", StringComparison.Ordinal))
        throw new InvalidOperationException("Unexpected temporary cleanup path.");
    Directory.Delete(resolved, recursive: true);
}

if (args.Length >= 3)
{
    // Export every captured frame for an independent float64 source-pose comparison.
    using var capture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[1], "manifest.json")));
    Directory.CreateDirectory(args[2]);
    var records = new List<object>();
    using var data = new BinaryWriter(File.Create(Path.Combine(args[2], "runtime-poses.bin")));
    foreach (var clip in capture.RootElement.GetProperty("clips").EnumerateArray())
    {
        var name = clip.GetProperty("name").GetString()!;
        int frames = clip.GetProperty("frameCount").GetInt32();
        int nodes = capture.RootElement.GetProperty("nodes").GetArrayLength();
        var bytes = File.ReadAllBytes(Path.Combine(args[1], clip.GetProperty("file").GetString()!));
        long offset = data.BaseStream.Position;
        for (int f = 0; f < frames; f++)
        {
            float time = BitConverter.ToSingle(bytes, f * (1 + nodes * 10) * 4);
            instance.Sample(name, time);
            foreach (var primitive in instance.Primitives)
            foreach (var pos in primitive.Positions) { data.Write(pos.X); data.Write(pos.Y); data.Write(pos.Z); }
        }
        records.Add(new { name, frames, offset });
    }
    File.WriteAllText(Path.Combine(args[2], "runtime-poses.json"), JsonSerializer.Serialize(new
    {
        schema = "libaottg.runtime-pose-check", version = 1, sha256 = package.AssetSha256,
        verticesPerFrame = package.Primitives.Sum(p => p.Positions.Length),
        primitives = package.Primitives.Select(p => new { node = p.Node, vertices = p.Positions.Length }).ToArray(), clips = records
    }, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"Runtime checks passed: {count}; {package.Clips.Count} clips, {package.Primitives.Count} primitives; max pose extent {maxSize:F3}.");
