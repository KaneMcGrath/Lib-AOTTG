// SPDX-License-Identifier: MIT
using System.Text.Json;
using LibAottg;
using LibAottg.Assets;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Input;
using Stride.Rendering;
using Stride.Rendering.Colors;
using Stride.Rendering.Lights;

namespace StrideAottg;

/// <summary>Interactive portability playground, with a fixed 50 Hz LibAottg simulation.</summary>
[DataContract("SandboxController")]
public sealed partial class SandboxController : SyncScript
{
    private const float FixedStep = 0.02f;
    private static readonly (SandboxAction Control, HeroButtons Action)[] Bindings =
        [(SandboxAction.Jump, HeroButtons.Jump), (SandboxAction.Dash, HeroButtons.Dash), (SandboxAction.Dodge, HeroButtons.Dodge),
         (SandboxAction.HookLeft, HeroButtons.HookLeft), (SandboxAction.HookRight, HeroButtons.HookRight), (SandboxAction.HookBoth, HeroButtons.HookBoth),
         (SandboxAction.Attack, HeroButtons.Attack), (SandboxAction.Skill, HeroButtons.Skill), (SandboxAction.Reload, HeroButtons.Reload),
         (SandboxAction.Supply, HeroButtons.Supply), (SandboxAction.Flare, HeroButtons.Flare)];
    private SandboxOptions options;
    private OptionsMenu optionsMenu;
    private DamageHudView damageHudView;
    private HeroHudView heroHudView;
    private KillInfoView killInfoView;
    private bool waitForInputRelease;
    private Int2 windowedSize;
    private Int2 windowedPosition;
    private bool haveWindowedGeometry;
    private bool? pendingFullscreen;
    private HeroController hero;
    private HeroAudio heroAudio;
    private SmokeTrailRenderer smokeTrail;
    private CharacterMotor motor;
    private float wallBurstSpeed;
    private StrideWorld world;
    private CityMap city;
    private bool citySmoke;
    private Entity player, camera, aimMarker;
    private readonly List<Entity>[] ropes = [new(), new()];
    private readonly HeroRope[] ropeModels = [new(), new()];
    private readonly List<(StrideWorld.Target Target, Entity Entity)> targets = new();
    private readonly List<(Entity Entity, float Expires)> effects = new();
    private readonly Entity[] guns = new Entity[2];
    private readonly Entity[] gasJets = new Entity[2];
    private Model cube;
    private RuntimeCharacterRenderer characterRenderer;
    private readonly HashSet<string> playedClips = new();
    private bool inspectAnimations, pauseAnimation;
    private int inspectedClip;
    private bool assetSmoke;
    private int galleryPose, galleryWait;
    private bool galleryValid = true;
    private readonly List<object> galleryResults = new();
    private float yaw, maxHeight, maxDistance, elapsed;
    private int ticks, frames;
    private int presetIndex;
    private readonly HeroStats[] presets = HeroStats.RcModPresets().Values.ToArray();
    private EquipmentMode equipment;
    private bool pointerReleased, focused;
    private readonly HashSet<string> featureChecks = new();
    private readonly HashSet<HeroCameraMode> cameraModes = new();
    private Dictionary<string, bool> baselineChecks;
    private bool cameraBlocked, sawLean;
    private float worldScale = 1;
    private bool smoke, captured;
    private string reportPath, screenshotPath;

    public override void Start()
    {
        DebugText.Visible = true;
        DebugText.TextColor = Color.White;
        var args = Environment.GetCommandLineArgs();
        smoke = args.Contains("--smoke-test");
        assetSmoke = args.Contains("--asset-smoke-test");
        optionsSmoke = args.Contains("--options-smoke-test");
        citySmoke = args.Contains("--city-smoke-test");
        heroHudSmoke = args.Contains("--hero-hud-smoke-test");
        damageHudSmoke = args.Contains("--damage-hud-smoke-test");
        killInfoSmoke = args.Contains("--kill-info-smoke-test");
        bool useCity = citySmoke || (!smoke && !assetSmoke && !optionsSmoke && !damageHudSmoke && !killInfoSmoke && !heroHudSmoke && !args.Contains("--sandbox"));
        int reportIndex = Array.IndexOf(args, "--report");
        reportPath = reportIndex >= 0 && reportIndex + 1 < args.Length
            ? Path.GetFullPath(args[reportIndex + 1])
            : Path.Combine(AppContext.BaseDirectory, "smoke-report.json");
        screenshotPath = Path.ChangeExtension(reportPath, ".png");
        options = smoke || assetSmoke || citySmoke || damageHudSmoke || killInfoSmoke || heroHudSmoke ? new SandboxOptions() : SandboxOptions.Load(optionsSmoke
            ? Path.Combine(Path.GetDirectoryName(reportPath), "options-test.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StrideAottg", "options.json"));
        cube = Content.Load<Model>("Cube");
        camera = Entity.Scene.Entities.First(e => e.Name == "Camera");
        var ambient = new Entity("Character fill light");
        ambient.Add(new LightComponent { Type = new LightAmbient(), Intensity = useCity ? 0.3f : 0.65f });
        Entity.Scene.Entities.Add(ambient);
        int packageIndex = Array.IndexOf(args, "--package");
        string packagePath = packageIndex >= 0 && packageIndex + 1 < args.Length
            ? args[packageIndex + 1] : Path.Combine(AppContext.BaseDirectory, "CharacterPackage");
        var package = CharacterPackage.Load(packagePath);
        int scaleIndex = Array.IndexOf(args, "--scale");
        if (scaleIndex >= 0) worldScale = float.Parse(args[scaleIndex + 1], System.Globalization.CultureInfo.InvariantCulture);
        new CharacterConfig { WorldScale = worldScale }.Validate();
        if (useCity)
        {
            var sunlight = Entity.Scene.Entities.First(e => e.Name == "Sun").Get<LightComponent>();
            sunlight.Intensity = 1.15f;
            var sun = (LightDirectional)sunlight.Type;
            sun.Color = new ColorRgbProvider(new Color3(1f, 0.95f, 0.86f));
            sun.Shadow.Enabled = true;
            sun.Shadow.Size = LightShadowMapSize.Large;
            sun.Shadow.Filter = new LightShadowMapFilterTypePcf();
            sun.Shadow.CascadeCount = LightShadowMapCascadeCount.FourCascades;
            sun.Shadow.StabilizationMode = LightShadowMapStabilizationMode.ViewSnapping;
            // Keep useful street detail rather than spreading shadows over the 3000-unit far plane.
            sun.Shadow.DepthRange.IsAutomatic = false;
            sun.Shadow.DepthRange.ManualMinDistance = 0.1f * worldScale;
            sun.Shadow.DepthRange.ManualMaxDistance = 650f * worldScale;
            sun.Shadow.DepthRange.IsBlendingCascades = true;
            sun.Shadow.BiasParameters.NormalOffsetScale *= worldScale;
        }
        var cameraComponent = camera.Get<CameraComponent>();
        cameraComponent.NearClipPlane = 0.1f * worldScale;
        cameraComponent.FarClipPlane = (useCity ? 3000 : 500) * worldScale;
        var floor = Entity.Scene.Entities.First(e => e.Name == "Floor");
        floor.Transform.Position *= worldScale;
        floor.Transform.Scale *= worldScale;
        world = new StrideWorld(package.Capsule.Radius, package.Capsule.Height, worldScale);
        if (useCity)
        {
            Entity.Scene.Entities.Remove(floor);
            city = new CityMap(GraphicsDevice, Entity.Scene, Path.Combine(AppContext.BaseDirectory, "CityPackage"), worldScale);
            world.City = city.Collision;
        }
        player = new Entity("AOTTG runtime character");
        player.Transform.Scale = new Vector3(worldScale / package.MetersPerSourceUnit);
        Entity.Scene.Entities.Add(player);
        characterRenderer = new RuntimeCharacterRenderer(GraphicsDevice, player, package);
        for (int n = 0; n < 2; n++)
        {
            guns[n] = Box("Example gun attachment", Vector3.Zero, new Vector3(0.12f, 0.12f, 0.5f) * worldScale, "TargetMaterial");
            gasJets[n] = Box("Gas jet", Vector3.Zero, new Vector3(0.08f, 0.08f, 0.4f) * worldScale, "RopeMaterial");
        }
        aimMarker = Box("Aim marker", Vector3.Zero, new Vector3(0.35f * worldScale), "RopeMaterial");
        if (!useCity)
        {
        AddTarget(new Vec3(0, 12, 40), new Vector3(10, 24, 6));
        AddTarget(new Vec3(-22, 9, 28), new Vector3(6, 18, 6));
        AddTarget(new Vec3(22, 9, 28), new Vector3(6, 18, 6));
        AddTarget(new Vec3(16, 10, 48), new Vector3(3, 6, 3), true);
        AddTarget(new Vec3(-4, 1.5f, 5), new Vector3(2, 3, 2));
        AddTarget(new Vec3(0, 3, 65), new Vector3(12, 1, 8));
        // Ground markings make movement readable without introducing external assets.
        for (int z = -80; z <= 100; z += 10)
            Box("Ground marker", new Vector3(0, 0.025f, z) * worldScale, new Vector3(80, 0.025f, 0.08f) * worldScale, "TargetMaterial");
        }
        else
        {
            AddCityTargets();
        }
        heroAudio = new HeroAudio(name => Content.Load<Stride.Audio.Sound>(name));
        smokeTrail = new SmokeTrailRenderer(Game.GraphicsDevice, Entity.Scene, SmokePackage.Load(Path.Combine(AppContext.BaseDirectory, "SmokePackage")));
        Reset();
        optionsMenu = new OptionsMenu((Game)Game, Entity.Scene, Content.Load<Stride.Graphics.SpriteFont>("OptionsFont"), options,
            () => { options.ApplyCamera(hero.Camera.Config); options.ApplyGameplay(motor.Config); hero.Input.Reset(); focused = false; waitForInputRelease = true; }, ToggleFullscreen);
        heroHudView = new HeroHudView(Entity.Scene, GraphicsDevice);
        damageHudView = new DamageHudView(Entity.Scene, GraphicsDevice);
        killInfoView = new KillInfoView(Entity.Scene, GraphicsDevice);
        world.TargetKilled += (target, hit) => killInfoView.Show(false, presets[presetIndex].Name, true, target.Name,
            hit.Damage, GraphicsDevice.Presenter.BackBuffer.Height);
        Game.Window.AllowUserResizing = true;
        Game.Window.FullscreenIsBorderlessWindow = true;
        Game.Window.Deactivated += (_, _) => WindowDeactivated();
        Game.Window.Activated += (_, _) => WindowActivated();
        // Keep SDL's own fullscreen shortcut (Alt+Enter) on the monitor's native mode as well.
        var startupMode = TargetOutput()?.CurrentDisplayMode;
        if (startupMode is { Width: > 0, Height: > 0 }) Game.Window.PreferredFullscreenSize = new Int2(startupMode.Width, startupMode.Height);
        if (!smoke && !assetSmoke && !damageHudSmoke && !killInfoSmoke && !heroHudSmoke && options.Fullscreen) pendingFullscreen = true;
        Game.Window.Title = useCity ? "AoTTG The City - Stride demo" : "LibAottg HERO - Stride reference host";
        if (optionsSmoke) StartOptionsSmoke();
    }

    private Entity Box(string name, Vector3 position, Vector3 size, string material)
    {
        var entity = new Entity(name);
        entity.Transform.Position = position;
        entity.Transform.Scale = size;
        var model = new ModelComponent(cube);
        model.Materials[0] = Content.Load<Material>(material);
        entity.Add(model);
        Entity.Scene.Entities.Add(entity);
        return entity;
    }

    private void AddTarget(Vec3 center, Vector3 size, bool moving = false, string targetName = null, bool infiniteHealth = false, bool oneHit = false)
    {
        center *= worldScale; size *= worldScale;
        Vector3 position = StrideWorld.ToStride(center);
        var entity = Box(targetName ?? (moving ? "Moving hook target" : "Training target"), position, size, "TargetMaterial");
        var target = world.AddTarget(center, new Vec3(size.X, size.Y, size.Z), moving, infiniteHealth, oneHit);
        target.Name = targetName ?? (moving ? "Moving Titan" : "Titan");
        targets.Add((target, entity));
    }

    private void AddCityTargets()
    {
        // Probe the imported collision mesh so targets sit above actual streets and roofs.
        // The source-unit locations cover the playable city instead of clustering at spawn.
        var probes = new (float X, float Z)[]
        {
            (-420, -980), (0, -980), (420, -980),
            (-620, -760), (-210, -760), (250, -760), (650, -760),
            (-760, -520), (-340, -520), (330, -520), (760, -520),
            (-500, -300), (0, -300), (520, -300)
        };
        int added = 0;
        foreach (var (x, z) in probes)
        {
            var probe = new Vec3(x, 400, z) * worldScale;
            if (!city.Collision.Raycast(probe, -Vec3.Up, 700 * worldScale, out var surface)) continue;
            var center = (surface.Point + Vec3.Up * (7 * worldScale)) / worldScale;
            AddTarget(center, new Vector3(3, 6, 3), targetName: $"City Target {++added}", oneHit: true);
        }
    }

    private void Reset()
    {
        damageHudView?.Reset();
        killInfoView?.Reset();
        foreach (var effect in effects) Entity.Scene.Entities.Remove(effect.Entity);
        effects.Clear();
        var config = presets[presetIndex].ToConfig(); config.WorldScale = worldScale;
        // Retain the recorded smoke's original tuning; interactive play uses the selected source preset.
        if (smoke) config = new CharacterConfig { WorldScale = worldScale };
        if (!smoke && !assetSmoke) options.ApplyGameplay(config);
        var cameraConfig = hero?.Camera?.Config ?? new HeroCameraConfig();
        if (!smoke && !assetSmoke) options.ApplyCamera(cameraConfig);
        bool poseEnabled = hero?.ProceduralPose ?? true;
        hero = new HeroController(config, world) { Presentation = characterRenderer.Hero, Camera = new HeroCamera(cameraConfig), ProceduralPose = poseEnabled };
        characterRenderer.Hero.ProceduralAim = poseEnabled;
        motor = hero.Motor;
        hero.Event += PresentEvent;
        hero.Hit += hit =>
        {
            damageHudView?.ShowDamage(hit.Damage);
        };
        if (heroAudio != null) { heroAudio.Stop(); hero.SoundRequested += heroAudio.Play; }
        hero.SetEquipment(equipment);
        hero.Respawn(city != null ? city.Spawns[0] : Vec3.Zero, true);
        hero.Presentation.Sample(motor, hero.Pose, default, 0);
        world.Events.Clear();
        world.ResetTargets();
        foreach (var rope in ropeModels) rope.Reset();
        smokeTrail?.Reset();
        yaw = 0;
        maxHeight = maxDistance = 0;
        ticks = 0;
        elapsed = 0;
    }

    public override void Update()
    {
        frames++;
        if (heroHudSmoke) { UpdateHeroHudSmoke(); return; }
        if (damageHudSmoke) { UpdateDamageHudSmoke(); return; }
        if (killInfoSmoke) { UpdateKillInfoSmoke(); return; }
        if (pendingFullscreen is { } requestedFullscreen)
        {
            pendingFullscreen = null; ApplyFullscreen(requestedFullscreen);
            options.Fullscreen = Game.Window.IsFullscreen; options.Save(); optionsMenu.Refresh(); focused = false;
        }
        if (optionsSmoke && UpdateOptionsSmoke()) return;
        if (!smoke && !assetSmoke && !citySmoke)
        {
            if ((!Game.IsActive || !Game.Window.Focused) && !optionsSmoke) { WindowDeactivated(); return; }
            if (optionsMenu.IsOpen)
            {
                Input.UnlockMousePosition(); Game.IsMouseVisible = true;
                if (!optionsMenu.IsCapturing && Pressed(SandboxAction.Fullscreen)) ToggleFullscreen();
                optionsMenu.Update(Input); return;
            }
            if (Input.IsKeyPressed(Keys.Escape))
            {
                optionsMenu.Open(); heroHudView?.Render(false,1,1); heroAudio.Stop(); Input.UnlockMousePosition(); Game.IsMouseVisible = true;
                hero.Input.Reset(); focused = false; return;
            }
            if (waitForInputRelease)
            {
                if (!Input.HasDownKeys && !Enum.GetValues<MouseButton>().Any(Input.IsMouseButtonDown)) waitForInputRelease = false;
                DrawStatus(); return;
            }
            if (Pressed(SandboxAction.Fullscreen)) ToggleFullscreen();
        }
        if (assetSmoke) { UpdateAssetSmoke(); return; }
        if (citySmoke) { UpdateCitySmoke(); return; }
        float frameDt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
        if (frameDt <= 0) return;
        damageHudView?.Update(frameDt, options.DamageScoreCardsEnabled && !smoke && !assetSmoke && !citySmoke,
            GraphicsDevice.Presenter.BackBuffer.Width, GraphicsDevice.Presenter.BackBuffer.Height,
            !Game.IsActive || pointerReleased);
        killInfoView?.Update(frameDt, options.KillInfoEnabled && !smoke && !assetSmoke && !citySmoke,
            GraphicsDevice.Presenter.BackBuffer.Width, GraphicsDevice.Presenter.BackBuffer.Height,
            !Game.IsActive || pointerReleased);
        // Render every scripted tick: shader/startup stalls must not skip the clips being checked.
        if (smoke) frameDt = FixedStep;
        if (!smoke)
        {
            if ((!Game.IsActive || !Game.Window.Focused) && !optionsSmoke)
            {
                WindowDeactivated(); return;
            }
            if (Pressed(SandboxAction.Cursor)) pointerReleased = !pointerReleased;
            if (Pressed(SandboxAction.Respawn)) Reset();
            if (Pressed(SandboxAction.Camera)) hero.Camera.Config.Mode = (HeroCameraMode)(((int)hero.Camera.Config.Mode + 1) % 3);
            if (Pressed(SandboxAction.Preset)) { presetIndex = (presetIndex + 1) % presets.Length; Reset(); }
            if (Pressed(SandboxAction.Equipment))
            {
                var desired = motor.Equipment == EquipmentMode.Blades ? EquipmentMode.Guns : EquipmentMode.Blades;
                if (hero.TrySetEquipment(desired)) equipment = motor.Equipment;
            }
            if (Pressed(SandboxAction.Pose)) { hero.ProceduralPose = !hero.ProceduralPose; characterRenderer.Hero.ProceduralAim = hero.ProceduralPose; }
            if (Pressed(SandboxAction.Tilt))
            {
                options.CameraTilt = !options.CameraTilt;
                options.ApplyCamera(hero.Camera.Config); options.Save();
            }
            if (Pressed(SandboxAction.SmokeTrail))
            {
                options.SmokeTrailEnabled = !options.SmokeTrailEnabled; options.Save();
                if (!options.SmokeTrailEnabled) smokeTrail.Reset();
            }
            if (Pressed(SandboxAction.Grab)) { if (motor.State == CharacterState.Grabbed) hero.EscapeGrab(motor.Position); else hero.Grab(); }
            if (Pressed(SandboxAction.Die)) hero.Die();
        }
        if (!smoke && Pressed(SandboxAction.Inspector))
        {
            inspectAnimations = !inspectAnimations;
            if (inspectAnimations) { Reset(); SelectInspectedClip(); }
            else hero.Synchronize();
        }
        if (inspectAnimations)
        {
            Input.UnlockMousePosition();
            characterRenderer.Hero.ProceduralAim = false;
            if (Pressed(SandboxAction.NextClip)) { inspectedClip++; SelectInspectedClip(); }
            if (Pressed(SandboxAction.PreviousClip)) { inspectedClip--; SelectInspectedClip(); }
            if (Pressed(SandboxAction.PauseClip)) pauseAnimation = !pauseAnimation;
            yaw += Axis(SandboxAction.LookLeft, SandboxAction.LookRight) * frameDt * 1.5f;
            if (!pauseAnimation) characterRenderer.Character.Advance(frameDt);
            UpdateInspector(); DrawStatus(); return;
        }
        characterRenderer.Hero.ProceduralAim = hero.ProceduralPose;
        elapsed += Math.Min(frameDt, hero.MaxFrameDelta);
        world.UpdateTargets(elapsed);
        foreach (var t in targets)
        {
            t.Entity.Transform.Position = StrideWorld.ToStride(t.Target.Center);
            t.Entity.Get<ModelComponent>().Enabled = t.Target.Active;
        }
        if (smoke)
        {
            if (ticks < 180) hero.Advance(frameDt, SmokeInput());
            else AdvanceFeatureSmoke(frameDt);
        }
        else
        {
            bool capture = !pointerReleased && (hero.Camera.Config.Mode == HeroCameraMode.Tps ||
                (hero.Camera.Config.Mode == HeroCameraMode.Wow && options.Down(Input, SandboxAction.Orbit)));
            bool changedCapture = capture != Input.IsMousePositionLocked;
            if (capture) { if (changedCapture) Input.LockMousePosition(true); }
            else Input.UnlockMousePosition();
            Game.IsMouseVisible = !capture;
            var raw = PlayerInput(focused && !changedCapture);
            hero.Advance(frameDt, raw);
            focused = true;
        }
        ticks++;
        maxHeight = Math.Max(maxHeight, motor.Position.Y); maxDistance = Math.Max(maxDistance, motor.Position.Length);
        playedClips.Add(motor.Animation.Clip);
        UpdateVisuals();
        smokeTrail.Trail.Enabled = options.SmokeTrailEnabled;
        smokeTrail.Advance(Math.Min(frameDt, hero.MaxFrameDelta), motor, hero.RenderPose, hero.Camera.Pose, Game.GraphicsContext.CommandList);
        DrawStatus();
        sawLean |= Math.Abs(hero.Pose.Roll) > 0.01f || Math.Abs(hero.Pose.Pitch) > 0.01f;
        if (smoke && ticks == 180) baselineChecks = GameplayChecks();
        if (smoke && (ticks == 435 || ticks == 530 || ticks == 690 || ticks == 715))
        {
            string name = ticks == 435 ? "blade-contact" : ticks == 530 ? "gun-aim" : ticks == 690 ? "moving-hooks" : "smoke-trail";
            using var viewOutput = File.Create(Path.Combine(Path.GetDirectoryName(reportPath), name + ".png"));
            GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, viewOutput, Stride.Graphics.ImageFileType.Png);
        }
        if (smoke && !captured && ticks >= 65 && frames > 2)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath));
            using var output = File.Create(screenshotPath);
            GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, output, Stride.Graphics.ImageFileType.Png);
            captured = true;
        }
        if (smoke && ticks >= 790) CompleteSmoke();
    }

    private bool Pressed(SandboxAction action) => options.Pressed(Input, action);
    private float Axis(SandboxAction negative, SandboxAction positive) => options.Axis(Input, negative, positive);
    private float ReelInput()
    {
        // HERO.GetReelAxis gives reel-in priority; wheel directions are full-strength actions.
        if (options.Down(Input, SandboxAction.ReelIn) || Input.MouseWheelDelta < 0) return -1;
        if (options.Down(Input, SandboxAction.ReelOut) || Input.MouseWheelDelta > 0) return 1;
        return 0;
    }
    private void ToggleFullscreen()
    {
        // UI clicks run during UI rendering; defer presenter changes until the next update.
        pendingFullscreen = !(pendingFullscreen ?? Game.Window.IsFullscreen);
    }
    private void ApplyFullscreen(bool fullscreen)
    {
        if (Game.Window.IsFullscreen == fullscreen) return;
        var graphics = ((Game)Game).GraphicsDeviceManager;
        // Borderless fullscreen keeps the desktop-sized presentation through Alt+Tab and avoids the
        // exclusive-mode transition that can restore the window as a normal window on focus loss.
        Game.Window.FullscreenIsBorderlessWindow = true;
        if (fullscreen)
        {
            windowedSize = new Int2(Math.Max(1, Game.Window.ClientBounds.Width), Math.Max(1, Game.Window.ClientBounds.Height));
            windowedPosition = Game.Window.Position;
            haveWindowedGeometry = true;
            // Stride ranks fullscreen display modes against PreferredBackBufferWidth/Height, so requesting the
            // windowed size picks an arbitrary window-sized mode (for example 1280x720 on a 2560x1440 monitor).
            // Fullscreen must target the monitor's own current display mode instead.
            var output = TargetOutput();
            var mode = output?.CurrentDisplayMode;
            var resolution = mode is { Width: > 0, Height: > 0 } ? new Int2(mode.Width, mode.Height) : new Int2(1920, 1080);
            Game.Window.PreferredFullscreenSize = resolution;
            graphics.PreferredBackBufferWidth = resolution.X;
            graphics.PreferredBackBufferHeight = resolution.Y;
            if (mode != null) graphics.PreferredRefreshRate = mode.RefreshRate;
            if (output != null) graphics.PreferredFullScreenOutputIndex = Array.IndexOf(GraphicsDevice.Adapter.Outputs, output);
        }
        else
        {
            var restoreSize = windowedSize;
            if (!haveWindowedGeometry || restoreSize.X <= 0 || restoreSize.Y <= 0) restoreSize = Game.Window.PreferredWindowedSize;
            if (restoreSize.X <= 0 || restoreSize.Y <= 0) restoreSize = new Int2(1280, 720);
            windowedSize = restoreSize;
            Game.Window.PreferredWindowedSize = restoreSize;
            graphics.PreferredBackBufferWidth = restoreSize.X;
            graphics.PreferredBackBufferHeight = restoreSize.Y;
        }
        graphics.IsFullScreen = fullscreen;
        graphics.ApplyChanges();
        if (!fullscreen)
        {
            // The presenter restores the client size, but the window itself can be left hidden or off its old
            // position after the mode switch, so put the saved windowed geometry back explicitly.
            Game.Window.SetSize(windowedSize);
            if (haveWindowedGeometry) Game.Window.Position = windowedPosition;
            Game.Window.Visible = true;
            haveWindowedGeometry = false;
        }
    }
    private void WindowDeactivated()
    {
        heroHudView?.Render(false,1,1);
        Input.UnlockMousePosition();
        Game.IsMouseVisible = true;
        focused = false;
        waitForInputRelease = true;
        hero?.Input.Reset();
    }
    private void WindowActivated()
    {
        focused = false;
        waitForInputRelease = true;
    }
    private Stride.Graphics.GraphicsOutput TargetOutput()
    {
        var outputs = GraphicsDevice.Adapter?.Outputs;
        if (outputs == null || outputs.Length == 0) return null;
        var position = Game.Window.Position;
        foreach (var output in outputs)
            if (output.DesktopBounds.Width > 0 && output.DesktopBounds.Contains(position.X, position.Y)) return output;
        return outputs[0];
    }
    private HeroInputFrame PlayerInput(bool mouseReady)
    {
        if (pointerReleased) return default;
        var raw = new HeroInputFrame { MoveX = Axis(SandboxAction.Left, SandboxAction.Right), MoveY = Axis(SandboxAction.Back, SandboxAction.Forward),
            Reel = ReelInput(),
            MouseDeltaX = mouseReady ? Input.AbsoluteMouseDelta.X : 0,
            MouseDeltaY = mouseReady ? Input.AbsoluteMouseDelta.Y : 0,
            HasPointer = true, PointerX = Input.MousePosition.X, PointerY = Input.MousePosition.Y,
            AspectRatio = Game.Window.ClientBounds.Width / (float)Math.Max(1, Game.Window.ClientBounds.Height),
            LookYaw = Axis(SandboxAction.LookLeft, SandboxAction.LookRight) * 90, LookPitch = Axis(SandboxAction.LookDown, SandboxAction.LookUp) * 60,
            OrbitHeld = options.Down(Input, SandboxAction.Orbit) };
        foreach (var binding in Bindings)
        {
            var slots = options[binding.Control].Where(b => !b.Empty && !(binding.Control == SandboxAction.Skill &&
                hero.Camera.Config.Mode == HeroCameraMode.Wow && options[SandboxAction.Orbit].Contains(b))).ToArray();
            bool held = slots.Any(b => b.Down(Input));
            if (held) raw.Held |= binding.Action;
            if (slots.Any(b => b.Pressed(Input)) && !slots.Any(b => b.Down(Input) && !b.Pressed(Input))) raw.Pressed |= binding.Action;
            if (!held && slots.Any(b => b.Released(Input))) raw.Released |= binding.Action;
        }
        return raw;
    }

    private CharacterInput SmokeInput() => new()
    {
        MoveY = ticks < 35 ? 1 : 0,
        LookDirection = new Vec3(0, 0, 1),
        AimDirection = (new Vec3(0, 12, 40) * worldScale - motor.Position).Normalized,
        Jump = new Button(ticks >= 10 && ticks < 20, ticks == 10, ticks == 20),
        Dash = new Button(false, ticks == 25, false),
        HookLeft = new Button(ticks >= 45 && ticks < 95, ticks == 45, ticks == 95),
        HookRight = new Button(ticks >= 55 && ticks < 95, ticks == 55, ticks == 95),
        Attack = new Button(false, ticks == 120, false)
    };

    private void AdvanceFeatureSmoke(float dt)
    {
        if (ticks == 180) { hero.Respawn(Vec3.Zero, true); featureChecks.Add("libraryRuntime"); }
        if (ticks < 210)
        {
            hero.Camera.Config.Mode = (HeroCameraMode)((ticks - 180) / 10);
            cameraModes.Add(hero.Camera.Config.Mode);
            hero.Advance(dt, new HeroInputFrame { MouseDeltaX = 2, MouseDeltaY = -1, HasPointer = true,
                PointerX = 0.8f, PointerY = 0.3f, AspectRatio = 16f / 9, OrbitHeld = true });
            if (hero.LastInput.AimDirection.Length > 0.99f && Math.Abs(hero.Camera.Pose.Yaw) > 0) featureChecks.Add("mouseAim");
            return;
        }
        if (ticks == 210) { hero.Respawn(new Vec3(0, 0, 35) * worldScale, true); hero.Camera.Reset(180); }
        if (ticks == 211) cameraBlocked = hero.Camera.Pose.Position.Z / worldScale < 37;
        if (ticks == 225) motor.ConsumeGas(20);
        if (ticks == 335) { if (world.Events.Contains(CharacterEvent.Supplied) && motor.Gas == motor.Config.GasCapacity) featureChecks.Add("supply"); motor.ConsumeBlade(10); }
        if (ticks == 410 && world.Events.Contains(CharacterEvent.Reloaded)) featureChecks.Add("bladeReload");
        if (ticks == 415) hero.Respawn(new Vec3(-4, 0, 3.4f) * worldScale, true);
        if (ticks == 465)
        {
            if (world.Hits.Any(h => !h.Gun) && motor.Blade < motor.Config.BladeCapacity) featureChecks.Add("bladeContacts");
            hero.Respawn(new Vec3(-4, 0, 0) * worldScale, true); hero.SetEquipment(EquipmentMode.Guns);
        }
        if (ticks == 535 && world.Events.Contains(CharacterEvent.GunFiredLeft) && world.Events.Contains(CharacterEvent.GunFiredRight)) featureChecks.Add("gunActions");
        if (ticks == 665)
        {
            if (motor.LeftGunLoaded && motor.RightGunLoaded && motor.LeftGunReserve < motor.Config.GunReservePerSide) featureChecks.Add("gunReload");
            if (world.Hits.Any(h => h.Gun)) featureChecks.Add("gunContacts");
            hero.Respawn(new Vec3(12, 8, 42) * worldScale); hero.SetEquipment(EquipmentMode.Blades);
        }
        if (ticks == 720)
        {
            if (world.MovingAnchorQueries > 0) featureChecks.Add("movingAnchor");
            hero.Grab();
            if (motor.State == CharacterState.Grabbed) featureChecks.Add("grab");
        }
        if (ticks == 725) { hero.EscapeGrab(motor.Position); if (motor.State != CharacterState.Grabbed) featureChecks.Add("escapeGrab"); }
        if (ticks == 730) { hero.Die(); if (motor.State == CharacterState.Dead) featureChecks.Add("death"); }
        if (ticks == 735) { hero.Respawn(Vec3.Zero, true); if (motor.State == CharacterState.Idle && motor.Gas == motor.Config.GasCapacity) featureChecks.Add("respawn"); }
        if (ticks == 740) VerifyPresetsAndSkills();
        if (ticks == 750) hero.Respawn(new Vec3(0, 5, 36.4f) * worldScale);
        var raw = new HeroInputFrame { HasView = true, ViewForward = new Vec3(0, 0, 1), AimOrigin = motor.Position + Vec3.Up * worldScale,
            AimDirection = new Vec3(0, 0, 1) };
        if (ticks == 220) raw.Pressed = HeroButtons.Flare;
        if (ticks == 230) raw.Pressed = HeroButtons.Supply;
        if (ticks == 335 || ticks == 555) raw.Pressed = HeroButtons.Reload;
        if (ticks == 416) raw.Pressed = HeroButtons.Attack;
        if (ticks >= 470 && ticks < 475) raw.Held = HeroButtons.Attack;
        if (ticks >= 515 && ticks < 520) raw.Held = HeroButtons.Skill;
        if (ticks >= 665 && ticks < 715)
        {
            raw.Held = HeroButtons.HookBoth | HeroButtons.Jump;
            raw.AimDirection = (world.Targets.Single(t => t.Moving).Center - raw.AimOrigin).Normalized;
            raw.Reel = ticks < 690 ? -1 : 1;
        }
        if (ticks == 750 || ticks == 751) raw.MoveY = 1;
        if (ticks == 753) { raw.MoveY = -1; raw.Pressed = HeroButtons.Dash; }
        if (ticks >= 754 && ticks <= 760) raw.MoveY = -1;
        if (ticks == 753 && motor.State == CharacterState.GroundDodge) featureChecks.Add("wallJumpState");
        hero.Advance(dt, raw);
        if (ticks == 753)
        {
            wallBurstSpeed = motor.Velocity.Z / worldScale;
            if (!motor.IsDashing && wallBurstSpeed < -40 && motor.Gas == motor.Config.GasCapacity * 0.96f)
                featureChecks.Add("wallBurstImpulse");
        }
        if (ticks == 760 && Math.Abs(motor.Velocity.Z / worldScale - wallBurstSpeed) < 0.002f)
            featureChecks.Add("wallBurstMomentum");
    }

    private void VerifyPresetsAndSkills()
    {
        bool valid = true;
        foreach (var preset in presets)
        {
            var config = preset.ToConfig(); config.WorldScale = worldScale;
            var demonstration = new HeroController(config, new CalibrationHeroWorld(worldScale: worldScale));
            demonstration.Respawn(Vec3.Zero, true);
            demonstration.Advance(FixedStep, new HeroInputFrame { Pressed = HeroButtons.Skill });
            valid &= demonstration.Motor.Config.GroundSpeed == preset.Speed / 10f && demonstration.Motor.SkillCooldownRemaining > 0;
        }
        if (valid) featureChecks.Add("presetsAndSkills");
        // Restore the shared rig after independent checks.
        hero.Presentation.Sample(motor, hero.RenderPose, hero.LastInput, 0);
    }

    private void UpdateVisuals()
    {
        aimMarker.Get<ModelComponent>().Enabled = true;
        player.Transform.Position = StrideWorld.ToStride(hero.RenderPose.Position);
        player.Transform.Rotation = StrideWorld.Rotation(hero.RenderPose);
        characterRenderer.ApplyEquipment();
        characterRenderer.Upload(Game.GraphicsContext.CommandList);
        var view = hero.Camera.Pose;
        camera.Transform.Position = StrideWorld.ToStride(view.Position);
        camera.Get<CameraComponent>().VerticalFieldOfView = view.FieldOfView;
        var cameraWorld = Matrix.Invert(Matrix.LookAtRH(camera.Transform.Position,
            camera.Transform.Position + StrideWorld.ToStride(view.Forward) * worldScale, StrideWorld.ToStride(view.Up)));
        camera.Transform.Rotation = Quaternion.RotationMatrix(cameraWorld);
        for (int n = 0; n < 2; n++)
        {
            var side = (HookSide)n;
            ropeModels[n].Sample(motor, side, characterRenderer.Hero.HookOrigin(motor, side), elapsed);
            UpdateRope(ropes[n], ropeModels[n].Points);
            guns[n].Get<ModelComponent>().Enabled = motor.Equipment == EquipmentMode.Guns && !characterRenderer.Hero.HasGunGeometry;
            if (characterRenderer.Hero.WeaponSegment(side, true, out var hand, out _))
            {
                Vec3 forward = hero.LastInput.AimDirection.Normalized;
                if (forward.LengthSquared < 0.1f) forward = hero.RenderPose.Forward;
                guns[n].Transform.Position = StrideWorld.ToStride(hand + forward * (0.2f * worldScale));
                guns[n].Transform.Rotation = Quaternion.RotationMatrix(Matrix.Invert(Matrix.LookAtRH(
                    guns[n].Transform.Position, guns[n].Transform.Position + StrideWorld.ToStride(forward) * worldScale, Vector3.UnitY)));
            }
            gasJets[n].Get<ModelComponent>().Enabled = motor.GasThrustActive;
            characterRenderer.Hero.TryNode(n == 0 ? "character_3dmg_gas_l(Clone)" : "character_3dmg_gas_r(Clone)", out var gas);
            gasJets[n].Transform.Position = StrideWorld.ToStride(gas - hero.RenderPose.Forward * (0.3f * worldScale));
            gasJets[n].Transform.Rotation = player.Transform.Rotation;
        }
        var input = hero.LastInput;
        aimMarker.Get<ModelComponent>().Enabled = false;
        var buffer = GraphicsDevice.Presenter.BackBuffer;
        var hud = heroHudView.State; hud.Read(motor); hud.ResourcesVisible=options.HeroHudEnabled;
        hud.AimVisible = options.CrosshairEnabled && !pointerReleased;
        hud.ShowDistance = options.CrosshairDistance; hud.ShowArrows = options.CrosshairArrows;
        hud.Speedometer = options.Speedometer; hud.CrosshairStyle=options.CrosshairStyle;
        // The camera uses pointer aiming outside TPS and centered aiming in TPS.
        hud.AimX = hero.Camera.Config.Mode == HeroCameraMode.Tps ? .5f : Input.MousePosition.X;
        hud.AimY = hero.Camera.Config.Mode == HeroCameraMode.Tps ? .5f : Input.MousePosition.Y;
        if (hud.AimVisible) hud.Aim(motor, input, world, view, (float)buffer.Width/buffer.Height);
        heroHudView.Render((options.HeroHudEnabled || options.CrosshairEnabled) && !smoke && !inspectAnimations,buffer.Width,buffer.Height);
        foreach (var effect in effects.ToArray())
            if (elapsed >= effect.Expires) { Entity.Scene.Entities.Remove(effect.Entity); effects.Remove(effect); }
    }

    private void UpdateInspector()
    {
        heroHudView?.Render(false,1,1);
        smokeTrail.Hide();
        characterRenderer.ShowAllPrimitives();
        aimMarker.Get<ModelComponent>().Enabled = false;
        player.Transform.Position = Vector3.Zero; player.Transform.Rotation = Quaternion.Identity;
        characterRenderer.Upload(Game.GraphicsContext.CommandList);
        camera.Transform.Position = new Vector3(MathF.Sin(yaw) * 4, 2.2f, MathF.Cos(yaw) * 4) * worldScale;
        camera.Transform.Rotation = Quaternion.RotationMatrix(Matrix.Invert(Matrix.LookAtRH(camera.Transform.Position,
            new Vector3(0, 1, 0) * worldScale, Vector3.UnitY)));
        foreach (var rope in ropes.SelectMany(r => r)) rope.Get<ModelComponent>().Enabled = false;
        foreach (var gun in guns) gun.Get<ModelComponent>().Enabled = false;
        foreach (var jet in gasJets) jet.Get<ModelComponent>().Enabled = false;
        camera.Get<CameraComponent>().VerticalFieldOfView = 50;
    }

    private void SelectInspectedClip()
    {
        var clips = characterRenderer.Character.Package.Clips;
        inspectedClip = (inspectedClip % clips.Count + clips.Count) % clips.Count;
        characterRenderer.Character.Play(clips[inspectedClip].Name, speed: clips[inspectedClip].CapturedSpeed,
            crossFadeSeconds: 0, restart: true);
    }

    public override void Cancel() { Input.UnlockMousePosition(); optionsMenu?.Dispose(); damageHudView?.Dispose(); heroHudView?.Dispose(); killInfoView?.Dispose(); heroAudio?.Dispose(); smokeTrail?.Dispose(); characterRenderer?.Dispose(); city?.Dispose(); }

    private void UpdateAssetSmoke()
    {
        smokeTrail.Hide();
        characterRenderer.ShowAllPrimitives();
        aimMarker.Get<ModelComponent>().Enabled = false;
        foreach (var rope in ropes.SelectMany(r => r)) rope.Get<ModelComponent>().Enabled = false;
        foreach (var gun in guns) gun.Get<ModelComponent>().Enabled = false;
        foreach (var jet in gasJets) jet.Get<ModelComponent>().Enabled = false;
        var character = characterRenderer.Character;
        var clips = character.Package.Clips;
        if (galleryWait > 0)
        {
            galleryWait--;
            if (galleryWait > 0) return;
            var clip = clips[galleryPose / 3];
            bool finite = character.Primitives.All(p => p.Positions.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)));
            galleryValid &= finite;
            string imagePath = null;
            if (new[] { "stand", "run", "attack1", "air_hook", "T-pose", "changeBlade", "supply" }.Contains(clip.Name))
            {
                imagePath = Path.Combine(Path.GetDirectoryName(reportPath), $"{clip.Name}-{galleryPose % 3}.png");
                using var output = File.Create(imagePath);
                GraphicsDevice.Presenter.BackBuffer.Save(Game.GraphicsContext.CommandList, output, Stride.Graphics.ImageFileType.Png);
            }
            galleryResults.Add(new { clip = clip.Name, time = character.Time, finite, imagePath });
            galleryPose++;
        }
        if (galleryPose >= clips.Count * 3)
        {
            bool passed = galleryValid && galleryResults.Count == clips.Count * 3 && characterRenderer.TextureCount == 7;
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new
            {
                passed, frames, poses = galleryResults.Count, clips = clips.Count, textures = characterRenderer.TextureCount,
                packageSha256 = character.Package.AssetSha256, results = galleryResults,
                scope = "All package clips sampled and rendered in Stride; no source-player visual acceptance."
            }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = passed ? 0 : 1;
            ((Stride.Engine.Game)Game).Exit(); return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        var next = clips[galleryPose / 3];
        float seconds = next.Duration * (galleryPose % 3) * 0.5f;
        character.Sample(next.Name, seconds);
        player.Transform.Position = Vector3.Zero; player.Transform.Rotation = Quaternion.Identity;
        characterRenderer.Upload(Game.GraphicsContext.CommandList);
        Vector3 center = new((character.BoundsMin.X + character.BoundsMax.X) / 2,
            (character.BoundsMin.Y + character.BoundsMax.Y) / 2, (character.BoundsMin.Z + character.BoundsMax.Z) / 2);
        center *= player.Transform.Scale.X;
        float size = Math.Max(2 * character.Package.MetersPerSourceUnit, (character.BoundsMax - character.BoundsMin).Length()) * player.Transform.Scale.X;
        float viewYaw = galleryPose % 3 == 1 ? MathF.PI : 0;
        camera.Transform.Position = center + new Vector3(MathF.Sin(viewYaw) * size * 1.4f, size * 0.22f, MathF.Cos(viewYaw) * size * 1.4f);
        camera.Transform.Rotation = Quaternion.RotationMatrix(Matrix.Invert(Matrix.LookAtRH(camera.Transform.Position, center, Vector3.UnitY)));
        DebugText.Print($"AOTTG asset runtime: {next.Name} at {seconds:F3}s | {galleryPose + 1}/{clips.Count * 3}", new Int2(20, 20));
        galleryWait = 2;
    }

    private void UpdateRope(List<Entity> segments, IReadOnlyList<Vec3> points)
    {
        while (segments.Count < Math.Max(0, points.Count - 1)) segments.Add(Box("Hook rope segment", Vector3.Zero, Vector3.One, "RopeMaterial"));
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            bool shown = i + 1 < points.Count && (points[i + 1] - points[i]).Length / worldScale > 0.0001f;
            segment.Get<ModelComponent>().Enabled = shown;
            if (!shown) continue;
            Vector3 a = StrideWorld.ToStride(points[i]), b = StrideWorld.ToStride(points[i + 1]);
            segment.Transform.Position = (a + b) * 0.5f;
            segment.Transform.Scale = new Vector3(0.025f * worldScale, 0.025f * worldScale, (b - a).Length());
            Vector3 up = Math.Abs(Vector3.Dot(Vector3.Normalize(b - a), Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
            segment.Transform.Rotation = Quaternion.RotationMatrix(Matrix.Invert(Matrix.LookAtRH(a, b, up)));
        }
    }

    private void PresentEvent(CharacterEvent kind)
    {
        if (kind != CharacterEvent.FlareFired && kind != CharacterEvent.SkillUsed && kind != CharacterEvent.GunFiredLeft &&
            kind != CharacterEvent.GunFiredRight && kind != CharacterEvent.AttackWindowOpened) return;
        Vec3 position = motor.Position + Vec3.Up * (1.5f * worldScale);
        if (kind == CharacterEvent.GunFiredLeft || kind == CharacterEvent.GunFiredRight)
            characterRenderer.Hero.WeaponSegment(kind == CharacterEvent.GunFiredLeft ? HookSide.Left : HookSide.Right, true, out position, out _);
        var effect = Box(kind.ToString(), StrideWorld.ToStride(position), new Vector3(0.25f * worldScale), "RopeMaterial");
        effects.Add((effect, elapsed + (kind == CharacterEvent.FlareFired ? 2 : 0.15f)));
    }

    private void DrawStatus()
    {
        DebugText.Print("LIBAOTTG HERO / STRIDE reference host", new Int2(20, 20));
        DebugText.Print($"{options.Label(SandboxAction.Forward)}/{options.Label(SandboxAction.Back)}/{options.Label(SandboxAction.Left)}/{options.Label(SandboxAction.Right)} move | {options.Label(SandboxAction.Jump)} jump/gas | {options.Label(SandboxAction.Dash)} burst | {options.Label(SandboxAction.Dodge)} dodge", new Int2(20, 45));
        DebugText.Print($"{options.Label(SandboxAction.HookLeft)}/{options.Label(SandboxAction.HookRight)} hooks | {options.Label(SandboxAction.HookBoth)} both | Wheel reel | {options.Label(SandboxAction.Attack)} attack | {options.Label(SandboxAction.Skill)} skill", new Int2(20, 65));
        DebugText.Print($"{options.Label(SandboxAction.Reload)} reload | {options.Label(SandboxAction.Supply)} supply | {options.Label(SandboxAction.Flare)} flare | {options.Label(SandboxAction.Respawn)} respawn", new Int2(20, 85));
        DebugText.Print($"Esc options | {options.Label(SandboxAction.Fullscreen)} fullscreen | {options.Label(SandboxAction.Inspector)} clips | {options.Label(SandboxAction.Camera)} camera | {options.Label(SandboxAction.Preset)} preset | {options.Label(SandboxAction.Equipment)} weapons", new Int2(20, 105));
        DebugText.Print($"{presets[presetIndex].Name} | {motor.Equipment} | Camera {hero.Camera.Config.Mode} | Pose {hero.ProceduralPose}" + (pointerReleased ? " | CURSOR FREE" : ""), new Int2(20, 135));
        DebugText.Print($"State {motor.State} | Gas {motor.Gas:F1} | Blade {motor.Blade:F1} | Pairs {motor.SpareBladePairs} | Skill {motor.SkillCooldownRemaining:F1}s", new Int2(20, 155));
        DebugText.Print($"Guns L:{motor.LeftGunLoaded}/{motor.LeftGunReserve} R:{motor.RightGunLoaded}/{motor.RightGunReserve} | Hits {world.Hits.Count} | Smoke {options.SmokeTrailEnabled} ({options.Label(SandboxAction.SmokeTrail)})", new Int2(20, 175));
        DebugText.Print($"Hooks L:{motor.LeftHookPhase} R:{motor.RightHookPhase} | Event {world.LastEvent}", new Int2(20, 195));
        DebugText.Print($"Clip {characterRenderer.Character.ClipName} | Time {characterRenderer.Character.Time:F2} | Targets {world.Targets.Count(t => t.Active)}/{world.Targets.Count}", new Int2(20, 215));
        if (city != null) DebugText.Print("Attack the highlighted city targets; each disappears and respawns after 5 seconds.", new Int2(20, 235));
        if (inspectAnimations) DebugText.Print($"CLIP INSPECTOR: {options.Label(SandboxAction.PreviousClip)}/{options.Label(SandboxAction.NextClip)} select | {options.Label(SandboxAction.PauseClip)} pause | {options.Label(SandboxAction.Inspector)} play | Esc options", new Int2(20, 245));
        else
        {
            var size = Game.Window.ClientBounds;
            var pointer = Input.MousePosition;
            var center = hero.Camera.Config.Mode == HeroCameraMode.Tps || smoke ? new Int2(size.Width / 2, size.Height / 2) : new Int2((int)(pointer.X * size.Width), (int)(pointer.Y * size.Height));
            DebugText.Print("+", center);
        }
    }

    private Dictionary<string, bool> GameplayChecks()
    {
        bool unityLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("UnityEngine", StringComparison.Ordinal));
        return new Dictionary<string, bool>
        {
            ["strideFramesUpdated"] = frames > 1,
            ["renderedFrameCaptured"] = captured && new FileInfo(screenshotPath).Length > 0,
            ["fixedTicksExecuted"] = ticks >= 180,
            ["movement"] = maxDistance > worldScale,
            ["jump"] = maxHeight > worldScale && world.Events.Contains(CharacterEvent.Jumped),
            ["airDash"] = world.Events.Contains(CharacterEvent.Dashed),
            ["leftHook"] = world.Events.Contains(CharacterEvent.HookAttachedLeft),
            ["rightHook"] = world.Events.Contains(CharacterEvent.HookAttachedRight),
            ["hookAudio"] = heroAudio.CueRequests.GetValueOrDefault(HeroSound.HookLaunch) > 0 && heroAudio.PlayingObservations == heroAudio.Requests,
            ["hookRelease"] = world.Events.Contains(CharacterEvent.HookReleasedLeft) && world.Events.Contains(CharacterEvent.HookReleasedRight),
            ["attack"] = world.Events.Contains(CharacterEvent.AttackStarted),
            ["gasConsumed"] = motor.Gas < motor.Config.GasCapacity,
            ["runtimePackageLoaded"] = characterRenderer.Character.Package.Primitives.Count == 22,
            ["embeddedTexturesLoaded"] = characterRenderer.TextureCount == 7,
            ["animationPlayback"] = playedClips.Contains("run") && playedClips.Any(c => c.StartsWith("attack")) && playedClips.Any(c => c.StartsWith("air_hook")) && characterRenderer.Character.ClipName == motor.Animation.Clip,
            ["skinnedFramesUploaded"] = characterRenderer.UploadedFrames > 2,
            ["solidCollision"] = world.CollisionCount > 0,
            ["noUnityAssemblyLoaded"] = !unityLoaded
        };
    }

    private void CompleteSmoke()
    {
        var checks = baselineChecks ?? GameplayChecks();
        foreach (var sound in Enum.GetValues<HeroSound>())
            checks[sound + "Audio"] = heroAudio.CueRequests.GetValueOrDefault(sound) > 0;
        checks["nativeAudioPlaying"] = heroAudio.Requests > 0 && heroAudio.PlayingObservations == heroAudio.Requests;
        foreach (string name in new[] { "libraryRuntime", "mouseAim", "supply", "bladeReload", "bladeContacts", "gunActions", "gunReload", "gunContacts", "movingAnchor", "grab", "escapeGrab", "death", "respawn", "presetsAndSkills", "wallJumpState", "wallBurstImpulse", "wallBurstMomentum" })
            checks[name] = featureChecks.Contains(name);
        checks["cameraModes"] = cameraModes.Count == 3;
        checks["cameraObstruction"] = cameraBlocked;
        checks["proceduralLean"] = sawLean;
        checks["flare"] = world.Events.Contains(CharacterEvent.FlareFired);
        checks["curvedRopes"] = ropes.Any(r => r.Count > 1);
        checks["originalSmokeTrail"] = smokeTrail.MaxObservedParticles > 0;
        bool passed = checks.Values.All(value => value);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            passed, checks, utc = DateTimeOffset.UtcNow, frames, ticks, maxHeight, maxDistance, screenshotPath,
            graphics = Stride.Graphics.GraphicsDevice.Platform.ToString(),
            strideAssembly = typeof(Game).Assembly.FullName,
            coreAssembly = typeof(CharacterMotor).Assembly.FullName,
            corePath = typeof(CharacterMotor).Assembly.Location,
            events = world.Events.Select(e => e.ToString()).OrderBy(e => e).ToArray(),
            gas = motor.Gas, blade = motor.Blade,
            packageSha256 = characterRenderer.Character.Package.AssetSha256,
            clips = playedClips.OrderBy(c => c).ToArray(), collisionCount = world.CollisionCount,
            hits = world.Hits.Select(h => new { targetId = h.TargetId, gun = h.Gun, side = h.Side.ToString(),
                point = new[] { h.Point.X, h.Point.Y, h.Point.Z }, speed = h.Speed }).ToArray(),
            movingAnchorQueries = world.MovingAnchorQueries,
            heroAudio = new { requests = heroAudio.Requests, playingObservations = heroAudio.PlayingObservations, cues = heroAudio.CueRequests.ToDictionary(e => e.Key.ToString(), e => e.Value) },
            smokeTrail = new { maxObservedParticles = smokeTrail.MaxObservedParticles, enabled = smokeTrail.Trail.Enabled },
            scope = "Portable runtime, cameras, poses, equipment, skills, lifecycle, combat and moving-anchor integration. Full rendered source parity unverified."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = passed ? 0 : 1;
        ((Stride.Engine.Game)Game).Exit();
    }
}
