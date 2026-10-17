// SPDX-License-Identifier: MIT
using LibAottg;
using LibAottg.Assets;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;
using Stride.Rendering.Sprites;

namespace StrideAottg;

/// <summary>Draws the original AoTTG bitmap HUD without system-font substitution.</summary>
internal sealed class BitmapHudView : IDisposable
{
    private readonly Dictionary<string, Texture> textures = new();
    private readonly GraphicsDevice device;
    private readonly Dictionary<string,(Color[] Pixels,int Width,int Height,PixelFormat Format)> texturePixels=new();
    private readonly List<ImageElement> images = new();
    private readonly List<Sprite> sprites = new();
    private readonly Entity entity;
    private readonly UIComponent component;
    private readonly Canvas root;
    internal int TextureCount => textures.Count;
    internal int VisibleQuads { get; private set; }

    public BitmapHudView(Scene scene, GraphicsDevice device, IReadOnlyDictionary<string, byte[]> textureData, string name)
    {
        this.device=device;
        foreach (var entry in textureData)
        {
            using var stream = new MemoryStream(entry.Value, false);
            // UIBatch uses premultiplied alpha; preserve the decoder's channel order.
            using var decoded = Image.Load(stream);
            var pixels = decoded.PixelBuffer[0].GetPixels<Color>();
            byte Premultiply(byte channel,byte alpha) {
                float srgb=channel/255f,linear=srgb<=.04045f?srgb/12.92f:MathF.Pow((srgb+.055f)/1.055f,2.4f);
                linear*=alpha/255f;
                float result=linear<=.0031308f?linear*12.92f:1.055f*MathF.Pow(linear,1/2.4f)-.055f;
                return (byte)Math.Clamp((int)MathF.Round(result*255),0,255);
            }
            for(int i=0;i<pixels.Length;i++) {
                var p=pixels[i];pixels[i]=new Color(Premultiply(p.R,p.A),Premultiply(p.G,p.A),Premultiply(p.B,p.A),p.A);
            }
            texturePixels[entry.Key]=(pixels,decoded.Description.Width,decoded.Description.Height,decoded.Description.Format.ToSRgb());
            textures[entry.Key] = Texture.New2D(device,decoded.Description.Width,decoded.Description.Height,decoded.Description.Format.ToSRgb(),pixels);
        }
        root = new Canvas { BackgroundColor = Color.Transparent };
        component = new UIComponent { Page = new UIPage { RootElement = root }, IsFullScreen = true,
            Resolution = new Vector3(1280, 800, 1000), Enabled = true };
        entity = new Entity(name);
        entity.Add(component); scene.Entities.Add(entity);
    }

    public void Render(IReadOnlyList<DamageHudQuad> quads, bool enabled, int width, int height)
    {
        component.Enabled = enabled;
        if (!enabled) return;
        root.Width = width; root.Height = height;
        component.Resolution = new Vector3(width, height, 1000);
        VisibleQuads = quads.Count;
        while (images.Count < quads.Count)
        {
            var sprite = new Sprite { IsTransparent = true };
            var sheet = new SpriteSheet(); sheet.Sprites.Add(sprite);
            var image = new ImageElement { CanBeHitByUser = false, Source = new SpriteFromSheet { Sheet = sheet } };
            sprites.Add(sprite);
            images.Add(image); root.Children.Add(image);
        }
        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            image.Visibility = i < quads.Count ? Visibility.Visible : Visibility.Collapsed;
            if (i >= quads.Count) continue;
            var q = quads[i];
            bool flipX=q.SourceWidth<0,flipY=q.SourceHeight<0;
            string key=q.Texture+(flipX?":mirrorX":"")+(flipY?":mirrorY":"");
            var source=texturePixels[q.Texture];
            if(!textures.TryGetValue(key,out var texture)) {
                // ImageElement's automatic sizing rejects negative source rectangles.
                // Mirror GPU pixels once and keep source rectangles positive.
                var mirrored=new Color[source.Pixels.Length];
                for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)
                    mirrored[y*source.Width+x]=source.Pixels[(flipY?source.Height-1-y:y)*source.Width+(flipX?source.Width-1-x:x)];
                texture=Texture.New2D(device,source.Width,source.Height,source.Format,mirrored);textures[key]=texture;
            }
            sprites[i].Texture = texture;
            sprites[i].Region = new RectangleF(flipX?source.Width-q.SourceX:q.SourceX,flipY?source.Height-q.SourceY:q.SourceY,Math.Abs(q.SourceWidth),Math.Abs(q.SourceHeight));
            image.Width = q.Width; image.Height = q.Height;
            image.LocalMatrix = Matrix.RotationZ(q.Rotation);
            image.Color = new Color(q.R*q.A, q.G*q.A, q.B*q.A, q.A);
            image.SetCanvasAbsolutePosition(new Vector3(q.X, q.Y, 0));
            image.SetPanelZIndex(i);
        }
    }

    public void Dispose()
    {
        entity.Scene?.Entities.Remove(entity);
        foreach (var texture in textures.Values) texture.Dispose();
    }
}
