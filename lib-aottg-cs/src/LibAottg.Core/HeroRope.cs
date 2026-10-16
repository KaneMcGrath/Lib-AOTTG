// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace LibAottg
{
    /// <summary>Optional rope points, using Bullet's attached sag/velocity rule and flight history.
    /// Output uses host units; renderer choice belongs to the host.</summary>
    public sealed class HeroRope
    {
        private readonly List<Vec3> history = new List<Vec3>();
        private readonly List<Vec3> points = new List<Vec3>();
        private HookPhase previous;
        public IReadOnlyList<Vec3> Points { get { return points; } }
        public void Reset() { history.Clear(); points.Clear(); previous = HookPhase.Inactive; }
        public void Sample(CharacterMotor motor, HookSide side, Vec3 muzzle, float time)
        {
            HookPhase phase = side == HookSide.Left ? motor.LeftHookPhase : motor.RightHookPhase;
            Vec3 end = side == HookSide.Left ? motor.LeftHookAnchor : motor.RightHookAnchor;
            float scale = motor.Scale;
            points.Clear();
            if (phase == HookPhase.Inactive) { Reset(); return; }
            if (phase != previous && phase == HookPhase.Flying) { history.Clear(); history.Add(muzzle); }
            if (phase == HookPhase.Attached)
            {
                Vec3 delta = (end - muzzle) / scale, velocity = motor.Velocity / scale;
                int count = Math.Max(2, Math.Min(6, (int)((delta.Length + velocity.Length) / 5)));
                float sag = (float)Math.Pow(delta.Length, 0.3f);
                for (int i = 0; i < count; i++)
                {
                    int half = count / 2;
                    float envelope = (float)Math.Sqrt((half - Math.Abs(i - half)) / (float)half);
                    float noise = (sag + velocity.Length) * 0.0015f * envelope;
                    var wiggle = new Vec3((float)Math.Sin(time * 47 + i * 11 + (int)side), (float)Math.Sin(time * 53 + i * 7), (float)Math.Cos(time * 41 + i * 13)) * noise;
                    points.Add(muzzle + (delta * (i / (float)count) + wiggle - Vec3.Up * (sag * 0.05f * envelope) - velocity * (0.001f * envelope * sag)) * scale);
                }
                points[points.Count - 1] = end;
            }
            else if (phase == HookPhase.Flying)
            {
                if (history.Count == 0) history.Add(muzzle);
                if ((history[history.Count - 1] - end).Length / scale > 0.05f) history.Add(end);
                if (history.Count > 128) history.RemoveAt(1);
                Vec3 offset = muzzle - history[0];
                for (int i = 0; i < history.Count; i++) points.Add(history[i] + offset * (float)Math.Pow(0.75, i));
                points[0] = muzzle;
                if (points.Count == 1) points.Add(end); else points[points.Count - 1] = end;
            }
            else { points.Add(muzzle); points.Add(end); }
            previous = phase;
        }
    }
}
