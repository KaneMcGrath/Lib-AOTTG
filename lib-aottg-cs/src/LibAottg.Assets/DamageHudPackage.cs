// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text.Json;

namespace LibAottg.Assets;

/// <summary>A renderer-independent textured rectangle in screen pixels (origin top left).
/// Submit in list order using straight alpha blending and bilinear clamp sampling.</summary>
public sealed class DamageHudQuad
{
    public string Widget { get; internal set; } = "";
    public string Texture { get; internal set; } = "";
    public float X { get; internal set; }
    public float Y { get; internal set; }
    public float Width { get; internal set; }
    public float Height { get; internal set; }
    public float SourceX { get; internal set; }
    public float SourceY { get; internal set; }
    public float SourceWidth { get; internal set; }
    public float SourceHeight { get; internal set; }
    public float R { get; internal set; }
    public float G { get; internal set; }
    public float B { get; internal set; }
    public float A { get; internal set; }
    public float Rotation { get; internal set; }
}

/// <summary>The original LabelScore/Stylish assets and NGUI bitmap text layout.</summary>
public sealed class DamageHudPackage
{
    private readonly JsonElement manifest;
    private readonly Dictionary<string, byte[]> textures = new();
    private readonly Dictionary<int, Font> fonts = new();
    private readonly Dictionary<string, Widget> widgets = new();
    public IReadOnlyDictionary<string, byte[]> Textures => textures;

    private sealed class Font
    {
        public string Texture = "";
        public int Size, Spacing, SpacingY;
        public Dictionary<int, Glyph> Glyphs = new();
    }
    private sealed class Glyph
    {
        public int X, Y, Width, Height, OffsetX, OffsetY, Advance;
        public Dictionary<int, int> Kerning = new();
    }
    private sealed class Widget
    {
        public string Name = "";
        public float X, Y, ScaleX, ScaleY;
        public int Pivot, Depth, Font, Effect;
        public float[] Color = new float[4], Shadow = new float[4];
        public float EffectX, EffectY;
        public string Sprite = "";
    }

    private readonly Dictionary<string, string> roles = new();
    internal DamageHudPackage(string directory, string schema)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        manifest = document.RootElement.Clone();
        if (manifest.GetProperty("schema").GetString() != schema || manifest.GetProperty("version").GetInt32() != 1)
            throw new InvalidDataException("Unsupported damage HUD manifest.");
        int I(JsonElement e, string k) => e.GetProperty(k).GetInt32();
        float F(JsonElement e, string k) => e.GetProperty(k).GetSingle();
        float[] Color(JsonElement e) => new[] { F(e, "r"), F(e, "g"), F(e, "b"), F(e, "a") };
        void Texture(JsonElement e)
        {
            string name = e.GetProperty("texture").GetString()!;
            if (Path.GetFileName(name) != name) throw new InvalidDataException("HUD textures must be local filenames.");
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, name));
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (hash != e.GetProperty("sha256").GetString()) throw new InvalidDataException("HUD texture hash mismatch: " + name);
            textures[name] = bytes;
        }
        foreach (var entry in manifest.GetProperty("fonts").EnumerateObject())
        {
            var e = entry.Value; Texture(e);
            var font = new Font { Texture = e.GetProperty("texture").GetString()!, Size = I(e, "size"), Spacing = I(e, "spacingX"), SpacingY = I(e, "spacingY") };
            foreach (var g in e.GetProperty("glyphs").EnumerateArray())
            {
                var glyph = new Glyph { X = I(g, "x"), Y = I(g, "y"), Width = I(g, "width"), Height = I(g, "height"),
                    OffsetX = I(g, "offsetX"), OffsetY = I(g, "offsetY"), Advance = I(g, "advance") };
                int[] kerning = g.GetProperty("kerning").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                for (int n = 0; n < kerning.Length; n += 2) glyph.Kerning[kerning[n]] = kerning[n + 1];
                font.Glyphs[I(g, "index")] = glyph;
            }
            fonts[int.Parse(entry.Name)] = font;
        }
        foreach (var e in manifest.GetProperty("sprites").EnumerateObject()) Texture(e.Value);
        foreach (var entry in manifest.GetProperty("objects").EnumerateObject())
        {
            var e = entry.Value; var transform = e.GetProperty("transform");
            if (e.GetProperty("components").GetArrayLength() == 0 || !e.GetProperty("components")[0].TryGetProperty("mColor", out _)) continue;
            var position = transform.GetProperty("m_LocalPosition"); var scale = transform.GetProperty("m_LocalScale");
            var c = e.GetProperty("components")[0];
            var widget = new Widget { Name = entry.Name, X = F(position, "x"), Y = F(position, "y"), ScaleX = F(scale, "x"), ScaleY = F(scale, "y"),
                Pivot = I(c, "mPivot"), Depth = I(c, "mDepth"), Color = Color(c.GetProperty("mColor")) };
            if (c.TryGetProperty("mFont", out var font))
            {
                widget.Font = I(font, "m_PathID"); widget.Effect = I(c, "mEffectStyle");
                widget.Shadow = Color(c.GetProperty("mEffectColor"));
                widget.EffectX = F(c.GetProperty("mEffectDistance"), "x"); widget.EffectY = F(c.GetProperty("mEffectDistance"), "y");
            }
            else widget.Sprite = c.GetProperty("mSpriteName").GetString()!;
            widgets[entry.Name] = widget;
        }
        if (schema == "libaottg.kill-info")
        {
            var objects = manifest.GetProperty("objects");
            var component = objects.GetProperty("KillInfo").GetProperty("components")[0];
            foreach (var role in component.EnumerateObject())
            {
                if (role.Value.ValueKind != JsonValueKind.Object || !role.Value.TryGetProperty("m_PathID", out var id)) continue;
                foreach (var obj in objects.EnumerateObject())
                    if (obj.Value.GetProperty("pathId").GetInt32() == id.GetInt32()) roles[role.Name] = obj.Name;
            }
            foreach (var groupName in new[] { "big", "small" })
            {
                var t = objects.GetProperty(groupName).GetProperty("transform");
                float px = F(t.GetProperty("m_LocalPosition"), "x"), py = F(t.GetProperty("m_LocalPosition"), "y");
                float sx = F(t.GetProperty("m_LocalScale"), "x"), sy = F(t.GetProperty("m_LocalScale"), "y");
                foreach (var obj in objects.EnumerateObject())
                {
                    if (!widgets.TryGetValue(obj.Name, out var widget) || obj.Name.EndsWith("_s", StringComparison.Ordinal) != (groupName == "small")) continue;
                    widget.X = px + widget.X * sx; widget.Y = py + widget.Y * sy;
                    widget.ScaleX *= sx; widget.ScaleY *= sy;
                }
            }
        }
    }

    public static DamageHudPackage Load(string directory) => new(directory, "libaottg.damage-hud");

    /// <summary>Compose the source's centered LabelScore and right-edge Stylish panel.
    /// Dimensions are actual viewport pixels, matching the original pixel-perfect UIRoot.</summary>
    public IReadOnlyList<DamageHudQuad> Compose(HeroDamageHud state, float width, float height)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        return Compose(state, null, width, height);
    }

    internal IReadOnlyList<DamageHudQuad> Compose(HeroKillInfo feed, float width, float height)
        => Compose(null, (feed ?? throw new ArgumentNullException(nameof(feed))).Entries, width, height);

    internal IReadOnlyList<DamageHudQuad> Compose(HeroKillInfoEntry entry, float width, float height)
        => Compose(null, new[] { entry ?? throw new ArgumentNullException(nameof(entry)) }, width, height);

    internal IReadOnlyList<DamageHudQuad> Compose(HeroHud hero, float width, float height) => Compose(null, null, width, height, hero);

    private IReadOnlyList<DamageHudQuad> Compose(HeroDamageHud? state, IReadOnlyList<HeroKillInfoEntry>? feed, float width, float height, HeroHud? hero = null)
    {
        if (width <= 0 || height <= 0 || float.IsNaN(width) || float.IsNaN(height) || float.IsInfinity(width) || float.IsInfinity(height))
            throw new ArgumentOutOfRangeException(nameof(width));
        var ordered = new List<(int Depth, int Order, DamageHudQuad Quad)>();
        var positions = new Dictionary<string,(float X,float Y)>();
        (float X,float Y) Position(Widget w) => positions.TryGetValue(w.Name,out var p)?p:(w.X,w.Y);
        // The source UIAnchor enables its Windows half-pixel offset.
        float centerX = (float)Math.Round(width * 0.5f) - 0.5f, centerY = (float)Math.Round(height * 0.5f) - 0.5f;
        float rowScale = 1, rowY = 0, rowAlpha = 1, shiftX = 0;
        void Add(Widget widget, string texture, float sx, float sy, float sw, float sh, float x, float y, float w, float h, float[] color)
        {
            if (sw <= 0 || sh <= 0 || w <= 0 || h <= 0) return;
            ordered.Add((widget.Depth, ordered.Count, new DamageHudQuad { Widget = widget.Name, Texture = texture, SourceX = sx, SourceY = sy,
                SourceWidth = sw, SourceHeight = sh, X = x, Y = y, Width = w, Height = h,
                R = color[0], G = color[1], B = color[2], A = color[3] }));
        }
        void Label(string name, string text, string? hex = null, float? scaleOverride = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var widget = widgets[name]; var font = fonts[widget.Font];
            float scaleX = (scaleOverride ?? widget.ScaleX) / font.Size * rowScale;
            float scaleY = (scaleOverride ?? widget.ScaleY) / font.Size * rowScale;
            if (scaleX <= 0 || scaleY <= 0) return;
            var color = (float[])widget.Color.Clone(); color[3] *= Math.Max(0, Math.Min(1, rowAlpha));
            float[] HexColor(string value, float alpha) => new[] { Convert.ToInt32(value.Substring(0, 2), 16) / 255f,
                Convert.ToInt32(value.Substring(2, 2), 16) / 255f, Convert.ToInt32(value.Substring(4, 2), 16) / 255f, alpha };
            if (hex != null) color = HexColor(hex, color[3]);
            var colors = new List<float[]> { color };
            int cursor = 0, previous = 0, line = 0;
            var lineWidths = new List<int>();
            var glyphs = new List<(Glyph Glyph, int X, int Line, float[] Color)>();
            for (int index = 0; index < text.Length; index++)
            {
                char c = text[index];
                if (c == '\n') { lineWidths.Add(cursor); cursor = 0; previous = 0; line++; continue; }
                if (c < ' ') { previous = 0; continue; }
                if (c == '[' && index + 2 < text.Length && text.Substring(index, 3) == "[-]")
                { if (colors.Count > 1) colors.RemoveAt(colors.Count - 1); index += 2; continue; }
                if (c == '[' && index + 7 < text.Length && text[index + 7] == ']' &&
                    int.TryParse(text.Substring(index + 1, 6), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                { colors.Add(HexColor(text.Substring(index + 1, 6), color[3])); index += 7; continue; }
                if (!font.Glyphs.TryGetValue(c, out var g)) continue;
                if (g.Kerning.TryGetValue(previous, out int kern)) cursor += kern;
                if (c != ' ') glyphs.Add((g, cursor, line, colors[colors.Count - 1]));
                cursor += g.Advance + font.Spacing; previous = c;
            }
            lineWidths.Add(cursor);
            int printedWidth = lineWidths.Max(), printedHeight = lineWidths.Count * (font.Size + font.SpacingY);
            float x = centerX + (Position(widget).X + shiftX) * rowScale + (state != null && name != "LabelScore" ? state.StylishX : 0);
            float y = centerY - Position(widget).Y * rowScale - rowY - (state != null && name != "LabelScore" ? state.StylishY : 0);
            // NGUI includes trailing spacing in its width and aligns each line inside it.
            x -= (widget.Pivot % 3) * 0.5f * printedWidth * scaleX;
            y -= (widget.Pivot / 3) * 0.5f * printedHeight * scaleY;
            void Pass(float dx, float dy, float[] tint, bool original = false)
            {
                foreach (var item in glyphs)
                {
                    var g = item.Glyph;
                    float alignment = widget.Pivot % 3 == 0 ? 0 : widget.Pivot % 3 == 2 ? printedWidth - lineWidths[item.Line]
                        : (float)Math.Round((printedWidth - lineWidths[item.Line]) * 0.5f) + ((printedWidth & 1) == 1 ? 0.5f : 0);
                    Add(widget, font.Texture, g.X, g.Y, g.Width, g.Height,
                        x + (item.X + g.OffsetX + alignment + dx) * scaleX,
                        y + (item.Line * (font.Size + font.SpacingY) + g.OffsetY + dy) * scaleY,
                        g.Width * scaleX, g.Height * scaleY, original ? item.Color : tint);
                }
            }
            if (widget.Effect != 0)
            {
                var shadow = (float[])widget.Shadow.Clone(); shadow[3] *= color[3];
                Pass(widget.EffectX, widget.EffectY, shadow);
                if (widget.Effect == 2)
                { Pass(-widget.EffectX, -widget.EffectY, shadow); Pass(widget.EffectX, -widget.EffectY, shadow); Pass(-widget.EffectX, widget.EffectY, shadow); }
            }
            Pass(0, 0, color, true);
        }
        void Sprite(string name, float fill, string? tint = null, bool vertical = false, bool radial = false, float rotation = 0)
        {
            var widget = widgets[name]; var sprite = manifest.GetProperty("sprites").GetProperty(widget.Sprite);
            int sw = sprite.GetProperty("width").GetInt32(), sh = sprite.GetProperty("height").GetInt32();
            var source = sprite.GetProperty("sourceSprite");
            float left = source.GetProperty("paddingLeft").GetSingle(), right = source.GetProperty("paddingRight").GetSingle();
            float top = source.GetProperty("paddingTop").GetSingle(), bottom = source.GetProperty("paddingBottom").GetSingle();
            float px = widget.Pivot % 3 == 0 ? left : widget.Pivot % 3 == 1 ? (left - right - 1) * 0.5f : -1 - right;
            float py = widget.Pivot / 3 == 0 ? -top : widget.Pivot / 3 == 1 ? (bottom - top + 1) * 0.5f : 1 + bottom;
            float wx = Math.Abs(widget.ScaleX), wy = Math.Abs(widget.ScaleY);
            px = widget.Pivot % 3 == 0 ? left : widget.Pivot % 3 == 1 ? (left-right-1)*.5f : -1-right;
            py = widget.Pivot / 3 == 0 ? -top : widget.Pivot / 3 == 1 ? (bottom-top+1)*.5f : 1+bottom;
            float x = centerX + (state?.StylishX ?? 0) + (Position(widget).X + px * wx) * rowScale;
            float y = centerY - (state?.StylishY ?? 0) - rowY - (Position(widget).Y + py * wy) * rowScale;
            var color = (float[])widget.Color.Clone(); color[3] *= Math.Max(0, Math.Min(1, rowAlpha));
            if (tint == "warning") { color[0]=1; color[1]=1; color[2]=0; }
            if (tint == "red") { color[0]=1; color[1]=0; color[2]=0; }
            if (tint == "empty") { color[0]=1; color[1]=0; color[2]=0; }
            void Piece(float u, float v, float uw, float vh)
            {
                int before = ordered.Count;
                float su = widget.ScaleX < 0 ? 1-u : u, sv = widget.ScaleY < 0 ? 1-v : v;
                Add(widget, sprite.GetProperty("texture").GetString()!, su*sw, sv*sh,
                    uw*sw, vh*sh, x+u*wx*rowScale, y+v*wy*rowScale, uw*wx*rowScale, vh*wy*rowScale, color);
                if (ordered.Count > before) {
                    var q = ordered[ordered.Count-1].Quad;
                    if(widget.ScaleX<0) q.SourceWidth = -q.SourceWidth;
                    if(widget.ScaleY<0) q.SourceHeight = -q.SourceHeight;
                    q.Rotation = rotation;
                }
            }
            if(radial && fill < 1) {
                // Rasterize the source clockwise radial sweep into subpixel strips.
                // Keeps the rectangle-only renderer contract across UI backends.
                const int slices=128;
                for(int row=0;row<slices;row++) {
                    int begin=-1;
                    for(int col=0;col<=slices;col++) {
                        double angle=col==slices?0:Math.Atan2((col+.5)/slices-.5,.5-(row+.5)/slices);
                        if(angle<0)angle+=2*Math.PI;
                        bool inside=col<slices && angle < fill*2*Math.PI;
                        if(inside && begin<0)begin=col;
                        if(!inside && begin>=0){Piece((float)begin/slices,(float)row/slices,(float)(col-begin)/slices,1f/slices);begin=-1;}
                    }
                }
            } else if(vertical) Piece(0,1-fill,1,fill);
            else Piece(0,0,fill,1);
        }
        if (hero != null)
        {
            if (!hero.Visible) return Array.Empty<DamageHudQuad>();
            string Tint(float f) => f <= 0 ? "empty" : f < .3f ? "warning" : "";
            void Place(string n,float x,float y) { positions[n]=(x,y); }
            // HERO.setSkillHUDPosition places GasUI and special at bottom center + 5.
            if(hero.ResourcesVisible) {
            rowY = -height*.5f+5;
            foreach(string side in new[]{"L","R"}) {
                Sprite("gas"+side,1,hero.Guns?null:Tint(hero.GasFill)); Sprite("gas"+side+"1",hero.GasFill,vertical:true);
                if(!hero.Guns) {
                    Sprite("bladeC"+side,hero.BladeFill,vertical:true);
                    for(int n=1;n<=Math.Min(5,hero.BladePairs);n++) Sprite("blade"+side.ToLowerInvariant()+n,1,n==1?Tint(hero.BladeFill):null);
                } else {
                    if(side=="L"?hero.LeftLoaded:hero.RightLoaded) Sprite("bullet"+side,1);
                    for(int n=1;n<=Math.Min(7,side=="L"?hero.LeftReserve:hero.RightReserve);n++) Sprite("bullet"+side+n,1);
                }
            }
            Place("skill_cd_bottom",0,0); Sprite("skill_cd_bottom",1);
            string skill = "skill_cd_"+hero.Skill;
            if(widgets.ContainsKey(skill)){Place(skill,0,0);Sprite(skill,hero.SpecialFill,radial:true);}
            }
            rowY=0;
            if(hero.AimVisible) {
                string cross="Cursor"+hero.CrosshairStyle;
                Place(cross,hero.AimX*width-centerX,centerY-hero.AimY*height); Sprite(cross,1,hero.InRange?null:"red");
                Place("CrosshairText",hero.AimX*width-centerX,centerY-hero.AimY*height-18); Label("CrosshairText",hero.AimText,hero.InRange?null:"FF0000");
                if(hero.ShowArrows) foreach(var pair in new[]{(hero.LeftArrow,"L"),(hero.RightArrow,"R")}) {
                    var arrow=pair.Item1;if(!arrow.Visible)continue;
                    string n="cross"+pair.Item2+(arrow.InRange?"1":"2");
                    Place(n,arrow.X*width-centerX,centerY-arrow.Y*height);Sprite(n,1,rotation:arrow.Rotation);
                }
            }
        }
        else
        if (state != null)
        {
        Sprite("Sprite", 1); Sprite("bar", state.FillAmount);
        Label("LabelTotal", state.PointsText); Label("LabelHits", state.HitsText); Label("LabelChain", state.ChainText);
        Label("LabelS", state.RankLetter, state.RankColor); Label("Labelsub", state.RankSuffix);
        if (state.DisplayRank == 5 || state.DisplayRank == 6) Label("LabelS2", "S", state.RankColor);
        if (state.DisplayRank == 6) Label("LabelS1", "S", state.RankColor);
        Label("LabelScore", state.ScoreText, scaleOverride: state.ScoreScale);
        }
        else foreach (var entry in feed!)
        {
            rowScale = entry.Scale; rowY = entry.Y; rowAlpha = entry.Alpha;
            string prefix = entry.Compact ? "s" : "";
            if (entry.KillerIsTitan) { Sprite(roles[prefix + "leftTitan"], 1); Sprite(roles[prefix + "spriteSkeleton"], 1); }
            else Sprite(roles[prefix + "spriteSword"], 1);
            if (entry.VictimIsTitan) Sprite(roles[prefix + "rightTitan"], 1);
            shiftX = entry.KillerIsTitan ? 0 : entry.Compact ? 16 : 18;
            Label(roles[prefix + "labelNameLeft"], entry.Killer);
            shiftX = entry.KillerIsTitan ? entry.Compact ? -16 : -18 : 0;
            Label(roles[prefix + "labelNameRight"], entry.Victim);
            shiftX = 0;
            if (entry.Damage != 0) Label(roles[prefix + "labelScore"], entry.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Damage > 1000 ? "FF0000" : null);
        }
        var root = manifest.GetProperty("ancestors").EnumerateArray().First(x => x.GetProperty("name").GetString() == "UI_IN_GAME").GetProperty("components")[0];
        float activeHeight = Math.Max(root.GetProperty("minimumHeight").GetInt32(), Math.Min(root.GetProperty("maximumHeight").GetInt32(), height));
        float uiScale = height / activeHeight;
        foreach (var entry in ordered)
        {
            var q = entry.Quad;
            q.X = centerX + (q.X - centerX) * uiScale;
            q.Y = centerY + (q.Y - centerY) * uiScale;
            q.Width *= uiScale; q.Height *= uiScale;
        }
        return ordered.OrderBy(x => x.Depth).ThenBy(x => x.Order).Select(x => x.Quad).ToArray();
    }
}
