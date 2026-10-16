// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    public enum HeroCameraMode { Original, Wow, Tps }
    public sealed class HeroCameraConfig
    {
        public HeroCameraMode Mode = HeroCameraMode.Tps;
        /// <summary>Degrees per device pixel. Unity's legacy Mouse X/Y scaling is host-specific.</summary>
        public float Sensitivity = 0.15f;
        public bool InvertY, Tilt = true, Obstruction = true;
        /// <summary>Original cameraDistance (settings value plus 0.3), distance and rig multipliers.</summary>
        public float Distance = 0.6f, BaseDistance = 10, DistanceMultiplier = 0.64f, HeightMultiplier = 0.64f;
        public float CollisionRadius = 0.1f;
        /// <summary>AOTTG's menu distance (0..1); the source adds 0.3 to its camera multiplier.</summary>
        public float CameraDistance
        {
            get { return Distance - 0.3f; }
            set { ValidateSetting(value, 0, 1, "CameraDistance"); Distance = value + 0.3f; }
        }
        /// <summary>AOTTG's mouse speed (0.01..1). Its Mouse X/Y axes scale pixels by 0.1,
        /// then the camera multiplies by 10, so this equals degrees per pixel.</summary>
        public float MouseSpeed
        {
            get { return Sensitivity; }
            set { ValidateSetting(value, 0.01f, 1, "MouseSpeed"); Sensitivity = value; }
        }
        public bool InvertMouse { get { return InvertY; } set { InvertY = value; } }
        public bool CameraTilt { get { return Tilt; } set { Tilt = value; } }
        private static void ValidateSetting(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name);
        }
        public void Validate()
        {
            foreach (float v in new[] { Sensitivity, Distance, BaseDistance, DistanceMultiplier, HeightMultiplier, CollisionRadius })
                if (float.IsNaN(v) || float.IsInfinity(v) || v < 0) throw new ArgumentOutOfRangeException("HeroCameraConfig");
            if (!Enum.IsDefined(typeof(HeroCameraMode), Mode)) throw new ArgumentOutOfRangeException("Mode");
        }
    }
    public struct HeroCameraPose
    {
        public Vec3 Position, Forward, Up, AimDirection;
        public float FieldOfView, Yaw, Pitch, Roll;
        public bool CapturePointer;
    }
    public interface IHeroCameraWorld
    {
        /// <summary>Sweep from the follow pivot toward the camera; radius and returned point use host units.</summary>
        bool SweepCamera(Vec3 pivot, Vec3 position, float radius, out Vec3 safePosition);
    }

    /// <summary>Optional source-based camera/input interpretation, independent of rendering and devices.</summary>
    public sealed class HeroCamera
    {
        public HeroCameraConfig Config { get; private set; }
        public HeroCameraPose Pose { get; private set; }
        private float yaw, pitch, roll;
        public HeroCamera(HeroCameraConfig config = null) { Config = config ?? new HeroCameraConfig(); Config.Validate(); }
        public void Reset(float yawDegrees = 0, float pitchDegrees = 0) { yaw = yawDegrees; pitch = pitchDegrees; roll = 0; }
        public void Look(float dt, HeroInputFrame input)
        {
            Config.Validate();
            if (dt < 0 || !WorldBox.Finite(new Vec3(dt, input.MouseDeltaX, input.MouseDeltaY)) ||
                !WorldBox.Finite(new Vec3(input.LookYaw, input.LookPitch, input.AspectRatio)) ||
                (input.HasPointer && !WorldBox.Finite(new Vec3(input.PointerX, input.PointerY, 0))))
                throw new ArgumentException("Camera input must be finite and time nonnegative.");
            if (Config.Mode == HeroCameraMode.Original && input.HasPointer)
            {
                float edge = input.PointerX < 0.4f ? input.PointerX - 0.4f : input.PointerX > 0.6f ? input.PointerX - 0.6f : 0;
                // getSensitivityMultiWithDeltaTime uses 62 * dt in the original player.
                yaw += edge * 0.4f * 150 * 62 * dt * Config.MouseSpeed;
                pitch = 70 * (0.4f - input.PointerY);
            }
            else if (Config.Mode != HeroCameraMode.Wow || input.OrbitHeld)
            {
                yaw += input.MouseDeltaX * Config.Sensitivity;
                pitch -= input.MouseDeltaY * Config.Sensitivity * (Config.InvertY ? -1 : 1);
            }
            yaw = (yaw + input.LookYaw * dt) % 360;
            pitch = Math3.Clamp(pitch + input.LookPitch * dt, -80, 80);
        }
        public HeroCameraPose Follow(float dt, CharacterMotor motor, Vec3 root, Vec3 head, HeroInputFrame input, IHeroCameraWorld world = null)
        {
            Vec3 forward = Direction(yaw, pitch), right = Math3.Direction(yaw + 90), up = Vec3.Cross(forward, right).Normalized;
            float targetRoll = 0;
            if (Config.Tilt && (motor.LeftHookAttached || motor.RightHookAttached))
            {
                Vec3 anchor = motor.LeftHookAttached && motor.RightHookAttached ? (motor.LeftHookAnchor + motor.RightHookAnchor) * 0.5f :
                    motor.LeftHookAttached ? motor.LeftHookAnchor : motor.RightHookAnchor;
                Vec3 delta = (anchor - root) / motor.Scale;
                float vertical = Vec3.Dot(delta, up), lateral = Vec3.Dot(delta, right);
                float angle = Angle(up * vertical, up * vertical + right * lateral);
                targetRoll = (lateral >= 0 ? -angle : angle) * Angle(delta, motor.Velocity / motor.Scale) * 0.005f;
            }
            roll += (targetRoll - roll) * Math3.Clamp(dt * 2, 0, 1);
            float r = roll * (float)Math.PI / 180;
            Vec3 rolledRight = right * (float)Math.Cos(r) + up * (float)Math.Sin(r);
            Vec3 rolledUp = up * (float)Math.Cos(r) - right * (float)Math.Sin(r);
            float offset = Config.Distance * (200 - motor.FieldOfView) / 150;
            Vec3 pivot = head + Vec3.Up * ((Config.HeightMultiplier - (0.6f - Config.Distance) * 2) * motor.Scale);
            Vec3 position = pivot - forward * (Config.BaseDistance * Config.DistanceMultiplier * offset * motor.Scale);
            if (Config.Distance < 0.65f) position += rolledRight * (Math.Max((0.6f - Config.Distance) * 2, 0.65f) * motor.Scale);
            Vec3 safe;
            if (Config.Obstruction && world != null && world.SweepCamera(head, position, Config.CollisionRadius * motor.Scale, out safe)) position = safe;
            Vec3 aim = forward;
            if (Config.Mode != HeroCameraMode.Tps && input.HasPointer)
            {
                float tangent = (float)Math.Tan(motor.FieldOfView * Math.PI / 360);
                aim = (forward + rolledRight * ((input.PointerX * 2 - 1) * tangent * (input.AspectRatio > 0 ? input.AspectRatio : 1)) +
                    rolledUp * ((1 - input.PointerY * 2) * tangent)).Normalized;
            }
            Pose = new HeroCameraPose { Position = position, Forward = forward, Up = rolledUp, AimDirection = aim,
                FieldOfView = motor.FieldOfView, Yaw = yaw, Pitch = pitch, Roll = roll,
                CapturePointer = Config.Mode == HeroCameraMode.Tps || (Config.Mode == HeroCameraMode.Wow && input.OrbitHeld) };
            return Pose;
        }
        internal static Vec3 Direction(float yaw, float elevation)
        {
            float p = elevation * (float)Math.PI / 180;
            return Math3.Direction(yaw) * (float)Math.Cos(p) + Vec3.Up * (float)Math.Sin(p);
        }
        internal static float Angle(Vec3 a, Vec3 b)
        {
            float n = a.Length * b.Length;
            return n <= 0.000001f ? 0 : (float)Math.Acos(Math3.Clamp(Vec3.Dot(a, b) / n, -1, 1)) * 180 / (float)Math.PI;
        }
    }
}
