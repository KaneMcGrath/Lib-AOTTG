// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    /// <summary>AoTTG's speed-based damage rule used by the legacy blade hit code.</summary>
    public static class HeroDamage
    {
        public static int Calculate(Vec3 attackerVelocity, Vec3 targetVelocity, float scoreMultiplier = 1f,
            float worldScale = 1f, int minimumDamage = 10)
        {
            if (!Finite(attackerVelocity) || !Finite(targetVelocity)) throw new ArgumentOutOfRangeException("velocity");
            if (!Finite(scoreMultiplier) || scoreMultiplier < 0) throw new ArgumentOutOfRangeException("scoreMultiplier");
            if (!Finite(worldScale) || worldScale <= 0) throw new ArgumentOutOfRangeException("worldScale");
            if (minimumDamage < 0) throw new ArgumentOutOfRangeException("minimumDamage");
            // Legacy Unity Mono retains intermediate multiplication precision until the
            // int cast. Multiplying stored 1.3f by speed 1 yields 12, not 13.
            double scaled = (attackerVelocity - targetVelocity).Length / worldScale * 10.0 * scoreMultiplier;
            int damage = scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
            return Math.Max(minimumDamage, damage);
        }

        private static bool Finite(Vec3 value) { return Finite(value.X) && Finite(value.Y) && Finite(value.Z); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
