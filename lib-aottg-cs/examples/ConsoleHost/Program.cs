// SPDX-License-Identifier: MIT
using System;
using LibAottg;

internal static class Program
{
    private static void Main()
    {
        const float scale = 10, dt = 0.02f;
        var config = HeroStats.RcModPresets()["Mikasa"].ToConfig(); config.WorldScale = scale;
        var world = new CalibrationHeroWorld(worldScale: scale);
        world.Collision.Boxes.Add(new WorldBox(new Vec3(-5, 0, 20) * scale, new Vec3(5, 25, 22) * scale));
        var hero = new HeroController(config, world);
        hero.Event += kind => Console.WriteLine("Event: " + kind);
        hero.Respawn(Vec3.Zero, grounded: true);
        for (int tick = 0; tick < 100; tick++)
        {
            var input = new HeroInputFrame {
                MoveY = 1, HasView = true, ViewForward = new Vec3(0, 0, 1), AimDirection = new Vec3(0, 0.6f, 1),
                AimOrigin = hero.Motor.Position + Vec3.Up * scale,
                Held = (tick < 30 ? HeroButtons.Jump : HeroButtons.None) |
                    (tick >= 20 && tick < 70 ? HeroButtons.HookLeft : HeroButtons.None),
                Pressed = tick == 40 ? HeroButtons.Dash : HeroButtons.None
            };
            // No renderer, camera, input package or separate physics loop required.
            hero.Advance(dt, input);
        }
        Console.WriteLine("Host-unit position " + hero.Motor.Position + " gas " + hero.Motor.Gas.ToString("F2"));
    }
}
