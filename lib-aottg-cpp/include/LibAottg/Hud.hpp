// SPDX-License-Identifier: MIT
#pragma once
#include "Core.hpp"
#include <string>
#include <vector>
#include <array>

namespace LibAottg {
struct CharacterMotor;
struct CharacterInput;
struct ICharacterWorld;
struct HeroCameraPose;
enum class HeroCrosshairStyle { Default, Square, Plus, Target, Dot };
enum class HeroSpeedometer { Off, Speed, Damage };
struct HeroHudArrow { bool Visible=false, InRange=false; float X=0,Y=0,Rotation=0; };
struct HeroHud {
    float GasFill=1, BladeFill=1, SpecialFill=1, Speed=0, Distance=0;
    int BladePairs=5, LeftReserve=0, RightReserve=0;
    bool Guns=false, LeftLoaded=false, RightLoaded=false, HasTarget=false, Visible=true, ResourcesVisible=true, AimVisible=true;
    bool ShowDistance=true, ShowArrows=true;
    HeroSpeedometer Speedometer=HeroSpeedometer::Speed;
    HeroCrosshairStyle CrosshairStyle=HeroCrosshairStyle::Default;
    std::string Skill="mikasa";
    float AimX=.5f,AimY=.5f;
    HeroHudArrow LeftArrow,RightArrow;
    bool InRange() const { return HasTarget && Distance<=120; }
    void Read(const CharacterMotor& motor);
    std::string AimText() const;
    void Aim(const CharacterMotor&,const CharacterInput&,ICharacterWorld&,const HeroCameraPose&,float aspect);
};

struct HeroDamage {
    static int Calculate(Vec3 attackerVelocity, Vec3 targetVelocity, float scoreMultiplier=1,
                         float worldScale=1, int minimumDamage=10);
};
class HeroDamageHud {
    float chainTime=0, shakeTime=0, scoreAge=2.5f, scoreSize=0, width=1280;
    bool lostRank=false, flip=false;
    void SetRank();
public:
    float StylePoints=0, FillAmount=0, StylishX=1280, StylishY=0, ScoreScale=0;
    int StyleRank=0, Hits=0, TotalDamage=0, ChainRank=0, DisplayRank=0;
    std::string PointsText, HitsText, ChainText, ScoreText;
    std::string RankLetter() const;
    std::string RankSuffix() const;
    std::string RankColor() const;
    void Resize(float screenWidth);
    void ShowDamage(int damage);
    void Style(int damage);
    void Advance(float deltaTime, bool paused=false);
    void Reset();
};
struct HeroKillInfoEntry {
    bool KillerIsTitan=false, VictimIsTitan=false, Compact=false;
    std::string Killer, Victim;
    int Damage=0, Row=0;
    float Age=0, Scale=.85f, Y=0, Alpha=1;
    unsigned Id=0;
    bool Expired() const { return Age>8.5f; }
    void Dismiss() { Age=8; }
    void Advance(float dt,float height);
};
class HeroKillInfo {
    std::vector<unsigned> tracked;
    unsigned nextId=0;
public:
    std::vector<HeroKillInfoEntry> Entries;
    void Show(bool killerIsTitan,std::string killer,bool victimIsTitan,std::string victim,int damage=0,float screenHeight=720);
    void Advance(float deltaTime,float screenHeight=720,bool paused=false);
    void Clear() { Entries.clear(); tracked.clear(); }
};
}
