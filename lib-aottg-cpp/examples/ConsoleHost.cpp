// SPDX-License-Identifier: MIT
#include <LibAottg/Stats.hpp>
#include <iostream>

int main() {
    auto config = LibAottg::HeroStats::RcModPresets().at("mikasa").ToConfig();
    config.WorldScale = 1;
    LibAottg::CalibrationHeroWorld world(0.5f, 1.7f, config.WorldScale);
    LibAottg::HeroController hero(&config, &world);
    hero.Respawn({}, true);
    LibAottg::HeroInputFrame input;
    input.MoveY = 1;
    for (int frame = 0; frame < 100; ++frame) hero.Advance(0.02f, input);
    std::cout << "HERO host completed 100 frames.\n";
}
