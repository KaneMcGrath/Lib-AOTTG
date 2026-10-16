// SPDX-License-Identifier: MIT
#include <LibAottg/Stats.hpp>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <sstream>
using namespace LibAottg;

struct TestWorld : HeroWorld {
    float Scale{};
    Vec3 Target{0, 6, 20}, Anchor{0, 6, 20};
    bool Exists{true};
    CharacterContacts Contacts{};
    std::vector<CharacterEvent> Events;
    std::vector<int> Hits;
    std::vector<int> Sounds;
    CharacterContacts GetContacts(CharacterMotor *, HeroPose) override { return Contacts; }
    bool TryHook(Vec3, Vec3, float, HookSide, HookHit &h) override {
        h = HookHit(Target * Scale);
        return true;
    }
    bool SweepHook(Vec3 a, Vec3 b, HookSide, HookHit &h) override {
        Vec3 target = Target * Scale, d = b - a;
        float t = d.LengthSquared() > 0 ? Vec3::Dot(target - a, d) / d.LengthSquared() : -1;
        h = HookHit(target);
        h.TargetId = 7;
        h.LocalPoint = {1, 2, 3};
        return t >= 0 && t <= 1 && (a + d * t - target).Length() < 0.001f * Scale;
    }
    bool ResolveHookAnchor(int, Vec3, Vec3 &p) override {
        p = Anchor * Scale;
        return Exists;
    }
    void OnCharacterEvent(CharacterEvent e) override { Events.push_back(e); }
    void CollectWeaponHits(HeroWeaponSweep s, std::vector<HeroHit> &hits) override {
        HeroHit h;
        h.TargetId = 1;
        h.Point = s.End;
        hits.push_back(h);
        hits.push_back(h);
        h.TargetId = 2;
        h.Point = s.Start;
        hits.push_back(h);
    }
    void OnHeroHit(HeroHit h) override { Hits.push_back(h.TargetId); }
};
struct TestRig : IHeroPresentation {
    CharacterMotor *m{};
    HeroPose p{};
    void Sample(CharacterMotor *motor, HeroPose pose, CharacterInput, float) override {
        m = motor;
        p = pose;
    }
    Vec3 Head() const override { return p.Position + Vec3::Up() * (1.5f * (m ? m->Scale : 1)); }
    Vec3 HookOrigin(CharacterMotor *motor, HookSide) override {
        return motor->Position() + Vec3::Up() * (0.85f * motor->Scale);
    }
    bool WeaponSegment(HookSide side, bool, Vec3 &start, Vec3 &end) override {
        start = p.Position + p.Rotate({(side == HookSide::Left ? -0.4f : 0.4f) * m->Scale, m->Scale, 0});
        end = start + p.Forward() * m->Scale;
        return true;
    }
};
template <class T> std::string Joined(const std::vector<T> &v) {
    std::ostringstream s;
    for (std::size_t n = 0; n < v.size(); n++) {
        if (n)
            s << ',';
        s << static_cast<int>(v[n]);
    }
    return s.str();
}
int main(int argc, char **argv) {
    if (argc != 3) {
        std::cerr << "Parity commands.txt output.tsv\n";
        return 2;
    }
    std::ifstream input(argv[1]);
    std::ofstream out(argv[2]);
    if (!input || !out)
        return 2;
    out << std::setprecision(9);
    std::unique_ptr<HeroController> hero;
    std::unique_ptr<HeroCamera> camera;
    std::unique_ptr<HeroSmokeTrail> smoke;
    TestWorld world;
    TestRig rig;
    HeroRope left, right;
    std::string line, skill;
    float scale{};
    int equipment{}, route{}, n{}, row{};
    auto presets = HeroStats::RcModPresets();
    while (std::getline(input, line)) {
        std::istringstream s(line);
        std::string op;
        s >> op;
        if (op == "reset") {
            int flags;
            s >> skill >> scale >> equipment >> route >> flags;
            auto cfg = presets.at(skill).ToConfig();
            cfg.WorldScale = scale;
            cfg.DoubleTapDash = (flags & 1) != 0;
            cfg.NoStopJump = (flags & 2) != 0;
            cfg.AutoWallJump = (flags & 4) != 0;
            cfg.NoForcedToRoof = (flags & 8) != 0;
            world = TestWorld{};
            world.Scale = scale;
            hero = std::make_unique<HeroController>(&cfg, &world);
            HeroCameraConfig cc;
            cc.Mode = static_cast<HeroCameraMode>(route);
            camera = std::make_unique<HeroCamera>(&cc);
            hero->Camera = camera.get();
            hero->SetPresentation(&rig);
            rig.Sample(hero->Motor, hero->Pose, {}, 0);
            hero->Motor->SetEquipment(static_cast<EquipmentMode>(equipment));
            hero->Respawn(Vec3{0, 5, 0} * scale);
            smoke = std::make_unique<HeroSmokeTrail>(nullptr, 77);
            left.Reset();
            right.Reset();
            n = 0;
            hero->SoundRequested = [&](HeroSoundCue cue) { world.Sounds.push_back(static_cast<int>(cue.Sound)); };
        } else if (op == "begin") {
            world.Events.clear();
            world.Hits.clear();
            world.Sounds.clear();
        } else if (op == "pose") {
            Vec3 p, v;
            float yaw;
            s >> p.X >> p.Y >> p.Z >> v.X >> v.Y >> v.Z >> yaw;
            hero->Motor->SetPose(p, v, yaw);
            hero->Synchronize();
        } else if (op == "grab")
            hero->Grab();
        else if (op == "die")
            hero->Die();
        else if (op == "escape" || op == "respawn") {
            Vec3 p;
            s >> p.X >> p.Y >> p.Z;
            if (op == "escape")
                hero->EscapeGrab(p);
            else
                hero->Respawn(p);
        } else if (op == "tick") {
            float dt;
            int mask, held, pressed, released, exists;
            HeroInputFrame raw;
            s >> dt >> route >> mask >> held >> pressed >> released >> raw.MoveX >> raw.MoveY >> raw.Reel >>
                raw.AimOrigin.X >> raw.AimOrigin.Y >> raw.AimOrigin.Z >> raw.AimDirection.X >>
                raw.AimDirection.Y >> raw.AimDirection.Z >> raw.MouseDeltaX >> raw.MouseDeltaY >>
                world.Anchor.X >> world.Anchor.Y >> world.Anchor.Z >> exists;
            raw.Held = static_cast<HeroButtons>(held);
            raw.Pressed = static_cast<HeroButtons>(pressed);
            raw.Released = static_cast<HeroButtons>(released);
            raw.HasView = true;
            raw.ViewForward = {0, 0, 1};
            raw.HasPointer = true;
            raw.PointerX = 0.35f;
            raw.PointerY = 0.45f;
            raw.AspectRatio = 16.0f / 9;
            raw.OrbitHeld = n % 300 < 200;
            world.Contacts.Grounded = (mask & 1) != 0;
            world.Contacts.GroundNormal = Vec3::Up();
            world.Contacts.FrontWall = (mask & 2) != 0;
            world.Contacts.UpperFrontClear = (mask & 4) != 0;
            world.Exists = exists != 0;
            auto m = hero->Motor;
            if (route == 0) {
                auto i = HeroInputMapper{}.Sample(raw, raw.ViewForward, raw.AimOrigin, raw.AimDirection);
                i.SourceAttackSelection = true;
                m->Step(dt, i, world.Contacts, &world);
                rig.Sample(m, HeroBodyPose::Evaluate(m), i, dt);
            } else if (route == 1) {
                auto i = hero->Input->Sample(raw, raw.ViewForward, raw.AimOrigin, raw.AimDirection);
                hero->Advance(dt, i);
            } else
                hero->Advance(dt, raw);
            auto pose = HeroBodyPose::Evaluate(m);
            left.Sample(m, HookSide::Left, rig.HookOrigin(m, HookSide::Left), n * 0.02f);
            right.Sample(m, HookSide::Right, rig.HookOrigin(m, HookSide::Right), n * 0.02f);
            smoke->Advance(dt, m, pose);
            const auto p = m->Position() / scale, v = m->Velocity() / scale, l = m->LeftHookAnchor() / scale,
                       r = m->RightHookAnchor() / scale, c = camera->Pose.Position / scale;
            const auto &a = m->Animation();
            const auto &particles = smoke->Particles();
            out << row++ << '\t' << skill << '\t' << scale << '\t' << equipment << '\t' << route << '\t'
                << n++ << '\t' << p.X << '\t' << p.Y << '\t' << p.Z << '\t' << v.X << '\t' << v.Y << '\t'
                << v.Z << '\t' << m->Gas << '\t' << m->Blade << '\t' << m->SpareBladePairs << '\t'
                << static_cast<int>(m->State) << '\t' << m->Grounded << '\t' << m->FacingDegrees << '\t'
                << m->FacingTargetDegrees << '\t' << m->AttackHitboxActive << '\t' << a.Clip << '\t' << a.Time
                << '\t' << a.Speed << '\t' << a.Revision << '\t' << m->SkillCooldownRemaining() << '\t'
                << m->FlareCooldownRemaining() << '\t' << static_cast<int>(m->LeftHookPhase()) << '\t'
                << static_cast<int>(m->RightHookPhase()) << '\t' << l.X << '\t' << l.Y << '\t' << l.Z << '\t'
                << r.X << '\t' << r.Y << '\t' << r.Z << '\t' << m->LeftGunLoaded << '\t' << m->RightGunLoaded
                << '\t' << m->LeftGunReserve << '\t' << m->RightGunReserve << '\t' << pose.Pitch << '\t'
                << pose.Roll << '\t' << pose.Leaning << '\t' << pose.LeanLeft << '\t' << c.X << '\t' << c.Y
                << '\t' << c.Z << '\t' << camera->Pose.Yaw << '\t' << camera->Pose.Pitch << '\t'
                << camera->Pose.Roll << '\t' << left.Points().size() << '\t' << right.Points().size() << '\t'
                << particles.size() << '\t' << (particles.empty() ? 0 : particles[0].Age) << '\t'
                << (particles.empty() ? 0 : particles[0].Size / scale) << '\t'
                << (particles.empty() ? 0 : particles[0].Position.X / scale) << '\t' << Joined(world.Events)
                << '\t' << Joined(world.Hits) << '\t' << Joined(world.Sounds) << '\t' << hero->PhysicsTicks << '\n';
        } else {
            std::cerr << "Unknown command " << op;
            return 3;
        }
        if (!s && op != "grab" && op != "die") {
            std::cerr << "Invalid command " << line;
            return 3;
        }
    }
    std::cout << "Native frames: " << row << '\n';
}
