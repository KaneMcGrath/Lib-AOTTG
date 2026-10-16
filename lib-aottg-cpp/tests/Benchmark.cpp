// SPDX-License-Identifier: MIT
#include <LibAottg/Core.hpp>
#include <chrono>
#include <fstream>
#include <iomanip>
#include <iostream>
using namespace LibAottg;
double checksum[2]{};
double Run(bool full, int count) {
    HeroWorld world;
    HeroController hero(nullptr, &world);
    auto m = hero.Motor;
    HeroCamera camera;
    hero.Camera = &camera;
    hero.Respawn({}, true);
    CharacterInput input;
    input.MoveY = 1;
    input.LookDirection = {0, 0, 1};
    HeroInputFrame raw;
    raw.MoveY = 1;
    raw.Held = HeroButtons::Jump;
    raw.MouseDeltaX = 0.1f;
    raw.AspectRatio = 16.0f / 9;
    HeroSmokeTrail smoke;
    HeroRope rope;
    CharacterContacts contacts;
    contacts.Grounded = true;
    auto start = std::chrono::steady_clock::now();
    for (int i = 0; i < count; i++) {
        if (full) {
            hero.Advance(0.016f, raw);
            smoke.Advance(0.016f, m, hero.RenderPose);
            rope.Sample(m, HookSide::Left, m->Position(), i * 0.016f);
        } else
            m->Step(0.02f, input, contacts, &world);
    }
    auto end = std::chrono::steady_clock::now();
    checksum[full] = m->Position().Z + m->Gas + smoke.Particles().size();
    return std::chrono::duration<double, std::milli>(end - start).count();
}
int main(int argc, char **argv) {
    if (argc != 2)
        return 2;
    Run(false, 50000);
    Run(true, 10000);
    std::ofstream out(argv[1]);
    out << std::setprecision(9) << "{";
    for (int full = 0; full < 2; full++) {
        int count = full ? 20000 : 200000;
        std::array<double, 7> times;
        for (auto &t : times)
            t = Run(full != 0, count);
        auto sorted = times;
        std::sort(sorted.begin(), sorted.end());
        if (full)
            out << ',';
        out << '"' << (full ? "runtime" : "motor") << "\":{\"iterations\":" << count
            << ",\"elapsedMilliseconds\":[";
        for (int n = 0; n < 7; n++) {
            if (n)
                out << ',';
            out << times[n];
        }
        out << "],\"medianMilliseconds\":" << sorted[3] << "}";
    }
    out << ",\"motorChecksum\":" << checksum[0] << ",\"runtimeChecksum\":" << checksum[1]
        << ",\"scope\":\"Single-thread simulation only; no Skyrim, rendering, input device, host physics, "
           "marshalling or startup\"}\n";
    std::cout << "Native benchmark completed\n";
}
