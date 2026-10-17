// SPDX-License-Identifier: MIT
using LibAottg;
using LibAottg.Assets;
using Stride.Engine;
using Stride.Graphics;

namespace StrideAottg;

internal sealed class KillInfoView : IDisposable
{
    private readonly KillInfoPackage package;
    private readonly BitmapHudView view;
    internal HeroKillInfo State { get; } = new();
    internal int TextureCount => view.TextureCount;
    internal int VisibleQuads => view.VisibleQuads;
    public KillInfoView(Scene scene, GraphicsDevice device)
    {
        package = KillInfoPackage.Load(Path.Combine(AppContext.BaseDirectory, "KillInfoPackage"));
        view = new BitmapHudView(scene, device, package.Textures, "AoTTG KillInfo");
    }
    public void Show(bool killerIsTitan, string killer, bool victimIsTitan, string victim, int damage, int height)
        => State.Show(killerIsTitan, killer, victimIsTitan, victim, damage, height);
    public void Reset() => State.Clear();
    public void Update(float deltaTime, bool enabled, int width, int height, bool paused = false)
    {
        State.Advance(deltaTime, height, paused);
        view.Render(enabled ? package.Compose(State, width, height) : Array.Empty<DamageHudQuad>(), enabled, width, height);
    }
    public void Dispose() => view.Dispose();
}
