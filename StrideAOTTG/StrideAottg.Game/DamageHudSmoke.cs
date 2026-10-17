// SPDX-License-Identifier: MIT
using System.Text.Json;
using Stride.Graphics;

namespace StrideAottg;

public sealed partial class SandboxController
{
    private bool damageHudSmoke;
    private readonly Dictionary<string, bool> damageHudChecks = new();
    private static readonly int[] DamageHudHits = [10, 200, 400, 1500, 2000, 4000, 10000, 50000];
    private void UpdateDamageHudSmoke()
    {
        DebugText.Visible = false;
        int tick = frames - 1, rank = tick / 60, phase = tick % 60;
        var buffer = GraphicsDevice.Presenter.BackBuffer;
        if (rank < DamageHudHits.Length)
        {
            if (phase == 0) damageHudView.ShowDamage(DamageHudHits[rank]);
            damageHudView.Update(0.02f, true, buffer.Width, buffer.Height);
            if (phase == 45)
            {
                damageHudChecks["rank" + rank] = damageHudView.State.DisplayRank == rank;
                damageHudChecks["geometry" + rank] = damageHudView.VisibleQuads > 15;
                using var output = File.Create(Path.Combine(Path.GetDirectoryName(reportPath), "damage-rank-" + rank + ".png"));
                buffer.Save(Game.GraphicsContext.CommandList, output, ImageFileType.Png);
            }
            return;
        }
        damageHudChecks["originalTextures"] = damageHudView.TextureCount == 6;
        float points = damageHudView.State.StylePoints;
        damageHudView.Update(0.5f, true, buffer.Width, buffer.Height, paused: true);
        damageHudChecks["pause"] = points == damageHudView.State.StylePoints;
        damageHudView.Reset();
        damageHudChecks["reset"] = damageHudView.State.Hits == 0 && damageHudView.State.ScoreScale == 0;
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new {
            passed = damageHudChecks.Values.All(x => x), checks = damageHudChecks,
            width = buffer.Width, height = buffer.Height, frames, textures = damageHudView.TextureCount
        }, new JsonSerializerOptions { WriteIndented = true }));
        ((Stride.Engine.Game)Game).Exit();
    }
}
