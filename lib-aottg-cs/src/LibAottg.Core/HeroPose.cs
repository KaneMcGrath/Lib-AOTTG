// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    public struct HeroPose
    {
        public Vec3 Position;
        public float Yaw, Pitch, Roll;
        public bool Leaning, LeanLeft;
        public Vec3 Forward { get { return Rotate(new Vec3(0, 0, 1)); } }
        public Vec3 Up { get { return Rotate(Vec3.Up); } }
        /// <summary>Source Euler order (Z, then X, then Y).</summary>
        public Vec3 Rotate(Vec3 v)
        {
            float z = Roll * (float)Math.PI / 180, x = Pitch * (float)Math.PI / 180, y = Yaw * (float)Math.PI / 180;
            v = new Vec3(v.X * (float)Math.Cos(z) - v.Y * (float)Math.Sin(z), v.X * (float)Math.Sin(z) + v.Y * (float)Math.Cos(z), v.Z);
            v = new Vec3(v.X, v.Y * (float)Math.Cos(x) - v.Z * (float)Math.Sin(x), v.Y * (float)Math.Sin(x) + v.Z * (float)Math.Cos(x));
            return new Vec3(v.X * (float)Math.Cos(y) + v.Z * (float)Math.Sin(y), v.Y, -v.X * (float)Math.Sin(y) + v.Z * (float)Math.Cos(y));
        }
    }
    /// <summary>HERO bodyLean/getLeanAngle rules, evaluated in original units. Optional presentation only.</summary>
    public static class HeroBodyPose
    {
        public static HeroPose Evaluate(CharacterMotor motor)
        {
            var pose = new HeroPose { Position = motor.Position, Yaw = motor.FacingDegrees };
            // HERO's grounded movement restores upright rotation after hook leaning.
            if (motor.Grounded) return pose;
            Vec3 velocity = motor.Velocity / motor.Scale;
            bool attack = motor.State == CharacterState.Attack;
            if (motor.Equipment == EquipmentMode.Blades && attack && motor.Animation.Clip != "attack3_1" && motor.Animation.Clip != "attack3_2")
            {
                float elevation = (float)Math.Atan2(velocity.Y, Math3.Horizontal(velocity).Length) * 180 / (float)Math.PI;
                pose.Pitch = -elevation * (1 - HeroCamera.Angle(velocity, Math3.Direction(pose.Yaw)) / 90);
                return pose;
            }
            bool left = motor.LeftHookAttached, right = motor.RightHookAttached;
            Vec3 midpoint = (motor.LeftHookAnchor + motor.RightHookAnchor) * 0.5f;
            bool almostSingle = left && right && (((motor.LeftHookAnchor - motor.RightHookAnchor) / motor.Scale).Length < 2 ||
                (HeroCamera.Angle(motor.Position - midpoint, motor.Position - motor.LeftHookAnchor) < 30 &&
                 HeroCamera.Angle(motor.Position - midpoint, motor.Position - motor.RightHookAnchor) < 30));
            if ((left != right) || almostSingle)
            {
                pose.Leaning = true;
                Vec3 delta = ((almostSingle || right ? motor.RightHookAnchor : motor.LeftHookAnchor) - motor.Position) / motor.Scale;
                float anchorYaw = (float)Math.Atan2(delta.X, delta.Z) * 180 / (float)Math.PI;
                float velocityYaw = (float)Math.Atan2(velocity.X, velocity.Z) * 180 / (float)Math.PI;
                float difference = DeltaAngle(anchorYaw, velocityYaw);
                pose.LeanLeft = difference > 0;
                float lean = HeroCamera.Angle(delta, Vec3.Up) * 0.1f * (1 + (float)Math.Pow(velocity.Length, 0.2f)) + Math.Abs(difference * 0.5f);
                if (!attack) lean = Math.Min(lean, 80);
                float multiplier = motor.Equipment == EquipmentMode.Guns ? 1 :
                    ((left && difference < 0) || (!left && difference > 0)) ? 0.1f : 0.5f;
                pose.Roll = lean * (difference >= 0 ? multiplier : -multiplier);
                if (!attack && motor.Equipment == EquipmentMode.Blades) pose.Pitch = -Math.Min(velocity.Length * 0.1f, 20);
            }
            return pose;
        }
        internal static float DeltaAngle(float from, float to) { return ((to - from) % 360 + 540) % 360 - 180; }
    }
}
