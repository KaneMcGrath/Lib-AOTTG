// SPDX-License-Identifier: MIT
using LibAottg;
using LibAottg.Assets;
using Stride.Engine;
using Stride.Graphics;

namespace StrideAottg;

internal sealed class DamageHudView : IDisposable
{
    private readonly DamageHudPackage package;
    private readonly BitmapHudView view;
    internal HeroDamageHud State { get; } = new();
    internal int TextureCount => view.TextureCount;
    internal int VisibleQuads => view.VisibleQuads;
    public DamageHudView(Scene scene, GraphicsDevice device)
    {
        package = DamageHudPackage.Load(Path.Combine(AppContext.BaseDirectory, "DamageHudPackage"));
        view = new BitmapHudView(scene, device, package.Textures, "AoTTG LabelScore and Stylish");
    }
    public void ShowDamage(int damage) => State.ShowDamage(damage);
    public void Reset() => State.Reset();
    public void Update(float deltaTime, bool enabled, int width, int height, bool paused = false)
    {
        State.Resize(width); State.Advance(deltaTime, paused);
        view.Render(enabled ? package.Compose(State, width, height) : Array.Empty<DamageHudQuad>(), enabled, width, height);
    }
    public void Dispose() => view.Dispose();
}
