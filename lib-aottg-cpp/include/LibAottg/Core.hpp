// SPDX-License-Identifier: MIT
#pragma once
#include "Support.hpp"
namespace LibAottg {
enum class CharacterState {
    Idle,
    Run,
    Jump,
    Fall,
    AirDodge,
    GroundDodge,
    WallRun,
    Attack,
    Reload,
    Supply,
    Grabbed,
    Dead,
    Land,
    Slide,
    RoofVault
};
enum class HookSide { Left, Right };
enum class CharacterEvent {
    Jumped,
    Dashed,
    Dodged,
    WallRunStarted,
    WallRunEnded,
    RoofVaulted,
    Grabbed,
    EscapedGrab,
    HookLaunchedLeft,
    HookLaunchedRight,
    HookAttachedLeft,
    HookAttachedRight,
    HookReleasedLeft,
    HookReleasedRight,
    AttackStarted,
    AttackEnded,
    GunFiredLeft,
    GunFiredRight,
    Reloaded,
    Supplied,
    FlareFired,
    SkillUsed,
    Died,
    HookMissedLeft,
    HookMissedRight,
    Landed,
    SlideStarted,
    WallJumped,
    AttackWindowOpened,
    AttackWindowClosed,
    BladesThrown
};
enum class HookPhase { Inactive, Flying, Attached, Retracting };
enum class EquipmentMode { Blades, Guns };
enum class HeroCameraMode { Original, Wow, Tps };
enum class HeroButtons {
    None = 0,
    Jump = 1,
    Dash = 2,
    Dodge = 4,
    HookLeft = 8,
    HookRight = 16,
    HookBoth = 32,
    Attack = 64,
    Skill = 128,
    Reload = 256,
    Supply = 512,
    Flare = 1024,
    Forward = 2048,
    Back = 4096,
    Left = 8192,
    Right = 16384
};
enum class HeroSound { HookLaunch, BladeSwing, AhssFire, BladeHit, Death };
enum class HeroQuery { Ground, Wall, HookAim, HookProjectile, Camera, Gun };
inline HeroButtons operator|(HeroButtons a, HeroButtons b) {
    return static_cast<HeroButtons>(static_cast<int>(a) | static_cast<int>(b));
}
inline HeroButtons operator&(HeroButtons a, HeroButtons b) {
    return static_cast<HeroButtons>(static_cast<int>(a) & static_cast<int>(b));
}
inline HeroButtons operator~(HeroButtons a) {
    return static_cast<HeroButtons>(~static_cast<int>(a));
}
inline HeroButtons &operator|=(HeroButtons &a, HeroButtons b) {
    return a = a | b;
}
struct CharacterConfig;
struct CharacterMotor;
struct Button;
struct CharacterInput;
struct CharacterContacts;
struct HookHit;
struct ICharacterWorld;
struct IHookGeometryWorld;
struct CharacterAnimation;
struct HeroAnimations;
struct HeroCameraConfig;
struct HeroCameraPose;
struct IHeroCameraWorld;
struct HeroCamera;
struct HeroController;
struct HeroInputFrame;
struct HeroInputMapper;
struct HeroPose;
struct HeroBodyPose;
struct HeroRope;
struct SmokeCurveKey;
struct HeroSmokeSettings;
struct HeroSmokeParticle;
struct HeroSmokeTrail;
struct HeroSoundCue;
struct WorldBox;
struct CollisionResult;
struct KinematicCollisionWorld;
struct IHeroCombatWorld;
struct IHeroAttackTargetWorld;
struct IHeroWorld;
struct HeroWorld;
struct CalibrationHeroWorld;
struct IHeroPresentation;
struct HeroWeaponSweep;
struct HeroHit;
struct WorldBox {
    WorldBox() = default;
    LibAottg::Vec3 Min{};
    LibAottg::Vec3 Max{};
    WorldBox(LibAottg::Vec3 min, LibAottg::Vec3 max);
    static bool Finite(LibAottg::Vec3 v);

  private:
    static bool Finite(float v);
};
struct CollisionResult {
    LibAottg::Vec3 Position{};
    LibAottg::Vec3 Velocity{};
    bool Collided{};
    bool Grounded{};
};
struct Button {
    Button() = default;
    bool Held{};
    bool Pressed{};
    bool Released{};
    Button(bool held, bool pressed, bool released);
};
struct CharacterInput {
    float MoveX{};
    float MoveY{};
    LibAottg::Vec3 LookDirection{};
    LibAottg::Button Jump{};
    LibAottg::Button Dash{};
    LibAottg::Button Dodge{};
    LibAottg::Button HookLeft{};
    LibAottg::Button HookRight{};
    LibAottg::Button Attack{};
    LibAottg::Button Reload{};
    LibAottg::Button Supply{};
    LibAottg::Button Flare{};
    LibAottg::Button Skill{};
    LibAottg::Button HookBoth{};
    LibAottg::Button Forward{};
    LibAottg::Button Back{};
    LibAottg::Button Left{};
    LibAottg::Button Right{};
    bool HasAimOrigin{};
    LibAottg::Vec3 AimOrigin{};
    bool AttackLeft{};
    bool SourceAttackSelection{};
    bool HasAttackTarget{};
    LibAottg::Vec3 AttackTarget{};
    float Reel{};
    LibAottg::Vec3 AimDirection{};
    bool HasAimTarget{};
    LibAottg::Vec3 AimTarget{};
};
struct CharacterContacts {
    bool Grounded{};
    LibAottg::Vec3 GroundNormal{};
    bool FrontWall{};
    bool UpperFrontClear{};
};
struct HookHit {
    HookHit() = default;
    LibAottg::Vec3 Point{};
    int TargetId{};
    LibAottg::Vec3 LocalPoint{};
    HookHit(LibAottg::Vec3 point);
};
struct CharacterAnimation {
    std::string Clip{};
    float Time{};
    float Speed{};
    float Duration{};
    float CrossFade{};
    bool Loop{};
    int Revision{};
    float NormalizedTime() const;
};
struct HeroSoundCue {
    HeroSoundCue() = default;
    LibAottg::HeroSound Sound{};
    LibAottg::Vec3 Position{};
    float Volume{};
    float Pitch{};
    HeroSoundCue(LibAottg::HeroSound sound, LibAottg::Vec3 position, float volume = 1, float pitch = 1);
};
struct HeroPose {
    LibAottg::Vec3 Position{};
    float Yaw{};
    float Pitch{};
    float Roll{};
    bool Leaning{};
    bool LeanLeft{};
    LibAottg::Vec3 Forward() const;
    LibAottg::Vec3 Up() const;
    LibAottg::Vec3 Rotate(LibAottg::Vec3 v) const;
};
struct HeroWeaponSweep {
    LibAottg::HookSide Side{};
    LibAottg::Vec3 PreviousStart{};
    LibAottg::Vec3 PreviousEnd{};
    LibAottg::Vec3 Start{};
    LibAottg::Vec3 End{};
    float Radius{};
    bool Gun{};
};
struct HeroHit {
    int TargetId{};
    LibAottg::Vec3 Point{};
    LibAottg::HookSide Side{};
    bool Gun{};
    float Speed{};
    Vec3 TargetVelocity{};
    float ScoreMultiplier{};
    int Damage{};
};
struct ICharacterWorld {
    virtual ~ICharacterWorld() = default;
    virtual bool TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                         LibAottg::HookSide side, LibAottg::HookHit &hit) = 0;
    virtual void OnCharacterEvent(LibAottg::CharacterEvent kind) = 0;
};
struct IHookGeometryWorld : public virtual LibAottg::ICharacterWorld {
    virtual ~IHookGeometryWorld() = default;
    virtual LibAottg::Vec3 HookOrigin(LibAottg::CharacterMotor *motor, LibAottg::HookSide side) = 0;
    virtual bool SweepHook(LibAottg::Vec3 start, LibAottg::Vec3 end, LibAottg::HookSide side,
                           LibAottg::HookHit &hit) = 0;
    virtual bool ResolveHookAnchor(int targetId, LibAottg::Vec3 localPoint, LibAottg::Vec3 &worldPoint) = 0;
};
struct IHeroCameraWorld {
    virtual ~IHeroCameraWorld() = default;
    virtual bool SweepCamera(LibAottg::Vec3 pivot, LibAottg::Vec3 position, float radius,
                             LibAottg::Vec3 &safePosition) = 0;
};
struct IHeroCombatWorld {
    virtual ~IHeroCombatWorld() = default;
    virtual void CollectWeaponHits(LibAottg::HeroWeaponSweep sweep, std::vector<LibAottg::HeroHit> &hits) = 0;
    virtual void OnHeroHit(LibAottg::HeroHit hit) = 0;
};
struct IHeroAttackTargetWorld {
    virtual ~IHeroAttackTargetWorld() = default;
    virtual bool TryAttackTarget(LibAottg::CharacterMotor *motor, LibAottg::CharacterInput input,
                                 LibAottg::Vec3 &point) = 0;
};
struct IHeroWorld : public virtual LibAottg::IHookGeometryWorld {
    virtual ~IHeroWorld() = default;
    virtual LibAottg::CharacterContacts GetContacts(LibAottg::CharacterMotor *motor,
                                                    LibAottg::HeroPose pose) = 0;
    virtual LibAottg::CollisionResult Move(LibAottg::CharacterMotor *motor, LibAottg::Vec3 start,
                                           LibAottg::HeroPose proposedPose, float dt) = 0;
};
struct IHeroPresentation {
    virtual ~IHeroPresentation() = default;
    virtual void Sample(LibAottg::CharacterMotor *motor, LibAottg::HeroPose pose,
                        LibAottg::CharacterInput input, float blendDelta) = 0;
    virtual LibAottg::Vec3 Head() const = 0;
    virtual LibAottg::Vec3 HookOrigin(LibAottg::CharacterMotor *motor, LibAottg::HookSide side) = 0;
    virtual bool WeaponSegment(LibAottg::HookSide side, bool gun, LibAottg::Vec3 &start,
                               LibAottg::Vec3 &end) = 0;
};
struct CharacterConfig {
    float GroundSpeed = 10.0f;
    float AirAcceleration = 20.0f;
    float Gravity = 20.0f;
    float JumpSpeed = 8.0f;
    float DashSpeed = 40.0f;
    float DashDuration = 0.5f;
    float DashCooldown = 0.2f;
    float DashGasFraction = 0.04f;
    float GroundDodgeSpeed = 24.0f;
    float GroundDodgeDuration = 1.1f;
    float WallRunSpeed = 10.0f;
    float WallRunMaxDuration = 1.0f;
    float RoofVaultSpeed = 8.0f;
    float GasCapacity = 100.0f;
    float GasUsePerSecond = 0.2f;
    bool ImprovedRaycasts = false;
    float HookRange = 10000.0f;
    float HookPullAcceleration = 20.0f;
    float HookGasUsePerSecond = 0.2f;
    float HookReleaseGrace = 0.3f;
    float BladeCapacity = 100.0f;
    int SpareBladePairs = 5;
    float AttackDuration = 0.5f;
    float ReloadDuration = 1.7500001f / 1.2f;
    float SupplyDuration = 2.0000002f;
    int GunReservePerSide = 7;
    float FlareCooldown = 30.0f;
    float SkillCooldown = 1.5f;
    float MaxStep = 0.1f;
    float WorldScale = 1.0f;
    std::string SkillId = "mikasa";
    float Mass = 0.5f;
    float MaxGroundVelocityChange = 10.0f;
    float HookProjectileSpeed = 150.0f;
    float HookMissTimeout = 0.8f;
    float HookRetractDuration = 0.5f;
    float HookLaunchGasCost = 0.2f;
    float HookedGravity = 10.0f;
    float DashDamping = 1.7f;
    float DoubleTapWindow = 0.2f;
    bool DoubleTapDash = true;
    bool InfiniteGas{};
    bool AutoWallJump{};
    bool NoForcedToRoof{};
    bool NoStopJump{};
    float AttackWindowStart = 0.5f;
    float AttackWindowEnd = 0.85f;
    std::string StandAnimation = "stand";
    std::string RunAnimation = "run";
    void Validate();
};
struct HeroAnimations {
    static float Duration(std::string clip);
    static float Speed(std::string clip);
};
struct HeroBodyPose {
    static LibAottg::HeroPose Evaluate(LibAottg::CharacterMotor *motor);
    static float DeltaAngle(float from, float to);
};
struct HeroInputFrame {
    LibAottg::HeroButtons Held{};
    LibAottg::HeroButtons Pressed{};
    LibAottg::HeroButtons Released{};
    float MoveX{};
    float MoveY{};
    float Reel{};
    float MouseDeltaX{};
    float MouseDeltaY{};
    float PointerX{};
    float PointerY{};
    float AspectRatio{};
    bool HasPointer{};
    bool OrbitHeld{};
    float LookYaw{};
    float LookPitch{};
    bool HasView{};
    LibAottg::Vec3 ViewForward{};
    LibAottg::Vec3 AimOrigin{};
    LibAottg::Vec3 AimDirection{};
};
struct HeroInputMapper {
  private:
    LibAottg::HeroButtons previous{};

  public:
    void Reset();
    LibAottg::CharacterInput Sample(LibAottg::HeroInputFrame frame, LibAottg::Vec3 forward,
                                    LibAottg::Vec3 origin, LibAottg::Vec3 aim);

  private:
    static LibAottg::Button Button(LibAottg::HeroButtons held, LibAottg::HeroButtons pressed,
                                   LibAottg::HeroButtons released, LibAottg::HeroButtons action);
};
struct HeroCameraConfig {
    LibAottg::HeroCameraMode Mode = HeroCameraMode::Tps;
    float Sensitivity = 0.15f;
    bool InvertY{};
    bool Tilt = true;
    bool Obstruction = true;
    float Distance = 0.6f;
    float BaseDistance = 10;
    float DistanceMultiplier = 0.64f;
    float HeightMultiplier = 0.64f;
    float CollisionRadius = 0.1f;
    float CameraDistance() const;
    void SetCameraDistance(float value);
    float MouseSpeed() const;
    void SetMouseSpeed(float value);
    bool InvertMouse() const;
    void SetInvertMouse(bool value);
    bool CameraTilt() const;
    void SetCameraTilt(bool value);

  private:
    static void ValidateSetting(float value, float min, float max, std::string name);

  public:
    void Validate();
};
struct HeroCameraPose {
    LibAottg::Vec3 Position{};
    LibAottg::Vec3 Forward{};
    LibAottg::Vec3 Up{};
    LibAottg::Vec3 AimDirection{};
    float FieldOfView{};
    float Yaw{};
    float Pitch{};
    float Roll{};
    bool CapturePointer{};
};
struct HeroCamera {
    HeroCamera(const HeroCamera &) = delete;
    HeroCamera &operator=(const HeroCamera &) = delete;
    HeroCamera(HeroCamera &&) = delete;
    HeroCamera &operator=(HeroCamera &&) = delete;

  private:
    std::unique_ptr<HeroCameraConfig> ownedConfig;

  public:
    LibAottg::HeroCameraConfig *Config{};
    LibAottg::HeroCameraPose Pose{};

  private:
    float yaw{};
    float pitch{};
    float roll{};

  public:
    HeroCamera(LibAottg::HeroCameraConfig *config = nullptr);
    void Reset(float yawDegrees = 0, float pitchDegrees = 0);
    void Look(float dt, LibAottg::HeroInputFrame input);
    LibAottg::HeroCameraPose Follow(float dt, LibAottg::CharacterMotor *motor, LibAottg::Vec3 root,
                                    LibAottg::Vec3 head, LibAottg::HeroInputFrame input,
                                    LibAottg::IHeroCameraWorld *world = nullptr);
    static LibAottg::Vec3 Direction(float yaw, float elevation);
    static float Angle(LibAottg::Vec3 a, LibAottg::Vec3 b);
};
struct CharacterMotor {
    CharacterMotor(const CharacterMotor &) = delete;
    CharacterMotor &operator=(const CharacterMotor &) = delete;
    CharacterMotor(CharacterMotor &&) = delete;
    CharacterMotor &operator=(CharacterMotor &&) = delete;

  public:
    struct WorldBoundary;

  private:
    std::unique_ptr<WorldBoundary> ownedBoundary;

  private:
    std::unique_ptr<CharacterConfig> ownedConfig;

  public:
    struct Rope;
    struct WorldBoundary;

  private:
    bool _attackReleased{};
    bool _bladesThrown{};
    bool _reloadEquipped{};
    bool _attackMove{};
    int _attackLoops{};
    float _speedBuff{};
    std::uint32_t _attackRandom = 0x6d2b79f5;
    std::string SelectBladeAttack(LibAottg::CharacterInput input);
    bool IsAction() const;
    void BeginAction(LibAottg::CharacterState state, std::string clip, float speed,
                     LibAottg::ICharacterWorld *world);
    void SetAttackWindow(bool active, LibAottg::ICharacterWorld *world);
    void CancelAction(LibAottg::ICharacterWorld *world);
    bool CanReload();
    void BeginReload(LibAottg::ICharacterWorld *world);
    void ThrowBlades(LibAottg::ICharacterWorld *world);

  public:
    void BladeHit(LibAottg::ICharacterWorld *world, int amount = 1);

  private:
    void FireGuns(bool both, LibAottg::CharacterInput input, LibAottg::ICharacterWorld *world);
    void UpdateAction(float dt, LibAottg::CharacterInput input, LibAottg::ICharacterWorld *world);
    void FinishAction(LibAottg::ICharacterWorld *world);
    void SkillPhysics(LibAottg::ICharacterWorld *world);
    void UseSkill(LibAottg::CharacterInput input, LibAottg::ICharacterWorld *world);
    void Play(std::string clip, bool loop, float speed = -1, float fade = 0.1f, float time = 0,
              bool restart = false);
    void AdvanceAnimation(float dt);
    void SelectLocomotion(LibAottg::Vec3 desired);
    LibAottg::CharacterConfig *_config{};
    LibAottg::CharacterState _actionState{};
    float _dashTime{};
    float _dashCooldown{};
    float _dashOriginSpeed{};
    float _flareCooldown{};
    float _skillCooldown{};
    bool _wasGrounded{};
    bool _hasPhysicsTick{};
    bool _jumpPending{};
    bool _roofImpulse{};
    float _wallRunTime{};
    LibAottg::CharacterAnimation _animation{};
    std::array<float, 4> _taps{-1, -1, -1, -1};
    float _frameClock{};
    float _currentSpeed{};
    LibAottg::Vec3 _pendingForce{};
    LibAottg::Vec3 _pendingImpulse{};
    LibAottg::Vec3 _pendingVelocityChange{};

  public:
    CharacterMotor(LibAottg::CharacterConfig *config = nullptr);
    LibAottg::CharacterConfig *Config() const;

  private:
    LibAottg::Vec3 _position{};
    LibAottg::Vec3 _velocity{};

  public:
    LibAottg::Vec3 Position() const;
    LibAottg::Vec3 Velocity() const;
    float Scale{};
    float FacingDegrees{};
    float FacingTargetDegrees{};
    LibAottg::CharacterState State{};
    bool Grounded{};
    bool IsDashing() const;
    bool Spinning{};
    bool GasThrustActive{};
    float Gas{};
    float Blade{};
    int SpareBladePairs{};
    bool BladesEquipped{};
    bool AttackHitboxActive{};
    LibAottg::EquipmentMode Equipment{};
    bool LeftGunLoaded{};
    bool RightGunLoaded{};
    int LeftGunReserve{};
    int RightGunReserve{};
    float FlareCooldownRemaining() const;
    float SkillCooldownRemaining() const;
    const LibAottg::CharacterAnimation &Animation() const;
    float FieldOfView{};
    void SetEquipment(LibAottg::EquipmentMode mode);
    void SetPose(LibAottg::Vec3 position, LibAottg::Vec3 velocity, float facingDegrees);
    void Step(float deltaTime, LibAottg::CharacterInput input, LibAottg::CharacterContacts contacts,
              LibAottg::ICharacterWorld *world);
    void AdvanceFrame(float dt, LibAottg::CharacterInput input, LibAottg::CharacterContacts contacts,
                      LibAottg::ICharacterWorld *world);
    void StepPhysics(float dt, LibAottg::CharacterInput input, LibAottg::CharacterContacts contacts,
                     LibAottg::ICharacterWorld *world);

  private:
    void ValidateStep(float dt, LibAottg::ICharacterWorld *world);
    void SetReferenceVelocity(LibAottg::Vec3 value);
    LibAottg::Vec3 Forward(LibAottg::CharacterInput input);
    LibAottg::Vec3 Desired(LibAottg::CharacterInput input);
    static float Yaw(LibAottg::Vec3 v);
    bool DoubleTap(LibAottg::CharacterInput input, LibAottg::Vec3 &direction);

  public:
    void ConsumeGas(float amount);
    void ConsumeBlade(float amount);
    void RefillGas();
    void Die(LibAottg::ICharacterWorld *world);
    void Grab(LibAottg::ICharacterWorld *world);

  private:
    void Stop(LibAottg::ICharacterWorld *world);

  public:
    void EscapeGrab(LibAottg::Vec3 position, LibAottg::ICharacterWorld *world);
    void Respawn(LibAottg::Vec3 position, bool grounded = false);
    // Host world transfer: clear movement/actions/hooks without refilling resources or cooldowns.
    void Teleport(LibAottg::Vec3 position, float facingDegrees);
    struct Rope {
        LibAottg::HookPhase Phase{};
        LibAottg::Vec3 Position{};
        LibAottg::Vec3 PreviousPosition{};
        LibAottg::Vec3 FlightVelocity{};
        LibAottg::Vec3 LocalPoint{};
        LibAottg::Vec3 RetractStart{};
        bool Special{};
        int TargetId{};
        float Age{};
        float SpecialTime{};
        int SpiralTicks{};
    };

  private:
    LibAottg::CharacterMotor::Rope _left{};
    LibAottg::CharacterMotor::Rope _right{};
    bool _skipGasThisTick{};
    bool _launchLeft{};
    bool _launchRight{};
    bool _leftHookedAtPhysicsStart{};
    bool _rightHookedAtPhysicsStart{};
    float _elapsedLeft{};
    float _elapsedRight{};
    LibAottg::Vec3 _launchPointLeft{};
    LibAottg::Vec3 _launchPointRight{};

  public:
    bool LeftHookAttached() const;
    bool RightHookAttached() const;
    LibAottg::HookPhase LeftHookPhase() const;
    LibAottg::HookPhase RightHookPhase() const;
    LibAottg::Vec3 LeftHookAnchor() const;
    LibAottg::Vec3 RightHookAnchor() const;
    bool LeftHookVisible() const;
    bool RightHookVisible() const;

  private:
    void UpdateHookFacing();
    float HookVelocityAngle(LibAottg::Vec3 delta);
    bool HookInputAllowed() const;
    void LaunchHooks(LibAottg::CharacterInput input, LibAottg::ICharacterWorld *world);
    void TryLaunch(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side, LibAottg::Vec3 rayOrigin,
                   LibAottg::Vec3 rayDirection, bool spread, LibAottg::ICharacterWorld *world,
                   float projectileSpeed = 0, bool special = false);
    bool TryHookAim(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                    LibAottg::HookSide side, LibAottg::ICharacterWorld *world, LibAottg::HookHit &target);
    void LaunchAt(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side, LibAottg::Vec3 rayOrigin,
                  LibAottg::HookHit target, bool spread, LibAottg::ICharacterWorld *world,
                  float projectileSpeed = 0, bool special = false);
    void AdvanceHooks(float dt, LibAottg::ICharacterWorld *world);
    void AdvanceHook(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side, float dt,
                     LibAottg::ICharacterWorld *world);
    void AdvanceRetraction(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side, float dt,
                           LibAottg::ICharacterWorld *world);
    void UpdateHookAnchor(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                          LibAottg::ICharacterWorld *world);
    void TickLaunchedHook(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side, bool held, float dt,
                          LibAottg::ICharacterWorld *world);
    bool RopeForce(LibAottg::CharacterMotor::Rope rope, bool otherLaunched, bool boost,
                   LibAottg::Vec3 &acceleration);
    void Release(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                 LibAottg::ICharacterWorld *world);
    void Disable(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                 LibAottg::ICharacterWorld *world);

  public:
    void ReleaseHook(LibAottg::HookSide side, LibAottg::ICharacterWorld *world);

  private:
    static LibAottg::Vec3 RotateTowards(LibAottg::Vec3 from, LibAottg::Vec3 to, float radians);
    void GroundMovement(LibAottg::Vec3 desired, LibAottg::CharacterInput input, float dt,
                        LibAottg::ICharacterWorld *world);
    void AirMovement(LibAottg::Vec3 desired, LibAottg::CharacterInput input,
                     LibAottg::CharacterContacts contacts, float dt, bool spin, LibAottg::Vec3 &acceleration,
                     LibAottg::ICharacterWorld *world);
    void WallJump(LibAottg::ICharacterWorld *world);
    static float DeltaAngle(float a, float b);
    void TurnTowards(float target, float factor);
    LibAottg::CharacterMotor::WorldBoundary *_world{};
    LibAottg::ICharacterWorld *BindWorld(LibAottg::ICharacterWorld *world);

  public:
    struct WorldBoundary : public virtual LibAottg::IHookGeometryWorld {
      private:
        LibAottg::CharacterMotor *_motor{};

      public:
        LibAottg::ICharacterWorld *Host{};
        WorldBoundary(LibAottg::CharacterMotor *motor);
        bool TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance, LibAottg::HookSide side,
                     LibAottg::HookHit &hit);
        LibAottg::Vec3 HookOrigin(LibAottg::CharacterMotor *motor, LibAottg::HookSide side);
        bool SweepHook(LibAottg::Vec3 start, LibAottg::Vec3 end, LibAottg::HookSide side,
                       LibAottg::HookHit &hit);
        bool ResolveHookAnchor(int targetId, LibAottg::Vec3 localPoint, LibAottg::Vec3 &point);
        void OnCharacterEvent(LibAottg::CharacterEvent kind);
    };
};
struct KinematicCollisionWorld : public virtual LibAottg::ICharacterWorld {
    std::vector<LibAottg::WorldBox> Boxes = std::vector<LibAottg::WorldBox>();
    std::function<void(LibAottg::CharacterEvent)> Event{};
    float Friction = 0.6f;

  private:
    float _radius{};
    float _height{};

  public:
    float WorldScale{};
    float Radius() const;
    float Height() const;
    inline static constexpr float Skin = 0.001f;
    KinematicCollisionWorld(float radius = 0.5f, float height = 1.7f, float worldScale = 1.0f);
    void OnCharacterEvent(LibAottg::CharacterEvent kind);
    bool TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance, LibAottg::HookSide side,
                 LibAottg::HookHit &hit);
    LibAottg::CharacterContacts Contacts(LibAottg::Vec3 feet, LibAottg::Vec3 forward);

  private:
    LibAottg::WorldBox ReferenceBox(LibAottg::WorldBox box);

  public:
    LibAottg::CollisionResult Move(LibAottg::Vec3 start, LibAottg::Vec3 target, LibAottg::Vec3 velocity);

  private:
    LibAottg::Vec3 Depenetrate(LibAottg::Vec3 feet);
    static void Choose(float value, LibAottg::Vec3 candidate, float &best, LibAottg::Vec3 &normal);
    bool Sweep(LibAottg::Vec3 feet, LibAottg::Vec3 motion, float &time, LibAottg::Vec3 &normal);
    LibAottg::WorldBox ExpandedSegment(LibAottg::WorldBox box);
    static LibAottg::Vec3 Closest(LibAottg::Vec3 p, LibAottg::WorldBox b);
    static float Axis(LibAottg::Vec3 v, int i);
    static LibAottg::Vec3 Unit(int i);
    static bool RoundedBoxSweep(LibAottg::Vec3 origin, LibAottg::Vec3 motion, LibAottg::WorldBox box,
                                float radius, float &time, LibAottg::Vec3 &normal);
    static void RootCandidate(LibAottg::Vec3 d, LibAottg::Vec3 m, LibAottg::Vec3 origin,
                              LibAottg::Vec3 motion, LibAottg::WorldBox box, float radius, float &best,
                              LibAottg::Vec3 &normal);
    static void Accept(float t, LibAottg::Vec3 origin, LibAottg::Vec3 motion, LibAottg::WorldBox box,
                       float radius, float &best, LibAottg::Vec3 &normal);
    static bool RayBox(LibAottg::Vec3 origin, LibAottg::Vec3 direction, LibAottg::WorldBox box, float &time);
};
struct HeroWorld : public virtual LibAottg::IHeroWorld,
                   public virtual LibAottg::IHeroCameraWorld,
                   public virtual LibAottg::IHeroCombatWorld {
    virtual bool Raycast(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                         LibAottg::HeroQuery query, LibAottg::HookHit &hit);
    virtual LibAottg::CollisionResult Move(LibAottg::CharacterMotor *motor, LibAottg::Vec3 start,
                                           LibAottg::HeroPose proposedPose, float dt);
    virtual LibAottg::CharacterContacts GetContacts(LibAottg::CharacterMotor *motor, LibAottg::HeroPose pose);
    virtual LibAottg::Vec3 HookOrigin(LibAottg::CharacterMotor *motor, LibAottg::HookSide side);
    virtual bool TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                         LibAottg::HookSide side, LibAottg::HookHit &hit);
    virtual bool SweepHook(LibAottg::Vec3 start, LibAottg::Vec3 end, LibAottg::HookSide side,
                           LibAottg::HookHit &hit);
    virtual bool ResolveHookAnchor(int targetId, LibAottg::Vec3 localPoint, LibAottg::Vec3 &worldPoint);
    virtual void OnCharacterEvent(LibAottg::CharacterEvent kind);
    virtual bool SweepCamera(LibAottg::Vec3 pivot, LibAottg::Vec3 position, float radius,
                             LibAottg::Vec3 &safePosition);
    virtual void CollectWeaponHits(LibAottg::HeroWeaponSweep sweep, std::vector<LibAottg::HeroHit> &hits);
    virtual void OnHeroHit(LibAottg::HeroHit hit);
};
struct CalibrationHeroWorld : public virtual LibAottg::HeroWorld {
  private:
    std::unique_ptr<KinematicCollisionWorld> ownedCollision;

  public:
    LibAottg::KinematicCollisionWorld *Collision{};
    CalibrationHeroWorld(float radius = 0.5f, float height = 1.7f, float worldScale = 1);
    virtual bool Raycast(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                         LibAottg::HeroQuery query, LibAottg::HookHit &hit);
    virtual LibAottg::CollisionResult Move(LibAottg::CharacterMotor *motor, LibAottg::Vec3 start,
                                           LibAottg::HeroPose proposedPose, float dt);
};
struct HeroController {
    HeroController(const HeroController &) = delete;
    HeroController &operator=(const HeroController &) = delete;
    HeroController(HeroController &&) = delete;
    HeroController &operator=(HeroController &&) = delete;

  private:
    std::unique_ptr<HeroWorld> defaultWorld;

  public:
    struct Services;

  private:
    std::unique_ptr<Services> ownedServices;

  private:
    std::unique_ptr<HeroInputMapper> ownedInput;

  private:
    std::unique_ptr<CharacterMotor> ownedMotor;

  public:
    struct Services;
    LibAottg::CharacterMotor *Motor{};
    LibAottg::HeroInputMapper *Input{};
    LibAottg::HeroCamera *Camera{};

  private:
    LibAottg::IHeroPresentation *presentation{};

  public:
    LibAottg::IHeroPresentation *Presentation() const;
    void SetPresentation(LibAottg::IHeroPresentation *value);
    bool ProceduralPose = true;
    bool Combat = true;
    bool SourceAttackSelection = true;
    float FixedStep{};
    float MaxFrameDelta = 0.1f;
    int PhysicsTicks{};
    LibAottg::HeroPose Pose{};
    LibAottg::HeroPose RenderPose{};
    LibAottg::CharacterInput LastInput{};
    std::function<void(LibAottg::CharacterEvent)> Event{};
    std::function<void(LibAottg::HeroHit)> Hit{};
    std::function<void(LibAottg::HeroSoundCue)> SoundRequested{};

  private:
    bool hookSoundPending{};
    LibAottg::IHeroWorld *world{};
    LibAottg::HeroController::Services *services{};
    double accumulator{};
    LibAottg::HeroPose previous{};
    std::unordered_set<int> struck = std::unordered_set<int>();
    std::unordered_set<int> shotTargets = std::unordered_set<int>();
    std::vector<LibAottg::HeroHit> hits = std::vector<LibAottg::HeroHit>();
    std::vector<LibAottg::HookSide> gunShots = std::vector<LibAottg::HookSide>();
    std::array<Vec3, 2> weaponStart{};
    std::array<Vec3, 2> weaponEnd{};
    std::array<bool, 2> weaponHistory{};
    bool attackWindow{};

  public:
    HeroController(LibAottg::CharacterConfig *config = nullptr, LibAottg::IHeroWorld *world = nullptr,
                   float fixedStep = 0.02f);
    void Respawn(LibAottg::Vec3 position, bool grounded = false);
    void Grab();
    void EscapeGrab(LibAottg::Vec3 position);
    void Die();
    void BladeHit(int amount = 1);
    void SetEquipment(LibAottg::EquipmentMode equipment);
    bool TrySetEquipment(LibAottg::EquipmentMode equipment);
    void Synchronize();

  private:
    void ClearWeaponHistory();

  public:
    void Advance(float frameDelta, LibAottg::HeroInputFrame raw);
    void Advance(float frameDelta, LibAottg::CharacterInput input);

  private:
    void ValidateDelta(float dt);
    void Advance(float dt, LibAottg::CharacterInput input, LibAottg::HeroInputFrame raw);
    LibAottg::HeroPose BodyPose();
    LibAottg::Vec3 Head();
    void Sample(LibAottg::HeroPose pose, float dt);
    void Raise(LibAottg::CharacterEvent kind);
    void ProcessBlades();
    void RequestSound(LibAottg::HeroSound sound);
    void ProcessGuns(LibAottg::CharacterInput input);
    void AcceptHits(LibAottg::IHeroCombatWorld *combat, LibAottg::HeroWeaponSweep sweep, bool blade);

  public:
    struct Services : public virtual LibAottg::IHookGeometryWorld {
      private:
        LibAottg::HeroController *owner{};

      public:
        Services(LibAottg::HeroController *owner);
        LibAottg::Vec3 HookOrigin(LibAottg::CharacterMotor *motor, LibAottg::HookSide side);
        bool TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance, LibAottg::HookSide side,
                     LibAottg::HookHit &hit);
        bool SweepHook(LibAottg::Vec3 start, LibAottg::Vec3 end, LibAottg::HookSide side,
                       LibAottg::HookHit &hit);
        bool ResolveHookAnchor(int id, LibAottg::Vec3 localPoint, LibAottg::Vec3 &point);
        void OnCharacterEvent(LibAottg::CharacterEvent kind);
    };
};
struct HeroRope {
  private:
    std::vector<LibAottg::Vec3> history = std::vector<LibAottg::Vec3>();
    std::vector<LibAottg::Vec3> points = std::vector<LibAottg::Vec3>();
    LibAottg::HookPhase previous{};

  public:
    const std::vector<LibAottg::Vec3> &Points() const;
    void Reset();
    void Sample(LibAottg::CharacterMotor *motor, LibAottg::HookSide side, LibAottg::Vec3 muzzle, float time);
};
struct SmokeCurveKey {
    SmokeCurveKey() = default;
    float Time{};
    float Value{};
    float InSlope{};
    float OutSlope{};
    SmokeCurveKey(float time, float value, float inSlope, float outSlope);
};
struct HeroSmokeSettings {
    float Rate = 60;
    float LifetimeMin = 3;
    float LifetimeMax = 4;
    float SpeedMin = 1;
    float SpeedMax = 1.35f;
    float SizeMin = 1;
    float SizeMax = 2;
    float RotationMin = -static_cast<float>(cs_pi);
    float RotationMax = static_cast<float>(cs_pi);
    float Radius = 0.31f;
    float ConeAngle = 21.44f;
    LibAottg::Vec3 LocalPosition = LibAottg::Vec3(0.112854f, 0.58514f, -0.343079f);
    LibAottg::Vec3 LocalDirection = Vec3::Up();
    LibAottg::Vec3 LocalRight = LibAottg::Vec3(1, 0, 0);
    LibAottg::Vec3 LocalUp = LibAottg::Vec3(0, 0, -1);
    LibAottg::Vec3 Color = LibAottg::Vec3(0.987006f, 0.987006f, 0.987006f);
    float Alpha = 70.0f / 255 * 2;
    int MaxParticles = 10000;
    std::vector<LibAottg::SmokeCurveKey> SizeCurve = {
        LibAottg::SmokeCurveKey(0, 0.41176465f, 11.783682f, 11.783682f),
        LibAottg::SmokeCurveKey(0.124273434f, 1, 0, 0),
        LibAottg::SmokeCurveKey(1, 0, 0.5305652f, 0.5305652f)};
    void Validate();
    float SizeAt(float t);
};
struct HeroSmokeParticle {
    LibAottg::Vec3 Position{};
    LibAottg::Vec3 Velocity{};
    LibAottg::Vec3 Color{};
    float Age{};
    float Lifetime{};
    float Rotation{};
    float Size{};
    float Alpha{};
    float StartSize{};
};
struct HeroSmokeTrail {
    HeroSmokeTrail(const HeroSmokeTrail &) = delete;
    HeroSmokeTrail &operator=(const HeroSmokeTrail &) = delete;
    HeroSmokeTrail(HeroSmokeTrail &&) = delete;
    HeroSmokeTrail &operator=(HeroSmokeTrail &&) = delete;

  private:
    std::unique_ptr<HeroSmokeSettings> ownedSettings;

  public:
    bool Enabled = true;
    LibAottg::HeroSmokeSettings *Settings{};
    const std::vector<LibAottg::HeroSmokeParticle> &Particles() const;

  private:
    std::vector<LibAottg::HeroSmokeParticle> particles = std::vector<LibAottg::HeroSmokeParticle>();

    std::uint32_t random{};
    std::uint32_t initialSeed{};
    double fraction{};
    bool hasEmitter{};
    LibAottg::Vec3 previousEmitter{};

  public:
    HeroSmokeTrail(LibAottg::HeroSmokeSettings *settings = nullptr, std::uint32_t seed = 1);
    void Reset();
    void Advance(float dt, LibAottg::CharacterMotor *motor, LibAottg::HeroPose pose);

  private:
    void UpdateAppearance(LibAottg::HeroSmokeParticle &p);
    float Next();
    float Range(float min, float max);
};
} // namespace LibAottg
