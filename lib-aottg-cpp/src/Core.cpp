// SPDX-License-Identifier: MIT
#include <LibAottg/Hud.hpp>
#include <LibAottg/Core.hpp>
namespace LibAottg {
WorldBox::WorldBox(LibAottg::Vec3 min, LibAottg::Vec3 max) {

    if (!Finite(min) || !Finite(max) || min.X >= max.X || min.Y >= max.Y || min.Z >= max.Z)
        throw std::invalid_argument(cs_string("Box bounds must be finite and ordered."));

    Min = min;

    Max = max;
}

bool WorldBox::Finite(LibAottg::Vec3 v) {

    return Finite(v.X) && Finite(v.Y) && Finite(v.Z);
}

bool WorldBox::Finite(float v) {

    return !std::isnan(v) && !std::isinf(v);
}
Button::Button(bool held, bool pressed, bool released) {

    Held = held;

    Pressed = pressed;

    Released = released;
}
HookHit::HookHit(LibAottg::Vec3 point) {

    Point = point;

    TargetId = 0;

    LocalPoint = point;
}
float CharacterAnimation::NormalizedTime() const {

    return (Duration > 0 ? Time / Duration : 0);
}
HeroSoundCue::HeroSoundCue(LibAottg::HeroSound sound, LibAottg::Vec3 position, float volume, float pitch) {

    Sound = sound;

    Position = position;

    Volume = volume;

    Pitch = pitch;
}
LibAottg::Vec3 HeroPose::Forward() const {

    return Rotate(LibAottg::Vec3(0, 0, 1));
}
LibAottg::Vec3 HeroPose::Up() const {

    return Rotate(Vec3::Up());
}

LibAottg::Vec3 HeroPose::Rotate(LibAottg::Vec3 v) const {

    float z = Roll * static_cast<float>(cs_pi) / 180, x = Pitch * static_cast<float>(cs_pi) / 180,
          y = Yaw * static_cast<float>(cs_pi) / 180;

    v = LibAottg::Vec3(v.X * static_cast<float>(std::cos(static_cast<double>(z))) -
                           v.Y * static_cast<float>(std::sin(static_cast<double>(z))),
                       v.X * static_cast<float>(std::sin(static_cast<double>(z))) +
                           v.Y * static_cast<float>(std::cos(static_cast<double>(z))),
                       v.Z);

    v = LibAottg::Vec3(v.X,
                       v.Y * static_cast<float>(std::cos(static_cast<double>(x))) -
                           v.Z * static_cast<float>(std::sin(static_cast<double>(x))),
                       v.Y * static_cast<float>(std::sin(static_cast<double>(x))) +
                           v.Z * static_cast<float>(std::cos(static_cast<double>(x))));

    return LibAottg::Vec3(v.X * static_cast<float>(std::cos(static_cast<double>(y))) +
                              v.Z * static_cast<float>(std::sin(static_cast<double>(y))),
                          v.Y,
                          -v.X * static_cast<float>(std::sin(static_cast<double>(y))) +
                              v.Z * static_cast<float>(std::cos(static_cast<double>(y))));
}

void CharacterConfig::Validate() {

    float values[] = {WorldScale,
                      GroundSpeed,
                      AirAcceleration,
                      Gravity,
                      JumpSpeed,
                      DashSpeed,
                      DashDuration,
                      DashCooldown,
                      DashGasFraction,
                      GroundDodgeSpeed,
                      GroundDodgeDuration,
                      WallRunSpeed,
                      WallRunMaxDuration,
                      RoofVaultSpeed,
                      GasCapacity,
                      GasUsePerSecond,
                      HookRange,
                      HookPullAcceleration,
                      HookGasUsePerSecond,
                      HookReleaseGrace,
                      BladeCapacity,
                      AttackDuration,
                      ReloadDuration,
                      SupplyDuration,
                      FlareCooldown,
                      SkillCooldown,
                      MaxStep,
                      Mass,
                      MaxGroundVelocityChange,
                      HookProjectileSpeed,
                      HookMissTimeout,
                      HookRetractDuration,
                      HookLaunchGasCost,
                      HookedGravity,
                      DashDamping,
                      DoubleTapWindow,
                      AttackWindowStart,
                      AttackWindowEnd};

    for (float value : values)
        if (std::isnan(value) || std::isinf(value) || value < 0)
            throw std::invalid_argument(cs_string("CharacterConfig") + std::string(": ") +
                                        cs_string("All tuning values must be finite and nonnegative."));

    if (WorldScale <= 0 || MaxStep <= 0 || GasCapacity <= 0 || BladeCapacity <= 0 || HookRange <= 0 ||
        Mass <= 0 || HookProjectileSpeed <= 0 || SpareBladePairs < 0 || GunReservePerSide < 0 ||
        AttackWindowEnd > 1 || AttackWindowStart > AttackWindowEnd || SkillId.empty() ||
        StandAnimation.empty() || RunAnimation.empty())
        throw std::invalid_argument(
            cs_string("CharacterConfig") + std::string(": ") +
            cs_string("Capacities, range and step must be positive; spare count cannot be negative."));
}

float HeroAnimations::Duration(std::string clip) {

    if (clip == "wallrun") {
        return 0.5f;
    } else if (clip == "toRoof") {
        return 0.683333337f;
    } else if (clip == "supply") {
        return 2.00000024f;
    } else if (clip == "stand_levi") {
        return 1.01666665f;
    } else if (clip == "stand") {
        return 1.01666665f;
    } else if (clip == "special_sasha") {
        return 1.00000012f;
    } else if (clip == "special_petra") {
        return 0.850000024f;
    } else if (clip == "special_marco_1") {
        return 1.33333337f;
    } else if (clip == "special_marco_0") {
        return 1.31666672f;
    } else if (clip == "special_armin") {
        return 2.85000014f;
    } else if (clip == "slide") {
        return 0.516666651f;
    } else if (clip == "salute") {
        return 1.00000012f;
    } else if (clip == "run_sasha") {
        return 0.266666681f;
    } else if (clip == "run_levi") {
        return 0.5f;
    } else if (clip == "run") {
        return 0.5f;
    } else if (clip == "onWall") {
        return 0.333333373f;
    } else if (clip == "jump") {
        return 0.516666651f;
    } else if (clip == "horse_run") {
        return 0.416666716f;
    } else if (clip == "horse_idle") {
        return 1.00000012f;
    } else if (clip == "horse_geton") {
        return 0.616666675f;
    } else if (clip == "horse_getoff") {
        return 0.450000048f;
    } else if (clip == "grabbed_jean") {
        return 1.10000002f;
    } else if (clip == "grabbed") {
        return 0.350000024f;
    } else if (clip == "dodge") {
        return 1.10000002f;
    } else if (clip == "dash_land") {
        return 0.833333373f;
    } else if (clip == "dash") {
        return 0.666666687f;
    } else if (clip == "changeBlade_air") {
        return 0.516666651f;
    } else if (clip == "changeBlade") {
        return 1.75000012f;
    } else if (clip == "attack5") {
        return 1.50000012f;
    } else if (clip == "attack4") {
        return 0.433333367f;
    } else if (clip == "attack3_2") {
        return 0.483333379f;
    } else if (clip == "attack3_1") {
        return 0.416666687f;
    } else if (clip == "attack2") {
        return 0.5f;
    } else if (clip == "attack1_hook_r2") {
        return 0.5f;
    } else if (clip == "attack1_hook_r1") {
        return 0.5f;
    } else if (clip == "attack1_hook_l2") {
        return 0.5f;
    } else if (clip == "attack1_hook_l1") {
        return 0.5f;
    } else if (clip == "attack1") {
        return 0.5f;
    } else if (clip == "air_rise") {
        return 1.00000012f;
    } else if (clip == "air_release") {
        return 0.5f;
    } else if (clip == "air_hook_r_just") {
        return 0.666666687f;
    } else if (clip == "air_hook_r") {
        return 0.666666687f;
    } else if (clip == "air_hook_l_just") {
        return 0.666666687f;
    } else if (clip == "air_hook_l") {
        return 0.666666687f;
    } else if (clip == "air_hook") {
        return 0.666666687f;
    } else if (clip == "air_fall") {
        return 1.00000012f;
    } else if (clip == "air_circle") {
        return 1.00000012f;
    } else if (clip == "air2_right") {
        return 1.00000012f;
    } else if (clip == "air2_left") {
        return 1.00000012f;
    } else if (clip == "air2_backward") {
        return 1.00000012f;
    } else if (clip == "air2") {
        return 0.333333373f;
    } else if (clip == "T-pose") {
        return 1.0f;
    } else if (clip == "AHSS_stand_gun") {
        return 1.00000012f;
    } else if (clip == "AHSS_shoot_r_air") {
        return 0.683333337f;
    } else if (clip == "AHSS_shoot_r") {
        return 0.683333337f;
    } else if (clip == "AHSS_shoot_l_air") {
        return 0.683333337f;
    } else if (clip == "AHSS_shoot_l") {
        return 0.683333337f;
    } else if (clip == "AHSS_shoot_both_air") {
        return 0.833333373f;
    } else if (clip == "AHSS_shoot_both") {
        return 0.833333373f;
    } else if (clip == "AHSS_hook_forward_r") {
        return 0.666666687f;
    } else if (clip == "AHSS_hook_forward_l") {
        return 0.666666687f;
    } else if (clip == "AHSS_hook_forward_both") {
        return 0.666666687f;
    } else if (clip == "AHSS_gun_reload_r_air") {
        return 0.76666671f;
    } else if (clip == "AHSS_gun_reload_r") {
        return 0.76666671f;
    } else if (clip == "AHSS_gun_reload_l_air") {
        return 0.76666671f;
    } else if (clip == "AHSS_gun_reload_l") {
        return 0.76666671f;
    } else if (clip == "AHSS_gun_reload_both_air") {
        return 0.76666671f;
    } else if (clip == "AHSS_gun_reload_both") {
        return 0.76666671f;
    } else {
        throw std::invalid_argument(cs_string((cs_string("Unknown HERO animation: ") + cs_string(clip))));
    }
}

float HeroAnimations::Speed(std::string clip) {

    if (clip == "attack5") {
        return 1.85f;
    } else if (clip == "changeBlade") {
        return 1.2f;
    } else if (clip == "air_release") {
        return 0.6f;
    } else if (clip == "changeBlade_air") {
        return 0.8f;
    } else if (clip == "AHSS_gun_reload_both") {
        return 0.38f;
    } else if (clip == "AHSS_gun_reload_l" || clip == "AHSS_gun_reload_r") {
        return 0.4f;
    } else if (clip == "AHSS_gun_reload_both_air" || clip == "AHSS_gun_reload_l_air" ||
               clip == "AHSS_gun_reload_r_air") {
        return 0.5f;
    } else {
        return 1.0f;
    }
}

LibAottg::HeroPose HeroBodyPose::Evaluate(LibAottg::CharacterMotor *motor) {

    auto pose = ([&] {
        auto value = LibAottg::HeroPose{};
        value.Position = motor->Position();
        value.Yaw = motor->FacingDegrees;
        return value;
    }());
    // HERO's grounded movement restores upright rotation after hook leaning.
    if (motor->Grounded)
        return pose;

    LibAottg::Vec3 velocity = motor->Velocity() / motor->Scale;

    bool attack = motor->State == CharacterState::Attack;

    if (motor->Equipment == EquipmentMode::Blades && attack && motor->Animation().Clip != "attack3_1" &&
        motor->Animation().Clip != "attack3_2") {

        float elevation =
            static_cast<float>(std::atan2(static_cast<double>(velocity.Y),
                                          static_cast<double>(Math3::Horizontal(velocity).Length()))) *
            180 / static_cast<float>(cs_pi);

        pose.Pitch = -elevation * (1 - HeroCamera::Angle(velocity, Math3::Direction(pose.Yaw)) / 90);

        return pose;
    }

    bool left = motor->LeftHookAttached(), right = motor->RightHookAttached();

    LibAottg::Vec3 midpoint = (motor->LeftHookAnchor() + motor->RightHookAnchor()) * 0.5f;

    bool almostSingle =
        left && right &&
        (((motor->LeftHookAnchor() - motor->RightHookAnchor()) / motor->Scale).Length() < 2 ||
         (HeroCamera::Angle(motor->Position() - midpoint, motor->Position() - motor->LeftHookAnchor()) < 30 &&
          HeroCamera::Angle(motor->Position() - midpoint, motor->Position() - motor->RightHookAnchor()) <
              30));

    if ((left != right) || almostSingle) {

        pose.Leaning = true;

        LibAottg::Vec3 delta =
            (((almostSingle || right ? motor->RightHookAnchor() : motor->LeftHookAnchor())) -
             motor->Position()) /
            motor->Scale;

        float anchorYaw =
            static_cast<float>(std::atan2(static_cast<double>(delta.X), static_cast<double>(delta.Z))) * 180 /
            static_cast<float>(cs_pi);

        float velocityYaw =
            static_cast<float>(std::atan2(static_cast<double>(velocity.X), static_cast<double>(velocity.Z))) *
            180 / static_cast<float>(cs_pi);

        float difference = DeltaAngle(anchorYaw, velocityYaw);

        pose.LeanLeft = difference > 0;

        float lean = HeroCamera::Angle(delta, Vec3::Up()) * 0.1f *
                         (1 + static_cast<float>(std::pow(static_cast<double>(velocity.Length()),
                                                          static_cast<double>(0.2f)))) +
                     std::abs(difference * 0.5f);

        if (!attack)
            lean = cs_min(lean, static_cast<float>(80));

        float multiplier = (motor->Equipment == EquipmentMode::Guns
                                ? 1
                                : (((left && difference < 0) || (!left && difference > 0)) ? 0.1f : 0.5f));

        pose.Roll = lean * ((difference >= 0 ? multiplier : -multiplier));

        if (!attack && motor->Equipment == EquipmentMode::Blades)
            pose.Pitch = -cs_min(velocity.Length() * 0.1f, static_cast<float>(20));
    }

    return pose;
}

float HeroBodyPose::DeltaAngle(float from, float to) {

    return cs_rem((cs_rem((to - from), 360) + 540), 360) - 180;
}

void HeroInputMapper::Reset() {

    previous = HeroButtons::None;
}

LibAottg::CharacterInput HeroInputMapper::Sample(LibAottg::HeroInputFrame frame, LibAottg::Vec3 forward,
                                                 LibAottg::Vec3 origin, LibAottg::Vec3 aim) {

    if (!WorldBox::Finite(LibAottg::Vec3(frame.MoveX, frame.MoveY, frame.Reel)))
        throw std::invalid_argument(cs_string("Input axes must be finite."));

    if (frame.HasView && (!WorldBox::Finite(frame.ViewForward) || !WorldBox::Finite(frame.AimOrigin) ||
                          !WorldBox::Finite(frame.AimDirection)))
        throw std::invalid_argument(cs_string("View and aim inputs must be finite."));

    float x = Math3::Clamp(frame.MoveX, static_cast<float>(-1), static_cast<float>(1)),
          y = Math3::Clamp(frame.MoveY, static_cast<float>(-1), static_cast<float>(1));

    auto held = frame.Held;

    if (x < -0.5f)
        held |= HeroButtons::Left;

    if (x > 0.5f)
        held |= HeroButtons::Right;

    if (y < -0.5f)
        held |= HeroButtons::Back;

    if (y > 0.5f)
        held |= HeroButtons::Forward;

    auto pressed = (held & ~previous) | frame.Pressed;

    auto released = (previous & ~held) | frame.Released;

    previous = held;

    return ([&] {
        auto value = LibAottg::CharacterInput{};
        value.MoveX = x;
        value.MoveY = y;
        value.Reel = Math3::Clamp(frame.Reel, static_cast<float>(-1), static_cast<float>(1));
        value.LookDirection = (frame.HasView ? frame.ViewForward : forward);
        value.HasAimOrigin = true;
        value.AimOrigin = (frame.HasView ? frame.AimOrigin : origin);
        value.AimDirection = (frame.HasView ? frame.AimDirection : aim);
        value.AttackLeft = frame.HasPointer && frame.PointerX < 0.5f;
        value.Jump = Button(held, pressed, released, HeroButtons::Jump);
        value.Dash = Button(held, pressed, released, HeroButtons::Dash);
        value.Dodge = Button(held, pressed, released, HeroButtons::Dodge);
        value.HookLeft = Button(held, pressed, released, HeroButtons::HookLeft);
        value.HookRight = Button(held, pressed, released, HeroButtons::HookRight);
        value.HookBoth = Button(held, pressed, released, HeroButtons::HookBoth);
        value.Attack = Button(held, pressed, released, HeroButtons::Attack);
        value.Skill = Button(held, pressed, released, HeroButtons::Skill);
        value.Reload = Button(held, pressed, released, HeroButtons::Reload);
        value.Supply = Button(held, pressed, released, HeroButtons::Supply);
        value.Flare = Button(held, pressed, released, HeroButtons::Flare);
        value.Forward = Button(held, pressed, released, HeroButtons::Forward);
        value.Back = Button(held, pressed, released, HeroButtons::Back);
        value.Left = Button(held, pressed, released, HeroButtons::Left);
        value.Right = Button(held, pressed, released, HeroButtons::Right);
        return value;
    }());
}

LibAottg::Button HeroInputMapper::Button(LibAottg::HeroButtons held, LibAottg::HeroButtons pressed,
                                         LibAottg::HeroButtons released, LibAottg::HeroButtons action) {

    return LibAottg::Button(static_cast<int>((held & action)) != 0, static_cast<int>((pressed & action)) != 0,
                            static_cast<int>((released & action)) != 0);
}
float HeroCameraConfig::CameraDistance() const {

    return Distance - 0.3f;
}
void HeroCameraConfig::SetCameraDistance(float value) {

    ValidateSetting(value, static_cast<float>(0), static_cast<float>(1), "CameraDistance");

    Distance = value + 0.3f;
}
float HeroCameraConfig::MouseSpeed() const {

    return Sensitivity;
}
void HeroCameraConfig::SetMouseSpeed(float value) {

    ValidateSetting(value, 0.01f, static_cast<float>(1), "MouseSpeed");

    Sensitivity = value;
}
bool HeroCameraConfig::InvertMouse() const {

    return InvertY;
}
void HeroCameraConfig::SetInvertMouse(bool value) {

    InvertY = value;
}
bool HeroCameraConfig::CameraTilt() const {

    return Tilt;
}
void HeroCameraConfig::SetCameraTilt(bool value) {

    Tilt = value;
}

void HeroCameraConfig::ValidateSetting(float value, float min, float max, std::string name) {

    if (std::isnan(value) || std::isinf(value) || value < min || value > max)
        throw std::invalid_argument(cs_string(name));
}

void HeroCameraConfig::Validate() {

    for (float v : std::array<float, 6>{Sensitivity, Distance, BaseDistance, DistanceMultiplier,
                                        HeightMultiplier, CollisionRadius})
        if (std::isnan(v) || std::isinf(v) || v < 0)
            throw std::invalid_argument(cs_string("HeroCameraConfig"));

    if (!cs_defined(Mode))
        throw std::invalid_argument(cs_string("Mode"));
}
HeroCamera::HeroCamera(LibAottg::HeroCameraConfig *config) {

    ownedConfig = std::make_unique<HeroCameraConfig>(config ? *config : HeroCameraConfig{});
    Config = ownedConfig.get();

    Config->Validate();
}

void HeroCamera::Reset(float yawDegrees, float pitchDegrees) {

    yaw = yawDegrees;

    pitch = pitchDegrees;

    roll = 0;
}

void HeroCamera::Look(float dt, LibAottg::HeroInputFrame input) {

    Config->Validate();

    if (dt < 0 || !WorldBox::Finite(LibAottg::Vec3(dt, input.MouseDeltaX, input.MouseDeltaY)) ||
        !WorldBox::Finite(LibAottg::Vec3(input.LookYaw, input.LookPitch, input.AspectRatio)) ||
        (input.HasPointer && !WorldBox::Finite(LibAottg::Vec3(input.PointerX, input.PointerY, 0))))
        throw std::invalid_argument(cs_string("Camera input must be finite and time nonnegative."));

    if (Config->Mode == HeroCameraMode::Original && input.HasPointer) {

        float edge = (input.PointerX < 0.4f ? input.PointerX - 0.4f
                                            : (input.PointerX > 0.6f ? input.PointerX - 0.6f : 0));
        // getSensitivityMultiWithDeltaTime uses 62 * dt in the original player.
        yaw += edge * 0.4f * 150 * 62 * dt * Config->MouseSpeed();

        pitch = 70 * (0.4f - input.PointerY);
    } else if (Config->Mode != HeroCameraMode::Wow || input.OrbitHeld) {

        yaw += input.MouseDeltaX * Config->Sensitivity;

        pitch -= input.MouseDeltaY * Config->Sensitivity * ((Config->InvertY ? -1 : 1));
    }

    yaw = cs_rem((yaw + input.LookYaw * dt), 360);

    pitch = Math3::Clamp(pitch + input.LookPitch * dt, static_cast<float>(-80), static_cast<float>(80));
}

LibAottg::HeroCameraPose HeroCamera::Follow(float dt, LibAottg::CharacterMotor *motor, LibAottg::Vec3 root,
                                            LibAottg::Vec3 head, LibAottg::HeroInputFrame input,
                                            LibAottg::IHeroCameraWorld *world) {

    LibAottg::Vec3 forward = Direction(yaw, pitch), right = Math3::Direction(yaw + 90),
                   up = Vec3::Cross(forward, right).Normalized();

    float targetRoll = 0;

    if (Config->Tilt && (motor->LeftHookAttached() || motor->RightHookAttached())) {

        LibAottg::Vec3 anchor =
            (motor->LeftHookAttached() && motor->RightHookAttached()
                 ? (motor->LeftHookAnchor() + motor->RightHookAnchor()) * 0.5f
                 : (motor->LeftHookAttached() ? motor->LeftHookAnchor() : motor->RightHookAnchor()));

        LibAottg::Vec3 delta = (anchor - root) / motor->Scale;

        float vertical = Vec3::Dot(delta, up), lateral = Vec3::Dot(delta, right);

        float angle = Angle(up * vertical, up * vertical + right * lateral);

        targetRoll =
            ((lateral >= 0 ? -angle : angle)) * Angle(delta, motor->Velocity() / motor->Scale) * 0.005f;
    }

    roll += (targetRoll - roll) * Math3::Clamp(dt * 2, static_cast<float>(0), static_cast<float>(1));

    float r = roll * static_cast<float>(cs_pi) / 180;

    LibAottg::Vec3 rolledRight = right * static_cast<float>(std::cos(static_cast<double>(r))) +
                                 up * static_cast<float>(std::sin(static_cast<double>(r)));

    LibAottg::Vec3 rolledUp = up * static_cast<float>(std::cos(static_cast<double>(r))) -
                              right * static_cast<float>(std::sin(static_cast<double>(r)));

    float offset = Config->Distance * (200 - motor->FieldOfView) / 150;

    LibAottg::Vec3 pivot =
        head + Vec3::Up() * ((Config->HeightMultiplier - (0.6f - Config->Distance) * 2) * motor->Scale);

    LibAottg::Vec3 position =
        pivot - forward * (Config->BaseDistance * Config->DistanceMultiplier * offset * motor->Scale);

    if (Config->Distance < 0.65f)
        position += rolledRight * (cs_max((0.6f - Config->Distance) * 2, 0.65f) * motor->Scale);

    LibAottg::Vec3 safe{};

    if (Config->Obstruction && world != nullptr &&
        world->SweepCamera(head, position, Config->CollisionRadius * motor->Scale, safe))
        position = safe;

    LibAottg::Vec3 aim = forward;

    if (Config->Mode != HeroCameraMode::Tps && input.HasPointer) {

        float tangent = static_cast<float>(std::tan(static_cast<double>(motor->FieldOfView * cs_pi / 360)));

        aim = (forward +
               rolledRight *
                   ((input.PointerX * 2 - 1) * tangent * ((input.AspectRatio > 0 ? input.AspectRatio : 1))) +
               rolledUp * ((1 - input.PointerY * 2) * tangent))
                  .Normalized();
    }

    Pose = ([&] {
        auto value = LibAottg::HeroCameraPose{};
        value.Position = position;
        value.Forward = forward;
        value.Up = rolledUp;
        value.AimDirection = aim;
        value.FieldOfView = motor->FieldOfView;
        value.Yaw = yaw;
        value.Pitch = pitch;
        value.Roll = roll;
        value.CapturePointer =
            Config->Mode == HeroCameraMode::Tps || (Config->Mode == HeroCameraMode::Wow && input.OrbitHeld);
        return value;
    }());

    return Pose;
}

LibAottg::Vec3 HeroCamera::Direction(float yaw, float elevation) {

    float p = elevation * static_cast<float>(cs_pi) / 180;

    return Math3::Direction(yaw) * static_cast<float>(std::cos(static_cast<double>(p))) +
           Vec3::Up() * static_cast<float>(std::sin(static_cast<double>(p)));
}

float HeroCamera::Angle(LibAottg::Vec3 a, LibAottg::Vec3 b) {

    float n = a.Length() * b.Length();

    return (n <= 0.000001f ? 0
                           : static_cast<float>(std::acos(static_cast<double>(Math3::Clamp(
                                 Vec3::Dot(a, b) / n, static_cast<float>(-1), static_cast<float>(1))))) *
                                 180 / static_cast<float>(cs_pi));
}

std::string CharacterMotor::SelectBladeAttack(LibAottg::CharacterInput input) {

    if (input.SourceAttackSelection) {

        auto pose = HeroBodyPose::Evaluate(this);

        if (pose.Leaning) {

            bool left = input.MoveX < 0 || (input.MoveX == 0 && pose.LeanLeft);

            _attackRandom = _attackRandom * 1664525u + 1013904223u;

            return (cs_string((cs_string("attack1_hook_") + cs_string(((left ? "l" : "r"))))) +
                    cs_string((((_attackRandom >> 16) & 1) + 1)));
        }

        if (input.MoveX == 0 && input.HasAttackTarget) {

            LibAottg::Vec3 delta = input.AttackTarget / Scale - _position;

            float angle = DeltaAngle(FacingDegrees, Yaw(delta));

            if (std::abs(angle) < 90 && delta.Length() < 6 && delta.Y <= 2 && delta.Y >= -5)
                return "attack4";

            return (angle > 0 ? "attack1" : "attack2");
        }
    }

    return (input.MoveX < 0 || (input.MoveX == 0 && input.AttackLeft) ? "attack2" : "attack1");
}
bool CharacterMotor::IsAction() const {

    return _actionState == CharacterState::Attack || _actionState == CharacterState::Reload ||
           _actionState == CharacterState::Supply;
}

void CharacterMotor::BeginAction(LibAottg::CharacterState state, std::string clip, float speed,
                                 LibAottg::ICharacterWorld *world) {

    _actionState = State = state;

    _bladesThrown = _reloadEquipped = _attackMove = false;

    Play(clip, false, speed, (state == CharacterState::Reload ? 0.1f : 0), static_cast<float>(0), true);

    if (state == CharacterState::Attack) {

        if (Grounded && Equipment == EquipmentMode::Blades)
            _pendingForce += Math3::Direction(FacingDegrees) * 200;

        _attackReleased = Grounded || clip == "attack3_1" || clip == "attack5" || clip == "special_petra" ||
                          clip.starts_with("special_");

        world->OnCharacterEvent(CharacterEvent::AttackStarted);
    }
}

void CharacterMotor::SetAttackWindow(bool active, LibAottg::ICharacterWorld *world) {

    if (AttackHitboxActive == active)
        return;

    AttackHitboxActive = active;

    world->OnCharacterEvent(
        (active ? CharacterEvent::AttackWindowOpened : CharacterEvent::AttackWindowClosed));
}

void CharacterMotor::CancelAction(LibAottg::ICharacterWorld *world) {

    SetAttackWindow(false, world);

    if (_actionState == CharacterState::Attack)
        world->OnCharacterEvent(CharacterEvent::AttackEnded);

    _actionState = CharacterState::Idle;

    _attackLoops = 0;

    _animation.Speed = HeroAnimations::Speed(_animation.Clip);
}

bool CharacterMotor::CanReload() {

    return Equipment == EquipmentMode::Blades || LeftGunReserve > 0 || RightGunReserve > 0;
}

void CharacterMotor::BeginReload(LibAottg::ICharacterWorld *world) {

    CancelAction(world);

    std::string clip{};

    if (Equipment == EquipmentMode::Blades)
        clip = (Grounded ? "changeBlade" : "changeBlade_air");
    else {

        if (LeftGunLoaded && RightGunLoaded)
            LeftGunLoaded = RightGunLoaded = false;

        clip =
            (cs_string(
                 (cs_string("AHSS_gun_reload_") +
                  cs_string(((!LeftGunLoaded && !RightGunLoaded ? "both" : (!LeftGunLoaded ? "l" : "r")))))) +
             cs_string(((Grounded ? "" : "_air"))));
    }

    BeginAction(CharacterState::Reload, clip, HeroAnimations::Speed(clip), world);
}

void CharacterMotor::ThrowBlades(LibAottg::ICharacterWorld *world) {

    if (!BladesEquipped)
        return;

    BladesEquipped = false;

    SpareBladePairs = cs_max(0, SpareBladePairs - 1);

    if (SpareBladePairs == 0)
        Blade = 0;

    SetAttackWindow(false, world);

    world->OnCharacterEvent(CharacterEvent::BladesThrown);
}

void CharacterMotor::BladeHit(LibAottg::ICharacterWorld *world, int amount) {

    if (world == nullptr)
        throw std::invalid_argument(cs_string("world"));

    if (amount < 0)
        throw std::invalid_argument(cs_string("amount"));

    ConsumeBlade(2.0f * ((amount == 0 ? 1 : amount)));

    if (Blade <= 0)
        ThrowBlades(world);
}

void CharacterMotor::FireGuns(bool both, LibAottg::CharacterInput input, LibAottg::ICharacterWorld *world) {

    if (!LeftGunLoaded && !RightGunLoaded) {

        BeginReload(world);

        return;
    }

    std::string side =
        (both && LeftGunLoaded && RightGunLoaded
             ? "both"
             : (LeftHookAttached() && LeftGunLoaded && RightGunLoaded ? "r" : (LeftGunLoaded ? "l" : "r")));

    LibAottg::Vec3 aim = (input.HasAimTarget ? input.AimTarget - _position : input.AimDirection);

    if (Math3::Horizontal(aim).LengthSquared() > 0)
        FacingTargetDegrees = Yaw(aim);

    BeginAction(
        CharacterState::Attack,
        (cs_string((cs_string("AHSS_shoot_") + cs_string(side))) + cs_string(((Grounded ? "" : "_air")))),
        static_cast<float>(1), world);

    _attackReleased = false;
}

void CharacterMotor::UpdateAction(float dt, LibAottg::CharacterInput input,
                                  LibAottg::ICharacterWorld *world) {

    float t = _animation.NormalizedTime();

    if (IsAction()) {

        if (_actionState == CharacterState::Attack && Equipment == EquipmentMode::Blades) {

            if (!_attackReleased) {

                if (!input.Attack.Held) {

                    _attackReleased = true;

                    _animation.Speed = HeroAnimations::Speed(_animation.Clip);
                } else if (t >= 0.32f)
                    _animation.Speed = 0;
            }

            float start = _config->AttackWindowStart, end = _config->AttackWindowEnd;

            if (_animation.Clip == "attack3_1") {
                start = 0.8f;
                end = std::numeric_limits<float>::max();
            } else if (_animation.Clip == "attack3_2") {
                start = end = -1;
            } else if (_animation.Clip == "attack5") {
                start = 0.35f;
                end = 0.5f;
            } else if (_animation.Clip == "special_petra") {
                start = 0.35f;
                end = 0.48f;
            } else if (_animation.Clip == "special_armin") {
                start = 0.25f;
                end = 0.35f;
            } else if (_animation.Clip == "attack4") {
                start = 0.6f;
                end = 0.9f;
            } else if (_animation.Clip == "special_sasha" || _animation.Clip == "special_marco_0" ||
                       _animation.Clip == "special_marco_1") {
                start = end = -1;
            }

            bool active = BladesEquipped && Blade > 0 &&
                          ((_animation.Clip == "attack3_1" ? t >= start : t > start)) && t < end;
            // HERO begins Mikasa's downward strike when its blade hitbox opens.
            if (_animation.Clip == "attack3_1" && active && !AttackHitboxActive)
                SetReferenceVelocity(-Vec3::Up() * 30);

            SetAttackWindow(active, world);
            // Once the first clip ends, HERO accelerates down on every render frame
            // until the fixed update observes the landing and starts attack3_2.
            if (_animation.Clip == "attack3_1" && t >= 1)
                SetReferenceVelocity(_velocity - Vec3::Up() * (30 * dt));

            if (_attackLoops > 0 && t > end) {

                _attackLoops--;

                _animation.Time = start * _animation.Duration;
            } else if (t >= 1 && _animation.Clip != "attack3_1")
                FinishAction(world);
        } else if (_actionState == CharacterState::Attack) {

            if (!_attackReleased && t > 0.167f) {

                _attackReleased = true;

                bool both = (cs_index(_animation.Clip, "both") >= 0);

                if (both || (cs_index(_animation.Clip, "shoot_l") >= 0)) {

                    LeftGunLoaded = false;

                    world->OnCharacterEvent(CharacterEvent::GunFiredLeft);
                }

                if (both || (cs_index(_animation.Clip, "shoot_r") >= 0)) {

                    RightGunLoaded = false;

                    world->OnCharacterEvent(CharacterEvent::GunFiredRight);
                }
                // Update2 uses ForceMode.Acceleration: force persists for one fixed step.
                _pendingForce +=
                    (-Math3::Direction(FacingDegrees) * ((both ? 1000 : 600)) + Vec3::Up() * 200) *
                    _config->Mass;
            }

            if (t >= 1)
                FinishAction(world);
        } else if (_actionState == CharacterState::Reload) {

            if (Equipment == EquipmentMode::Blades) {

                if (!_bladesThrown && t >= ((Grounded ? 0.13f : 0.2f))) {

                    _bladesThrown = true;

                    ThrowBlades(world);
                }

                if (!_reloadEquipped && t >= ((Grounded ? 0.37f : 0.56f)) && SpareBladePairs > 0) {

                    _reloadEquipped = BladesEquipped = true;

                    Blade = _config->BladeCapacity;

                    world->OnCharacterEvent(CharacterEvent::Reloaded);
                }
            } else if (!_reloadEquipped && t > 0.62f) {

                _reloadEquipped = true;

                if (!LeftGunLoaded && LeftGunReserve > 0) {

                    LeftGunLoaded = true;

                    if (!_config->InfiniteGas)
                        LeftGunReserve--;
                }

                if (!RightGunLoaded && RightGunReserve > 0) {

                    RightGunLoaded = true;

                    if (!_config->InfiniteGas)
                        RightGunReserve--;
                }

                world->OnCharacterEvent(CharacterEvent::Reloaded);
            }

            if (t >= 1)
                FinishAction(world);
        } else if (_actionState == CharacterState::Supply && t >= 1) {

            Gas = _config->GasCapacity;

            Blade = _config->BladeCapacity;

            SpareBladePairs = _config->SpareBladePairs;

            BladesEquipped = SpareBladePairs > 0;

            LeftGunLoaded = RightGunLoaded = true;

            LeftGunReserve = RightGunReserve = _config->GunReservePerSide;

            world->OnCharacterEvent(CharacterEvent::Supplied);

            FinishAction(world);
        }
    } else if ((State == CharacterState::Slide && !Grounded) ||
               (State == CharacterState::GroundDodge && (t >= 1 || (!Grounded && t > 0.6f))) ||
               (State == CharacterState::Land && t >= 1))
        State = CharacterState::Idle;
}

void CharacterMotor::FinishAction(LibAottg::ICharacterWorld *world) {

    CancelAction(world);

    State = CharacterState::Idle;

    Play((Equipment == EquipmentMode::Guns ? "AHSS_stand_gun" : _config->StandAnimation), true);
}

void CharacterMotor::SkillPhysics(LibAottg::ICharacterWorld *world) {

    if (_actionState != CharacterState::Attack)
        return;

    std::string clip = _animation.Clip;

    float t = _animation.NormalizedTime();

    if ((clip == "attack5" || clip == "special_petra") && t > 0.4f && !_attackMove) {

        _attackMove = true;

        if (_launchPointRight.LengthSquared() > 0)
            _pendingImpulse += (_launchPointRight - _position).Normalized() * 13;

        if (clip == "special_petra" && _launchPointLeft.LengthSquared() > 0) {

            _pendingImpulse += (_launchPointLeft - _position).Normalized() * 13;

            Disable(_left, HookSide::Left, world);

            Disable(_right, HookSide::Right, world);
        }

        _pendingImpulse += Vec3::Up() * 2;
    }

    if (clip == "attack3_1" && t >= 1 && Grounded) {

        SetReferenceVelocity(Vec3::Zero());

        Play("attack3_2", false, static_cast<float>(1), static_cast<float>(0), static_cast<float>(0), true);

        SetAttackWindow(false, world);
    }
}

void CharacterMotor::UseSkill(LibAottg::CharacterInput input, LibAottg::ICharacterWorld *world) {

    std::string skill = cs_lower(_config->SkillId);

    if ((skill == "armin" || skill == "marco" || skill == "sasha") && !Grounded)
        return;

    std::string clip{};

    if (skill == "mikasa") {
        clip = "attack3_1";
        SetReferenceVelocity(Vec3::Up() * 10);
    } else if (skill == "levi") {
        clip = "attack5";
        SetReferenceVelocity(_velocity + Vec3::Up() * 5);
    } else if (skill == "petra") {
        clip = "special_petra";
        SetReferenceVelocity(_velocity + Vec3::Up() * 5);
    } else if (skill == "armin") {
        clip = "special_armin";
    } else if (skill == "marco") {
        clip = "special_marco_0";
    } else if (skill == "sasha") {
        clip = "special_sasha";
        _speedBuff = 10;
    } else {
        world->OnCharacterEvent(CharacterEvent::SkillUsed);
        _skillCooldown = _config->SkillCooldown;
        return;
    }

    BeginAction(CharacterState::Attack, clip, HeroAnimations::Speed(clip), world);

    _skillCooldown = _config->SkillCooldown;

    if (skill == "levi" || skill == "petra") {

        LibAottg::Vec3 origin = (input.HasAimOrigin ? input.AimOrigin : _position);

        LibAottg::HookHit target{};

        if (input.AimDirection.LengthSquared() > 0 &&
            TryHookAim(origin, input.AimDirection.Normalized(), 1e7f, HookSide::Right, world, target)) {
            // HERO disables/replaces these projectiles only when the special's ray hits.
            // launch flags/elapsed timers survive replacement until a new landing.
            if (skill == "petra") {

                Disable(_left, HookSide::Left, world);

                LaunchAt(_left, HookSide::Left, origin, target, false, world, static_cast<float>(150));
            }

            Disable(_right, HookSide::Right, world);

            LaunchAt(_right, HookSide::Right, origin, target, false, world,
                     static_cast<float>((skill == "levi" ? 250 : 150)), skill == "levi");

            FacingTargetDegrees = Yaw(target.Point - _position);
        }

        _attackLoops = (skill == "levi" ? 3 : 0);
    }

    world->OnCharacterEvent(CharacterEvent::SkillUsed);
}

void CharacterMotor::Play(std::string clip, bool loop, float speed, float fade, float time, bool restart) {

    if (!restart && _animation.Clip == clip)
        return;

    float duration = HeroAnimations::Duration(clip);

    if (clip == "attack1" || clip == "attack2" || clip.starts_with("attack1_hook_"))
        duration = _config->AttackDuration;

    if (clip == "changeBlade")
        duration = _config->ReloadDuration * 1.2f;

    if (clip == "dodge")
        duration = _config->GroundDodgeDuration;

    if (clip == "supply")
        duration = _config->SupplyDuration;

    _animation = ([&] {
        auto value = LibAottg::CharacterAnimation{};
        value.Clip = clip;
        value.Loop = loop;
        value.Duration = duration;
        value.Speed = (speed < 0 ? HeroAnimations::Speed(clip) : speed);
        value.CrossFade = fade;
        value.Time = time;
        value.Revision = _animation.Revision + 1;
        return value;
    }());
}

void CharacterMotor::AdvanceAnimation(float dt) {

    _animation.Time += dt * _animation.Speed;

    if (_animation.Loop && _animation.Duration > 0)
        _animation.Time = cs_rem(_animation.Time, _animation.Duration);
}

void CharacterMotor::SelectLocomotion(LibAottg::Vec3 desired) {
    // A brief floor contact must not leave Slide blocking airborne clip selection.
    if (State == CharacterState::Slide && !Grounded)
        State = CharacterState::Idle;

    if (IsAction() || State == CharacterState::GroundDodge || State == CharacterState::Land ||
        State == CharacterState::Slide || State == CharacterState::WallRun ||
        State == CharacterState::RoofVault)
        return;

    if (_dashTime > 0)
        return;

    if (Grounded) {

        State = (desired.LengthSquared() > 0 ? CharacterState::Run : CharacterState::Idle);

        if (_animation.Clip == "jump" && _animation.NormalizedTime() < 1)
            return;

        Play((desired.LengthSquared() > 0
                  ? ((_speedBuff > 0 ? "run_sasha" : _config->RunAnimation))
                  : (Equipment == EquipmentMode::Guns ? "AHSS_stand_gun" : _config->StandAnimation)),
             true);

        return;
    }

    State = (_velocity.Y >= 0 ? CharacterState::Jump : CharacterState::Fall);

    std::string clip = _animation.Clip;

    if ((clip == "jump" || clip == "air_release" || clip == "air_hook_l_just" || clip == "air_hook_r_just") &&
        _animation.NormalizedTime() < 1)
        return;

    if (clip == "dash" && _animation.NormalizedTime() < 0.99f)
        return;

    if (!LeftHookAttached() && !RightHookAttached() && _velocity.Y > 20 &&
        (clip == "air_hook_l" || clip == "air_hook_r" || clip == "air_hook")) {

        Play("air_release", false);

        return;
    }

    if (std::abs(_velocity.X) + std::abs(_velocity.Z) <= 25)
        Play((_velocity.Y < 0 ? "air_fall" : "air_rise"), true, static_cast<float>(-1), 0.2f);
    else if (LeftHookAttached() || RightHookAttached())
        Play((Equipment == EquipmentMode::Guns
                  ? (LeftHookAttached() && RightHookAttached()
                         ? "AHSS_hook_forward_both"
                         : (LeftHookAttached() ? "AHSS_hook_forward_l" : "AHSS_hook_forward_r"))
                  : (LeftHookAttached() && RightHookAttached()
                         ? "air_hook"
                         : (LeftHookAttached() ? "air_hook_l" : "air_hook_r"))),
             true);
    else {

        float angle = DeltaAngle(FacingDegrees, Yaw(_velocity));
        // The short forward-flight transition holds its last pose (ClampForever).
        // The sideways and backward flight clips are authored loops.
        Play((std::abs(angle) < 45
                  ? "air2"
                  : (angle > 0 && angle < 135 ? "air2_right"
                                              : (angle < 0 && angle > -135 ? "air2_left" : "air2_backward"))),
             std::abs(angle) >= 45, static_cast<float>(-1), 0.2f);
    }
}
CharacterMotor::CharacterMotor(LibAottg::CharacterConfig *config) {

    ownedConfig = std::make_unique<CharacterConfig>(config ? *config : CharacterConfig{});
    _config = ownedConfig.get();

    _config->Validate();

    Scale = _config->WorldScale;

    ownedBoundary = std::make_unique<WorldBoundary>(this);
    _world = ownedBoundary.get();

    Respawn(Vec3::Zero());
}
LibAottg::CharacterConfig *CharacterMotor::Config() const {

    return _config;
}
LibAottg::Vec3 CharacterMotor::Position() const {

    return _position * Scale;
}
LibAottg::Vec3 CharacterMotor::Velocity() const {

    return _velocity * Scale;
}
bool CharacterMotor::IsDashing() const {

    return _dashTime > 0;
}
float CharacterMotor::FlareCooldownRemaining() const {

    return _flareCooldown;
}
float CharacterMotor::SkillCooldownRemaining() const {

    return _skillCooldown;
}
const LibAottg::CharacterAnimation &CharacterMotor::Animation() const {

    return _animation;
}

void CharacterMotor::SetEquipment(LibAottg::EquipmentMode mode) {

    if (IsAction())
        throw std::invalid_argument(
            cs_string("Wait for the current action to finish before changing equipment."));

    Equipment = mode;

    Play((mode == EquipmentMode::Guns ? "AHSS_stand_gun" : _config->StandAnimation), true);
}

void CharacterMotor::SetPose(LibAottg::Vec3 position, LibAottg::Vec3 velocity, float facingDegrees) {

    if (!WorldBox::Finite(position) || !WorldBox::Finite(velocity) || std::isnan(facingDegrees) ||
        std::isinf(facingDegrees))
        throw std::invalid_argument(cs_string("Pose must be finite."));

    _position = position / Scale;

    _velocity = velocity / Scale;

    FacingDegrees = facingDegrees;

    if (!_hasPhysicsTick)
        FacingTargetDegrees = facingDegrees;
}

void CharacterMotor::Step(float deltaTime, LibAottg::CharacterInput input,
                          LibAottg::CharacterContacts contacts, LibAottg::ICharacterWorld *world) {

    ValidateStep(deltaTime, world);

    AdvanceFrame(deltaTime, input, contacts, world);

    StepPhysics(deltaTime, input, contacts, world);
}

void CharacterMotor::AdvanceFrame(float dt, LibAottg::CharacterInput input,
                                  LibAottg::CharacterContacts contacts, LibAottg::ICharacterWorld *world) {

    if (world == nullptr)
        throw std::invalid_argument(cs_string("world"));

    if (std::isnan(dt) || std::isinf(dt) || dt <= 0)
        throw std::invalid_argument(cs_string("dt"));

    if (State == CharacterState::Dead || State == CharacterState::Grabbed)
        return;

    world = BindWorld(world);

    if (input.HasAimOrigin)
        input.AimOrigin /= Scale;

    if (input.HasAimTarget)
        input.AimTarget /= Scale;

    Grounded = contacts.Grounded;

    _frameClock += dt;

    _dashCooldown = cs_max(static_cast<float>(0), _dashCooldown - dt);

    _flareCooldown = cs_max(static_cast<float>(0), _flareCooldown - dt);

    _skillCooldown = cs_max(static_cast<float>(0), _skillCooldown - dt);

    _speedBuff = cs_max(static_cast<float>(0), _speedBuff - dt);

    AdvanceAnimation(dt);

    UpdateAction(dt, input, world);

    AdvanceRetraction(_left, HookSide::Left, dt, world);

    AdvanceRetraction(_right, HookSide::Right, dt, world);

    if (_dashTime > 0) {

        _dashTime = cs_max(static_cast<float>(0), _dashTime - dt);

        if (_currentSpeed > _dashOriginSpeed)
            _pendingVelocityChange -= _velocity * (dt * _config->DashDamping);
    }

    LibAottg::Vec3 desired = Desired(input);

    LibAottg::Vec3 f = Forward(input), right = LibAottg::Vec3(f.Z, 0, -f.X);

    LibAottg::Vec3 dash =
        (input.MoveY != 0 ? f * cs_sign(input.MoveY)
                          : (input.MoveX != 0 ? right * cs_sign(input.MoveX) : Vec3::Zero()));

    bool doubleTap = DoubleTap(input, dash);

    if (!Grounded && _dashTime <= 0 && _dashCooldown <= 0 && Gas > 0 &&
        ((input.Dash.Pressed && dash.LengthSquared() > 0) || doubleTap)) {

        CancelAction(world);

        _jumpPending = false;

        _wallRunTime = 0;

        _dashOriginSpeed = _currentSpeed;

        _pendingVelocityChange += dash * _config->DashSpeed;

        FacingDegrees = FacingTargetDegrees = Yaw(dash);
        // HERO.dash assigns dashTime before setting state. Its state setter clears
        // that timer when leaving GroundDodge (including the airborne wall jump).
        // Keep the additive impulse, gas cost and cooldown, but skip burst damping.
        _dashTime = (State == CharacterState::GroundDodge ? 0 : _config->DashDuration);

        _dashCooldown = _config->DashCooldown;

        ConsumeGas(_config->GasCapacity * _config->DashGasFraction);

        Play("dash", false, static_cast<float>(1), 0.1f, 0.1f, true);

        State = CharacterState::AirDodge;

        world->OnCharacterEvent(CharacterEvent::Dashed);

        if (_dashTime <= 0) {

            State = CharacterState::Idle;

            Play(_config->StandAnimation, true);
        }
    }

    if (Grounded &&
        (State == CharacterState::Idle || State == CharacterState::Run || State == CharacterState::Slide) &&
        !IsAction() && _dashTime <= 0 && input.Dodge.Pressed && _animation.Clip != "jump") {

        if (desired.LengthSquared() > 0)
            FacingTargetDegrees = Yaw(desired) + 180;

        Play("dodge", false, static_cast<float>(1), 0.1f, static_cast<float>(0), true);

        State = CharacterState::GroundDodge;

        world->OnCharacterEvent(CharacterEvent::Dodged);
    }

    if (Grounded && !IsAction() && input.Jump.Pressed && _animation.Clip != "jump") {

        if (!_config->NoStopJump)
            State = CharacterState::Idle;

        _jumpPending = true;

        Play("jump", false, static_cast<float>(1), 0.1f, static_cast<float>(0), true);
    }

    if (input.Supply.Pressed && Grounded && !IsAction() &&
        (Gas < _config->GasCapacity || Blade < _config->BladeCapacity ||
         SpareBladePairs < _config->SpareBladePairs || LeftGunReserve < _config->GunReservePerSide ||
         RightGunReserve < _config->GunReservePerSide))
        BeginAction(CharacterState::Supply, "supply", static_cast<float>(1), world);
    else if (input.Reload.Pressed && !IsAction() && _dashTime <= 0 &&
             (!Grounded || _animation.Clip == _config->StandAnimation ||
              _animation.Clip == "AHSS_stand_gun") &&
             CanReload())
        BeginReload(world);
    else if (input.Skill.Pressed && !IsAction() && _dashTime <= 0 && _skillCooldown <= 0 &&
             Equipment == EquipmentMode::Blades)
        UseSkill(input, world);
    else if (Equipment == EquipmentMode::Guns && !IsAction() && _dashTime <= 0 &&
             (input.Attack.Released || input.Skill.Released))
        FireGuns(input.Skill.Released, input, world);
    else if (input.Attack.Pressed && !IsAction() && _dashTime <= 0 && Equipment == EquipmentMode::Blades) {

        if (BladesEquipped && Blade > 0) {

            BeginAction(CharacterState::Attack, SelectBladeAttack(input), static_cast<float>(1), world);
        }
    }

    if (input.Flare.Pressed && _flareCooldown <= 0) {

        _flareCooldown = _config->FlareCooldown;

        world->OnCharacterEvent(CharacterEvent::FlareFired);
    }

    LaunchHooks(input, world);
}

void CharacterMotor::StepPhysics(float dt, LibAottg::CharacterInput input,
                                 LibAottg::CharacterContacts contacts, LibAottg::ICharacterWorld *world) {

    ValidateStep(dt, world);

    Grounded = contacts.Grounded;

    GasThrustActive = false;

    if (State == CharacterState::Dead || State == CharacterState::Grabbed)
        return;

    world = BindWorld(world);

    float speed = _velocity.Length();

    _currentSpeed = speed;

    if (_animation.Clip != "attack3_2" && _animation.Clip != "attack5" && _animation.Clip != "special_petra")
        TurnTowards(FacingTargetDegrees, dt * 6);

    Spinning = false;

    UpdateHookAnchor(_left, HookSide::Left, world);

    UpdateHookAnchor(_right, HookSide::Right, world);

    LibAottg::Vec3 force = Vec3::Zero();

    _leftHookedAtPhysicsStart = _launchLeft && LeftHookAttached();

    _rightHookedAtPhysicsStart = _launchRight && RightHookAttached();

    bool spinL = RopeForce(_left, _launchRight, input.Jump.Held, force);

    TickLaunchedHook(_left, HookSide::Left, input.HookLeft.Held || input.HookBoth.Held, dt, world);

    spinL &= LeftHookAttached();

    bool spinR = RopeForce(_right, _launchLeft, input.Jump.Held, force);

    TickLaunchedHook(_right, HookSide::Right, input.HookRight.Held || input.HookBoth.Held, dt, world);

    spinR &= RightHookAttached();

    LibAottg::Vec3 desired = Desired(input);

    SelectLocomotion(desired);

    if (Grounded)
        GroundMovement(desired, input, dt, world);
    else
        AirMovement(desired, input, contacts, dt, spinL || spinR, force, world);

    if (spinL || spinR) {

        LibAottg::Vec3 anchor = (spinL && spinR ? (_left.Position + _right.Position) * 0.5f
                                                : (spinL ? _left.Position : _right.Position));
        // AddForce(VelocityChange) is queued in Unity; RotateTowards sees the pre-force velocity.
        LibAottg::Vec3 direction = RotateTowards(anchor - _position, _velocity,
                                                 1.53938f * (1 + Math3::Clamp(input.Reel, -0.8f, 0.8f)));

        SetReferenceVelocity(direction.Normalized() * (speed + 0.1f));

        Spinning = true;

        GasThrustActive = true;

        if (!_skipGasThisTick)
            ConsumeGas(_config->GasUsePerSecond * dt);
    }

    SkillPhysics(world);

    bool above = (_left.Phase == HookPhase::Attached && _left.Position.Y > _position.Y) ||
                 (_right.Phase == HookPhase::Attached && _right.Position.Y > _position.Y);

    float gravity = (above ? _config->HookedGravity : _config->Gravity);

    if (State == CharacterState::RoofVault && _animation.NormalizedTime() < 0.22f)
        force += Vec3::Up() * _config->Gravity;

    UpdateHookFacing();

    _skipGasThisTick = false;
    // In the reference player HERO.FixedUpdate runs before Bullet.FixedUpdate.
    // A newly landing bullet adds its launch force now; sustained pull starts next tick.
    AdvanceHooks(dt, world);

    _velocity += (force - Vec3::Up() * gravity + _pendingForce / _config->Mass) * dt +
                 _pendingImpulse / _config->Mass + _pendingVelocityChange;

    _pendingForce = _pendingImpulse = _pendingVelocityChange = Vec3::Zero();

    _position += _velocity * dt;

    float targetFov = (speed > 10 ? cs_min(static_cast<float>(100), speed + 40) : 50);

    FieldOfView += (targetFov - FieldOfView) * 0.1f;

    _wasGrounded = Grounded;

    _hasPhysicsTick = true;
}

void CharacterMotor::ValidateStep(float dt, LibAottg::ICharacterWorld *world) {

    if (world == nullptr)
        throw std::invalid_argument(cs_string("world"));

    if (std::isnan(dt) || std::isinf(dt) || dt <= 0 || dt > _config->MaxStep)
        throw std::invalid_argument(cs_string("dt") + std::string(": ") +
                                    cs_string("Use a positive step no greater than Config.MaxStep."));
}
// Observed in the original Unity 4.6 player: a velocity assignment clears accumulated
// Impulse/VelocityChange, but preserves Force/Acceleration for the next simulation.
void CharacterMotor::SetReferenceVelocity(LibAottg::Vec3 value) {

    _velocity = value;

    _pendingImpulse = _pendingVelocityChange = Vec3::Zero();
}

LibAottg::Vec3 CharacterMotor::Forward(LibAottg::CharacterInput input) {

    LibAottg::Vec3 forward = Math3::Horizontal(input.LookDirection).Normalized();

    return (forward.LengthSquared() > 0 ? forward : Math3::Direction(FacingDegrees));
}

LibAottg::Vec3 CharacterMotor::Desired(LibAottg::CharacterInput input) {

    LibAottg::Vec3 f = Forward(input);

    LibAottg::Vec3 v = f * Math3::Clamp(input.MoveY, static_cast<float>(-1), static_cast<float>(1)) +
                       LibAottg::Vec3(f.Z, 0, -f.X) *
                           Math3::Clamp(input.MoveX, static_cast<float>(-1), static_cast<float>(1));

    float n = v.Length();

    return (n < 0.25f ? Vec3::Zero() : v.Normalized() * ((n > 0.95f ? 1 : n)));
}

float CharacterMotor::Yaw(LibAottg::Vec3 v) {

    return static_cast<float>((std::atan2(static_cast<double>(v.X), static_cast<double>(v.Z)) * 180 / cs_pi));
}

bool CharacterMotor::DoubleTap(LibAottg::CharacterInput input, LibAottg::Vec3 &direction) {

    if (!_config->DoubleTapDash) {

        for (int n = 0; n < static_cast<int>(_taps.size()); n++)
            _taps[n] = -1;

        return false;
    }

    LibAottg::Vec3 f = Forward(input), r = LibAottg::Vec3(f.Z, 0, -f.X);

    bool result = false;

    for (int n = 0; n < 4; n++)
        if (((n == 0 ? input.Back : (n == 1 ? input.Forward : (n == 2 ? input.Left : input.Right))))
                .Pressed) {

            if (!result && !Grounded && _taps[n] >= 0 && _frameClock - _taps[n] < _config->DoubleTapWindow) {

                direction = (n == 0 ? -f : (n == 1 ? f : (n == 2 ? -r : r)));

                result = true;

                _taps[n] = -1;
            } else
                _taps[n] = _frameClock;
        }

    return result;
}

void CharacterMotor::ConsumeGas(float amount) {

    if (!_config->InfiniteGas)
        Gas = Math3::Clamp(Gas - cs_max(static_cast<float>(0), amount), static_cast<float>(0),
                           _config->GasCapacity);
}

void CharacterMotor::ConsumeBlade(float amount) {

    if (!_config->InfiniteGas)
        Blade = Math3::Clamp(Blade - cs_max(static_cast<float>(0), amount), static_cast<float>(0),
                             _config->BladeCapacity);
}

void CharacterMotor::RefillGas() {

    Gas = _config->GasCapacity;
}

void CharacterMotor::Die(LibAottg::ICharacterWorld *world) {

    if (world == nullptr)
        throw std::invalid_argument(cs_string("world"));

    if (State == CharacterState::Dead)
        return;

    Stop(world);

    State = CharacterState::Dead;

    Play("T-pose", false);

    world->OnCharacterEvent(CharacterEvent::Died);
}

void CharacterMotor::Grab(LibAottg::ICharacterWorld *world) {

    if (world == nullptr)
        throw std::invalid_argument(cs_string("world"));

    if (State == CharacterState::Dead || State == CharacterState::Grabbed)
        return;

    Stop(world);

    _velocity = Vec3::Zero();

    State = CharacterState::Grabbed;

    Play("grabbed", false);

    world->OnCharacterEvent(CharacterEvent::Grabbed);
}

void CharacterMotor::Stop(LibAottg::ICharacterWorld *world) {

    ReleaseHook(HookSide::Left, world);

    ReleaseHook(HookSide::Right, world);

    CancelAction(world);

    _dashTime = _wallRunTime = 0;

    _jumpPending = false;

    Spinning = false;

    GasThrustActive = false;

    _pendingForce = _pendingImpulse = _pendingVelocityChange = Vec3::Zero();
}

void CharacterMotor::EscapeGrab(LibAottg::Vec3 position, LibAottg::ICharacterWorld *world) {

    if (world == nullptr)
        throw std::invalid_argument(cs_string("world"));

    if (State != CharacterState::Grabbed)
        return;

    _position = position / Scale;

    _velocity = Vec3::Zero();

    State = CharacterState::Idle;

    Play(_config->StandAnimation, true);

    world->OnCharacterEvent(CharacterEvent::EscapedGrab);
}

void CharacterMotor::Teleport(LibAottg::Vec3 position, float facingDegrees) {
    if (!WorldBox::Finite(position) || !std::isfinite(facingDegrees))
        throw std::invalid_argument(cs_string("Pose must be finite."));
    const float gas = Gas, blade = Blade, skill = _skillCooldown, flare = _flareCooldown,
                dash = _dashCooldown, speedBuff = _speedBuff;
    const int blades = SpareBladePairs, leftReserve = LeftGunReserve, rightReserve = RightGunReserve;
    const bool equipped = BladesEquipped, leftLoaded = LeftGunLoaded, rightLoaded = RightGunLoaded;
    Respawn(position);
    SetPose(position, Vec3::Zero(), facingDegrees);
    Gas = gas; Blade = blade; SpareBladePairs = blades; BladesEquipped = equipped;
    LeftGunLoaded = leftLoaded; RightGunLoaded = rightLoaded;
    LeftGunReserve = leftReserve; RightGunReserve = rightReserve;
    _skillCooldown = skill; _flareCooldown = flare; _dashCooldown = dash; _speedBuff = speedBuff;
}

void CharacterMotor::Respawn(LibAottg::Vec3 position, bool grounded) {

    _left = _right = LibAottg::CharacterMotor::Rope();

    _launchLeft = _launchRight = _leftHookedAtPhysicsStart = _rightHookedAtPhysicsStart = false;

    _elapsedLeft = _elapsedRight = 0;

    _launchPointLeft = _launchPointRight = Vec3::Zero();

    _dashTime = _dashCooldown = _wallRunTime = _flareCooldown = _skillCooldown = _frameClock = 0;

    _jumpPending = _roofImpulse = _skipGasThisTick = false;

    _wasGrounded = _hasPhysicsTick = grounded;

    _pendingForce = _pendingImpulse = _pendingVelocityChange = Vec3::Zero();

    _speedBuff = 0;

    _attackLoops = 0;

    _currentSpeed = 0;

    _actionState = CharacterState::Idle;

    AttackHitboxActive = Spinning = GasThrustActive = false;

    for (int n = 0; n < 4; n++)
        _taps[n] = -1;

    Gas = _config->GasCapacity;

    Blade = _config->BladeCapacity;

    SpareBladePairs = _config->SpareBladePairs;

    BladesEquipped = SpareBladePairs > 0;

    LeftGunLoaded = RightGunLoaded = true;

    LeftGunReserve = RightGunReserve = _config->GunReservePerSide;

    _position = position / Scale;

    _velocity = Vec3::Zero();

    FacingDegrees = FacingTargetDegrees = 0;

    Grounded = grounded;

    State = CharacterState::Idle;

    FieldOfView = 50;

    Play((Equipment == EquipmentMode::Guns ? "AHSS_stand_gun" : _config->StandAnimation), true,
         static_cast<float>(1), static_cast<float>(0), static_cast<float>(0), true);
}
bool CharacterMotor::LeftHookAttached() const {

    return _left.Phase == HookPhase::Attached;
}
bool CharacterMotor::RightHookAttached() const {

    return _right.Phase == HookPhase::Attached;
}
LibAottg::HookPhase CharacterMotor::LeftHookPhase() const {

    return _left.Phase;
}
LibAottg::HookPhase CharacterMotor::RightHookPhase() const {

    return _right.Phase;
}
LibAottg::Vec3 CharacterMotor::LeftHookAnchor() const {

    return _left.Position * Scale;
}
LibAottg::Vec3 CharacterMotor::RightHookAnchor() const {

    return _right.Position * Scale;
}
bool CharacterMotor::LeftHookVisible() const {

    return _left.Phase != HookPhase::Inactive;
}
bool CharacterMotor::RightHookVisible() const {

    return _right.Phase != HookPhase::Inactive;
}
// HERO.setHookedPplDirection runs after movement and overrides camera-relative facing.
// Keep the target separate from the interpolated body rotation, as in the source.
void CharacterMotor::UpdateHookFacing() {

    bool left = LeftHookAttached(), right = RightHookAttached();

    if ((!left && !right) || (Equipment == EquipmentMode::Guns && _actionState == CharacterState::Attack))
        return;

    bool attack = _actionState == CharacterState::Attack;

    float facing{};

    if (left && right) {

        LibAottg::Vec3 normal = _left.Position - _right.Position;

        LibAottg::Vec3 midpoint = (_left.Position + _right.Position) * 0.5f;

        LibAottg::Vec3 delta = midpoint - _position;

        if (normal.LengthSquared() < 4) {

            facing = Yaw(delta);

            if (Equipment == EquipmentMode::Guns && !attack)
                facing += HookVelocityAngle(delta);
        } else if (HeroCamera::Angle(_position - midpoint, _position - _left.Position) < 30 &&
                   HeroCamera::Angle(_position - midpoint, _position - _right.Position) < 30)
            facing = Yaw(delta);
        else {

            normal = normal.Normalized();

            LibAottg::Vec3 forward = HeroBodyPose::Evaluate(this).Forward();

            forward -= normal * Vec3::Dot(normal, forward);

            if (forward.LengthSquared() < 0.000001f)
                forward = (std::abs(normal.Z) > 0.70710678f ? LibAottg::Vec3(0, -normal.Z, normal.Y)
                                                            : LibAottg::Vec3(-normal.Y, normal.X, 0));

            facing = Yaw(forward.Normalized());

            if (DeltaAngle(Yaw(_position - _left.Position), facing) > 0)
                facing += 180;
        }
    } else {

        LibAottg::Vec3 delta = ((right ? _right.Position : _left.Position)) - _position;

        facing = Yaw(delta);

        if (!attack) {

            float angle = HookVelocityAngle(delta);

            facing += angle * ((Equipment == EquipmentMode::Guns
                                    ? 1
                                    : ((left && angle < 0) || (right && angle > 0) ? -0.1f : 0.1f)));
        }
    }

    FacingTargetDegrees = facing;
    // bodyLean snaps ordinary blade attacks to their hook-facing rotation.
    if (attack && Equipment == EquipmentMode::Blades && _animation.Clip != "attack3_1" &&
        _animation.Clip != "attack3_2")
        FacingDegrees = facing;
}

float CharacterMotor::HookVelocityAngle(LibAottg::Vec3 delta) {

    float current =
        -static_cast<float>(std::atan2(static_cast<double>(_velocity.Z), static_cast<double>(_velocity.X))) *
        180 / static_cast<float>(cs_pi);

    float target =
        -static_cast<float>(std::atan2(static_cast<double>(delta.Z), static_cast<double>(delta.X))) * 180 /
        static_cast<float>(cs_pi);

    return -DeltaAngle(current, target);
}
bool CharacterMotor::HookInputAllowed() const {

    return State != CharacterState::Grabbed && State != CharacterState::Dead &&
           (_animation.Clip != "attack3_1" && _animation.Clip != "attack5" &&
            _animation.Clip != "special_petra");
}

void CharacterMotor::LaunchHooks(LibAottg::CharacterInput input, LibAottg::ICharacterWorld *world) {

    if (input.AimDirection.LengthSquared() < 0.001f || Gas <= 0)
        return;

    if (!HookInputAllowed())
        return;

    LibAottg::Vec3 origin = (input.HasAimOrigin ? input.AimOrigin : _position);

    LibAottg::Vec3 direction = input.AimDirection.Normalized();

    if (input.HookLeft.Held || input.HookLeft.Pressed)
        TryLaunch(_left, HookSide::Left, origin, direction, false, world);

    if (input.HookRight.Held || input.HookRight.Pressed)
        TryLaunch(_right, HookSide::Right, origin, direction, false, world);

    if ((input.HookBoth.Held || input.HookBoth.Pressed) && _left.Phase == HookPhase::Inactive &&
        _right.Phase == HookPhase::Inactive) {

        TryLaunch(_left, HookSide::Left, origin, direction, true, world);

        TryLaunch(_right, HookSide::Right, origin, direction, true, world);
    }
}

void CharacterMotor::TryLaunch(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                               LibAottg::Vec3 rayOrigin, LibAottg::Vec3 rayDirection, bool spread,
                               LibAottg::ICharacterWorld *world, float projectileSpeed, bool special) {

    if (rope.Phase != HookPhase::Inactive || Gas <= 0)
        return;

    LibAottg::HookHit target{};

    if (rayDirection.LengthSquared() == 0 ||
        !TryHookAim(rayOrigin, rayDirection, (special ? 1e7f : (spread ? 1e6f : _config->HookRange)),
                        side, world, target))
        return;

    LaunchAt(rope, side, rayOrigin, target, spread, world, projectileSpeed, special);
}

bool CharacterMotor::TryHookAim(Vec3 origin, Vec3 direction, float distance, HookSide side,
                               ICharacterWorld *world, HookHit &target) {
    if (!_config->ImprovedRaycasts) return world->TryHook(origin, direction, distance, side, target);
    direction = direction.Normalized();
    float skip = std::min((origin - _position).Length() * 0.5f, distance);
    if (skip < distance && world->TryHook(origin + direction * skip, direction, distance - skip, side, target)) return true;
    target = HookHit{};
    target.Point = origin + direction * distance;
    return direction.LengthSquared() > 0;
}

void CharacterMotor::LaunchAt(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                              LibAottg::Vec3 rayOrigin, LibAottg::HookHit target, bool spread,
                              LibAottg::ICharacterWorld *world, float projectileSpeed, bool special) {

    if (Gas <= 0)
        return;

    auto geometry = dynamic_cast<LibAottg::IHookGeometryWorld *>(world);

    LibAottg::Vec3 muzzle =
        (geometry != nullptr ? geometry->HookOrigin(this, side) : _position + Vec3::Up() * 0.85f);

    LibAottg::Vec3 point = target.Point;

    if (spread) {

        float distance = (point - rayOrigin).Length();

        float offset = distance * ((distance <= 50 ? 0.05f : 0.3f));

        LibAottg::Vec3 f = Math3::Direction(FacingDegrees);

        point += LibAottg::Vec3(f.Z, 0, -f.X) * ((side == HookSide::Left ? -offset : offset));
    }

    LibAottg::Vec3 direction = (point - muzzle).Normalized();

    if (direction.LengthSquared() <= 0)
        return;
    // Bullet.launch inherits only the positive component parallel to the shot.
    float inherited = cs_max(static_cast<float>(0), Vec3::Dot(_velocity, direction));

    rope = ([&] {
        auto value = LibAottg::CharacterMotor::Rope{};
        value.Phase = HookPhase::Flying;
        value.Position = muzzle;
        value.PreviousPosition = muzzle;
        value.Special = special;
        value.FlightVelocity =
            direction *
            (((projectileSpeed > 0 ? projectileSpeed : _config->HookProjectileSpeed)) + inherited);
        return value;
    }());

    if (side == HookSide::Left)
        _launchPointLeft = Vec3::Zero();
    else
        _launchPointRight = Vec3::Zero();

    ConsumeGas(_config->HookLaunchGasCost);

    world->OnCharacterEvent(
        (side == HookSide::Left ? CharacterEvent::HookLaunchedLeft : CharacterEvent::HookLaunchedRight));
}

void CharacterMotor::AdvanceHooks(float dt, LibAottg::ICharacterWorld *world) {

    AdvanceHook(_left, HookSide::Left, dt, world);

    AdvanceHook(_right, HookSide::Right, dt, world);
}

void CharacterMotor::AdvanceHook(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side, float dt,
                                 LibAottg::ICharacterWorld *world) {

    if (rope.Special && (rope.Phase == HookPhase::Attached || rope.Phase == HookPhase::Retracting) &&
        ++rope.SpiralTicks >= 60) {

        rope = LibAottg::CharacterMotor::Rope();

        return;
    }

    auto geometry = dynamic_cast<LibAottg::IHookGeometryWorld *>(world);

    if (rope.Phase == HookPhase::Flying) {

        LibAottg::Vec3 end = rope.Position + rope.FlightVelocity * dt;

        LibAottg::HookHit hit{};

        LibAottg::Vec3 segment = end - rope.PreviousPosition;

        bool collided = (geometry != nullptr ? geometry->SweepHook(rope.PreviousPosition, end, side, hit)
                                             : world->TryHook(rope.PreviousPosition, segment.Normalized(),
                                                              segment.Length(), side, hit));
        // An adapter must return a hit on this segment, never a distant aim target.
        if (collided && (hit.Point - rope.PreviousPosition).Length() <= segment.Length() + 0.001f) {

            rope.Position = hit.Point;

            rope.TargetId = hit.TargetId;

            rope.LocalPoint = hit.LocalPoint;

            rope.Age = 0;

            if (side == HookSide::Left) {

                _launchLeft = true;

                _elapsedLeft = 0;

                _launchPointLeft = hit.Point;
            } else {

                _launchRight = true;

                _elapsedRight = 0;

                _launchPointRight = hit.Point;
            }

            bool special = _animation.Clip == "attack5" || _animation.Clip == "special_petra";

            if (!special) {

                CancelAction(world);

                State = CharacterState::Idle;
            }

            _dashTime = 0;

            _jumpPending = false;

            _wallRunTime = 0;

            LibAottg::Vec3 launch = (hit.Point - _position).Normalized() * 20;
            // Bullet sets its phase AFTER HERO.launch, so this newly landing side is not hooked yet.
            launch.Y = cs_max(launch.Y, static_cast<float>(30));

            if (hit.Point.Y >= _position.Y)
                launch.Y += (hit.Point.Y - _position.Y) * 10;

            if (!special)
                _pendingForce += launch;

            _skipGasThisTick = true;

            FacingDegrees = FacingTargetDegrees = Yaw(launch);

            if (!special)
                Play((Equipment == EquipmentMode::Guns
                          ? "AHSS_hook_forward_both"
                          : (side == HookSide::Left
                                 ? ((_rightHookedAtPhysicsStart ? "dash" : "air_hook_l_just"))
                                 : ((_leftHookedAtPhysicsStart ? "dash" : "air_hook_r_just")))),
                     false, static_cast<float>(1), 0.1f, static_cast<float>(0), true);
            // This exception is selected by the current clip, not Bullet.leviMode.
            // Even a normal hook landing during Levi grants the RIGHT timer a long grace.
            if (special)
                _elapsedRight = -100;

            if (_animation.Clip == "special_petra") {

                _elapsedLeft = _elapsedRight = -100;

                Disable(_left, HookSide::Left, world);

                Disable(_right, HookSide::Right, world);

                return;
            }

            rope.Phase = HookPhase::Attached;

            world->OnCharacterEvent((side == HookSide::Left ? CharacterEvent::HookAttachedLeft
                                                            : CharacterEvent::HookAttachedRight));
        } else {

            rope.PreviousPosition = rope.Position;

            rope.Position = end;

            rope.Age += dt;

            if (rope.Age > _config->HookMissTimeout) {

                rope.Phase = HookPhase::Retracting;

                rope.Age = 0;

                rope.RetractStart = rope.Position;

                world->OnCharacterEvent((side == HookSide::Left ? CharacterEvent::HookMissedLeft
                                                                : CharacterEvent::HookMissedRight));
            }
        }
    }
}

void CharacterMotor::AdvanceRetraction(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                                       float dt, LibAottg::ICharacterWorld *world) {

    if (rope.Special && rope.Phase != HookPhase::Inactive) {

        rope.SpecialTime += dt;

        if (rope.SpecialTime > 0.4f && (rope.Phase == HookPhase::Flying || rope.Phase == HookPhase::Attached))
            Disable(rope, side, world);

        if (rope.Phase == HookPhase::Retracting)
            return;
    }

    if (rope.Phase != HookPhase::Retracting)
        return;

    rope.Age += dt;

    auto geometry = dynamic_cast<LibAottg::IHookGeometryWorld *>(world);

    rope.Position =
        Vec3::Lerp(rope.RetractStart, geometry->HookOrigin(this, side),
                   (_config->HookRetractDuration > 0 ? rope.Age / _config->HookRetractDuration : 1));

    if (rope.Age > _config->HookRetractDuration)
        rope = LibAottg::CharacterMotor::Rope();
}

void CharacterMotor::UpdateHookAnchor(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                                      LibAottg::ICharacterWorld *world) {

    if (rope.Phase == HookPhase::Attached && rope.TargetId != 0) {

        auto geometry = dynamic_cast<LibAottg::IHookGeometryWorld *>(world);

        LibAottg::Vec3 anchor{};

        if (geometry == nullptr || !geometry->ResolveHookAnchor(rope.TargetId, rope.LocalPoint, anchor)) {

            Release(rope, side, world);

            return;
        }

        rope.Position = anchor;
    }
}

void CharacterMotor::TickLaunchedHook(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                                      bool held, float dt, LibAottg::ICharacterWorld *world) {

    if ((side == HookSide::Left ? _launchLeft : _launchRight)) {

        float elapsed = (side == HookSide::Left ? (_elapsedLeft += dt) : (_elapsedRight += dt));
        // The special blocks QHold/EHold even if the physical buttons remain held.
        if (HookInputAllowed() && held && Gas > 0)
            ConsumeGas(_config->HookGasUsePerSecond * dt);
        else if (elapsed > _config->HookReleaseGrace)
            Release(rope, side, world);
    }
}

bool CharacterMotor::RopeForce(LibAottg::CharacterMotor::Rope rope, bool otherLaunched, bool boost,
                               LibAottg::Vec3 &acceleration) {

    if (rope.Phase != HookPhase::Attached)
        return false;

    LibAottg::Vec3 direction = (rope.Position - _position).Normalized();

    bool away = Vec3::Dot(_velocity, direction) < 0;

    if (away && boost)
        return true;
    // HERO halves each pull when both sides are launched. There is no rope-length constraint.
    float pull = (otherLaunched ? _config->HookPullAcceleration * 0.5f : _config->HookPullAcceleration);

    acceleration += direction * (pull / _config->Mass);

    if (away)
        acceleration -= _velocity * 2;

    return false;
}

void CharacterMotor::Release(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                             LibAottg::ICharacterWorld *world) {

    if (side == HookSide::Left)
        _launchLeft = false;
    else
        _launchRight = false;

    if (rope.Phase == HookPhase::Inactive)
        return;

    rope = LibAottg::CharacterMotor::Rope();

    world->OnCharacterEvent(
        (side == HookSide::Left ? CharacterEvent::HookReleasedLeft : CharacterEvent::HookReleasedRight));
}

void CharacterMotor::Disable(LibAottg::CharacterMotor::Rope &rope, LibAottg::HookSide side,
                             LibAottg::ICharacterWorld *world) {

    if (rope.Phase == HookPhase::Inactive || rope.Phase == HookPhase::Retracting)
        return;

    rope.Phase = HookPhase::Retracting;

    rope.Age = 0;

    rope.RetractStart = rope.Position;

    world->OnCharacterEvent(
        (side == HookSide::Left ? CharacterEvent::HookReleasedLeft : CharacterEvent::HookReleasedRight));
}

void CharacterMotor::ReleaseHook(LibAottg::HookSide side, LibAottg::ICharacterWorld *world) {

    if (world == nullptr)
        throw std::invalid_argument(cs_string("world"));

    if (side == HookSide::Left)
        Release(_left, side, world);
    else
        Release(_right, side, world);
}

LibAottg::Vec3 CharacterMotor::RotateTowards(LibAottg::Vec3 from, LibAottg::Vec3 to, float radians) {

    float a = from.Length(), b = to.Length();

    if (a < 0.00001f)
        return to;

    if (b < 0.00001f)
        return from;

    LibAottg::Vec3 u = from / a, v = to / b;

    float angle = static_cast<float>(std::acos(
        static_cast<double>(Math3::Clamp(Vec3::Dot(u, v), static_cast<float>(-1), static_cast<float>(1)))));

    if (angle <= radians)
        return to;

    LibAottg::Vec3 tangent = (v - u * Vec3::Dot(u, v)).Normalized();

    if (tangent.LengthSquared() < 0.001f) {
        // Unity's OrthoNormalVectorFast branch, checked against the 4.6 player.
        LibAottg::Vec3 axis = (std::abs(u.Z) > 0.70710678f ? LibAottg::Vec3(0, -u.Z, u.Y).Normalized()
                                                           : LibAottg::Vec3(-u.Y, u.X, 0).Normalized());

        tangent = Vec3::Cross(axis, u);
    }

    return (u * static_cast<float>(std::cos(static_cast<double>(radians))) +
            tangent * static_cast<float>(std::sin(static_cast<double>(radians)))) *
           a;
}

void CharacterMotor::GroundMovement(LibAottg::Vec3 desired, LibAottg::CharacterInput, float dt,
                                    LibAottg::ICharacterWorld *world) {

    if (!_wasGrounded && !IsAction()) {

        _dashTime = _wallRunTime = 0;

        world->OnCharacterEvent(CharacterEvent::Landed);

        if (desired.LengthSquared() == 0 && !LeftHookVisible() && !RightHookVisible()) {

            State = CharacterState::Land;

            Play("dash_land", false, static_cast<float>(1), 0.01f);
        } else if (Math3::Horizontal(_velocity).LengthSquared() >
                   _config->GroundSpeed * _config->GroundSpeed * 1.5f) {

            State = CharacterState::Slide;

            FacingTargetDegrees = Yaw(_velocity);

            Play("slide", true, static_cast<float>(1), 0.05f);

            world->OnCharacterEvent(CharacterEvent::SlideStarted);
        }
    }

    LibAottg::Vec3 target = Vec3::Zero();

    if (IsAction()) {

        if (_actionState == CharacterState::Attack &&
            (_animation.Clip == "attack1" || _animation.Clip == "attack2" ||
             (_animation.Clip == "attack5" && _animation.NormalizedTime() > 0.4f &&
              _animation.NormalizedTime() < 0.61f) ||
             (_animation.Clip == "special_petra" && _animation.NormalizedTime() > 0.35f &&
              _animation.NormalizedTime() < 0.48f)))
            _pendingForce += Math3::Direction(FacingDegrees) * 200;
    } else if (State == CharacterState::GroundDodge && _animation.Clip == "dodge") {

        float t = _animation.NormalizedTime();

        if (t >= 0.2f && t < 0.8f)
            target = -Math3::Direction(FacingDegrees) * _config->GroundDodgeSpeed;
        else if (t > 0.8f)
            target = Math3::Horizontal(_velocity) * 0.9f;
    } else if (State == CharacterState::Land && _animation.NormalizedTime() < 1)
        target = Math3::Horizontal(_velocity) * 0.96f;
    else if (State == CharacterState::Slide) {

        target = Math3::Horizontal(_velocity) * 0.99f;

        if (_velocity.Length() < _config->GroundSpeed * 1.2f)
            State = CharacterState::Idle;
    } else {

        target = desired * (_config->GroundSpeed * ((_speedBuff > 0 ? 4 : 1)));

        if (desired.LengthSquared() > 0)
            FacingTargetDegrees = Yaw(desired);
    }
    // Unity queues AddForce; hook angle tests and spin still see the pre-force velocity.
    if (!(IsAction() && Equipment == EquipmentMode::Guns && _actionState == CharacterState::Attack))
        _pendingVelocityChange +=
            LibAottg::Vec3(Math3::Clamp(target.X - _velocity.X, -_config->MaxGroundVelocityChange,
                                        _config->MaxGroundVelocityChange),
                           0,
                           Math3::Clamp(target.Z - _velocity.Z, -_config->MaxGroundVelocityChange,
                                        _config->MaxGroundVelocityChange));

    TurnTowards(FacingTargetDegrees, dt * 10);

    if (_animation.Clip == "jump" && _animation.NormalizedTime() > 0.18f) {

        _pendingVelocityChange += Vec3::Up() * _config->JumpSpeed;

        if (_jumpPending) {

            _jumpPending = false;

            world->OnCharacterEvent(CharacterEvent::Jumped);
        }
    }
}

void CharacterMotor::AirMovement(LibAottg::Vec3 desired, LibAottg::CharacterInput input,
                                 LibAottg::CharacterContacts contacts, float dt, bool spin,
                                 LibAottg::Vec3 &acceleration, LibAottg::ICharacterWorld *world) {

    if (State == CharacterState::RoofVault && _animation.Clip == "toRoof") {

        if (_config->NoForcedToRoof &&
            (desired.LengthSquared() == 0 || (_config->AutoWallJump && input.Dodge.Held))) {

            WallJump(world);

            return;
        }

        if (_animation.NormalizedTime() < 0.22f)
            SetReferenceVelocity(Vec3::Zero());
        else {

            if (!_roofImpulse) {

                _pendingImpulse += Vec3::Up() * _config->RoofVaultSpeed;

                _roofImpulse = true;
            }

            _pendingImpulse += Math3::Direction(FacingDegrees) * 0.05f;
        }

        if (_animation.NormalizedTime() >= 1)
            State = CharacterState::Fall;

        return;
    }

    if (State == CharacterState::WallRun) {

        _pendingVelocityChange += Vec3::Up() * _config->WallRunSpeed - _velocity;

        _wallRunTime += dt;

        if (_wallRunTime > _config->WallRunMaxDuration || desired.LengthSquared() == 0 ||
            (_config->AutoWallJump && input.Dodge.Held))
            WallJump(world);
        else if (contacts.UpperFrontClear) {

            State = CharacterState::RoofVault;

            _roofImpulse = false;

            Play("toRoof", false);

            world->OnCharacterEvent(CharacterEvent::RoofVaulted);
        } else if (!contacts.FrontWall) {

            State = CharacterState::Fall;

            world->OnCharacterEvent(CharacterEvent::WallRunEnded);
        }

        return;
    }

    float angle = std::abs(DeltaAngle(FacingDegrees, Yaw(desired)));

    if (!IsAction() && _dashTime <= 0 && State != CharacterState::GroundDodge &&
        desired.LengthSquared() > 0 && angle < 45 && !input.Jump.Held && !input.HookLeft.Held &&
        !input.HookRight.Held && !input.HookBoth.Held && contacts.FrontWall) {

        State = CharacterState::WallRun;

        _wallRunTime = 0;

        Play("wallrun", true);

        world->OnCharacterEvent(CharacterEvent::WallRunStarted);

        return;
    }
    // Direction input alone never brakes or caps aerial momentum.
    if (_dashTime <= 0 && _animation.Clip != "jump" && _animation.Clip != "dash" &&
        _animation.Clip != "attack5" && _animation.Clip != "special_petra" && !spin && input.Jump.Held &&
        Gas > 0) {

        acceleration += desired * _config->AirAcceleration;

        GasThrustActive = true;

        if (!_skipGasThisTick)
            ConsumeGas(_config->GasUsePerSecond * dt);
    }

    if (desired.LengthSquared() > 0 && _dashTime <= 0)
        FacingTargetDegrees = Yaw(desired);
}

void CharacterMotor::WallJump(LibAottg::ICharacterWorld *world) {

    _pendingImpulse -= Math3::Direction(FacingDegrees) * (_config->GroundSpeed * 0.75f);

    State = CharacterState::GroundDodge;

    Play("dodge", false, static_cast<float>(1), 0.1f, _config->GroundDodgeDuration * 0.2f, true);

    _wallRunTime = 0;

    world->OnCharacterEvent(CharacterEvent::WallRunEnded);

    world->OnCharacterEvent(CharacterEvent::WallJumped);
}

float CharacterMotor::DeltaAngle(float a, float b) {

    return cs_rem((cs_rem((b - a + 540), 360) + 360), 360) - 180;
}

void CharacterMotor::TurnTowards(float target, float factor) {
    // Quaternion.Lerp for a pure yaw, rather than a linear interpolation of Euler degrees.
    double half = DeltaAngle(FacingDegrees, target) * cs_pi / 360;

    float t = Math3::Clamp(factor, static_cast<float>(0), static_cast<float>(1));

    FacingDegrees +=
        static_cast<float>((2 *
                            std::atan2(static_cast<double>(t * std::sin(static_cast<double>(half))),
                                       static_cast<double>(1 - t + t * std::cos(static_cast<double>(half)))) *
                            180 / cs_pi));
}

LibAottg::ICharacterWorld *CharacterMotor::BindWorld(LibAottg::ICharacterWorld *world) {

    _world->Host = world;

    return _world;
}
CharacterMotor::WorldBoundary::WorldBoundary(LibAottg::CharacterMotor *motor) {

    _motor = motor;
}

bool CharacterMotor::WorldBoundary::TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                                            LibAottg::HookSide side, LibAottg::HookHit &hit) {

    bool found = Host->TryHook(origin * _motor->Scale, direction, distance * _motor->Scale, side, hit);

    hit.Point /= _motor->Scale;

    return found;
}

LibAottg::Vec3 CharacterMotor::WorldBoundary::HookOrigin(LibAottg::CharacterMotor *motor,
                                                         LibAottg::HookSide side) {

    auto geometry = dynamic_cast<LibAottg::IHookGeometryWorld *>(Host);

    return (geometry != nullptr ? geometry->HookOrigin(motor, side) / motor->Scale
                                : motor->_position + Vec3::Up() * 0.85f);
}

bool CharacterMotor::WorldBoundary::SweepHook(LibAottg::Vec3 start, LibAottg::Vec3 end,
                                              LibAottg::HookSide side, LibAottg::HookHit &hit) {

    auto geometry = dynamic_cast<LibAottg::IHookGeometryWorld *>(Host);

    if (geometry == nullptr) {

        LibAottg::Vec3 segment = end - start;

        return TryHook(start, segment.Normalized(), segment.Length(), side, hit);
    }

    bool found = geometry->SweepHook(start * _motor->Scale, end * _motor->Scale, side, hit);

    hit.Point /= _motor->Scale;

    return found;
}

bool CharacterMotor::WorldBoundary::ResolveHookAnchor(int targetId, LibAottg::Vec3 localPoint,
                                                      LibAottg::Vec3 &point) {

    auto geometry = dynamic_cast<LibAottg::IHookGeometryWorld *>(Host);

    point = Vec3::Zero();

    if (geometry == nullptr || !geometry->ResolveHookAnchor(targetId, localPoint, point))
        return false;

    point /= _motor->Scale;

    return true;
}

void CharacterMotor::WorldBoundary::OnCharacterEvent(LibAottg::CharacterEvent kind) {

    Host->OnCharacterEvent(kind);
}
float KinematicCollisionWorld::Radius() const {

    return _radius * WorldScale;
}
float KinematicCollisionWorld::Height() const {

    return _height * WorldScale;
}
KinematicCollisionWorld::KinematicCollisionWorld(float radius, float height, float worldScale) {

    if (radius <= 0 || height < 2 * radius || !WorldBox::Finite(LibAottg::Vec3(radius, height, 0)))
        throw std::invalid_argument(cs_string("radius") + std::string(": ") +
                                    cs_string("Require finite positive radius and height >= diameter."));

    if (worldScale <= 0 || !WorldBox::Finite(LibAottg::Vec3(worldScale, 0, 0)))
        throw std::invalid_argument(cs_string("worldScale"));

    WorldScale = worldScale;

    _radius = radius;

    _height = height;
}

void KinematicCollisionWorld::OnCharacterEvent(LibAottg::CharacterEvent kind) {

    if (Event != nullptr)
        Event(kind);
}

bool KinematicCollisionWorld::TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                                      LibAottg::HookSide, LibAottg::HookHit &hit) {

    origin /= WorldScale;

    distance /= WorldScale;

    direction = direction.Normalized();

    float nearest = distance;

    bool found = false;

    for (auto hostBox : Boxes) {

        LibAottg::WorldBox box = ReferenceBox(hostBox);

        float t{};

        if (RayBox(origin, direction, box, t) && t >= 0 && t <= nearest) {

            nearest = t;

            found = true;
        }
    }

    if (direction.Y < -0.00001f && origin.Y >= 0) {

        float t = -origin.Y / direction.Y;

        if (t <= nearest) {

            nearest = t;

            found = true;
        }
    }

    hit = (found ? LibAottg::HookHit((origin + direction * nearest) * WorldScale) : LibAottg::HookHit{});

    return found;
}

LibAottg::CharacterContacts KinematicCollisionWorld::Contacts(LibAottg::Vec3 feet, LibAottg::Vec3 forward) {

    LibAottg::HookHit hit{};

    LibAottg::Vec3 direction = LibAottg::Vec3(forward.X, 0, forward.Z).Normalized();

    bool ground =
        TryHook(feet + Vec3::Up() * (0.1f * WorldScale), -Vec3::Up(), 0.3f * WorldScale, HookSide::Left, hit);

    bool front = TryHook(feet + Vec3::Up() * WorldScale, direction, WorldScale, HookSide::Left, hit);

    bool upper =
        !TryHook(feet + Vec3::Up() * (3.0f * WorldScale), direction, 1.2f * WorldScale, HookSide::Left, hit);

    return ([&] {
        auto value = LibAottg::CharacterContacts{};
        value.Grounded = ground;
        value.GroundNormal = Vec3::Up();
        value.FrontWall = front;
        value.UpperFrontClear = upper;
        return value;
    }());
}

LibAottg::WorldBox KinematicCollisionWorld::ReferenceBox(LibAottg::WorldBox box) {

    return LibAottg::WorldBox(box.Min / WorldScale, box.Max / WorldScale);
}

LibAottg::CollisionResult KinematicCollisionWorld::Move(LibAottg::Vec3 start, LibAottg::Vec3 target,
                                                        LibAottg::Vec3 velocity) {

    if (!WorldBox::Finite(start) || !WorldBox::Finite(target) || !WorldBox::Finite(velocity))
        throw std::invalid_argument(cs_string("Collision inputs must be finite."));

    if (Friction < 0 || !WorldBox::Finite(LibAottg::Vec3(Friction, 0, 0)))
        throw std::invalid_argument(cs_string("Friction"));

    start /= WorldScale;

    target /= WorldScale;

    velocity /= WorldScale;

    LibAottg::Vec3 position = Depenetrate(start);

    bool collided = (position - start).LengthSquared() > 0;

    LibAottg::Vec3 remaining = target - start;

    float step =
        (velocity.LengthSquared() > 0
             ? cs_max(static_cast<float>(0), Vec3::Dot(remaining, velocity) / velocity.LengthSquared())
             : 0);

    bool ground = false;

    for (int iteration = 0; iteration < 8 && remaining.LengthSquared() > 1e-12f; iteration++) {

        float t{};

        LibAottg::Vec3 normal{};

        if (!Sweep(position, remaining, t, normal)) {

            position += remaining;

            break;
        }

        bool restingContact = std::abs(Vec3::Dot(remaining, normal)) * t <= Skin * 1.5f;

        collided = true;

        ground |= normal.Y > 0.5f;

        position += remaining * t + normal * Skin;

        remaining *= 1 - t;

        float into = Vec3::Dot(remaining, normal);

        if (into < 0)
            remaining -= normal * into;

        into = Vec3::Dot(velocity, normal);

        if (into < 0) {

            velocity -= normal * into;

            float tangentSpeed = velocity.Length();

            if (tangentSpeed > 0) {

                LibAottg::Vec3 beforeFriction = velocity;

                velocity *= cs_max(static_cast<float>(0), 1 - Friction * -into / tangentSpeed);

                LibAottg::Vec3 change = velocity - beforeFriction;

                remaining += change * (step * (1 - t));

                if (restingContact)
                    position += change * (step * t);
            }
        }
    }

    return ([&] {
        auto value = LibAottg::CollisionResult{};
        value.Position = position * WorldScale;
        value.Velocity = velocity * WorldScale;
        value.Collided = collided;
        value.Grounded = ground;
        return value;
    }());
}

LibAottg::Vec3 KinematicCollisionWorld::Depenetrate(LibAottg::Vec3 feet) {

    for (int pass = 0; pass < 12; pass++) {

        bool corrected = false;

        if (feet.Y < 0) {

            feet.Y = Skin;

            corrected = true;
        }

        for (auto hostBox : Boxes) {

            LibAottg::WorldBox box = ReferenceBox(hostBox);

            auto bounds = ExpandedSegment(box);

            LibAottg::Vec3 center = feet + Vec3::Up() * (_height / 2);

            LibAottg::Vec3 delta = center - Closest(center, bounds);

            float length = delta.Length();

            if (length >= _radius)
                continue;

            LibAottg::Vec3 normal{};

            float depth{};

            if (length > 0.000001f) {

                normal = delta / length;

                depth = _radius - length;
            } else {

                float best = center.X - bounds.Min.X;

                normal = LibAottg::Vec3(-1, 0, 0);

                Choose(bounds.Max.X - center.X, LibAottg::Vec3(1, 0, 0), best, normal);

                Choose(center.Y - bounds.Min.Y, LibAottg::Vec3(0, -1, 0), best, normal);

                Choose(bounds.Max.Y - center.Y, Vec3::Up(), best, normal);

                Choose(center.Z - bounds.Min.Z, LibAottg::Vec3(0, 0, -1), best, normal);

                Choose(bounds.Max.Z - center.Z, LibAottg::Vec3(0, 0, 1), best, normal);

                depth = best + _radius;
            }

            feet += normal * (depth + Skin);

            corrected = true;
        }

        if (!corrected)
            break;
    }

    return feet;
}

void KinematicCollisionWorld::Choose(float value, LibAottg::Vec3 candidate, float &best,
                                     LibAottg::Vec3 &normal) {

    if (value < best) {

        best = value;

        normal = candidate;
    }
}

bool KinematicCollisionWorld::Sweep(LibAottg::Vec3 feet, LibAottg::Vec3 motion, float &time,
                                    LibAottg::Vec3 &normal) {

    time = 1;

    normal = Vec3::Zero();

    bool found = false;

    if (motion.Y < 0 && feet.Y + motion.Y <= 0) {

        time = cs_max(static_cast<float>(0), -feet.Y / motion.Y);

        normal = Vec3::Up();

        found = true;
    }

    LibAottg::Vec3 center = feet + Vec3::Up() * (_height / 2);

    for (auto hostBox : Boxes) {

        LibAottg::WorldBox box = ReferenceBox(hostBox);

        float t{};

        LibAottg::Vec3 n{};

        if (RoundedBoxSweep(center, motion, ExpandedSegment(box), _radius, t, n) && t <= time) {

            time = t;

            normal = n;

            found = true;
        }
    }

    return found;
}

LibAottg::WorldBox KinematicCollisionWorld::ExpandedSegment(LibAottg::WorldBox box) {

    float halfSegment = _height / 2 - _radius;

    return LibAottg::WorldBox(box.Min - Vec3::Up() * halfSegment, box.Max + Vec3::Up() * halfSegment);
}

LibAottg::Vec3 KinematicCollisionWorld::Closest(LibAottg::Vec3 p, LibAottg::WorldBox b) {

    return LibAottg::Vec3(Math3::Clamp(p.X, b.Min.X, b.Max.X), Math3::Clamp(p.Y, b.Min.Y, b.Max.Y),
                          Math3::Clamp(p.Z, b.Min.Z, b.Max.Z));
}

float KinematicCollisionWorld::Axis(LibAottg::Vec3 v, int i) {

    return (i == 0 ? v.X : (i == 1 ? v.Y : v.Z));
}

LibAottg::Vec3 KinematicCollisionWorld::Unit(int i) {

    return (i == 0 ? LibAottg::Vec3(1, 0, 0) : (i == 1 ? Vec3::Up() : LibAottg::Vec3(0, 0, 1)));
}
// Capsule-box Minkowski sum = box expanded by vertical capsule segment, rounded by radius.
// Test its six faces, twelve edge cylinders and eight corner spheres analytically.
bool KinematicCollisionWorld::RoundedBoxSweep(LibAottg::Vec3 origin, LibAottg::Vec3 motion,
                                              LibAottg::WorldBox box, float radius, float &time,
                                              LibAottg::Vec3 &normal) {

    float best = 2;

    LibAottg::Vec3 bestNormal = Vec3::Zero();

    for (int axis = 0; axis < 3; axis++)
        for (int side = 0; side < 2; side++) {

            float velocity = Axis(motion, axis);

            if (std::abs(velocity) < 1e-10f)
                continue;

            float boundary = Axis((side == 0 ? box.Min : box.Max), axis) + ((side == 0 ? -radius : radius));

            Accept((boundary - Axis(origin, axis)) / velocity, origin, motion, box, radius, best, bestNormal);
        }

    for (int axis = 0; axis < 3; axis++)
        for (int a = 0; a < 2; a++)
            for (int b = 0; b < 2; b++) {

                int u = (axis + 1) % 3, v = (axis + 2) % 3;

                LibAottg::Vec3 edge = Unit(u) * Axis((a == 0 ? box.Min : box.Max), u) +
                                      Unit(v) * Axis((b == 0 ? box.Min : box.Max), v);

                LibAottg::Vec3 d = origin - edge;

                d -= Unit(axis) * Axis(d, axis);

                LibAottg::Vec3 m = motion - Unit(axis) * Axis(motion, axis);

                RootCandidate(d, m, origin, motion, box, radius, best, bestNormal);
            }

    for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++) {

                auto corner =
                    LibAottg::Vec3((x == 0 ? box.Min.X : box.Max.X), (y == 0 ? box.Min.Y : box.Max.Y),
                                   (z == 0 ? box.Min.Z : box.Max.Z));

                RootCandidate(origin - corner, motion, origin, motion, box, radius, best, bestNormal);
            }

    time = best;

    normal = bestNormal;

    return best <= 1;
}

void KinematicCollisionWorld::RootCandidate(LibAottg::Vec3 d, LibAottg::Vec3 m, LibAottg::Vec3 origin,
                                            LibAottg::Vec3 motion, LibAottg::WorldBox box, float radius,
                                            float &best, LibAottg::Vec3 &normal) {

    double a = Vec3::Dot(m, m), b = 2.0 * Vec3::Dot(d, m), c = Vec3::Dot(d, d) - radius * radius;

    if (a < 1e-16)
        return;

    double discriminant = b * b - 4 * a * c;

    if (discriminant < 0)
        return;

    float t = static_cast<float>(((-b - std::sqrt(static_cast<double>(discriminant))) / (2 * a)));

    Accept(t, origin, motion, box, radius, best, normal);
}

void KinematicCollisionWorld::Accept(float t, LibAottg::Vec3 origin, LibAottg::Vec3 motion,
                                     LibAottg::WorldBox box, float radius, float &best,
                                     LibAottg::Vec3 &normal) {

    if (t < -0.000001f || t > 1 || t >= best)
        return;

    t = cs_max(static_cast<float>(0), t);

    LibAottg::Vec3 point = origin + motion * t, delta = point - Closest(point, box);

    float distance = delta.Length();

    if (std::abs(distance - radius) > 0.0001f || distance < 1e-8f)
        return;

    LibAottg::Vec3 n = delta / distance;

    if (Vec3::Dot(motion, n) >= -1e-8f)
        return;

    best = t;

    normal = n;
}

bool KinematicCollisionWorld::RayBox(LibAottg::Vec3 origin, LibAottg::Vec3 direction, LibAottg::WorldBox box,
                                     float &time) {

    float near = 0, far = std::numeric_limits<float>::infinity();

    for (int i = 0; i < 3; i++) {

        float o = Axis(origin, i), d = Axis(direction, i), min = Axis(box.Min, i), max = Axis(box.Max, i);

        if (std::abs(d) < 1e-8f) {

            if (o < min || o > max) {

                time = 0;

                return false;
            }

            continue;
        }

        float a = (min - o) / d, b = (max - o) / d;

        near = cs_max(near, cs_min(a, b));

        far = cs_min(far, cs_max(a, b));

        if (near > far) {

            time = 0;

            return false;
        }
    }

    time = near;

    return far >= 0;
}

bool HeroWorld::Raycast(LibAottg::Vec3, LibAottg::Vec3, float, LibAottg::HeroQuery, LibAottg::HookHit &hit) {

    hit = LibAottg::HookHit{};

    return false;
}

LibAottg::CollisionResult HeroWorld::Move(LibAottg::CharacterMotor *motor, LibAottg::Vec3, LibAottg::HeroPose,
                                          float) {

    return ([&] {
        auto value = LibAottg::CollisionResult{};
        value.Position = motor->Position();
        value.Velocity = motor->Velocity();
        return value;
    }());
}

LibAottg::CharacterContacts HeroWorld::GetContacts(LibAottg::CharacterMotor *motor, LibAottg::HeroPose pose) {

    LibAottg::HookHit hit{};

    float scale = motor->Scale;

    return ([&] {
        auto value = LibAottg::CharacterContacts{};
        value.Grounded = Raycast(motor->Position() + Vec3::Up() * (0.1f * scale), -Vec3::Up(), 0.3f * scale,
                                 HeroQuery::Ground, hit);
        value.GroundNormal = Vec3::Up();
        value.FrontWall =
            Raycast(motor->Position() + pose.Up() * scale, pose.Forward(), scale, HeroQuery::Wall, hit);
        value.UpperFrontClear = !Raycast(motor->Position() + pose.Up() * (3 * scale), pose.Forward(),
                                         1.2f * scale, HeroQuery::Wall, hit);
        return value;
    }());
}

LibAottg::Vec3 HeroWorld::HookOrigin(LibAottg::CharacterMotor *motor, LibAottg::HookSide) {

    return motor->Position() + Vec3::Up() * (0.85f * motor->Scale);
}

bool HeroWorld::TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance, LibAottg::HookSide,
                        LibAottg::HookHit &hit) {

    return Raycast(origin, direction, distance, HeroQuery::HookAim, hit);
}

bool HeroWorld::SweepHook(LibAottg::Vec3 start, LibAottg::Vec3 end, LibAottg::HookSide,
                          LibAottg::HookHit &hit) {

    LibAottg::Vec3 d = end - start;

    return Raycast(start, d.Normalized(), d.Length(), HeroQuery::HookProjectile, hit);
}

bool HeroWorld::ResolveHookAnchor(int, LibAottg::Vec3, LibAottg::Vec3 &worldPoint) {

    worldPoint = Vec3::Zero();

    return false;
}

void HeroWorld::OnCharacterEvent(LibAottg::CharacterEvent) {}

bool HeroWorld::SweepCamera(LibAottg::Vec3 pivot, LibAottg::Vec3 position, float radius,
                            LibAottg::Vec3 &safePosition) {

    LibAottg::Vec3 d = position - pivot;

    LibAottg::HookHit hit{};

    bool found = Raycast(pivot, d.Normalized(), d.Length() + radius, HeroQuery::Camera, hit);

    safePosition =
        (found ? pivot + d.Normalized() * cs_max(static_cast<float>(0), (hit.Point - pivot).Length() - radius)
               : position);

    return found;
}

void HeroWorld::CollectWeaponHits(LibAottg::HeroWeaponSweep sweep, std::vector<LibAottg::HeroHit> &hits) {

    if (!sweep.Gun)
        return;

    LibAottg::Vec3 d = sweep.End - sweep.Start;

    LibAottg::HookHit hit{};

    if (Raycast(sweep.Start, d.Normalized(), d.Length(), HeroQuery::Gun, hit) && hit.TargetId != 0)
        hits.push_back(([&] {
            auto value = LibAottg::HeroHit{};
            value.TargetId = hit.TargetId;
            value.Point = hit.Point;
            return value;
        }()));
}

void HeroWorld::OnHeroHit(LibAottg::HeroHit) {}
CalibrationHeroWorld::CalibrationHeroWorld(float radius, float height, float worldScale) {

    ownedCollision = std::make_unique<KinematicCollisionWorld>(radius, height, worldScale);
    Collision = ownedCollision.get();
}

bool CalibrationHeroWorld::Raycast(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                                   LibAottg::HeroQuery, LibAottg::HookHit &hit) {

    return Collision->TryHook(origin, direction, distance, HookSide::Left, hit);
}

LibAottg::CollisionResult CalibrationHeroWorld::Move(LibAottg::CharacterMotor *motor, LibAottg::Vec3 start,
                                                     LibAottg::HeroPose, float) {

    return Collision->Move(start, motor->Position(), motor->Velocity());
}
LibAottg::IHeroPresentation *HeroController::Presentation() const {

    return presentation;
}
void HeroController::SetPresentation(LibAottg::IHeroPresentation *value) {

    if (!(presentation == value))
        ClearWeaponHistory();

    presentation = value;
}
HeroController::HeroController(LibAottg::CharacterConfig *config, LibAottg::IHeroWorld *world,
                               float fixedStep) {

    ownedMotor = std::make_unique<CharacterMotor>(config);
    Motor = ownedMotor.get();

    if (fixedStep <= 0 || !WorldBox::Finite(LibAottg::Vec3(fixedStep, 0, 0)) ||
        fixedStep > Motor->Config()->MaxStep)
        throw std::invalid_argument(cs_string("fixedStep"));

    FixedStep = fixedStep;

    if (!world)
        defaultWorld = std::make_unique<HeroWorld>();
    this->world = world ? world : defaultWorld.get();

    ownedInput = std::make_unique<HeroInputMapper>();
    Input = ownedInput.get();

    ownedServices = std::make_unique<Services>(this);
    services = ownedServices.get();

    Respawn(Vec3::Zero());
}

void HeroController::Respawn(LibAottg::Vec3 position, bool grounded) {

    Motor->Respawn(position, grounded);

    Input->Reset();

    accumulator = 0;

    PhysicsTicks = 0;

    hookSoundPending = false;

    ClearWeaponHistory();

    attackWindow = false;

    struck.clear();

    gunShots.clear();

    Pose = previous = RenderPose = BodyPose();

    LastInput = LibAottg::CharacterInput{};

    if (Camera != nullptr)
        Camera->Reset();

    Sample(Pose, static_cast<float>(0));
}

void HeroController::Grab() {

    Motor->Grab(services);

    Synchronize();
}

void HeroController::EscapeGrab(LibAottg::Vec3 position) {

    Motor->EscapeGrab(position, services);

    Synchronize();
}

void HeroController::Die() {

    Motor->Die(services);

    Synchronize();
}

void HeroController::BladeHit(int amount) {

    Motor->BladeHit(services, amount);
}

void HeroController::SetEquipment(LibAottg::EquipmentMode equipment) {

    Motor->SetEquipment(equipment);

    ClearWeaponHistory();
}

bool HeroController::TrySetEquipment(LibAottg::EquipmentMode equipment) {

    if (Motor->State == CharacterState::Attack || Motor->State == CharacterState::Reload ||
        Motor->State == CharacterState::Supply)
        return false;

    SetEquipment(equipment);

    return true;
}

void HeroController::Synchronize() {

    Pose = previous = RenderPose = BodyPose();

    accumulator = 0;

    ClearWeaponHistory();

    Sample(Pose, static_cast<float>(0));
}

void HeroController::ClearWeaponHistory() {

    weaponHistory[0] = weaponHistory[1] = false;
}

void HeroController::Advance(float frameDelta, LibAottg::HeroInputFrame raw) {

    ValidateDelta(frameDelta);

    float dt = cs_min(frameDelta, MaxFrameDelta);

    if (Camera != nullptr) {

        Camera->Look(dt, raw);

        Camera->Follow(static_cast<float>(0), Motor, Pose.Position, Head(), raw,
                       dynamic_cast<LibAottg::IHeroCameraWorld *>(world));
    }

    LibAottg::Vec3 forward =
        (Camera != nullptr ? Camera->Pose.Forward : Math3::Direction(Motor->FacingDegrees));

    LibAottg::Vec3 origin = (Camera != nullptr ? Camera->Pose.Position : Head());

    LibAottg::Vec3 aim = (Camera != nullptr ? Camera->Pose.AimDirection : forward);

    auto input = Input->Sample(raw, forward, origin, aim);

    Advance(dt, input, raw);
}

void HeroController::Advance(float frameDelta, LibAottg::CharacterInput input) {

    ValidateDelta(frameDelta);

    Advance(cs_min(frameDelta, MaxFrameDelta), input, LibAottg::HeroInputFrame{});
}

void HeroController::ValidateDelta(float dt) {

    if (!WorldBox::Finite(LibAottg::Vec3(dt, MaxFrameDelta, 0)) || dt <= 0 || MaxFrameDelta <= 0)
        throw std::invalid_argument(cs_string("frameDelta"));
}

void HeroController::Advance(float dt, LibAottg::CharacterInput input, LibAottg::HeroInputFrame raw) {

    input.SourceAttackSelection |= SourceAttackSelection;

    auto targets = dynamic_cast<LibAottg::IHeroAttackTargetWorld *>(world);

    LibAottg::Vec3 target{};

    if (!input.HasAttackTarget && targets != nullptr && targets->TryAttackTarget(Motor, input, target)) {

        input.HasAttackTarget = true;

        input.AttackTarget = target;
    }

    LibAottg::HookHit aimHit{};

    if (!input.HasAimTarget && input.AimDirection.LengthSquared() > 0) {
        Vec3 origin = input.HasAimOrigin ? input.AimOrigin : Motor->Position();
        Vec3 direction = input.AimDirection.Normalized();
        float distance = Motor->Config()->HookRange * Motor->Scale;
        float skip = Motor->Config()->ImprovedRaycasts ? std::min((origin - Motor->Position()).Length() * 0.5f, distance) : 0;
        if (world->TryHook(origin + direction * skip, direction, distance - skip, HookSide::Left, aimHit)) {
            input.HasAimTarget = true;
            input.AimTarget = aimHit.Point;
        } else if (Motor->Config()->ImprovedRaycasts) {
            input.HasAimTarget = true;
            input.AimTarget = origin + direction * distance;
        }
    }

    LastInput = input;

    Sample(Pose, static_cast<float>(0));

    Motor->AdvanceFrame(dt, input, world->GetContacts(Motor, Pose), services);

    if (hookSoundPending) {

        hookSoundPending = false;

        if (SoundRequested != nullptr)
            SoundRequested(LibAottg::HeroSoundCue(HeroSound::HookLaunch, Motor->Position()));
    }

    Pose = BodyPose();

    Sample(Pose, dt);
    // Source actions fire on the frame clock even when this frame has no physics tick.
    ProcessGuns(input);

    accumulator += dt;

    while (accumulator + 1e-9 >= FixedStep) {

        previous = Pose;

        LibAottg::Vec3 start = Motor->Position();

        Motor->StepPhysics(FixedStep, input, world->GetContacts(Motor, Pose), services);

        auto motion = world->Move(Motor, start, BodyPose(), FixedStep);

        Motor->SetPose(motion.Position, motion.Velocity, Motor->FacingDegrees);

        Pose = BodyPose();

        Sample(Pose, static_cast<float>(0));

        ProcessBlades();

        PhysicsTicks++;

        accumulator -= FixedStep;
    }

    float alpha = Math3::Clamp(static_cast<float>((accumulator / FixedStep)), static_cast<float>(0),
                               static_cast<float>(1));

    RenderPose = ([&] {
        auto value = LibAottg::HeroPose{};
        value.Position = Vec3::Lerp(previous.Position, Pose.Position, alpha);
        value.Yaw = previous.Yaw + HeroBodyPose::DeltaAngle(previous.Yaw, Pose.Yaw) * alpha;
        value.Pitch =
            (Motor->Grounded ? 0
                             : previous.Pitch + HeroBodyPose::DeltaAngle(previous.Pitch, Pose.Pitch) * alpha);
        value.Roll =
            (Motor->Grounded ? 0
                             : previous.Roll + HeroBodyPose::DeltaAngle(previous.Roll, Pose.Roll) * alpha);
        value.Leaning = Pose.Leaning;
        value.LeanLeft = Pose.LeanLeft;
        return value;
    }());

    Sample(RenderPose, static_cast<float>(0));

    if (Camera != nullptr)
        Camera->Follow(dt, Motor, RenderPose.Position, Head(), raw,
                       dynamic_cast<LibAottg::IHeroCameraWorld *>(world));
}

LibAottg::HeroPose HeroController::BodyPose() {

    return (ProceduralPose ? HeroBodyPose::Evaluate(Motor) : ([&] {
        auto value = LibAottg::HeroPose{};
        value.Position = Motor->Position();
        value.Yaw = Motor->FacingDegrees;
        return value;
    }()));
}

LibAottg::Vec3 HeroController::Head() {

    return (Presentation() != nullptr ? Presentation()->Head()
                                      : RenderPose.Position + Vec3::Up() * (1.5f * Motor->Scale));
}

void HeroController::Sample(LibAottg::HeroPose pose, float dt) {

    if (Presentation() != nullptr)
        Presentation()->Sample(Motor, pose, LastInput, dt);
}

void HeroController::Raise(LibAottg::CharacterEvent kind) {
    if (kind == CharacterEvent::Died) RequestSound(HeroSound::Death);

    if (kind == CharacterEvent::HookLaunchedLeft || kind == CharacterEvent::HookLaunchedRight)
        hookSoundPending = true;

    if (kind == CharacterEvent::AttackWindowOpened) {

        struck.clear();

        attackWindow = true;
        RequestSound(HeroSound::BladeSwing);
    }

    if (kind == CharacterEvent::AttackWindowClosed)
        attackWindow = false;

    if (kind == CharacterEvent::GunFiredLeft)
        gunShots.push_back(HookSide::Left);

    if (kind == CharacterEvent::GunFiredRight)
        gunShots.push_back(HookSide::Right);

    world->OnCharacterEvent(kind);

    if (Event != nullptr)
        Event(kind);
}

void HeroController::ProcessBlades() {

    auto combat = dynamic_cast<LibAottg::IHeroCombatWorld *>(world);

    if (Presentation() == nullptr)
        return;

    for (int n = 0; n < 2; n++) {

        LibAottg::Vec3 start{}, end{};

        if (!Presentation()->WeaponSegment(static_cast<LibAottg::HookSide>(n), false, start, end)) {

            weaponHistory[n] = false;

            continue;
        }

        if (Combat && combat != nullptr && attackWindow && Motor->AttackHitboxActive) {

            auto sweep = ([&] {
                auto value = LibAottg::HeroWeaponSweep{};
                value.Side = static_cast<LibAottg::HookSide>(n);
                value.Start = start;
                value.End = end;
                value.PreviousStart = (weaponHistory[n] ? weaponStart[n] : start);
                value.PreviousEnd = (weaponHistory[n] ? weaponEnd[n] : end);
                value.Radius = 0.1f * Motor->Scale;
                return value;
            }());

            AcceptHits(combat, sweep, true);
        }

        weaponStart[n] = start;

        weaponEnd[n] = end;

        weaponHistory[n] = true;
    }
}

void HeroController::RequestSound(LibAottg::HeroSound sound) {
    if (SoundRequested) SoundRequested(LibAottg::HeroSoundCue(sound, Motor->Position()));
}
void HeroController::ProcessGuns(LibAottg::CharacterInput input) {
    if (!gunShots.empty()) RequestSound(HeroSound::AhssFire);

    auto combat = dynamic_cast<LibAottg::IHeroCombatWorld *>(world);

    for (auto side : gunShots) {

        LibAottg::Vec3 start{}, end{};

        if (Presentation() == nullptr || !Presentation()->WeaponSegment(side, true, start, end))
            start = Motor->Position() + Vec3::Up() * Motor->Scale;

        LibAottg::Vec3 target =
            (input.HasAimTarget
                 ? input.AimTarget
                 : (input.HasAimOrigin
                        ? input.AimOrigin + input.AimDirection.Normalized() * (10000 * Motor->Scale)
                        : start + input.AimDirection.Normalized() * (10000 * Motor->Scale)));

        LibAottg::HookHit aimed{};

        if (world->TryHook((input.HasAimOrigin ? input.AimOrigin : start), input.AimDirection.Normalized(),
                           10000 * Motor->Scale, side, aimed))
            target = aimed.Point;

        if (Combat && combat != nullptr)
            AcceptHits(combat, ([&] {
                           auto value = LibAottg::HeroWeaponSweep{};
                           value.Gun = true;
                           value.Side = side;
                           value.Start = start;
                           value.End = target;
                           return value;
                       }()),
                       false);
    }

    gunShots.clear();
}

void HeroController::AcceptHits(LibAottg::IHeroCombatWorld *combat, LibAottg::HeroWeaponSweep sweep,
                                bool blade) {

    hits.clear();

    combat->CollectWeaponHits(sweep, hits);

    if (!blade)
        shotTargets.clear();

    for (auto candidate : hits) {

        if (candidate.TargetId == 0 || ((blade ? !struck.insert(candidate.TargetId).second
                                               : !shotTargets.insert(candidate.TargetId).second)))
            continue;

        auto hit = candidate;

        hit.Side = sweep.Side;

        hit.Gun = sweep.Gun;

        hit.Speed = Motor->Velocity().Length() / Motor->Scale;
        hit.Damage = HeroDamage::Calculate(Motor->Velocity(), hit.TargetVelocity,
            hit.ScoreMultiplier > 0 ? hit.ScoreMultiplier : 1.0f, Motor->Scale);

        if (blade) {
            Motor->BladeHit(services);
            RequestSound(HeroSound::BladeHit);
        }

        combat->OnHeroHit(hit);

        if (Hit != nullptr)
            Hit(hit);

        if (blade && !Motor->AttackHitboxActive)
            break;
    }
}
HeroController::Services::Services(LibAottg::HeroController *owner) {

    this->owner = owner;
}

LibAottg::Vec3 HeroController::Services::HookOrigin(LibAottg::CharacterMotor *motor,
                                                    LibAottg::HookSide side) {

    return (owner->Presentation() != nullptr ? owner->Presentation()->HookOrigin(motor, side)
                                             : owner->world->HookOrigin(motor, side));
}

bool HeroController::Services::TryHook(LibAottg::Vec3 origin, LibAottg::Vec3 direction, float distance,
                                       LibAottg::HookSide side, LibAottg::HookHit &hit) {

    return owner->world->TryHook(origin, direction, distance, side, hit);
}

bool HeroController::Services::SweepHook(LibAottg::Vec3 start, LibAottg::Vec3 end, LibAottg::HookSide side,
                                         LibAottg::HookHit &hit) {

    return owner->world->SweepHook(start, end, side, hit);
}

bool HeroController::Services::ResolveHookAnchor(int id, LibAottg::Vec3 localPoint, LibAottg::Vec3 &point) {

    return owner->world->ResolveHookAnchor(id, localPoint, point);
}

void HeroController::Services::OnCharacterEvent(LibAottg::CharacterEvent kind) {

    owner->Raise(kind);
}
const std::vector<LibAottg::Vec3> &HeroRope::Points() const {

    return points;
}

void HeroRope::Reset() {

    history.clear();

    points.clear();

    previous = HookPhase::Inactive;
}

void HeroRope::Sample(LibAottg::CharacterMotor *motor, LibAottg::HookSide side, LibAottg::Vec3 muzzle,
                      float time) {

    LibAottg::HookPhase phase = (side == HookSide::Left ? motor->LeftHookPhase() : motor->RightHookPhase());

    LibAottg::Vec3 end = (side == HookSide::Left ? motor->LeftHookAnchor() : motor->RightHookAnchor());

    float scale = motor->Scale;

    points.clear();

    if (phase == HookPhase::Inactive) {

        Reset();

        return;
    }

    if (phase != previous && phase == HookPhase::Flying) {

        history.clear();

        history.push_back(muzzle);
    }

    if (phase == HookPhase::Attached) {

        LibAottg::Vec3 delta = (end - muzzle) / scale, velocity = motor->Velocity() / scale;

        int count = cs_max(2, cs_min(6, static_cast<int>(((delta.Length() + velocity.Length()) / 5))));

        float sag =
            static_cast<float>(std::pow(static_cast<double>(delta.Length()), static_cast<double>(0.3f)));

        for (int i = 0; i < count; i++) {

            int half = count / 2;

            float envelope = static_cast<float>(
                std::sqrt(static_cast<double>((half - std::abs(i - half)) / static_cast<float>(half))));

            float noise = (sag + velocity.Length()) * 0.0015f * envelope;

            auto wiggle =
                LibAottg::Vec3(static_cast<float>(std::sin(
                                   static_cast<double>(time * 47 + i * 11 + static_cast<int>(side)))),
                               static_cast<float>(std::sin(static_cast<double>(time * 53 + i * 7))),
                               static_cast<float>(std::cos(static_cast<double>(time * 41 + i * 13)))) *
                noise;

            points.push_back(muzzle +
                             (delta * (i / static_cast<float>(count)) + wiggle -
                              Vec3::Up() * (sag * 0.05f * envelope) - velocity * (0.001f * envelope * sag)) *
                                 scale);
        }

        points[static_cast<int>(points.size()) - 1] = end;
    } else if (phase == HookPhase::Flying) {

        if (static_cast<int>(history.size()) == 0)
            history.push_back(muzzle);

        if ((history[static_cast<int>(history.size()) - 1] - end).Length() / scale > 0.05f)
            history.push_back(end);

        if (static_cast<int>(history.size()) > 128)
            history.erase(history.begin() + 1);

        LibAottg::Vec3 offset = muzzle - history[0];

        for (int i = 0; i < static_cast<int>(history.size()); i++)
            points.push_back(history[i] + offset * static_cast<float>(std::pow(static_cast<double>(0.75),
                                                                               static_cast<double>(i))));

        points[0] = muzzle;

        if (static_cast<int>(points.size()) == 1)
            points.push_back(end);
        else
            points[static_cast<int>(points.size()) - 1] = end;
    } else {

        points.push_back(muzzle);

        points.push_back(end);
    }

    previous = phase;
}
SmokeCurveKey::SmokeCurveKey(float time, float value, float inSlope, float outSlope) {

    Time = time;

    Value = value;

    InSlope = inSlope;

    OutSlope = outSlope;
}

void HeroSmokeSettings::Validate() {

    float values[] = {Rate,    LifetimeMin, LifetimeMax, SpeedMin,  SpeedMax,
                      SizeMin, SizeMax,     Radius,      ConeAngle, Alpha};

    for (float v : values)
        if (std::isnan(v) || std::isinf(v) || v < 0)
            throw std::invalid_argument(cs_string("HeroSmokeSettings"));

    if (LifetimeMin <= 0 || LifetimeMin > LifetimeMax || SpeedMin > SpeedMax || SizeMin > SizeMax ||
        MaxParticles <= 0 || MaxParticles > 100000 || ConeAngle > 90 || SizeCurve.empty() ||
        static_cast<int>(SizeCurve.size()) == 0 || !WorldBox::Finite(LocalPosition) ||
        !WorldBox::Finite(LocalDirection) || !WorldBox::Finite(LocalRight) || !WorldBox::Finite(LocalUp) ||
        !WorldBox::Finite(Color) || !WorldBox::Finite(LibAottg::Vec3(RotationMin, RotationMax, 0)) ||
        RotationMin > RotationMax)
        throw std::invalid_argument(cs_string("HeroSmokeSettings"));

    for (int n = 0; n < static_cast<int>(SizeCurve.size()); n++) {

        auto k = SizeCurve[n];

        if (!WorldBox::Finite(LibAottg::Vec3(k.Time, k.Value, k.InSlope)) || std::isnan(k.OutSlope) ||
            std::isinf(k.OutSlope) || k.Time < 0 || k.Time > 1 || (n > 0 && k.Time <= SizeCurve[n - 1].Time))
            throw std::invalid_argument(cs_string("SizeCurve"));
    }
}

float HeroSmokeSettings::SizeAt(float t) {

    if (t <= SizeCurve[0].Time)
        return SizeCurve[0].Value;

    for (int n = 1; n < static_cast<int>(SizeCurve.size()); n++) {

        auto a = SizeCurve[n - 1];

        auto b = SizeCurve[n];

        if (t > b.Time)
            continue;

        float span = b.Time - a.Time, u = (t - a.Time) / span, u2 = u * u, u3 = u2 * u;

        return cs_max(static_cast<float>(0), (2 * u3 - 3 * u2 + 1) * a.Value +
                                                 (u3 - 2 * u2 + u) * span * a.OutSlope +
                                                 (-2 * u3 + 3 * u2) * b.Value + (u3 - u2) * span * b.InSlope);
    }

    return SizeCurve[static_cast<int>(SizeCurve.size()) - 1].Value;
}
const std::vector<LibAottg::HeroSmokeParticle> &HeroSmokeTrail::Particles() const {

    return particles;
}
HeroSmokeTrail::HeroSmokeTrail(LibAottg::HeroSmokeSettings *settings, std::uint32_t seed) {

    ownedSettings = std::make_unique<HeroSmokeSettings>(settings ? *settings : HeroSmokeSettings{});
    Settings = ownedSettings.get();

    Settings->Validate();

    initialSeed = (seed == 0 ? 1u : seed);

    random = initialSeed;
}

void HeroSmokeTrail::Reset() {

    particles.clear();

    fraction = 0;

    hasEmitter = false;

    random = initialSeed;
}

void HeroSmokeTrail::Advance(float dt, LibAottg::CharacterMotor *motor, LibAottg::HeroPose pose) {

    if (motor == nullptr)
        throw std::invalid_argument(cs_string("motor"));

    if (std::isnan(dt) || std::isinf(dt) || dt <= 0)
        throw std::invalid_argument(cs_string("dt"));

    LibAottg::Vec3 emitter = pose.Position + pose.Rotate(Settings->LocalPosition * motor->Scale);

    if (!hasEmitter)
        previousEmitter = emitter;

    for (int n = static_cast<int>(particles.size()) - 1; n >= 0; n--) {

        auto p = particles[n];

        p.Age += dt;

        if (p.Age >= p.Lifetime) {

            particles.erase(particles.begin() + n);

            continue;
        }

        p.Position += p.Velocity * dt;

        UpdateAppearance(p);

        particles[n] = p;
    }

    if (Enabled && motor->GasThrustActive) {

        double total = fraction + Settings->Rate * static_cast<double>(dt);

        int births = static_cast<int>(cs_min(total, Settings->MaxParticles));

        double first = (Settings->Rate > 0 ? (1 - fraction) / Settings->Rate : dt);

        fraction = total - std::floor(static_cast<double>(total));

        for (int n = 0; n < births && static_cast<int>(particles.size()) < Settings->MaxParticles; n++) {

            float time = static_cast<float>((first + n / static_cast<double>(Settings->Rate)));

            float age = cs_max(static_cast<float>(0), dt - time);

            float lifetime = Range(Settings->LifetimeMin, Settings->LifetimeMax);

            if (age >= lifetime)
                continue;

            float azimuth = Range(static_cast<float>(0), static_cast<float>(cs_pi) * 2);

            float radius = static_cast<float>(std::sqrt(static_cast<double>(Next()))) * Settings->Radius;

            LibAottg::Vec3 radial =
                Settings->LocalRight * static_cast<float>(std::cos(static_cast<double>(azimuth))) +
                Settings->LocalUp * static_cast<float>(std::sin(static_cast<double>(azimuth)));

            float angle = Settings->ConeAngle * radius / cs_max(Settings->Radius, 0.00001f) *
                          static_cast<float>(cs_pi) / 180;

            LibAottg::Vec3 direction =
                Settings->LocalDirection * static_cast<float>(std::cos(static_cast<double>(angle))) +
                radial * static_cast<float>(std::sin(static_cast<double>(angle)));

            auto p = ([&] {
                auto value = LibAottg::HeroSmokeParticle{};
                value.Position = Vec3::Lerp(previousEmitter, emitter, time / dt) +
                                 pose.Rotate(radial * (radius * motor->Scale));
                value.Velocity =
                    pose.Rotate(direction) * (Range(Settings->SpeedMin, Settings->SpeedMax) * motor->Scale);
                value.Age = age;
                value.Lifetime = lifetime;
                value.StartSize = Range(Settings->SizeMin, Settings->SizeMax) * motor->Scale;
                value.Rotation = Range(Settings->RotationMin, Settings->RotationMax);
                value.Color = Settings->Color;
                return value;
            }());

            p.Position += p.Velocity * age;

            UpdateAppearance(p);

            particles.push_back(p);
        }
    } else
        fraction = 0;

    previousEmitter = emitter;

    hasEmitter = true;
}

void HeroSmokeTrail::UpdateAppearance(LibAottg::HeroSmokeParticle &p) {

    float t = p.Age / p.Lifetime;

    p.Size = p.StartSize * Settings->SizeAt(t);

    p.Alpha = Settings->Alpha * (1 - t);
}

float HeroSmokeTrail::Next() {

    random ^= random << 13;

    random ^= random >> 17;

    random ^= random << 5;

    return (random >> 8) / 16777216.0f;
}

float HeroSmokeTrail::Range(float min, float max) {

    return min + (max - min) * Next();
}
} // namespace LibAottg
