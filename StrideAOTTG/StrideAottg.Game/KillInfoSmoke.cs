// SPDX-License-Identifier: MIT
using System.Text.Json;
using Stride.Graphics;

namespace StrideAottg;

public sealed partial class SandboxController
{
    private bool killInfoSmoke;
    private readonly Dictionary<string, bool> killInfoChecks = new();
    private void UpdateKillInfoSmoke()
    {
        DebugText.Visible = false;
        int tick = frames - 1;
        var buffer = GraphicsDevice.Presenter.BackBuffer;
        if (tick < 240 && tick % 60 == 0)
        {
            int scenario = tick / 60;
            killInfoView.Show(scenario >= 2, "[ACFA58]Eren[-]", scenario % 2 == 0, "Titan", scenario == 0 ? 0 : scenario == 1 ? 1000 : 1001, buffer.Height);
        }
        if (tick == 245)
        {
            // Actual host death event, including rejection of duplicate notices.
            var target = world.Targets[0]; target.Health = 100; target.InfiniteHealth = false;
            var hit = new LibAottg.HeroHit { TargetId = target.Id, Damage = 1500 };
            int before = killInfoView.State.Entries.Count;
            world.OnHeroHit(hit); world.OnHeroHit(hit);
            killInfoChecks["confirmedKillOnce"] = killInfoView.State.Entries.Count == before + 1;
        }
        if (tick == 260 || tick == 275) killInfoView.Show(false, "Mikasa", true, "Abnormal", 450, buffer.Height);
        if (tick == 300)
        {
            var target = world.Targets[0]; target.Health = 100; target.InfiniteHealth = true;
            int before = killInfoView.State.Entries.Count;
            world.OnHeroHit(new LibAottg.HeroHit { TargetId = target.Id, Damage = 1500 });
            killInfoChecks["invulnerableTargetIsNotKilled"] = killInfoView.State.Entries.Count == before && target.Health == 100;
        }
        killInfoView.Update(0.02f, true, buffer.Width, buffer.Height);
        if (tick < 240 && tick % 60 == 45)
        {
            int scenario = tick / 60;
            killInfoChecks["iconsAndText" + scenario] = killInfoView.VisibleQuads >= 11;
            CaptureKillInfo("kill-info-" + scenario);
        }
        if (tick == 295)
        {
            killInfoChecks["fiveRows"] = killInfoView.State.Entries.Count(e => !e.Expired && e.Row < 5) == 5;
            killInfoChecks["compactRows"] = killInfoView.State.Entries.Count(e => e.Compact) >= 4;
            CaptureKillInfo("kill-info-stacked");
        }
        if (tick == 660)
        {
            killInfoChecks["fade"] = killInfoView.State.Entries.Any(e => e.Age > 8 && e.Alpha < 1);
            CaptureKillInfo("kill-info-fade");
        }
        if (tick < 750) return;
        killInfoChecks["expired"] = killInfoView.State.Entries.Count == 0;
        killInfoChecks["originalTextures"] = killInfoView.TextureCount == 10;
        killInfoView.Show(false, "Human", true, "Titan", 100, buffer.Height);
        killInfoView.Update(0.5f, true, buffer.Width, buffer.Height, paused: true);
        killInfoChecks["pause"] = killInfoView.State.Entries[0].Age == 0;
        killInfoView.Reset(); killInfoChecks["reset"] = killInfoView.State.Entries.Count == 0;
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed = killInfoChecks.Values.All(x => x), checks = killInfoChecks,
            width = buffer.Width, height = buffer.Height, frames, textures = killInfoView.TextureCount }, new JsonSerializerOptions { WriteIndented = true }));
        ((Stride.Engine.Game)Game).Exit();
    }
    private void CaptureKillInfo(string name)
    {
        using var output = File.Create(Path.Combine(Path.GetDirectoryName(reportPath), name + ".png"));
        GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, output, ImageFileType.Png);
    }
}
