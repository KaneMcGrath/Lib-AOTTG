// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
namespace LibAottg
{
    public enum HeroCrosshairStyle { Default, Square, Plus, Target, Dot }
    public enum HeroSpeedometer { Off, Speed, Damage }
    /// <summary>Renderer-independent snapshot of HERO resource and aiming UI in source units.</summary>
    public sealed class HeroHud
    {
        public float GasFill = 1, BladeFill = 1, SpecialFill = 1, Speed, Distance;
        public int BladePairs = 5, LeftReserve, RightReserve;
        public bool Guns, LeftLoaded, RightLoaded, HasTarget, Visible = true, ResourcesVisible = true, AimVisible = true;
        public bool ShowDistance = true, ShowArrows = true;
        public HeroSpeedometer Speedometer = HeroSpeedometer.Speed;
        public HeroCrosshairStyle CrosshairStyle = HeroCrosshairStyle.Default;
        public string Skill = "mikasa";
        // Normalized screen position; hosts supply their actual aim/cursor point.
        public float AimX = .5f, AimY = .5f;
        public HeroHudArrow LeftArrow = new HeroHudArrow(), RightArrow = new HeroHudArrow();
        public bool InRange { get { return HasTarget && Distance <= 120; } }
        public static float Fraction(float value, float capacity)
        { return capacity > 0 && !float.IsNaN(value) ? Math.Max(0, Math.Min(1, value / capacity)) : 0; }
        public void Read(CharacterMotor motor)
        {
            GasFill = Fraction(motor.Gas, motor.Config.GasCapacity);
            BladeFill = Fraction(motor.Blade, motor.Config.BladeCapacity);
            SpecialFill = motor.Config.SkillCooldown > 0 ? Fraction(motor.Config.SkillCooldown - motor.SkillCooldownRemaining, motor.Config.SkillCooldown) : 1;
            BladePairs = motor.SpareBladePairs; Guns = motor.Equipment == EquipmentMode.Guns;
            LeftLoaded = motor.LeftGunLoaded; RightLoaded = motor.RightGunLoaded;
            LeftReserve = motor.LeftGunReserve; RightReserve = motor.RightGunReserve;
            Skill = motor.Config.SkillId; Speed = motor.Velocity.Length / motor.Scale;
            Visible = motor.State != CharacterState.Dead;
        }
        /// <summary>Source showAimUI2 rays and projected hook arrows. Screen coordinates are top-left normalized.</summary>
        public void Aim(CharacterMotor motor, CharacterInput input, ICharacterWorld world, HeroCameraPose view, float aspect)
        {
            LeftArrow.Visible = RightArrow.Visible = false;
            HookHit hit;
            HasTarget = world.TryHook(input.AimOrigin, input.AimDirection, 10000000 * motor.Scale, HookSide.Left, out hit);
            Distance = HasTarget ? (hit.Point-motor.Position).Length/motor.Scale : 0;
            if (!HasTarget || !ShowArrows) return;
            Vec3 right = new Vec3((float)Math.Cos(motor.FacingDegrees*Math.PI/180),0,-(float)Math.Sin(motor.FacingDegrees*Math.PI/180));
            float rayDistance=(hit.Point-input.AimOrigin).Length/motor.Scale;
            float spread=rayDistance<=50?rayDistance*.05f:rayDistance*.3f;
            for(int side=0;side<2;side++) {
                float sign=side==0?-1:1;
                Vec3 origin=motor.Position+(Vec3.Up*.4f+right*(sign*.3f))*motor.Scale;
                Vec3 direction=(hit.Point+right*(sign*spread*motor.Scale)-origin).Normalized;
                HookHit arrowHit;
                var arrow=side==0?LeftArrow:RightArrow;
                if(!world.TryHook(origin,direction,1000000*motor.Scale,(HookSide)side,out arrowHit))continue;
                Vec3 delta=arrowHit.Point-view.Position;float depth=Vec3.Dot(delta,view.Forward);
                if(depth<=.0001f)continue;
                float tangent=(float)Math.Tan(view.FieldOfView*Math.PI/360);
                arrow.X=.5f+Vec3.Dot(delta,Vec3.Cross(view.Up,view.Forward).Normalized)/(2*depth*tangent*aspect);
                arrow.Y=.5f-Vec3.Dot(delta,view.Up)/(2*depth*tangent);
                arrow.Rotation=(float)Math.Atan2(arrow.Y-AimY,(arrow.X-AimX)*aspect)+(side==0?(float)Math.PI:0);
                arrow.InRange=(arrowHit.Point-origin).Length/motor.Scale<=120;arrow.Visible=true;
            }
        }
        public string AimText
        {
            get
            {
                string text = ShowDistance && HasTarget ? (Distance <= 1000 ? ((int)Distance).ToString(CultureInfo.InvariantCulture) : "???") : "";
                string speed = Speedometer == HeroSpeedometer.Speed ? Speed.ToString("F1", CultureInfo.InvariantCulture) + " u/s"
                    : Speedometer == HeroSpeedometer.Damage ? (Speed / 100).ToString("F1", CultureInfo.InvariantCulture) + "K" : "";
                return text + (text.Length > 0 && speed.Length > 0 ? "\n" : "") + speed;
            }
        }
    }
    public sealed class HeroHudArrow
    {
        public bool Visible, InRange;
        public float X, Y, Rotation;
    }
}
