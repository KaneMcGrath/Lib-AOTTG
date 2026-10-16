// SPDX-License-Identifier: MIT
#include <LibAottg/Stats.hpp>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <sstream>
using namespace LibAottg;
struct RecordedWorld : HeroWorld {
    bool aimHit{}, useMuzzle{};
    Vec3 target{0, 10, 20}, muzzle{};
    bool TryHook(Vec3, Vec3, float, HookSide, HookHit &hit) override {
        hit = HookHit(target);
        return aimHit;
    }
    Vec3 HookOrigin(CharacterMotor *m, HookSide) override { return useMuzzle ? muzzle : m->Position(); }
    bool SweepHook(Vec3 a, Vec3 b, HookSide, HookHit &hit) override {
        auto d = b - a;
        float t = d.LengthSquared() > 0 ? Vec3::Dot(target - a, d) / d.LengthSquared() : -1;
        hit = HookHit(target);
        return t >= 0 && t <= 1 && (a + d * t - target).Length() < 0.001f;
    }
};
int main(int argc, char **argv) {
    if (argc != 3)
        return 2;
    std::ifstream in(argv[1]);
    if (!in)
        return 2;
    std::unique_ptr<CharacterMotor> motor;
    RecordedWorld world;
    KinematicCollisionWorld collision;
    bool floor = false;
    int count = 0, failed = 0;
    float maxP = 0, maxV = 0, maxG = 0;
    std::string line;
    auto cfg = HeroStats::RcModPresets().at("mikasa").ToConfig();
    while (std::getline(in, line)) {
        std::istringstream s(line);
        std::string op;
        s >> op;
        CharacterInput i;
        i.LookDirection = {0, 0, 1};
        i.AimDirection = {0, 1, 1};
        if (op == "reset") {
            Vec3 p, v;
            bool ground;
            float gas;
            s >> p.X >> p.Y >> p.Z >> v.X >> v.Y >> v.Z >> ground >> gas >> floor;
            motor = std::make_unique<CharacterMotor>(&cfg);
            motor->Respawn(p, ground);
            motor->SetPose(p, v, 0);
            motor->ConsumeGas(motor->Config()->GasCapacity - gas);
        } else if (op == "dash" || op == "jump") {
            CharacterContacts c;
            if (op == "dash") {
                i.MoveY = 1;
                i.Dash = {true, true, false};
            } else {
                i.Jump = {false, true, false};
                c.Grounded = true;
            }
            motor->AdvanceFrame(0.00000001f, i, c, &world);
        } else if (op == "velocity") {
            Vec3 v;
            s >> v.X >> v.Y >> v.Z;
            motor->SetPose(motor->Position(), v, motor->FacingDegrees);
        } else if (op == "hook") {
            s >> world.target.X >> world.target.Y >> world.target.Z >> world.muzzle.X >> world.muzzle.Y >>
                world.muzzle.Z;
            world.aimHit = true;
            world.useMuzzle = true;
            i.HookLeft = {true, false, false};
            i.AimDirection = world.target - motor->Position();
            motor->AdvanceFrame(0.00000001f, i, {}, &world);
        } else if (op == "frame" || op == "physics") {
            float dt;
            bool jump, left;
            CharacterContacts c;
            s >> dt >> i.MoveY >> jump >> left >> c.Grounded >> c.FrontWall >> c.UpperFrontClear >>
                i.LookDirection.X >> i.LookDirection.Y >> i.LookDirection.Z;
            i.Jump = {jump, false, false};
            i.HookLeft = {left, false, false};
            if (op == "frame")
                motor->AdvanceFrame(dt, i, c, &world);
            else {
                auto start = motor->Position();
                motor->StepPhysics(dt, i, c, &world);
                if (floor) {
                    auto offset = Vec3::Up() * 1000;
                    auto r = collision.Move(start - offset, motor->Position() - offset, motor->Velocity());
                    motor->SetPose(r.Position + offset, r.Velocity, motor->FacingDegrees);
                }
            }
        } else if (op == "observed") {
            Vec3 p, v;
            float gas;
            std::string scenario;
            int tick;
            s >> p.X >> p.Y >> p.Z >> v.X >> v.Y >> v.Z >> gas >> scenario >> tick;
            float pe = (p - motor->Position()).Length(), ve = (v - motor->Velocity()).Length(),
                  ge = gas >= 0 ? std::abs(gas - motor->Gas) : 0;
            maxP = std::max(maxP, pe);
            maxV = std::max(maxV, ve);
            maxG = std::max(maxG, ge);
            count++;
            if (pe > (floor ? 0.025f : 0.0002f) || ve > (floor ? 0.003f : 0.0002f) || ge > 0.0002f) {
                failed++;
                if (failed <= 5)
                    std::cerr << scenario << " tick " << tick << " p=" << pe << " v=" << ve << " gas=" << ge
                              << '\n';
            }
        } else if (op == "complete") {
            int expected;
            s >> expected;
            if (count != expected || count == 0)
                return 3;
        } else
            return 4;
    }
    std::ofstream report(argv[2]);
    report << std::setprecision(9) << "{\"passed\":" << (failed ? "false" : "true")
           << ",\"observations\":" << count << ",\"failed\":" << failed << ",\"maxPositionError\":" << maxP
           << ",\"maxVelocityError\":" << maxV << ",\"maxGasError\":" << maxG << "}\n";
    std::cout << "Original HERO: " << count << " observations, " << failed << " failures, p=" << maxP
              << ", v=" << maxV << '\n';
    return failed || count == 0 ? 1 : 0;
}
