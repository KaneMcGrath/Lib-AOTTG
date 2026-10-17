// SPDX-License-Identifier: MIT
using System.Text.Json;
using LibAottg;
using Stride.Core.Mathematics;
using Stride.Input;

namespace StrideAottg;

public sealed partial class SandboxController
{
    private bool optionsSmoke;
    private InputSourceSimulated optionsInput;
    private KeyboardSimulated optionsKeyboard;
    private MouseSimulated optionsMouse;
    private readonly Dictionary<string, bool> optionsChecks = new();
    private Vec3 pausedPosition;
    private int pausedTicks;
    private bool optionsFullscreenObserved;
    private bool skipFullscreenOptions;

    private void StartOptionsSmoke()
    {
        skipFullscreenOptions = Environment.GetCommandLineArgs().Contains("--skip-fullscreen-check");
        // Isolated in-engine input devices: no global keystrokes, personal settings or other windows.
        Game.Window.SetSize(new Int2(1280, 800));
        pointerReleased = true;
        optionsInput = new InputSourceSimulated();
        Input.Sources.Add(optionsInput);
        optionsKeyboard = optionsInput.AddKeyboard();
        optionsMouse = optionsInput.AddMouse();
        optionsKeyboard.Priority = 100;
        optionsMouse.Priority = 100;
        // Stride sorts primary devices at registration, rather than when Priority changes.
        optionsInput.Devices.Remove(optionsKeyboard.Id); optionsInput.Devices.Add(optionsKeyboard.Id, optionsKeyboard);
        optionsInput.Devices.Remove(optionsMouse.Id); optionsInput.Devices.Add(optionsMouse.Id, optionsMouse);
    }
    private void OptionsCheck(string name, bool result)
    {
        optionsChecks[name] = result;
        Console.WriteLine($"Options frame {frames}: {name} = {result}");
    }
    private void OptionsClick(float x, float y)
    {
        optionsMouse.SetPosition(new Vector2(x / 1280, y / 800));
        optionsMouse.SimulateMouseDown(MouseButton.Left);
    }
    private bool UpdateOptionsSmoke()
    {
        // Separate events by rendered frames so this exercises UI hit testing and input capture.
        if (frames == 2) optionsKeyboard.SimulateDown(Keys.F7);
        if (frames == 4) optionsKeyboard.SimulateUp(Keys.F7);
        if (frames == 5)
        {
            OptionsCheck("smokeToggle", !options.SmokeTrailEnabled && !smokeTrail.Trail.Enabled);
            OptionsCheck("smokePreferencePersists", !SandboxOptions.Load(options.FilePath).SmokeTrailEnabled);
        }
        if (frames == 6) optionsKeyboard.SimulateDown(Keys.F7);
        if (frames == 8) optionsKeyboard.SimulateUp(Keys.F7);
        if (frames == 9) OptionsCheck("smokeCanBeEnabled", options.SmokeTrailEnabled && smokeTrail.Trail.Enabled);
        if (frames == 10) optionsKeyboard.SimulateDown(Keys.Escape);
        if (frames == 12) optionsKeyboard.SimulateUp(Keys.Escape);
        if (frames == 18)
        {
            OptionsCheck("escapeOpensMenu", optionsMenu.IsOpen);
            pausedPosition = motor.Position; pausedTicks = ticks;
            OptionsClick(720, 251); // Jump primary slot.
        }
        if (frames == 20) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 25)
        {
            OptionsCheck("bindingButtonHitTest", optionsMenu.IsCapturing);
            optionsKeyboard.SimulateDown(Keys.J);
        }
        if (frames == 27) optionsKeyboard.SimulateUp(Keys.J);
        if (frames == 32)
        {
            OptionsCheck("keyCaptured", options[SandboxAction.Jump][0].Key == Keys.J && !optionsMenu.IsCapturing);
            var saved = SandboxOptions.Load(options.FilePath);
            OptionsCheck("bindingPersists", saved[SandboxAction.Jump][0].Key == Keys.J);
            optionsKeyboard.SimulateDown(Keys.J);
        }
        if (frames == 34)
        {
            pointerReleased = false;
            OptionsCheck("mappedInputReachesHero", (PlayerInput(false).Held & HeroButtons.Jump) != 0);
            pointerReleased = true;
            optionsKeyboard.SimulateUp(Keys.J);
            OptionsClick(850, 251); // Jump alternate slot.
        }
        if (frames == 36) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 40) optionsMouse.SimulateMouseDown(MouseButton.Middle);
        if (frames == 42) optionsMouse.SimulateMouseUp(MouseButton.Middle);
        if (frames == 46)
        {
            OptionsCheck("mouseCaptured", options[SandboxAction.Jump][1].Mouse == MouseButton.Middle);
            OptionsClick(720, 251);
        }
        if (frames == 48) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 52) optionsKeyboard.SimulateDown(Keys.Escape);
        if (frames == 54) optionsKeyboard.SimulateUp(Keys.Escape);
        if (frames == 58)
        {
            OptionsCheck("escapeCancelsCapture", optionsMenu.IsOpen && !optionsMenu.IsCapturing && options[SandboxAction.Jump][0].Key == Keys.J);
            OptionsClick(720, 251);
        }
        if (frames == 60) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 64) optionsKeyboard.SimulateDown(Keys.Delete);
        if (frames == 66) optionsKeyboard.SimulateUp(Keys.Delete);
        if (frames == 70)
        {
            OptionsCheck("bindingCanBeCleared", options[SandboxAction.Jump][0].Empty);
            if (!skipFullscreenOptions) OptionsClick(965, 91); // Fullscreen button.
        }
        if (frames == 72) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 78 && !skipFullscreenOptions)
        {
            optionsFullscreenObserved = Game.Window.IsFullscreen;
            OptionsCheck("fullscreenButton", optionsFullscreenObserved);
            OptionsCheck("fullscreenPersists", SandboxOptions.Load(options.FilePath).Fullscreen);
            // Toggle back through the same UI event after the monitor changes the client aspect.
            optionsMenu.Buttons["fullscreen"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
        }
        if (frames == 86)
        {
            if (!skipFullscreenOptions) OptionsCheck("returnToWindow", optionsFullscreenObserved && !Game.Window.IsFullscreen);
            OptionsCheck("menuPausesSimulation", ticks == pausedTicks && (motor.Position - pausedPosition).LengthSquared == 0);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            using var output = File.Create(screenshotPath);
            GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, output, Stride.Graphics.ImageFileType.Png);
            optionsMenu.Buttons["defaults"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
        }
        if (frames == 90)
        {
            OptionsCheck("restoreDefaults", options[SandboxAction.Jump][0].Key == Keys.LeftShift && options[SandboxAction.Jump][1].Empty);
            optionsMenu.Buttons["resume"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
        }
        if (frames == 96)
        {
            OptionsCheck("resumeAdvancesSimulation", !optionsMenu.IsOpen && ticks > pausedTicks);
            OptionsCheck("menuClickDoesNotAttack", motor.State != CharacterState.Attack);
            OptionsCheck("menuRendered", File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0);
            optionsMenu.Open();
            OptionsClick(507, 170); // Camera tab.
        }
        if (frames == 98) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 104) OptionsClick(659, 265); // Distance slider, near 0.25.
        if (frames == 106) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 110)
        {
            OptionsCheck("cameraDistanceSliderHitTest", options.CameraDistance is > 0.1f and < 0.4f);
            OptionsClick(659, 350); // Mouse speed slider.
        }
        if (frames == 112) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 116)
        {
            OptionsCheck("mouseSpeedSliderHitTest", options.MouseSpeed is > 0.1f and < 0.4f);
            OptionsClick(950, 435); // Invert mouse.
        }
        if (frames == 118) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 122) OptionsClick(950, 505); // Camera tilt.
        if (frames == 124) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 130)
        {
            OptionsCheck("cameraTogglesHitTest", options.InvertMouse && !options.CameraTilt);
            var savedCamera = SandboxOptions.Load(options.FilePath);
            OptionsCheck("cameraPreferencesPersist", savedCamera.CameraDistance == options.CameraDistance &&
                savedCamera.MouseSpeed == options.MouseSpeed && savedCamera.InvertMouse && !savedCamera.CameraTilt);
            using (var output = File.Create(Path.ChangeExtension(reportPath, ".camera.png")))
                GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, output, Stride.Graphics.ImageFileType.Png);
            optionsMenu.Close();
            OptionsCheck("cameraPreferencesReachLibrary", Math.Abs(hero.Camera.Config.Distance - options.CameraDistance - 0.3f) < 0.0001f &&
                hero.Camera.Config.Sensitivity == options.MouseSpeed && hero.Camera.Config.InvertY && !hero.Camera.Config.Tilt);
            Reset();
            OptionsCheck("cameraPreferencesSurviveRespawn", hero.Camera.Config.InvertMouse && !hero.Camera.Config.CameraTilt);
        }
        if (frames == 134) optionsKeyboard.SimulateDown(Keys.F6);
        if (frames == 136) optionsKeyboard.SimulateUp(Keys.F6);
        if (frames == 140)
        {
            OptionsCheck("tiltShortcutPersists", options.CameraTilt && hero.Camera.Config.CameraTilt && SandboxOptions.Load(options.FilePath).CameraTilt);
            optionsMenu.Open();
            optionsMenu.Buttons["defaults"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
            OptionsCheck("cameraDefaults", options.CameraDistance == 1 && options.MouseSpeed == 0.5f && !options.InvertMouse && options.CameraTilt);
            optionsMenu.Close();
        }
        if (frames == 146)
        {
            string old = Path.Combine(Path.GetDirectoryName(reportPath), "old-options.json");
            File.WriteAllText(old, "{\"Fullscreen\":false}");
            var migrated = SandboxOptions.Load(old);
            OptionsCheck("oldSettingsUseCameraDefaults", migrated.CameraDistance == 1 && migrated.MouseSpeed == 0.5f && !migrated.InvertMouse && migrated.CameraTilt);
            File.WriteAllText(old, "{\"CameraDistance\":5,\"MouseSpeed\":-2}");
            migrated = SandboxOptions.Load(old);
            OptionsCheck("cameraSettingsClamp", migrated.CameraDistance == 1 && migrated.MouseSpeed == 0.01f);
            // Corrupt preferences should recover without crashing startup.
            string broken = Path.Combine(Path.GetDirectoryName(reportPath), "corrupt-options.json");
            File.WriteAllText(broken, "{bad json");
            OptionsCheck("invalidSettingsRecover", SandboxOptions.Load(broken)[SandboxAction.Jump][0].Key == Keys.LeftShift);
            optionsMenu.Open();
            OptionsClick(1035, 170); // Gameplay tab.
        }
        if (frames == 148) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 154)
        {
            OptionsCheck("gameplayTabHitTest", optionsMenu.Buttons.ContainsKey("doubleTapBurst"));
            OptionsClick(950, 265); // Double-tap burst toggle.
        }
        if (frames == 156) optionsMouse.SimulateMouseUp(MouseButton.Left);
        if (frames == 162)
        {
            OptionsCheck("doubleTapToggleHitTest", !options.DoubleTapBurst);
            OptionsCheck("doubleTapPreferencePersists", !SandboxOptions.Load(options.FilePath).DoubleTapBurst);
            using (var output = File.Create(Path.ChangeExtension(reportPath, ".gameplay.png")))
                GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, output, Stride.Graphics.ImageFileType.Png);
            optionsMenu.Close();
            OptionsCheck("doubleTapPreferenceReachesMotor", !motor.Config.DoubleTapDash);
            Reset();
            OptionsCheck("doubleTapPreferenceSurvivesRespawn", !motor.Config.DoubleTapDash);
            optionsMenu.Open();
            optionsMenu.Buttons["tab_Actions"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
        }
        if (frames == 168) optionsKeyboard.SimulateDown(Keys.LeftCtrl);
        if (frames == 170)
        {
            pointerReleased = false;
            var raw = PlayerInput(false);
            OptionsCheck("burstBindingIsIndependent", (raw.Held & HeroButtons.Dash) != 0 && (raw.Held & HeroButtons.Dodge) == 0);
            pointerReleased = true;
            optionsKeyboard.SimulateUp(Keys.LeftCtrl);
        }
        if (frames == 174) optionsKeyboard.SimulateDown(Keys.LeftAlt);
        if (frames == 176)
        {
            pointerReleased = false;
            var raw = PlayerInput(false);
            OptionsCheck("dodgeBindingIsIndependent", (raw.Held & HeroButtons.Dodge) != 0 && (raw.Held & HeroButtons.Dash) == 0);
            pointerReleased = true;
            optionsKeyboard.SimulateUp(Keys.LeftAlt);
        }
        if (frames == 180)
        {
            optionsMenu.Buttons["bind_Dodge_0"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
            optionsMenu.Capture(new(Keys.H));
            var saved = SandboxOptions.Load(options.FilePath);
            OptionsCheck("dodgeCanBeReboundSeparately", saved[SandboxAction.Dodge][0].Key == Keys.H && saved[SandboxAction.Dash][0].Key == Keys.LeftCtrl);
            optionsMenu.Buttons["tab_Gameplay"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
            optionsMenu.Buttons["defaults"].RaiseEvent(new Stride.UI.Events.RoutedEventArgs(Stride.UI.Controls.ButtonBase.ClickEvent));
            optionsMenu.Close();
            OptionsCheck("gameplayDefaultsReenableDoubleTap", options.DoubleTapBurst && motor.Config.DoubleTapDash);
        }
        if (frames == 182)
        {
            // Match the user's reel binding in these isolated test preferences.
            pointerReleased = false;
            options[SandboxAction.ReelIn][0] = new(Keys.LeftCtrl);
            optionsKeyboard.SimulateDown(Keys.LeftCtrl);
            optionsKeyboard.SimulateDown(Keys.LeftShift);
        }
        if (frames == 184)
        {
            pointerReleased = false;
            var raw = PlayerInput(false);
            OptionsCheck("heldReelInIsFullStrength", raw.Reel == -1);
            OptionsCheck("heldGasAndReelReachInputTogether", (raw.Held & HeroButtons.Jump) != 0 && raw.Reel == -1);
        }
        if (frames == 185)
        {
            optionsKeyboard.SimulateUp(Keys.LeftCtrl);
            optionsKeyboard.SimulateUp(Keys.LeftShift);
        }
        if (frames == 186)
        {
            options[SandboxAction.ReelOut][0] = new(Keys.LeftAlt);
            optionsKeyboard.SimulateDown(Keys.LeftAlt);
        }
        if (frames == 188)
        {
            OptionsCheck("heldReelOutIsFullStrength", PlayerInput(false).Reel == 1);
            optionsKeyboard.SimulateDown(Keys.LeftCtrl);
        }
        if (frames == 190)
        {
            OptionsCheck("reelInWinsWhenBothButtonsHeld", PlayerInput(false).Reel == -1);
            optionsKeyboard.SimulateUp(Keys.LeftCtrl);
        }
        if (frames == 192)
        {
            OptionsCheck("releaseReelInResumesHeldReelOut", PlayerInput(false).Reel == 1);
            optionsKeyboard.SimulateDown(Keys.LeftCtrl);
        }
        if (frames == 194)
        {
            OptionsCheck("repressReelInOverridesHeldReelOut", PlayerInput(false).Reel == -1);
            optionsKeyboard.SimulateUp(Keys.LeftAlt);
        }
        if (frames == 196)
        {
            OptionsCheck("releaseReelOutPreservesHeldReelIn", PlayerInput(false).Reel == -1);
            optionsKeyboard.SimulateUp(Keys.LeftCtrl);
        }
        if (frames == 198)
        {
            OptionsCheck("releaseBothReelButtonsIsNeutral", PlayerInput(false).Reel == 0);
            optionsKeyboard.SimulateDown(Keys.LeftAlt);
            optionsMouse.SimulateMouseWheel(-0.1f);
        }
        if (frames == 199)
            OptionsCheck("wheelInOverridesHeldReelOut", Input.MouseWheelDelta < 0 && PlayerInput(false).Reel == -1);
        if (frames == 200)
        {
            optionsKeyboard.SimulateDown(Keys.LeftCtrl);
            optionsMouse.SimulateMouseWheel(1);
        }
        if (frames == 201)
            OptionsCheck("heldReelInOverridesWheelOut", Input.MouseWheelDelta > 0 && PlayerInput(false).Reel == -1);
        if (frames == 202)
        {
            optionsKeyboard.SimulateUp(Keys.LeftCtrl);
            optionsKeyboard.SimulateUp(Keys.LeftAlt);
            optionsMouse.SimulateMouseWheel(-0.1f);
        }
        if (frames == 203)
            OptionsCheck("fractionalWheelInIsFullStrength", Input.MouseWheelDelta < 0 && PlayerInput(false).Reel == -1);
        if (frames == 204) optionsMouse.SimulateMouseWheel(0.1f);
        if (frames == 205)
            OptionsCheck("fractionalWheelOutIsFullStrength", Input.MouseWheelDelta > 0 && PlayerInput(false).Reel == 1);
        if (frames == 206)
        {
            string old = Path.Combine(Path.GetDirectoryName(reportPath), "old-controls.json");
            File.WriteAllText(old, "{\"Bindings\":{\"Dash\":[{\"Key\":\"LeftCtrl\"},{\"Key\":\"None\"}]}}");
            var migrated = SandboxOptions.Load(old);
            OptionsCheck("oldControlsGainSeparateDodge", migrated[SandboxAction.Dash][0].Key == Keys.LeftCtrl &&
                migrated[SandboxAction.Dodge][0].Key == Keys.LeftAlt && migrated.DoubleTapBurst);
            bool passed = optionsChecks.Values.All(v => v);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed, checks = optionsChecks, frames, ticks, screenshotPath,
                settingsPath = options.FilePath,
                skippedChecks = skipFullscreenOptions ? new[] { "fullscreenButton", "fullscreenPersists", "returnToWindow" } : Array.Empty<string>(),
                scope = "Native UI hit testing, simulated local input, persistence, pause/resume, camera and gameplay options." },
                new JsonSerializerOptions { WriteIndented = true }));
            Input.Sources.Remove(optionsInput);
            Environment.ExitCode = passed ? 0 : 1;
            ((Stride.Engine.Game)Game).Exit(); return true;
        }
        return false;
    }
}
