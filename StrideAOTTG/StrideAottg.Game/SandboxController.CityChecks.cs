// SPDX-License-Identifier: MIT
using System.Text.Json;
using LibAottg;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;

namespace StrideAottg;

public sealed partial class SandboxController
{
    private readonly Dictionary<string, bool> cityChecks = new();
    private Vec3 cityHookPoint;
    private bool cityHookFound;

    private void CityCamera(Vec3 position, Vec3 target)
    {
        camera.Transform.Position = StrideWorld.ToStride(position * worldScale);
        camera.Transform.Rotation = Quaternion.RotationMatrix(Matrix.Invert(Matrix.LookAtRH(camera.Transform.Position,
            StrideWorld.ToStride(target * worldScale), Vector3.UnitY)));
        camera.Get<CameraComponent>().VerticalFieldOfView = 55;
    }
    private void CityScreenshot(string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        using var stream = File.Create(Path.Combine(Path.GetDirectoryName(reportPath), name + ".png"));
        GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, stream, ImageFileType.Png);
    }
    private void UpdateCitySmoke()
    {
        if (frames == 1)
        {
            cityChecks["originalMapLoaded"] = city.MeshCount >= 4 && city.TextureCount >= 3 && city.Collision.TriangleCount == 3868;
            cityChecks["sourceSpawnPoints"] = city.Spawns.Length == 5;
            cityChecks["spawnHasSourceGround"] = world.Raycast(motor.Position + Vec3.Up * worldScale, -Vec3.Up, 2 * worldScale, HeroQuery.Ground, out _);
            for (int angle = -80; angle <= 80 && !cityHookFound; angle += 10)
            {
                float radians = angle * MathF.PI / 180;
                Vec3 direction = new(MathF.Sin(radians), 0, MathF.Cos(radians));
                Vec3 origin = motor.Position + Vec3.Up * (10 * worldScale);
                if (world.Raycast(origin, direction, 250 * worldScale, HeroQuery.HookAim, out var hit) && (hit.Point - origin).Length > 2 * worldScale)
                { cityHookPoint = hit.Point; cityHookFound = true; }
            }
            cityChecks["buildingHookRay"] = cityHookFound;
        }
        var input = new CharacterInput { LookDirection = new(0, 0, 1) };
        if (frames >= 35 && frames < 95 && cityHookFound)
        {
            input.HasAimOrigin = true; input.AimOrigin = motor.Position + Vec3.Up * worldScale;
            input.AimDirection = (cityHookPoint - input.AimOrigin).Normalized;
            input.HookLeft = new Button(true, frames == 35, false);
            input.Jump = new Button(true, false, false); input.Reel = -1;
        }
        hero.Advance(FixedStep, input);
        ticks++; playedClips.Add(motor.Animation.Clip); UpdateVisuals();
        smokeTrail.Advance(FixedStep, motor, hero.RenderPose, hero.Camera.Pose, Game.GraphicsContext.CommandList);
        cityChecks["finiteMovement"] = float.IsFinite(motor.Position.X) && float.IsFinite(motor.Position.Y) && float.IsFinite(motor.Position.Z);
        cityChecks["hookAttachedToOriginalBuilding"] = cityChecks.GetValueOrDefault("hookAttachedToOriginalBuilding") || motor.LeftHookAttached;
        cityChecks["collisionFeedback"] = world.CollisionCount > 0;
        cityChecks["gasAndHookMovement"] = motor.Gas < motor.Config.GasCapacity && (motor.Position - city.Spawns[0]).Length > worldScale;
        if (frames < 15) CityCamera(new(0, 650, -1150), new(0, 30, 0));
        else if (frames < 30) CityCamera(new(120, 170, -400), new(0, 30, 0));
        else if (frames < 35) CityCamera(city.Spawns[0] / worldScale + new Vec3(0, 6, -18), city.Spawns[0] / worldScale + new Vec3(0, 12, 100));
        if (frames == 14) CityScreenshot("city-overview");
        if (frames == 29) CityScreenshot("city-rooftops");
        if (frames == 34) CityScreenshot("city-street");
        if (frames == 85) CityScreenshot("city-hook");
        DebugText.Print($"The City | original geometry/textures | {city.Collision.TriangleCount} collider triangles", new Int2(20, 20));
        if (frames < 110) return;
        CityScreenshot("smoke-report");
        bool passed = cityChecks.Values.All(v => v);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed, checks = cityChecks, frames, ticks,
            screenshotPath, meshes = city.MeshCount, textures = city.TextureCount, collisionTriangles = city.Collision.TriangleCount,
            scope = "Original City scene assets, spawn ground, building ray/rope attachment, capsule collision and rendered views. Demo collision solver is not Unity PhysX." },
            new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = passed ? 0 : 1; ((Game)Game).Exit();
    }
}
