// SPDX-License-Identifier: MIT
using System;

namespace LibAottg
{
    /// <summary>Mutable tuning values; configure before stepping a character.</summary>
    [Serializable]
    public sealed class CharacterConfig
    {
        public float GroundSpeed = 10f;
        public float AirAcceleration = 20f;
        public float Gravity = 20f;
        public float JumpSpeed = 8f;
        public float DashSpeed = 40f;
        public float DashDuration = 0.5f;
        public float DashCooldown = 0.2f;
        public float DashGasFraction = 0.04f;
        public float GroundDodgeSpeed = 24f;
        public float GroundDodgeDuration = 1.1f;
        public float WallRunSpeed = 10f;
        public float WallRunMaxDuration = 1f;
        public float RoofVaultSpeed = 8f;
        public float GasCapacity = 100f;
        public float GasUsePerSecond = 0.2f;
        /// <summary>Recommended for unbounded host worlds; false preserves original AoTTG aiming.</summary>
        public bool ImprovedRaycasts = false;
        public float HookRange = 10000f;
        public float HookPullAcceleration = 20f;
        public float HookGasUsePerSecond = 0.2f;
        public float HookReleaseGrace = 0.3f;
        public float BladeCapacity = 100f;
        public int SpareBladePairs = 5;
        public float AttackDuration = 0.5f;
        public float ReloadDuration = 1.7500001f / 1.2f;
        public float SupplyDuration = 2.0000002f;
        public int GunReservePerSide = 7;
        public float FlareCooldown = 30f;
        public float SkillCooldown = 1.5f;
        public float MaxStep = 0.1f;
        /// <summary>Host units per original AoTTG unit. Times, stats and resources stay unchanged.</summary>
        public float WorldScale = 1f;
        public string SkillId = "mikasa";
        // RCMod HERO force modes depend on ACL-derived mass, not only acceleration.
        public float Mass = 0.5f;
        public float MaxGroundVelocityChange = 10f;
        public float HookProjectileSpeed = 150f;
        public float HookMissTimeout = 0.8f;
        public float HookRetractDuration = 0.5f;
        public float HookLaunchGasCost = 0.2f;
        public float HookedGravity = 10f;
        public float DashDamping = 1.7f;
        public float DoubleTapWindow = 0.2f;
        public bool DoubleTapDash = true;
        public bool InfiniteGas;
        public bool AutoWallJump;
        public bool NoForcedToRoof;
        /// <summary>Optional momentum-preserving jump extension. Vanilla jumping exits Slide and brakes during the jump windup.</summary>
        public bool NoStopJump;
        public float AttackWindowStart = 0.5f;
        public float AttackWindowEnd = 0.85f;
        public string StandAnimation = "stand", RunAnimation = "run";

        /// <summary>Throws for invalid values. Call after changing tuning.</summary>
        public void Validate()
        {
            float[] values = { WorldScale, GroundSpeed, AirAcceleration, Gravity, JumpSpeed,
                DashSpeed, DashDuration, DashCooldown, DashGasFraction, GroundDodgeSpeed, GroundDodgeDuration,
                WallRunSpeed, WallRunMaxDuration, RoofVaultSpeed, GasCapacity, GasUsePerSecond, HookRange,
                HookPullAcceleration, HookGasUsePerSecond, HookReleaseGrace,
                BladeCapacity, AttackDuration, ReloadDuration, SupplyDuration,
                FlareCooldown, SkillCooldown, MaxStep,
                Mass, MaxGroundVelocityChange, HookProjectileSpeed, HookMissTimeout, HookRetractDuration,
                HookLaunchGasCost, HookedGravity, DashDamping, DoubleTapWindow, AttackWindowStart, AttackWindowEnd };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException("CharacterConfig", "All tuning values must be finite and nonnegative.");
            if (WorldScale <= 0 || MaxStep <= 0 || GasCapacity <= 0 || BladeCapacity <= 0 || HookRange <= 0 || Mass <= 0 || HookProjectileSpeed <= 0 || SpareBladePairs < 0 || GunReservePerSide < 0 || AttackWindowEnd > 1 || AttackWindowStart > AttackWindowEnd || string.IsNullOrEmpty(SkillId) || string.IsNullOrEmpty(StandAnimation) || string.IsNullOrEmpty(RunAnimation))
                throw new ArgumentOutOfRangeException("CharacterConfig", "Capacities, range and step must be positive; spare count cannot be negative.");
        }
    }
}
