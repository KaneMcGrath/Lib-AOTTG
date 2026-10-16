// SPDX-License-Identifier: MIT
#include <LibAottg/Stats.hpp>
#include <fstream>
#include <iostream>
#include <limits>
#include <sstream>
#include <type_traits>
using namespace LibAottg;
static_assert(!std::is_copy_constructible_v<CharacterMotor>);
static_assert(!std::is_move_constructible_v<CharacterMotor>);
static_assert(!std::is_copy_constructible_v<HeroController>);
static_assert(!std::is_move_constructible_v<HeroController>);
static_assert(sizeof(Vec3) == 12);
int main(int argc, char **argv) {
    if (argc != 3)
        return 2;
    int checks = 0, failed = 0, queries = 0, hits = 0;
    float maxError = 0;
    auto check = [&](bool condition) {
        checks++;
        if (!condition)
            failed++;
    };
    auto rejects = [&](auto f) {
        bool rejected = false;
        try {
            f();
        } catch (const std::invalid_argument &) {
            rejected = true;
        }
        check(rejected);
    };
    for (float value :
         {0.0f, -1.0f, std::numeric_limits<float>::infinity(), std::numeric_limits<float>::quiet_NaN()}) {
        rejects([&] {
            CharacterConfig c;
            c.WorldScale = value;
            CharacterMotor m(&c);
        });
        rejects([&] {
            HeroController h;
            h.Advance(value, HeroInputFrame{});
        });
    }
    rejects([&] {
        CharacterMotor m;
        m.SetPose({0, std::numeric_limits<float>::quiet_NaN(), 0}, {}, 0);
    });
    rejects([&] {
        CharacterMotor m;
        m.Step(0.02f, {}, {}, nullptr);
    });
    check(HeroAnimations::Duration("attack1") == 0.5f);
    rejects([&] { HeroAnimations::Duration("invalid"); });
    // Exercise exception construction/destruction and live pointer-bound callbacks repeatedly.
    for (int n = 0; n < 100; n++) {
        HeroController h;
        h.Advance(0.02f, HeroInputFrame{});
        check(h.PhysicsTicks == 1);
        rejects([&] { HeroController invalid(nullptr, nullptr, -1); });
        HeroCamera camera;
        HeroSmokeTrail smoke;
        check(camera.Config != nullptr && smoke.Settings != nullptr);
    }
    std::ifstream input(argv[1]);
    if (!input)
        return 2;
    std::string line;
    while (std::getline(input, line)) {
        std::istringstream s(line);
        float scale, friction;
        Vec3 start, target, v, d;
        s >> scale >> start.X >> start.Y >> start.Z >> target.X >> target.Y >> target.Z >> v.X >> v.Y >>
            v.Z >> friction >> d.X >> d.Y >> d.Z;
        KinematicCollisionWorld world(0.5f, 1.7f, scale);
        world.Friction = friction;
        world.Boxes.emplace_back(Vec3{-4, 0, 4} * scale, Vec3{4, 8, 5} * scale);
        world.Boxes.emplace_back(Vec3{6, 0, -5} * scale, Vec3{8, 4, 5} * scale);
        world.Boxes.emplace_back(Vec3{-3, 5, -3} * scale, Vec3{3, 6, 3} * scale);
        auto r = world.Move(start, target, v);
        auto c = world.Contacts(r.Position, {0, 0, 1});
        HookHit h;
        bool hit = world.TryHook(start, d, 30 * scale, HookSide::Left, h);
        auto p = r.Position / scale, velocity = r.Velocity / scale, point = h.Point / scale;
        for (float actual : {p.X, p.Y, p.Z, velocity.X, velocity.Y, velocity.Z}) {
            float expected;
            s >> expected;
            maxError = std::max(maxError, std::abs(actual - expected));
            check(std::abs(actual - expected) <= 0.0002f + 0.000002f * std::abs(expected));
        }
        for (bool actual : {r.Collided, r.Grounded, c.Grounded, c.FrontWall, c.UpperFrontClear, hit}) {
            int expected;
            s >> expected;
            check(actual == (expected != 0));
        }
        for (float actual : {point.X, point.Y, point.Z}) {
            float expected;
            s >> expected;
            maxError = std::max(maxError, std::abs(actual - expected));
            check(std::abs(actual - expected) <= 0.0002f + 0.000002f * std::abs(expected));
        }
        if (!s)
            return 3;
        queries++;
        hits += hit;
    }
    std::ofstream report(argv[2]);
    report << "{\"passed\":" << (failed ? "false" : "true") << ",\"checks\":" << checks
           << ",\"collisionQueries\":" << queries << ",\"rayHits\":" << hits << ",\"failed\":" << failed
           << ",\"maxError\":" << maxError << "}\n";
    std::cout << "Native checks: " << checks << ", failures=" << failed << ", collision error=" << maxError
              << '\n';
    return failed || queries == 0 ? 1 : 0;
}
