// SPDX-License-Identifier: MIT
using System.Text.Json;
using LibAottg;
using LibAottg.Assets;

int checks = 0, snapshots = 0, geometry = 0;
void Equal<T>(T actual, T expected, string label)
{ checks++; if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new Exception($"{label}: {actual} != {expected}"); }
void Near(float actual, float expected, float tolerance, string label)
{ checks++; if (Math.Abs(actual - expected) > tolerance) throw new Exception($"{label}: {actual:R} != {expected:R}"); }
string directory = Path.Combine(AppContext.BaseDirectory, "KillInfoPackage");
var package = KillInfoPackage.Load(directory);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures/unity-reference.json")));
var root = reference.RootElement;
int width = root.GetProperty("width").GetInt32(), height = root.GetProperty("height").GetInt32();
var feed = new HeroKillInfo(); var entries = new List<HeroKillInfoEntry>();
int frame = 0;
foreach (var step in root.GetProperty("steps").EnumerateArray())
{
    int damage = step.GetProperty("damage").GetInt32();
    if (damage != -2) entries.Add(feed.Show(step.GetProperty("killerTitan").GetBoolean(), step.GetProperty("killer").GetString()!,
        step.GetProperty("victimTitan").GetBoolean(), step.GetProperty("victim").GetString()!, damage, height));
    for (int n = 0; n < step.GetProperty("updates").GetInt32(); n++) feed.Advance(step.GetProperty("dt").GetSingle(), height);
    var expected = step.GetProperty("entries").EnumerateArray().ToArray();
    Equal(feed.Entries.Count, expected.Length, $"entry count frame {frame}");
    foreach (var e in expected)
    {
        var actual = entries[e.GetProperty("id").GetInt32()];
        Equal(actual.Row, e.GetProperty("row").GetInt32(), "row");
        Equal(actual.Compact, e.GetProperty("compact").GetBoolean(), "compact");
        Near(actual.Age, e.GetProperty("age").GetSingle(), 0.00001f, "age");
        Near(actual.Scale, e.GetProperty("scale").GetSingle(), 0.00001f, "scale");
        Near(actual.Y, e.GetProperty("y").GetSingle(), 0.005f, "position");
        Near(actual.Alpha, e.GetProperty("alpha").GetSingle(), 0.00001f, "alpha"); snapshots++;
        if (!e.TryGetProperty("widgets", out var widgets)) continue;
        var quads = package.Compose(actual, width, height);
        Equal(quads.Count, widgets.EnumerateArray().Sum(w => w.GetProperty("quads").GetArrayLength()), "visible widget geometry");
        foreach (var widget in widgets.EnumerateArray())
        {
            string name = widget.GetProperty("name").GetString()!;
            var aq = quads.Where(q => q.Widget == name).ToArray(); var eq = widget.GetProperty("quads").EnumerateArray().ToArray();
            Equal(aq.Length, eq.Length, name + " quad count");
            var component = manifest.RootElement.GetProperty("objects").GetProperty(name).GetProperty("components")[0];
            var sprite = component.TryGetProperty("mFont", out var font)
                ? manifest.RootElement.GetProperty("fonts").GetProperty(font.GetProperty("m_PathID").GetInt32().ToString()).GetProperty("sourceSprite")
                : manifest.RootElement.GetProperty("sprites").GetProperty(component.GetProperty("mSpriteName").GetString()!).GetProperty("sourceSprite");
            float atlasX = sprite.GetProperty("outer").GetProperty("x").GetSingle(), atlasY = sprite.GetProperty("outer").GetProperty("y").GetSingle();
            for (int i = 0; i < aq.Length; i++)
            {
                var a = aq[i]; var rect = eq[i].GetProperty("rect"); var uv = eq[i].GetProperty("uv"); var c = eq[i].GetProperty("color");
                Near(a.X, rect[0].GetSingle(), 0.005f, name + " x"); Near(a.Y, rect[1].GetSingle(), 0.005f, name + " y");
                Near(a.Width, rect[2].GetSingle(), 0.005f, name + " width"); Near(a.Height, rect[3].GetSingle(), 0.005f, name + " height");
                Near((atlasX + a.SourceX) / 2048, uv[0].GetSingle(), 0.000001f, "uv left");
                Near(1 - (atlasY + a.SourceY + a.SourceHeight) / 2048, uv[1].GetSingle(), 0.000001f, "uv bottom");
                Near((atlasX + a.SourceX + a.SourceWidth) / 2048, uv[2].GetSingle(), 0.000001f, "uv right");
                Near(1 - (atlasY + a.SourceY) / 2048, uv[3].GetSingle(), 0.000001f, "uv top");
                Near(a.R * 255, c[0].GetInt32(), 1, "red"); Near(a.G * 255, c[1].GetInt32(), 1, "green");
                Near(a.B * 255, c[2].GetInt32(), 1, "blue"); Near(a.A * 255, c[3].GetInt32(), 1, "alpha"); geometry++;
            }
        }
    }
    frame++;
}
Equal(package.Textures.Count, 10, "original textures");
Equal(feed.Entries.Count, 0, "expired feed");
feed.Show(false, "Human", true, "Titan"); float age = feed.Entries[0].Age;
feed.Advance(1, height, paused: true); Equal(feed.Entries[0].Age, age, "pause");
feed.Clear(); Equal(feed.Entries.Count, 0, "clear");
Console.WriteLine($"Passed {checks} checks: {snapshots} original KillInfo state snapshots and {geometry} original font/icon quads.");
