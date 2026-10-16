// SPDX-License-Identifier: MIT
namespace LibAottg
{
    /// <summary>Broad player states for gameplay and UI. Presentation follows CharacterAnimation.</summary>
    public enum CharacterState { Idle, Run, Jump, Fall, AirDodge, GroundDodge, WallRun, Attack, Reload, Supply, Grabbed, Dead, Land, Slide, RoofVault }
    public enum HookSide { Left, Right }
    public enum CharacterEvent { Jumped, Dashed, Dodged, WallRunStarted, WallRunEnded, RoofVaulted, Grabbed, EscapedGrab, HookLaunchedLeft, HookLaunchedRight, HookAttachedLeft, HookAttachedRight, HookReleasedLeft, HookReleasedRight, AttackStarted, AttackEnded, GunFiredLeft, GunFiredRight, Reloaded, Supplied, FlareFired, SkillUsed, Died, HookMissedLeft, HookMissedRight, Landed, SlideStarted, WallJumped, AttackWindowOpened, AttackWindowClosed, BladesThrown }
    public enum HookPhase { Inactive, Flying, Attached, Retracting }
    public enum EquipmentMode { Blades, Guns }

    /// <summary>Edges are consumed once by AdvanceFrame; StepPhysics uses held values only.</summary>
    public struct Button
    {
        public bool Held, Pressed, Released;
        public Button(bool held, bool pressed, bool released) { Held = held; Pressed = pressed; Released = released; }
    }

    /// <summary>Input in camera-relative axes. LookDirection is the horizontal camera forward vector.</summary>
    public struct CharacterInput
    {
        public float MoveX, MoveY;
        public Vec3 LookDirection;
        public Button Jump, Dash, Dodge, HookLeft, HookRight, Attack, Reload, Supply, Flare, Skill;
        public Button HookBoth;
        /// <summary>Directional edges for reference double-tap dash detection.</summary>
        public Button Forward, Back, Left, Right;
        public bool HasAimOrigin;
        public Vec3 AimOrigin;
        /// <summary>Mouse is left of screen center: select attack2 instead of attack1.</summary>
        public bool AttackLeft;
        /// <summary>Opt in to source hook-lean and target-dependent blade clip selection.</summary>
        public bool SourceAttackSelection, HasAttackTarget;
        public Vec3 AttackTarget;
        /// <summary>Positive reels out, negative reels in; clamped to [-1, 1].</summary>
        public float Reel;
        /// <summary>World direction for hook targeting. Zero disables new launches.</summary>
        public Vec3 AimDirection;
        /// <summary>Optional resolved camera ray target for head/arm aiming and shot convergence.</summary>
        public bool HasAimTarget;
        public Vec3 AimTarget;
    }

    /// <summary>Host-owned collision results for this tick. Hooks are raycast through ICharacterWorld.</summary>
    public struct CharacterContacts
    {
        public bool Grounded;
        public Vec3 GroundNormal;
        /// <summary>Whether there is a wall immediately in front of the character.</summary>
        public bool FrontWall;
        /// <summary>Whether there is free space above that front wall for a roof vault.</summary>
        public bool UpperFrontClear;
    }

    /// <summary>Host-unit hit point; LocalPoint is opaque target-local data for moving anchors.</summary>
    public struct HookHit
    {
        public Vec3 Point;
        public int TargetId;
        public Vec3 LocalPoint;
        public HookHit(Vec3 point) { Point = point; TargetId = 0; LocalPoint = point; }
    }

    /// <summary>Engine services required for new hook launches and audiovisual events.</summary>
    public interface ICharacterWorld
    {
        bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit);
        void OnCharacterEvent(CharacterEvent kind);
    }

    /// <summary>Optional hook services. Aim queries and projectile collision can use different masks.</summary>
    public interface IHookGeometryWorld : ICharacterWorld
    {
        Vec3 HookOrigin(CharacterMotor motor, HookSide side);
        bool SweepHook(Vec3 start, Vec3 end, HookSide side, out HookHit hit);
        bool ResolveHookAnchor(int targetId, Vec3 localPoint, out Vec3 worldPoint);
    }

    /// <summary>Authoritative clip clock and transition. Hosts sample this time without fitting action timers.</summary>
    public struct CharacterAnimation
    {
        public string Clip;
        public float Time, Speed, Duration, CrossFade;
        public bool Loop;
        public int Revision;
        public float NormalizedTime { get { return Duration > 0 ? Time / Duration : 0; } }
    }
}
