// SPDX-License-Identifier: MIT
#pragma once
#include "Hud.hpp"
#include <filesystem>
#include <map>
namespace LibAottg {
struct DamageHudQuad {
    std::string Texture;
    float X,Y,Width,Height,SourceX,SourceY,SourceWidth,SourceHeight;
    std::array<float,4> Color;
    float Rotation=0;
};
// Original bitmap fonts and NGUI widget geometry, baked from the C# asset manifest.
class HudPackage {
    struct Glyph { int x,y,w,h,ox,oy,advance; std::map<int,int> kerning; };
    struct Font { std::string texture; int size,spacing,spacingY; std::map<int,Glyph> glyphs; };
    struct Sprite { std::string texture; int w,h; float left,right,top,bottom; };
    struct Widget { float x,y,sx,sy; int pivot,depth; std::array<float,4> color; int font,effect; std::array<float,4> shadow; float ex,ey; std::string sprite; };
    std::map<int,Font> fonts;
    std::map<std::string,Sprite> sprites;
    std::map<std::string,Widget> widgets;
    std::map<std::string,std::string> roles;
    std::vector<DamageHudQuad> Compose(const HeroDamageHud*,const HeroKillInfo*,float,float,const HeroHud* = nullptr) const;
public:
    std::vector<DamageHudQuad> Compose(const HeroHud& s,float w,float h) const { return Compose(nullptr,nullptr,w,h,&s); }
    void Load(const std::filesystem::path& file);
    std::vector<std::string> Textures() const;
    bool IsFontTexture(const std::string& texture) const { for(const auto& [_,font]:fonts) if(font.texture==texture) return true; return false; }
    std::vector<DamageHudQuad> Compose(const HeroDamageHud& s,float w,float h) const { return Compose(&s,nullptr,w,h); }
    std::vector<DamageHudQuad> Compose(const HeroKillInfo& s,float w,float h) const { return Compose(nullptr,&s,w,h); }
};
}
