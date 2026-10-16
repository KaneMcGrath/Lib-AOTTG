// SPDX-License-Identifier: MIT
#include <LibAottg/HudPackage.hpp>
#include <fstream>
#include <iostream>
#include <cmath>
#include <stdexcept>
using namespace LibAottg;
int main(int argc,char** argv){try{
    if(argc!=3)throw std::runtime_error("Usage: LibAottgHeroHudChecks hud.bin managed-oracle.txt");
    HudPackage package;package.Load(argv[1]);std::ifstream oracle(argv[2]);if(!oracle)throw std::runtime_error("Missing oracle");
    int checks=0;const char* skills[]={"mikasa","levi","armin","petra","sasha","jean","marco","eren"};
    for(int i=0;i<30;i++){
        HeroHud s;s.GasFill=(i%5)/4.f;s.BladeFill=(i%4)/3.f;s.SpecialFill=(i%9)/8.f;s.BladePairs=i%6;
        s.Guns=i>=15;s.LeftLoaded=i%2==0;s.RightLoaded=i%3==0;s.LeftReserve=i%8;s.RightReserve=(i+2)%8;
        s.HasTarget=i%2==0;s.Distance=i%3==0?120.f:1001.f;s.Speed=i%2==0?75.25f:75.35f;s.ResourcesVisible=i!=28;s.AimVisible=i!=29;s.Skill=skills[i%8];s.CrosshairStyle=static_cast<HeroCrosshairStyle>(i%5);s.Speedometer=static_cast<HeroSpeedometer>(i%3);
        s.LeftArrow={true,i%2==0,.4f,.45f,.3f};s.RightArrow={true,i%2!=0,.6f,.45f,-.3f};
        auto quads=package.Compose(s,i%2==0?1280.f:1920.f,i%2==0?720.f:1080.f);size_t count;oracle>>count;if(count!=quads.size())throw std::runtime_error("Quad count mismatch in case "+std::to_string(i));
        for(const auto& q:quads){std::string texture;oracle>>texture;if(texture!=q.Texture)throw std::runtime_error("Texture mismatch");
            const float values[]={q.X,q.Y,q.Width,q.Height,q.SourceX,q.SourceY,q.SourceWidth,q.SourceHeight,q.Color[0],q.Color[1],q.Color[2],q.Color[3],q.Rotation};
            for(float actual:values){float expected;oracle>>expected;if(!oracle || std::abs(actual-expected)>.002f)throw std::runtime_error("Native HUD geometry mismatch case "+std::to_string(i));checks++;}
        }
    }
    std::cout<<"HERO HUD native parity passed: "<<checks<<" values\n";return 0;
}catch(const std::exception& e){std::cerr<<e.what()<<"\n";return 1;}}
