// SPDX-License-Identifier: MIT
#pragma once
#include "Core.hpp"
namespace LibAottg {
struct HeroStats {
    std::string Name;
    int Speed, Gas, Blade, Acceleration;
    std::string SkillId;
    HeroStats(std::string name, int speed, int gas, int blade, int acceleration, std::string skill)
        : Name(std::move(name)), Speed(speed), Gas(gas), Blade(blade), Acceleration(acceleration),
          SkillId(std::move(skill)) {
        if (Name.empty() || SkillId.empty() || Speed <= 0 || Gas <= 0 || Blade <= 0 || Acceleration <= 0)
            throw std::invalid_argument("HeroStats requires a name, skill and positive stats");
    }
    CharacterConfig ToConfig() const {
        CharacterConfig c;
        c.GroundSpeed = Speed / 10.0f;
        c.AirAcceleration = Acceleration / 10.0f * 2;
        c.Mass = 0.5f - (Acceleration - 100) * 0.001f;
        c.WallRunSpeed = Speed / 10.0f;
        c.GroundDodgeSpeed = Speed / 10.0f * 2.4f;
        c.GasCapacity = static_cast<float>(Gas);
        c.BladeCapacity = static_cast<float>(Blade);
        auto s = cs_lower(SkillId);
        c.SkillCooldown = s == "levi" || s == "petra" ? 3.5f
                          : s == "armin"              ? 5
                          : s == "marco"              ? 10
                          : s == "jean"               ? 0.001f
                          : s == "eren"               ? 120
                          : s == "sasha"              ? 20
                                                      : 1.5f;
        c.SkillId = SkillId;
        c.StandAnimation = SkillId == "levi" ? "stand_levi" : "stand";
        c.RunAnimation = SkillId == "levi" ? "run_levi" : "run";
        c.Validate();
        return c;
    }
    // Keys are normalized because std::unordered_map has no C# OrdinalIgnoreCase comparer.
    static std::unordered_map<std::string, HeroStats> RcModPresets() {
        return {{"mikasa", {"Mikasa", 125, 75, 75, 135, "mikasa"}},
                {"levi", {"Levi", 95, 100, 100, 150, "levi"}},
                {"armin", {"Armin", 75, 150, 125, 85, "armin"}},
                {"marco", {"Marco", 110, 100, 115, 95, "marco"}},
                {"jean", {"Jean", 100, 150, 80, 100, "jean"}},
                {"eren", {"Eren", 100, 90, 90, 100, "eren"}},
                {"petra", {"Petra", 80, 110, 100, 140, "petra"}},
                {"sasha", {"Sasha", 140, 100, 100, 115, "sasha"}}};
    }
};
} // namespace LibAottg
