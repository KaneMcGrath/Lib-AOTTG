// SPDX-License-Identifier: MIT
using System.Text.Json;
using LibAottg;
using LibAottg.Assets;

int checks = 0;
void Equal<T>(T actual, T expected, string label)
{ checks++; if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new Exception($"{label}: {actual} != {expected}"); }
void Near(float actual, float expected, float tolerance, string label)
{ checks++; if (Math.Abs(actual - expected) > tolerance) throw new Exception($"{label}: {actual:R} != {expected:R}"); }
string packagePath = Path.Combine(AppContext.BaseDirectory, "DamageHudPackage");
var package = DamageHudPackage.Load(packagePath);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(packagePath, "manifest.json")));
using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/unity-reference.json")));
var root = reference.RootElement;
int width = root.GetProperty("width").GetInt32(), height = root.GetProperty("height").GetInt32();
var hud = new HeroDamageHud(); hud.Resize(width);
int frame = 0, geometry = 0, sourceUpdates = 0;
foreach (var step in root.GetProperty("steps").EnumerateArray())
{
    int damage = step.GetProperty("damage").GetInt32();
    if (damage != -2) hud.ShowDamage(damage);
    int updateCount = step.TryGetProperty("updates", out var updates) ? updates.GetInt32() : 1;
    sourceUpdates += updateCount;
    for (int n = 0; n < updateCount; n++)
        hud.Advance(step.GetProperty("dt").GetSingle());
    Near(hud.StylePoints, step.GetProperty("stylePoints").GetSingle(), 0.06f, $"points frame {frame}");
    Equal(hud.StyleRank, step.GetProperty("styleRank").GetInt32(), $"rank frame {frame}");
    Equal(hud.Hits, step.GetProperty("styleHits").GetInt32(), "hits");
    Equal(hud.TotalDamage, step.GetProperty("styleTotalDamage").GetInt32(), "total damage");
    Equal(hud.ChainRank, step.GetProperty("chainKillRank").GetInt32(), "chain rank");
    Equal(hud.PointsText, step.GetProperty("points").GetString(), "points label");
    Equal(hud.HitsText, step.GetProperty("hits").GetString(), "hits label");
    Equal(hud.ChainText, step.GetProperty("chain").GetString(), "chain label");
    Near(hud.FillAmount, step.GetProperty("fill").GetSingle(), 0.00001f, "fill");
    Near(hud.StylishX, step.GetProperty("x").GetSingle(), 0.001f, "panel x");
    Near(hud.StylishY, step.GetProperty("y").GetSingle(), 0.001f, "panel y");
    if (step.TryGetProperty("labels", out var labels))
    {
        // The Unity oracle fixes the score at 100 to isolate geometry from tween timing.
        typeof(HeroDamageHud).GetProperty(nameof(HeroDamageHud.ScoreScale))!.SetValue(hud, 100f);
        var quads = package.Compose(hud, width, height);
        foreach (var label in labels.EnumerateArray())
        {
            string name = label.GetProperty("name").GetString()!;
            var actual = quads.Where(q => q.Widget == name).ToArray();
            var expected = label.GetProperty("quads").EnumerateArray().ToArray();
            Equal(actual.Length, expected.Length, $"{name} quad count");
            var component = manifest.RootElement.GetProperty("objects").GetProperty(name).GetProperty("components")[0];
            var sprite = component.TryGetProperty("mFont", out var font)
                ? manifest.RootElement.GetProperty("fonts").GetProperty(font.GetProperty("m_PathID").GetInt32().ToString()).GetProperty("sourceSprite")
                : manifest.RootElement.GetProperty("sprites").GetProperty(component.GetProperty("mSpriteName").GetString()!).GetProperty("sourceSprite");
            float atlasX = sprite.GetProperty("outer").GetProperty("x").GetSingle();
            float atlasY = sprite.GetProperty("outer").GetProperty("y").GetSingle();
            for (int i = 0; i < actual.Length; i++)
            {
                var a = actual[i]; var e = expected[i]; var rect = e.GetProperty("rect"); var uv = e.GetProperty("uv"); var c = e.GetProperty("color");
                Near(a.X, rect[0].GetSingle(), 0.002f, $"{name} x");
                Near(a.Y, rect[1].GetSingle(), 0.002f, $"{name} y");
                Near(a.Width, rect[2].GetSingle(), 0.002f, $"{name} width");
                Near(a.Height, rect[3].GetSingle(), 0.002f, $"{name} height");
                Near((atlasX + a.SourceX) / 2048, uv[0].GetSingle(), 0.000001f, "uv left");
                Near(1 - (atlasY + a.SourceY + a.SourceHeight) / 2048, uv[1].GetSingle(), 0.000001f, "uv bottom");
                Near((atlasX + a.SourceX + a.SourceWidth) / 2048, uv[2].GetSingle(), 0.000001f, "uv right");
                Near(1 - (atlasY + a.SourceY) / 2048, uv[3].GetSingle(), 0.000001f, "uv top");
                Near(a.R * 255, c[0].GetInt32(), 1, "red"); Near(a.G * 255, c[1].GetInt32(), 1, "green");
                Near(a.B * 255, c[2].GetInt32(), 1, "blue"); Near(a.A * 255, c[3].GetInt32(), 1, "alpha");
                geometry++;
            }
        }
    }
    frame++;
}
Equal(package.Textures.Count, 6, "original textures");
foreach (var c in root.GetProperty("damageCases").EnumerateArray())
    Equal(HeroDamage.Calculate(new Vec3(c.GetProperty("speed").GetSingle(), 0, 0), Vec3.Zero, c.GetProperty("multiplier").GetSingle()),
        c.GetProperty("damage").GetInt32(), "actual Unity speed damage");
Equal(HeroDamage.Calculate(new Vec3(3, 4, 0), Vec3.Zero), 50, "speed damage");
Equal(HeroDamage.Calculate(new Vec3(30, 40, 0), Vec3.Zero, worldScale: 10), 50, "scaled world damage");
Equal(HeroDamage.Calculate(new Vec3(3, 4, 0), new Vec3(3, 4, 0)), 10, "minimum damage");
Equal(HeroDamage.Calculate(new Vec3(10, 0, 0), new Vec3(-10, 0, 0), 1.5f), 300, "relative velocity multiplier");
hud.Reset(); hud.ShowDamage(1000); hud.Advance(1);
Near(hud.ScoreScale, 100, 0.0001f, "elastic end");
hud.Advance(1); Near(hud.ScoreScale, 100, 0.0001f, "bounce start");
hud.Advance(0.5f); Near(hud.ScoreScale, 0, 0.0001f, "bounce end");
hud.ShowDamage(10); hud.Advance(0.5f, paused: true); Equal(hud.ScoreScale, 0f, "pause");
Console.WriteLine($"Passed {checks} checks: {sourceUpdates} actual Unity Stylish updates ({frame} snapshots), 35 Unity damage cases, and {geometry} original UILabel/UISprite quads.");
