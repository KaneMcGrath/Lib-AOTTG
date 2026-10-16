// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    [Flags]
    public enum HeroButtons
    {
        None = 0, Jump = 1, Dash = 2, Dodge = 4, HookLeft = 8, HookRight = 16,
        HookBoth = 32, Attack = 64, Skill = 128, Reload = 256, Supply = 512, Flare = 1024,
        Forward = 2048, Back = 4096, Left = 8192, Right = 16384
    }

    /// <summary>Device-independent values. Mouse delta is in pixels, positive Y down;
    /// pointer coordinates are normalized, with the origin at the top left.</summary>
    public struct HeroInputFrame
    {
        public HeroButtons Held, Pressed, Released;
        public float MoveX, MoveY, Reel, MouseDeltaX, MouseDeltaY;
        public float PointerX, PointerY, AspectRatio;
        public bool HasPointer, OrbitHeld;
        /// <summary>Optional stick/keyboard camera rotation in degrees per second.</summary>
        public float LookYaw, LookPitch;
        /// <summary>Use a host camera/AI ray instead of the optional library camera.</summary>
        public bool HasView;
        public Vec3 ViewForward, AimOrigin, AimDirection;
    }

    /// <summary>Derives edges from held actions, including camera-relative directional double taps.
    /// Explicit edges preserve presses/releases occurring between two sampled frames.</summary>
    public sealed class HeroInputMapper
    {
        private HeroButtons previous;
        public void Reset() { previous = HeroButtons.None; }
        public CharacterInput Sample(HeroInputFrame frame, Vec3 forward, Vec3 origin, Vec3 aim)
        {
            if (!WorldBox.Finite(new Vec3(frame.MoveX, frame.MoveY, frame.Reel))) throw new ArgumentException("Input axes must be finite.");
            if (frame.HasView && (!WorldBox.Finite(frame.ViewForward) || !WorldBox.Finite(frame.AimOrigin) || !WorldBox.Finite(frame.AimDirection)))
                throw new ArgumentException("View and aim inputs must be finite.");
            float x = Math3.Clamp(frame.MoveX, -1, 1), y = Math3.Clamp(frame.MoveY, -1, 1);
            var held = frame.Held;
            if (x < -0.5f) held |= HeroButtons.Left;
            if (x > 0.5f) held |= HeroButtons.Right;
            if (y < -0.5f) held |= HeroButtons.Back;
            if (y > 0.5f) held |= HeroButtons.Forward;
            var pressed = (held & ~previous) | frame.Pressed;
            var released = (previous & ~held) | frame.Released;
            previous = held;
            return new CharacterInput {
                MoveX = x, MoveY = y, Reel = Math3.Clamp(frame.Reel, -1, 1),
                LookDirection = frame.HasView ? frame.ViewForward : forward,
                HasAimOrigin = true, AimOrigin = frame.HasView ? frame.AimOrigin : origin,
                AimDirection = frame.HasView ? frame.AimDirection : aim,
                AttackLeft = frame.HasPointer && frame.PointerX < 0.5f,
                Jump = Button(held, pressed, released, HeroButtons.Jump),
                Dash = Button(held, pressed, released, HeroButtons.Dash),
                Dodge = Button(held, pressed, released, HeroButtons.Dodge),
                HookLeft = Button(held, pressed, released, HeroButtons.HookLeft),
                HookRight = Button(held, pressed, released, HeroButtons.HookRight),
                HookBoth = Button(held, pressed, released, HeroButtons.HookBoth),
                Attack = Button(held, pressed, released, HeroButtons.Attack),
                Skill = Button(held, pressed, released, HeroButtons.Skill),
                Reload = Button(held, pressed, released, HeroButtons.Reload),
                Supply = Button(held, pressed, released, HeroButtons.Supply),
                Flare = Button(held, pressed, released, HeroButtons.Flare),
                Forward = Button(held, pressed, released, HeroButtons.Forward),
                Back = Button(held, pressed, released, HeroButtons.Back),
                Left = Button(held, pressed, released, HeroButtons.Left),
                Right = Button(held, pressed, released, HeroButtons.Right)
            };
        }
        private static Button Button(HeroButtons held, HeroButtons pressed, HeroButtons released, HeroButtons action)
        { return new Button((held & action) != 0, (pressed & action) != 0, (released & action) != 0); }
    }
}
