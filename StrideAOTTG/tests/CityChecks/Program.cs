// SPDX-License-Identifier: MIT
using System.Text.Json;
using LibAottg;
using StrideAottg;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
string directory = args.Length > 0 ? args[0] : "StrideAOTTG/Assets/City";
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
foreach (float scale in new[] { .001f, 1f, 10f })
{
    var city = new CityCollision(Path.Combine(directory, "collision.bin"), scale);
    Check(city.TriangleCount == manifest.RootElement.GetProperty("collisionTriangles").GetInt32(), "all original collider triangles loaded");
    foreach (var s in manifest.RootElement.GetProperty("spawns").EnumerateArray())
    {
        Vec3 spawn = new(s[0].GetSingle(), s[1].GetSingle(), s[2].GetSingle()); spawn *= scale;
        Check(city.Raycast(spawn + Vec3.Up * (3 * scale), -Vec3.Up, 10 * scale, out var floor), "source spawn has ground");
        Check(Math.Abs(floor.Point.Y / scale - spawn.Y / scale) < .05f, "source spawn is at street height");
        var fall = city.Move(spawn + Vec3.Up * (5 * scale), spawn - Vec3.Up * (5 * scale), -Vec3.Up * (500 * scale), .5f * scale, 1.7f * scale);
        Check(fall.Collided && fall.Grounded, "continuous capsule catches high-speed floor crossing");
        Check(Math.Abs(fall.Position.Y / scale - floor.Point.Y / scale) < .01f, "capsule stops at actual ground");
        Check(Math.Abs(fall.Velocity.Y / scale) < .001f, "floor removes inward velocity");
    }
    int roofs = 0;
    for (int x = -400; x <= 400; x += 40)
    for (int z = -400; z <= 400; z += 40)
    {
        if (!city.Raycast(new Vec3(x, 400, z) * scale, -Vec3.Up, 500 * scale, out var roof) || roof.Point.Y / scale < 5 || roof.Point.Y / scale > 100) continue;
        Vec3 normal = city.LastNormal;
        Check(normal.Y > 0, "roof query reports upward surface normal");
        Check(city.Raycast(roof.Point + Vec3.Up * (5 * scale), -Vec3.Up, 10 * scale, out var hook), "roof is hookable");
        Check((hook.Point - roof.Point).Length / scale < .02f, "hook and movement geometry use the same surface");
        var fall = city.Move(roof.Point + Vec3.Up * (10 * scale), roof.Point - Vec3.Up * (20 * scale), -Vec3.Up * (1500 * scale), .5f * scale, 1.7f * scale);
        Check(fall.Collided && fall.Grounded, "high-speed fall contacts roof");
        Check(city.Raycast(fall.Position + Vec3.Up * (2 * scale), -Vec3.Up, 5 * scale, out var surface) &&
            fall.Position.Y >= surface.Point.Y - .02f * scale, "capsule remains above the surface after sliding on a pitched roof");
        roofs++;
    }
    Check(roofs > 40, "recognizable dense city rooftops present");
    Console.WriteLine($"Scale {scale}: source spawns and {roofs} rooftop probes passed.");
}
Console.WriteLine($"Passed {checks} City geometry/collision checks.");
