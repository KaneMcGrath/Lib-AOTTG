// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace LibAottg
{
    /// <summary>Portable version of the four RCMod character stat dimensions.</summary>
    public sealed class HeroStats
    {
        public string Name { get; private set; }
        public int Speed { get; private set; }
        public int Gas { get; private set; }
        public int Blade { get; private set; }
        public int Acceleration { get; private set; }
        public string SkillId { get; private set; }

        public HeroStats(string name, int speed, int gas, int blade, int acceleration, string skillId)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(skillId) ||
                speed <= 0 || gas <= 0 || blade <= 0 || acceleration <= 0)
                throw new ArgumentException("Name, skill and all stat values must be set and positive.");
            Name = name;
            Speed = speed;
            Gas = gas;
            Blade = blade;
            Acceleration = acceleration;
            SkillId = skillId;
        }

        /// <summary>Creates a fresh, editable motor config using RCMod's speed, gas and blade scales.</summary>
        public CharacterConfig ToConfig()
        {
            var config = new CharacterConfig {
                GroundSpeed = Speed / 10f,
                AirAcceleration = Acceleration / 10f * 2f,
                Mass = 0.5f - (Acceleration - 100) * 0.001f,
                WallRunSpeed = Speed / 10f,
                GroundDodgeSpeed = Speed / 10f * 2.4f,
                GasCapacity = Gas,
                BladeCapacity = Blade,
                SkillCooldown = SkillCooldownFor(SkillId),
                SkillId = SkillId,
                StandAnimation = SkillId == "levi" ? "stand_levi" : "stand",
                RunAnimation = SkillId == "levi" ? "run_levi" : "run"
            };
            config.Validate();
            return config;
        }

        private static float SkillCooldownFor(string skill)
        {
            switch (skill.ToLowerInvariant())
            {
                case "levi": case "petra": return 3.5f;
                case "armin": return 5f;
                case "marco": return 10f;
                case "jean": return 0.001f;
                case "eren": return 120f;
                case "sasha": return 20f;
                default: return 1.5f;
            }
        }

        /// <summary>Known human presets from RCMod HeroStat. Returns a new dictionary each time.</summary>
        public static IDictionary<string, HeroStats> RcModPresets()
        {
            return new Dictionary<string, HeroStats>(StringComparer.OrdinalIgnoreCase) {
                { "Mikasa", new HeroStats("Mikasa", 125, 75, 75, 135, "mikasa") },
                { "Levi", new HeroStats("Levi", 95, 100, 100, 150, "levi") },
                { "Armin", new HeroStats("Armin", 75, 150, 125, 85, "armin") },
                { "Marco", new HeroStats("Marco", 110, 100, 115, 95, "marco") },
                { "Jean", new HeroStats("Jean", 100, 150, 80, 100, "jean") },
                { "Eren", new HeroStats("Eren", 100, 90, 90, 100, "eren") },
                { "Petra", new HeroStats("Petra", 80, 110, 100, 140, "petra") },
                { "Sasha", new HeroStats("Sasha", 140, 100, 100, 115, "sasha") }
            };
        }
    }
}
