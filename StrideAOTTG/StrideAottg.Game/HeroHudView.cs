// SPDX-License-Identifier: MIT
using LibAottg;
using LibAottg.Assets;
using Stride.Engine;
using Stride.Graphics;
namespace StrideAottg;
internal sealed class HeroHudView : IDisposable
{
    private readonly HeroHudPackage package;
    private readonly BitmapHudView view;
    public HeroHud State { get; } = new();
    public int VisibleQuads => view.VisibleQuads;
    public HeroHudView(Scene scene, GraphicsDevice device)
    {
        package=HeroHudPackage.Load(Path.Combine(AppContext.BaseDirectory,"HeroHudPackage"));
        view=new BitmapHudView(scene,device,package.Textures,"AoTTG HERO HUD");
    }
    public void Render(bool enabled,int width,int height) => view.Render(enabled?package.Compose(State,width,height):Array.Empty<DamageHudQuad>(),enabled,width,height);
    public void Dispose()=>view.Dispose();
}
