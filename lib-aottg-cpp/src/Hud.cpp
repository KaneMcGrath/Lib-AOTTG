// SPDX-License-Identifier: MIT
#include <LibAottg/Core.hpp>
#include <sstream>
#include <iomanip>
#include <locale>
#include <LibAottg/Hud.hpp>
#include <algorithm>
#include <cmath>
#include <limits>
#include <stdexcept>
namespace LibAottg {
namespace {
constexpr int thresholds[]={350,950,2450,4550,7000,15000,100000};
constexpr int depletion[]={1,2,5,10,15,20,25,25};
constexpr float multipliers[]={1,1.1f,1.2f,1.3f,1.5f,1.7f,2,2.3f,2.5f};
constexpr const char* chains[]={"","x1.1!","x1.2!","x1.3!","x1.5!","x1.7!","x2!","x2.3!","x2.5!"};
void Positive(float v) { if (!std::isfinite(v)||v<=0) throw std::invalid_argument("HUD size/scale"); }
void Delta(float v) { if (!std::isfinite(v)||v<0) throw std::invalid_argument("HUD deltaTime"); }
float Bounce(float t) {
    if(t<1/2.75f) return 7.5625f*t*t;
    if(t<2/2.75f) { t-=1.5f/2.75f; return 7.5625f*t*t+.75f; }
    if(t<2.5f/2.75f) { t-=2.25f/2.75f; return 7.5625f*t*t+.9375f; }
    t-=2.625f/2.75f; return 7.5625f*t*t+.984375f;
}
}
int HeroDamage::Calculate(Vec3 a,Vec3 b,float m,float scale,int minimum) {
    if(!WorldBox::Finite(a)||!WorldBox::Finite(b)||!std::isfinite(m)||m<0||minimum<0) throw std::invalid_argument("damage inputs");
    Positive(scale);
    // Match stored float speed followed by Mono's extended multiplication precision.
    double value=double((a-b).Length()/scale)*10.0*double(m);
    return std::max(minimum,value>=std::numeric_limits<int>::max()?std::numeric_limits<int>::max():int(value));
}
std::string HeroDamageHud::RankLetter() const { return std::array<const char*,8>{"D","C","B","A","S","S","S","X"}[DisplayRank]; }
std::string HeroDamageHud::RankSuffix() const { return std::array<const char*,8>{"eja Vu","asual","oppin!","mazing!","ensational!","pectacular!!","tylish!!!","TREEME!!!"}[DisplayRank]; }
std::string HeroDamageHud::RankColor() const { return std::array<const char*,8>{"FFFFFF","ACFA58","F4FA58","FAAC58","FA8258","BE81F7","FF0000","000000"}[DisplayRank]; }
void HeroDamageHud::Resize(float w) { Positive(w); if(w==width)return; width=w; StylishX=StylePoints>0?float(int(width*.5f-2)):width; StylishY=0; }
void HeroDamageHud::ShowDamage(int d) { Style(d); ScoreText=std::to_string(d); scoreAge=0; scoreSize=float(std::clamp(int(d*.1f),40,150)); ScoreScale=0; }
void HeroDamageHud::Style(int d) {
    if(d<-1)throw std::invalid_argument("damage");
    if(d!=-1) { StylePoints+=float(int((double(d)+200)*double(multipliers[ChainRank]))); TotalDamage+=d; ChainRank=std::min(ChainRank+1,8);chainTime=5;++Hits;SetRank(); }
    else if(StylePoints==0){ ++StylePoints;SetRank(); }
    shakeTime=std::max(shakeTime,.3f);
    PointsText=std::to_string(int(StylePoints)); HitsText=std::to_string(Hits)+(Hits<=1?"Hit":"Hits"); ChainText=chains[ChainRank];
}
void HeroDamageHud::SetRank() {
    int previous=StyleRank,rank=0;while(rank<7&&StylePoints>thresholds[rank])++rank;StyleRank=rank;
    if(rank<previous){ if(lostRank){StylePoints=0;Hits=TotalDamage=StyleRank=0;}else lostRank=true; }else if(rank>previous)lostRank=false;
}
void HeroDamageHud::Advance(float dt,bool paused) {
    Delta(dt);if(paused||dt==0)return;scoreAge+=dt;
    float elastic=scoreAge==0||scoreAge==1?scoreAge:float(std::pow(2.,-10*scoreAge)*std::sin((scoreAge-.075)*6.283185/.3)+1);
    ScoreScale=scoreAge<1?scoreSize*elastic:scoreAge<2?scoreSize:scoreAge<2.5f?scoreSize*Bounce((2.5f-scoreAge)*2):0;
    if(StylePoints>0){DisplayRank=StyleRank;int percent=StyleRank==0?int(StylePoints*100)/thresholds[0]:StyleRank==7?100:int((StylePoints-thresholds[StyleRank-1])*100/(thresholds[StyleRank]-thresholds[StyleRank-1]));FillAmount=std::clamp(percent*.01f,0.f,1.f);StylePoints-=depletion[StyleRank]*dt*10;SetRank();}
    else StylishX+=(width-StylishX)*std::min(1.f,dt*3);
    if(chainTime>0)chainTime-=dt;else {chainTime=0;ChainRank=0;}
    if(shakeTime>0){shakeTime-=dt;StylishX=float(int(width*.5f-2));StylishY=flip?5.f:-5.f;flip=!flip;if(shakeTime<=0)StylishY=0;}
}
void HeroDamageHud::Reset(){float w=width;*this=HeroDamageHud{};width=w;StylishX=w;}
void HeroKillInfoEntry::Advance(float dt,float height){
    Age=float(Age+double(dt));float lerp=std::clamp(dt*10,0.f,1.f);
    if(Age<.2f)Scale=float(Scale+(1.5-Scale)*lerp);else if(Age<1)Scale=float(Scale+(1.-Scale)*lerp);
    if(Age>8){Y=float(Y+dt*.15*std::clamp(height,320.f,1536.f)*.5);Alpha=float((1.-dt*45.)+8.-Age);}
    else {float target=-float(int(100.f-height*.5f)+Row*24);Y=float(Y+(target-double(Y))*lerp);}
}
void HeroKillInfo::Show(bool kt,std::string k,bool vt,std::string v,int damage,float height){
    Positive(height);for(auto& e:Entries)if(std::find(tracked.begin(),tracked.end(),e.Id)!=tracked.end()){++e.Row;e.Compact=true;if(e.Row>4)e.Dismiss();}
    if(tracked.size()>4){for(auto& e:Entries)if(e.Id==tracked.front())e.Dismiss();tracked.erase(tracked.begin());}
    HeroKillInfoEntry e;e.KillerIsTitan=kt;e.VictimIsTitan=vt;e.Killer=std::move(k);e.Victim=std::move(v);e.Damage=damage;e.Y=-100+height*.5f;e.Id=++nextId;tracked.push_back(e.Id);Entries.push_back(std::move(e));
}
void HeroKillInfo::Advance(float dt,float height,bool paused){Positive(height);Delta(dt);if(paused)return;for(auto& e:Entries)e.Advance(dt,height);for(auto& e:Entries)if(e.Expired())std::erase(tracked,e.Id);std::erase_if(Entries,[](const auto& e){return e.Expired();});}
}

namespace LibAottg {
void HeroHud::Read(const CharacterMotor& m) {
    auto fraction=[](float v,float cap){return cap>0 && std::isfinite(v)?std::clamp(v/cap,0.f,1.f):0.f;};
    GasFill=fraction(m.Gas,m.Config()->GasCapacity);BladeFill=fraction(m.Blade,m.Config()->BladeCapacity);
    SpecialFill=m.Config()->SkillCooldown>0?fraction(m.Config()->SkillCooldown-m.SkillCooldownRemaining(),m.Config()->SkillCooldown):1;
    BladePairs=m.SpareBladePairs;Guns=m.Equipment==EquipmentMode::Guns;LeftLoaded=m.LeftGunLoaded;RightLoaded=m.RightGunLoaded;
    LeftReserve=m.LeftGunReserve;RightReserve=m.RightGunReserve;Skill=m.Config()->SkillId;
    Speed=m.Velocity().Length()/m.Scale;Visible=m.State!=CharacterState::Dead;
}
std::string HeroHud::AimText() const {
    std::string result=ShowDistance && HasTarget?(Distance<=1000?std::to_string(int(Distance)):"???"):"";
    if(Speedometer!=HeroSpeedometer::Off){std::ostringstream text;text.imbue(std::locale::classic());text<<std::fixed<<std::setprecision(1)<<(Speedometer==HeroSpeedometer::Speed?Speed:Speed/100)<<(Speedometer==HeroSpeedometer::Speed?" u/s":"K");if(!result.empty())result+='\n';result+=text.str();}
    return result;
}
}

namespace LibAottg {
void HeroHud::Aim(const CharacterMotor& m,const CharacterInput& input,ICharacterWorld& world,const HeroCameraPose& view,float aspect){
    LeftArrow.Visible=RightArrow.Visible=false;HookHit hit;
    HasTarget=world.TryHook(input.AimOrigin,input.AimDirection,10000000*m.Scale,HookSide::Left,hit);
    Distance=HasTarget?(hit.Point-m.Position()).Length()/m.Scale:0;if(!HasTarget || !ShowArrows)return;
    Vec3 right(float(std::cos(m.FacingDegrees*cs_pi/180)),0,float(-std::sin(m.FacingDegrees*cs_pi/180)));
    float rayDistance=(hit.Point-input.AimOrigin).Length()/m.Scale,spread=rayDistance<=50?rayDistance*.05f:rayDistance*.3f;
    for(int side=0;side<2;side++){
        float sign=side==0?-1.f:1.f;Vec3 origin=m.Position()+(Vec3::Up()*.4f+right*(sign*.3f))*m.Scale;
        auto direction=(hit.Point+right*(sign*spread*m.Scale)-origin).Normalized();HookHit arrowHit;
        auto& arrow=side==0?LeftArrow:RightArrow;if(!world.TryHook(origin,direction,1000000*m.Scale,static_cast<HookSide>(side),arrowHit))continue;
        auto delta=arrowHit.Point-view.Position;float depth=Vec3::Dot(delta,view.Forward);if(depth<=.0001f)continue;
        float tangent=float(std::tan(view.FieldOfView*cs_pi/360));
        arrow.X=.5f+Vec3::Dot(delta,Vec3::Cross(view.Up,view.Forward).Normalized())/(2*depth*tangent*aspect);
        arrow.Y=.5f-Vec3::Dot(delta,view.Up)/(2*depth*tangent);
        arrow.Rotation=std::atan2(arrow.Y-AimY,(arrow.X-AimX)*aspect)+(side==0?float(cs_pi):0);
        arrow.InRange=(arrowHit.Point-origin).Length()/m.Scale<=120;arrow.Visible=true;
    }
}
}
