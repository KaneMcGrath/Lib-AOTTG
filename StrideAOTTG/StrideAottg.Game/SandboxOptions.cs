// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Serialization;
using Stride.Input;
using LibAottg;

namespace StrideAottg;

public enum SandboxAction
{
    Forward, Back, Left, Right, ReelIn, ReelOut, LookLeft, LookRight, LookUp, LookDown, Orbit,
    Jump, Dash, HookLeft, HookRight, HookBoth, Attack, Skill, Reload, Supply, Flare,
    Respawn, Camera, Preset, Equipment, Pose, Tilt, Cursor, Inspector, Grab, Die, Fullscreen,
    PreviousClip, NextClip, PauseClip, SmokeTrail, Dodge
}

public readonly record struct ControlBinding(Keys Key = Keys.None, MouseButton? Mouse = null)
{
    [JsonIgnore] public bool Empty => Key == Keys.None && Mouse == null;
    public bool Down(InputManager input) => Mouse is { } m ? input.IsMouseButtonDown(m) : Key != Keys.None && input.IsKeyDown(Key);
    public bool Pressed(InputManager input) => Mouse is { } m ? input.IsMouseButtonPressed(m) : Key != Keys.None && input.IsKeyPressed(Key);
    public bool Released(InputManager input) => Mouse is { } m ? input.IsMouseButtonReleased(m) : Key != Keys.None && input.IsKeyReleased(Key);
    public override string ToString() => Mouse is { } m ? $"Mouse {m}" : Key switch
    {
        Keys.None => "Unbound", Keys.LeftShift => "Left Shift", Keys.RightShift => "Right Shift",
        Keys.LeftCtrl => "Left Ctrl", Keys.RightCtrl => "Right Ctrl", Keys.LeftAlt => "Left Alt",
        Keys.RightAlt => "Right Alt", Keys.PageUp => "Page Up", Keys.PageDown => "Page Down",
        _ => Key.ToString()
    };
}

public sealed record ControlDefinition(SandboxAction Action, string Label, string Group, ControlBinding Primary, ControlBinding Alternate = default);

/// <summary>Demo preferences; separate from the portable HERO controller and stored per user.</summary>
public sealed class SandboxOptions
{
    public static readonly ControlDefinition[] Controls =
    [
        new(SandboxAction.Jump, "Jump / gas", "Actions", new(Keys.LeftShift)),
        new(SandboxAction.Dash, "Air burst", "Actions", new(Keys.LeftCtrl)),
        new(SandboxAction.Dodge, "Ground dodge", "Actions", new(Keys.LeftAlt)),
        new(SandboxAction.HookLeft, "Left hook", "Actions", new(Keys.Q)),
        new(SandboxAction.HookRight, "Right hook", "Actions", new(Keys.E)),
        new(SandboxAction.HookBoth, "Both hooks", "Actions", new(Keys.Space), new(Keys.B)),
        new(SandboxAction.Attack, "Attack", "Actions", new(Keys.F), new(Mouse: MouseButton.Left)),
        new(SandboxAction.Skill, "Skill / both guns", "Actions", new(Keys.C), new(Mouse: MouseButton.Right)),
        new(SandboxAction.Reload, "Reload", "Actions", new(Keys.R)),
        new(SandboxAction.Supply, "Supply", "Actions", new(Keys.T)),
        new(SandboxAction.Flare, "Flare", "Actions", new(Keys.V)),
        new(SandboxAction.Forward, "Move forward", "Movement", new(Keys.W)),
        new(SandboxAction.Back, "Move backward", "Movement", new(Keys.S)),
        new(SandboxAction.Left, "Move left", "Movement", new(Keys.A)),
        new(SandboxAction.Right, "Move right", "Movement", new(Keys.D)),
        new(SandboxAction.ReelIn, "Reel in", "Movement", new(Keys.Z)),
        new(SandboxAction.ReelOut, "Reel out", "Movement", new(Keys.X)),
        new(SandboxAction.LookLeft, "Look left", "Movement", new(Keys.Left)),
        new(SandboxAction.LookRight, "Look right", "Movement", new(Keys.Right)),
        new(SandboxAction.LookUp, "Look up", "Movement", new(Keys.Up)),
        new(SandboxAction.LookDown, "Look down", "Movement", new(Keys.Down)),
        new(SandboxAction.Orbit, "WOW camera orbit", "Movement", new(Mouse: MouseButton.Right)),
        new(SandboxAction.Respawn, "Respawn", "Demo", new(Keys.Home)),
        new(SandboxAction.Camera, "Cycle camera", "Demo", new(Keys.F2)),
        new(SandboxAction.Preset, "Cycle hero preset", "Demo", new(Keys.F3)),
        new(SandboxAction.Equipment, "Switch weapons", "Demo", new(Keys.F4)),
        new(SandboxAction.Pose, "Toggle procedural pose", "Demo", new(Keys.F5)),
        new(SandboxAction.Tilt, "Toggle camera tilt", "Demo", new(Keys.F6)),
        new(SandboxAction.SmokeTrail, "Toggle smoke trail", "Effects", new(Keys.F7)),
        new(SandboxAction.Cursor, "Release / capture cursor", "Demo", new(Keys.Tab)),
        new(SandboxAction.Inspector, "Open / close clip inspector", "Demo", new(Keys.F1)),
        new(SandboxAction.Grab, "Grab / escape", "Demo", new(Keys.G)),
        new(SandboxAction.Die, "Death", "Demo", new(Keys.K)),
        new(SandboxAction.Fullscreen, "Toggle fullscreen", "Demo", new(Keys.F11)),
        new(SandboxAction.PreviousClip, "Previous clip", "Inspector", new(Keys.PageUp)),
        new(SandboxAction.NextClip, "Next clip", "Inspector", new(Keys.PageDown)),
        new(SandboxAction.PauseClip, "Pause / resume clip", "Inspector", new(Keys.P))
    ];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public bool Fullscreen { get; set; }
    public bool SmokeTrailEnabled { get; set; } = true;
    public bool ImprovedRaycasts { get; set; } = true;
    public bool DoubleTapBurst { get; set; } = true;
    public bool DamageScoreCardsEnabled { get; set; } = true;
    public bool HeroHudEnabled { get; set; } = true;
    public bool CrosshairEnabled { get; set; } = true;
    public bool CrosshairDistance { get; set; } = true;
    public bool CrosshairArrows { get; set; } = true;
    public HeroCrosshairStyle CrosshairStyle { get; set; } = HeroCrosshairStyle.Default;
    public HeroSpeedometer Speedometer { get; set; } = HeroSpeedometer.Speed;
    public bool KillInfoEnabled { get; set; } = true;
    public void ApplyGameplay(CharacterConfig config)
    { config.DoubleTapDash = DoubleTapBurst; config.ImprovedRaycasts = ImprovedRaycasts; }
    public float CameraDistance { get; set; } = 1f;
    public float MouseSpeed { get; set; } = 0.5f;
    public bool InvertMouse { get; set; }
    public bool CameraTilt { get; set; } = true;
    public void ApplyCamera(HeroCameraConfig config)
    {
        config.CameraDistance = CameraDistance; config.MouseSpeed = MouseSpeed;
        config.InvertMouse = InvertMouse; config.CameraTilt = CameraTilt;
    }
    public void RestoreCamera()
    { CameraDistance = 1f; MouseSpeed = 0.5f; InvertMouse = false; CameraTilt = true; }
    public Dictionary<SandboxAction, ControlBinding[]> Bindings { get; set; } = new();
    [JsonIgnore] public string FilePath { get; private set; }
    [JsonIgnore] public string Status { get; private set; } = "Changes are saved automatically.";

    public SandboxOptions() { RestoreBindings(); }
    public ControlBinding[] this[SandboxAction action] => Bindings[action];
    public void RestoreBindings() => Bindings = Controls.ToDictionary(c => c.Action, c => new[] { c.Primary, c.Alternate });
    public bool Down(InputManager input, SandboxAction action) => this[action].Any(b => b.Down(input));
    public bool Pressed(InputManager input, SandboxAction action) => this[action].Any(b => b.Pressed(input));
    public float Axis(InputManager input, SandboxAction negative, SandboxAction positive) => (Down(input, positive) ? 1 : 0) - (Down(input, negative) ? 1 : 0);
    public string Label(SandboxAction action) => string.Join(" / ", this[action].Where(b => !b.Empty).Select(b => b.ToString())) is { Length: > 0 } s ? s : "Unbound";

    public static SandboxOptions Load(string filePath)
    {
        var result = new SandboxOptions { FilePath = filePath };
        try
        {
            if (!File.Exists(filePath)) return result;
            var saved = JsonSerializer.Deserialize<SandboxOptions>(File.ReadAllText(filePath), Json);
            if (saved == null) throw new JsonException("Empty settings");
            result.Fullscreen = saved.Fullscreen;
            result.SmokeTrailEnabled = saved.SmokeTrailEnabled;
            result.DoubleTapBurst = saved.DoubleTapBurst;
            result.ImprovedRaycasts = saved.ImprovedRaycasts;
            result.DamageScoreCardsEnabled = saved.DamageScoreCardsEnabled;
            result.KillInfoEnabled = saved.KillInfoEnabled;
            result.HeroHudEnabled=saved.HeroHudEnabled; result.CrosshairEnabled=saved.CrosshairEnabled;
            result.CrosshairDistance=saved.CrosshairDistance; result.CrosshairArrows=saved.CrosshairArrows;
            result.CrosshairStyle=Enum.IsDefined(saved.CrosshairStyle)?saved.CrosshairStyle:HeroCrosshairStyle.Default;
            result.Speedometer=Enum.IsDefined(saved.Speedometer)?saved.Speedometer:HeroSpeedometer.Speed;
            result.CameraDistance = float.IsFinite(saved.CameraDistance) ? Math.Clamp(saved.CameraDistance, 0, 1) : 1f;
            result.MouseSpeed = float.IsFinite(saved.MouseSpeed) ? Math.Clamp(saved.MouseSpeed, 0.01f, 1) : 0.5f;
            result.InvertMouse = saved.InvertMouse;
            result.CameraTilt = saved.CameraTilt;
            foreach (var control in Controls)
                if (saved.Bindings != null && saved.Bindings.TryGetValue(control.Action, out var slots) && slots is { Length: 2 } && slots.All(Valid))
                    result.Bindings[control.Action] = slots;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { result.Status = "Could not read settings. Defaults loaded."; }
        return result;
    }
    private static bool Valid(ControlBinding binding) => Enum.IsDefined(binding.Key) && binding.Key != Keys.Escape &&
        (binding.Mouse == null || (binding.Key == Keys.None && Enum.IsDefined(binding.Mouse.Value)));
    public void SetBinding(SandboxAction action, int slot, ControlBinding binding)
    {
        if (slot is < 0 or > 1 || !Valid(binding)) throw new ArgumentException("Invalid binding");
        this[action][slot] = binding;
        Save();
        var duplicates = Controls.Where(c => c.Action != action && this[c.Action].Contains(binding)).Select(c => c.Label).ToArray();
        if (!binding.Empty && duplicates.Length > 0) Status = "Also assigned to: " + string.Join(", ", duplicates) + ".";
    }
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, Json));
            File.Move(temporary, FilePath, overwrite: true);
            Status = "Saved. Changes take effect when you resume.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Status = "Could not save settings. Changes work for this session."; }
    }
}
