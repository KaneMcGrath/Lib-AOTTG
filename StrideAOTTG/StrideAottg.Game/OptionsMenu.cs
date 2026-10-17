// SPDX-License-Identifier: MIT
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering.Sprites;
using Stride.UI;
using Stride.UI.Controls;
using Stride.UI.Panels;

namespace StrideAottg;

/// <summary>Native Stride UI with a modal, release-before-capture binding picker.</summary>
internal sealed class OptionsMenu : IDisposable
{
    private readonly Game game;
    private readonly SandboxOptions options;
    private readonly SpriteFont font;
    private readonly Entity entity;
    private readonly UIComponent component;
    private readonly Action resumed;
    private readonly Action fullscreenChanged;
    private readonly Canvas root;
    private Canvas panel;
    private string group = "Actions";
    private (SandboxAction Action, int Slot)? capture;
    private bool captureArmed;
    private TextBlock notice;
    private readonly List<Texture> sliderTextures = new();
    private readonly SpriteFromTexture sliderTrack, sliderFill, sliderThumb;
    public bool IsOpen { get; private set; }
    public bool IsCapturing => capture != null;
    public readonly Dictionary<string, Button> Buttons = new();
    public readonly Dictionary<string, Slider> Sliders = new();

    public OptionsMenu(Game game, Scene scene, SpriteFont font, SandboxOptions options, Action resumed, Action fullscreenChanged)
    {
        this.game = game; this.font = font; this.options = options; this.resumed = resumed; this.fullscreenChanged = fullscreenChanged;
        sliderTrack = SolidSprite(330, 40, new Color(0.18f, 0.24f, 0.32f), 16);
        sliderFill = SolidSprite(330, 40, new Color(0.1f, 0.55f, 0.72f), 16);
        sliderThumb = SolidSprite(16, 28, new Color(0.65f, 0.87f, 0.98f));
        root = new Canvas { Width = 1280, Height = 800, BackgroundColor = new Color(0, 0, 0, 0.65f) };
        component = new UIComponent { Page = new UIPage { RootElement = root }, IsFullScreen = true,
            Resolution = new Vector3(1280, 800, 1000), Enabled = false };
        entity = new Entity("Options menu"); entity.Add(component); scene.Entities.Add(entity);
        Rebuild();
    }
    public void Open() { IsOpen = true; component.Enabled = true; capture = null; Rebuild(); }
    public void Close() { capture = null; IsOpen = false; component.Enabled = false; resumed(); }
    public void Refresh() { Rebuild(); }
    public void Update(InputManager input)
    {
        if (!IsOpen) return;
        if (input.IsKeyPressed(Keys.Escape))
        {
            if (capture != null) { capture = null; Rebuild(); } else Close();
            return;
        }
        if (capture == null) return;
        // The click which opened the picker must be released before it can become a binding.
        if (!captureArmed)
        {
            if (!input.HasDownKeys && !Enum.GetValues<MouseButton>().Any(input.IsMouseButtonDown)) captureArmed = true;
            return;
        }
        if (input.IsKeyPressed(Keys.Back) || input.IsKeyPressed(Keys.Delete)) { Capture(default); return; }
        foreach (var key in input.PressedKeys) if (key != Keys.None && key != Keys.Escape) { Capture(new(key)); return; }
        foreach (var mouse in Enum.GetValues<MouseButton>()) if (input.IsMouseButtonPressed(mouse)) { Capture(new(Mouse: mouse)); return; }
    }
    public void Capture(ControlBinding binding)
    {
        if (capture is not { } target) return;
        options.SetBinding(target.Action, target.Slot, binding);
        capture = null; Rebuild();
    }
    private void Rebuild()
    {
        root.Children.Clear(); Buttons.Clear(); Sliders.Clear();
        panel = new Canvas { Width = 980, Height = 710, BackgroundColor = new Color(0.055f, 0.075f, 0.11f, 0.98f) };
        Place(root, panel, 150, 45);
        Place(panel, Text("OPTIONS", 32, Color.White), 32, 22);
        Place(panel, Text("Game paused  /  Escape to resume", 18, new Color(0.65f, 0.73f, 0.82f)), 34, 65);
        AddButton("fullscreen", game.Window.IsFullscreen ? "Fullscreen: ON" : "Fullscreen: OFF", 680, 26, 266, 45,
            fullscreenChanged);
        string[] groups = ["Actions", "Movement", "Camera", "Demo", "Effects", "Inspector", "Gameplay", "HUD"];
        for (int i = 0; i < groups.Length; i++)
        {
            string selected = groups[i];
            AddButton("tab_" + selected, selected, 32 + i * 116, 106, 108, 38,
                () => { capture = null; group = selected; Rebuild(); }, group == selected);
        }
        if (group == "Camera") BuildCamera();
        else if (group == "HUD")
        {
            void Toggle(string name,string label,int y,bool value,Action change) {
                Place(panel,Text(label,22,Color.White),44,y+8);
                AddButton(name,value?"ON":"OFF",676,y,248,40,()=>{change();options.Save();Rebuild();},value);
            }
            Toggle("heroHud","AoTTG special, gas and blades",200,options.HeroHudEnabled,()=>options.HeroHudEnabled=!options.HeroHudEnabled);
            Toggle("crosshair","Crosshair",280,options.CrosshairEnabled,()=>options.CrosshairEnabled=!options.CrosshairEnabled);
            Toggle("crosshairDistance","Crosshair distance",360,options.CrosshairDistance,()=>options.CrosshairDistance=!options.CrosshairDistance);
            Toggle("crosshairArrows","Hook direction arrows",440,options.CrosshairArrows,()=>options.CrosshairArrows=!options.CrosshairArrows);
            Place(panel,Text("Crosshair style",22,Color.White),44,608);
            AddButton("crosshairStyle",options.CrosshairStyle.ToString(),676,600,248,40,()=>{options.CrosshairStyle=(LibAottg.HeroCrosshairStyle)(((int)options.CrosshairStyle+1)%5);options.Save();Rebuild();});
            Place(panel,Text("Speedometer",22,Color.White),44,528);
            AddButton("speedometer",options.Speedometer.ToString(),676,520,248,40,()=>{options.Speedometer=(LibAottg.HeroSpeedometer)(((int)options.Speedometer+1)%3);options.Save();Rebuild();});
        }
        else if (group == "Gameplay")
        {
            Place(panel, Text("Double-tap movement to air burst", 22, Color.White), 44, 208);
            AddButton("doubleTapBurst", options.DoubleTapBurst ? "ON" : "OFF", 676, 200, 248, 40,
                () => { options.DoubleTapBurst = !options.DoubleTapBurst; options.Save(); Rebuild(); }, options.DoubleTapBurst);
            Place(panel, Text("AoTTG damage and Stylish HUD", 22, Color.White), 44, 300);
            AddButton("damageScoreCards", options.DamageScoreCardsEnabled ? "ON" : "OFF", 676, 292, 248, 40,
                () => { options.DamageScoreCardsEnabled = !options.DamageScoreCardsEnabled; options.Save(); Rebuild(); }, options.DamageScoreCardsEnabled);
            Place(panel, Text("Show the original damage number, style rank, hits, and chain multiplier.", 17, Color.LightGray), 44, 365);
            Place(panel, Text("AoTTG KillInfo feed", 22, Color.White), 44, 448);
            AddButton("killInfo", options.KillInfoEnabled ? "ON" : "OFF", 676, 440, 248, 40,
                () => { options.KillInfoEnabled = !options.KillInfoEnabled; options.Save(); Rebuild(); }, options.KillInfoEnabled);
            Place(panel, Text("Show confirmed kills with the original names, damage, and human/Titan icons.", 17, Color.LightGray), 44, 505);
            Place(panel, Text("Improved Raycasts (recommended)", 22, Color.White), 44, 552);
            AddButton("improvedRaycasts", options.ImprovedRaycasts ? "ON" : "OFF", 676, 544, 248, 40,
                () => { options.ImprovedRaycasts = !options.ImprovedRaycasts; options.Save(); Rebuild(); }, options.ImprovedRaycasts);
        }
        else
        {
            Place(panel, Text("ACTION", 15, Color.LightGray), 44, 163);
            Place(panel, Text("PRIMARY", 15, Color.LightGray), 434, 163);
            Place(panel, Text("ALTERNATE", 15, Color.LightGray), 694, 163);
            var controls = SandboxOptions.Controls.Where(c => c.Group == group).ToArray();
            for (int i = 0; i < controls.Length; i++)
            {
                var control = controls[i];
                float y = 191 + i * 34;
                Place(panel, Text(control.Label, 19, Color.White), 44, y + 6);
                for (int slot = 0; slot < 2; slot++)
                {
                    int currentSlot = slot;
                    bool picking = capture == (control.Action, slot);
                    AddButton($"bind_{control.Action}_{slot}", picking ? "Press a key or mouse button..." : options[control.Action][slot].ToString(),
                        416 + slot * 260, y, 248, 30,
                        () => { capture = (control.Action, currentSlot); captureArmed = false; Rebuild(); }, picking, 16);
                }
            }
        }
        string help = capture != null ? "Press a key or mouse button. Backspace/Delete clears. Escape cancels."
            : group == "Camera" ? "Use the same values as AOTTG. Changes apply when you resume."
            : group == "Gameplay" ? "Improved Raycasts: fire into open sky and skip near-camera hits. OFF uses original aiming."
            : group == "Movement" ? "Mouse wheel also reels. WOW reserves the orbit binding for the camera."
            : "Click a binding to change it. Escape is reserved for this menu.";
        Place(panel, Text(help, 17, new Color(0.65f, 0.73f, 0.82f)), 34, 583);
        notice = Text(capture == null ? options.Status : "Waiting for input...", 16, new Color(0.49f, 0.8f, 0.95f));
        notice.Width = 910; notice.WrapText = true;
        Place(panel, notice, 34, 612);
        AddButton("defaults", group == "Camera" ? "Restore AOTTG camera defaults" : group == "Gameplay" ? "Restore gameplay defaults" : "Restore default bindings", 32, 660, 330, 36,
            () => { capture = null; if (group == "Camera") options.RestoreCamera(); else if (group == "Gameplay") { options.DoubleTapBurst = true; options.ImprovedRaycasts = true; } else options.RestoreBindings(); options.Save(); Rebuild(); });
        AddButton("resume", "Resume", 586, 660, 176, 36, Close, true);
        AddButton("quit", "Quit", 780, 660, 166, 36, () => game.Exit());
    }
    private void BuildCamera()
    {
        CameraSlider("cameraDistance", "Camera distance", 200, options.CameraDistance, 0, v => options.CameraDistance = v);
        CameraSlider("mouseSpeed", "Mouse speed", 285, options.MouseSpeed, 0.01f, v => options.MouseSpeed = v);
        Place(panel, Text("Invert mouse", 22, Color.White), 44, 381);
        AddButton("invertMouse", options.InvertMouse ? "ON" : "OFF", 676, 372, 248, 40,
            () => { options.InvertMouse = !options.InvertMouse; options.Save(); Rebuild(); }, options.InvertMouse);
        Place(panel, Text("Camera tilt", 22, Color.White), 44, 451);
        AddButton("cameraTilt", options.CameraTilt ? "ON" : "OFF", 676, 442, 248, 40,
            () => { options.CameraTilt = !options.CameraTilt; options.Save(); Rebuild(); }, options.CameraTilt);
        Place(panel, Text("Invert affects TPS / WOW mouse look. Original uses cursor height.", 17, Color.LightGray), 44, 521);
    }
    private void CameraSlider(string name, string label, float y, float value, float minimum, Action<float> changed)
    {
        Place(panel, Text(label, 22, Color.White), 44, y + 8);
        var number = Text(value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), 22, Color.White);
        Place(panel, number, 834, y + 8);
        var slider = new Slider { Name = name, Width = 330, Height = 40, Minimum = minimum, Maximum = 1,
            Value = value, Step = 0.01f, TickFrequency = (1 - minimum) * 100, ShouldSnapToTicks = true,
            Orientation = Orientation.Horizontal, TrackBackgroundImage = sliderTrack,
            TrackForegroundImage = sliderFill, ThumbImage = sliderThumb, MouseOverThumbImage = sliderThumb };
        slider.ValueChanged += (_, e) =>
        {
            e.Handled = true;
            float selected = Math.Clamp(MathF.Round(slider.Value, 2), minimum, 1);
            changed(selected); options.Save();
            number.Text = selected.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            if (notice != null) notice.Text = options.Status;
        };
        Sliders[name] = slider; Place(panel, slider, 426, y);
        AddButton(name + "Minus", "-", 366, y, 44, 40, slider.Decrease);
        AddButton(name + "Plus", "+", 772, y, 44, 40, slider.Increase);
    }
    private SpriteFromTexture SolidSprite(int width, int height, Color color, int verticalPadding = 0)
    {
        var pixels = new Color[width * height];
        Array.Fill(pixels, color, width * verticalPadding, width * (height - 2 * verticalPadding));
        var texture = Texture.New2D(game.GraphicsDevice, width, height, PixelFormat.R8G8B8A8_UNorm,
            pixels);
        sliderTextures.Add(texture);
        return new SpriteFromTexture { Texture = texture };
    }
    private TextBlock Text(string text, float size, Color color) => new()
    { Text = text, Font = font, TextSize = size, TextColor = color, SynchronousCharacterGeneration = true, CanBeHitByUser = false };
    private void AddButton(string name, string text, float x, float y, float width, float height, Action clicked, bool selected = false, float textSize = 19)
    {
        var button = new Button { Name = name, Width = width, Height = height, Content = Text(text, textSize, Color.White),
            BackgroundColor = selected ? new Color(0.1f, 0.37f, 0.51f) : new Color(0.13f, 0.18f, 0.25f) };
        button.Click += (_, e) => { e.Handled = true; clicked(); };
        Buttons[name] = button; Place(panel, button, x, y);
    }
    private static void Place(Canvas canvas, UIElement element, float x, float y)
    { element.SetCanvasAbsolutePosition(new Vector3(x, y, 0)); canvas.Children.Add(element); }
    public void Dispose() { entity.Scene?.Entities.Remove(entity); foreach (var texture in sliderTextures) texture.Dispose(); }
}
