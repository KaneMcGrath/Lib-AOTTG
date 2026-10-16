// SPDX-License-Identifier: MIT
#include <LibAottg/HudPackage.hpp>
#include <fstream>
#include <algorithm>
#include <stdexcept>
#include <cmath>
#include <set>
namespace LibAottg {
namespace {
struct Reader {
    std::ifstream f;
    explicit Reader(const std::filesystem::path& p):f(p,std::ios::binary) { if(!f||std::filesystem::file_size(p)>16*1024*1024)throw std::runtime_error("Invalid HUD package"); }
    template<class T>T Value(){T v{};if(!f.read(reinterpret_cast<char*>(&v),sizeof(v)))throw std::runtime_error("Truncated HUD package");return v;}
    int Count(int max=65536){int n=Value<int>();if(n<0||n>max)throw std::runtime_error("HUD count out of bounds");return n;}
    std::string String(){std::string s(Count(4096),'\0');if(!f.read(s.data(),s.size()))throw std::runtime_error("HUD string");return s;}
    std::array<float,4> Color(){std::array<float,4> c;for(auto& v:c)v=Value<float>();return c;}
};
std::array<float,4> Hex(std::string h,float alpha){unsigned n=unsigned(std::stoul(h,nullptr,16));return {((n>>16)&255)/255.f,((n>>8)&255)/255.f,(n&255)/255.f,alpha};}
}
void HudPackage::Load(const std::filesystem::path& path){
    Reader r(path);std::string magic;for(int i=0;i<8;++i)magic+=r.Value<char>();if(magic!="AOHUD001")throw std::runtime_error("HUD version");
    fonts.clear();sprites.clear();widgets.clear();roles.clear();
    for(int n=r.Count(32);n--;){int id=r.Value<int>();Font f;f.texture=r.String();f.size=r.Value<int>();f.spacing=r.Value<int>();f.spacingY=r.Value<int>();if(f.size<=0)throw std::runtime_error("HUD font size");
        for(int k=r.Count();k--;){int code=r.Value<int>();Glyph g;g.x=r.Value<int>();g.y=r.Value<int>();g.w=r.Value<int>();g.h=r.Value<int>();g.ox=r.Value<int>();g.oy=r.Value<int>();g.advance=r.Value<int>();for(int t=r.Count();t--;){int a=r.Value<int>(),b=r.Value<int>();g.kerning[a]=b;}f.glyphs[code]=std::move(g);}fonts[id]=std::move(f);}
    for(int n=r.Count(64);n--;){auto name=r.String();Sprite s;s.texture=r.String();s.w=r.Value<int>();s.h=r.Value<int>();s.left=r.Value<float>();s.right=r.Value<float>();s.top=r.Value<float>();s.bottom=r.Value<float>();sprites[name]=s;}
    for(int n=r.Count(128);n--;){auto name=r.String();Widget w;w.x=r.Value<float>();w.y=r.Value<float>();w.sx=r.Value<float>();w.sy=r.Value<float>();w.pivot=r.Value<int>();w.depth=r.Value<int>();w.color=r.Color();w.font=r.Value<int>();w.effect=r.Value<int>();w.shadow=r.Color();w.ex=r.Value<float>();w.ey=r.Value<float>();w.sprite=r.String();if(w.pivot<0||w.pivot>8)throw std::runtime_error("HUD pivot");widgets[name]=w;}
    for(int n=r.Count(64);n--;){auto a=r.String(),b=r.String();roles[a]=b;}
    if(r.f.peek()!=std::char_traits<char>::eof())throw std::runtime_error("HUD trailing data");
    for(auto& t:Textures())if(std::filesystem::path(t).filename()!=t)throw std::runtime_error("HUD texture path");
}
std::vector<std::string> HudPackage::Textures()const{std::set<std::string> s;for(auto& [_,f]:fonts)s.insert(f.texture);for(auto& [_,v]:sprites)s.insert(v.texture);return {s.begin(),s.end()};}
std::vector<DamageHudQuad> HudPackage::Compose(const HeroDamageHud* state,const HeroKillInfo* feed,float width,float height,const HeroHud* hero)const{
    if(!std::isfinite(width)||!std::isfinite(height)||width<=0||height<=0)throw std::invalid_argument("HUD viewport");
    auto layout=widgets;
    std::vector<std::pair<int,DamageHudQuad>> ordered;float cx=std::nearbyint(width*.5f)-.5f,cy=std::nearbyint(height*.5f)-.5f;
    float rowScale=1,rowY=0,rowAlpha=1,shiftX=0;
    auto add=[&](const Widget& w,const std::string& texture,float sx,float sy,float sw,float sh,float x,float y,float rw,float rh,std::array<float,4> color){if(sw<=0||sh<=0||rw<=0||rh<=0)return;ordered.push_back({w.depth,{texture,x,y,rw,rh,sx,sy,sw,sh,color}});};
    auto label=[&](const std::string& name,const std::string& text,const std::string& hex="",float overrideScale=-1){
        if(text.empty())return;auto& w=layout.at(name);auto& f=fonts.at(w.font);float sx=(overrideScale<0?w.sx:overrideScale)/f.size*rowScale,sy=(overrideScale<0?w.sy:overrideScale)/f.size*rowScale;if(sx<=0||sy<=0)return;
        auto color=w.color;color[3]*=std::clamp(rowAlpha,0.f,1.f);if(!hex.empty())color=Hex(hex,color[3]);std::vector<std::array<float,4>> colors{color};
        struct Item{const Glyph* g;int x,line;std::array<float,4> c;};std::vector<Item> glyphs;std::vector<int> widths;int cursor=0,previous=0,line=0;
        for(size_t i=0;i<text.size();++i){unsigned code=static_cast<unsigned char>(text[i]);
            if(code=='\n'){widths.push_back(cursor);cursor=0;previous=0;++line;continue;}if(code<32){previous=0;continue;}
            if(text.compare(i,3,"[-]")==0){if(colors.size()>1)colors.pop_back();i+=2;continue;}
            if(code=='['&&i+7<text.size()&&text[i+7]==']'&&text.substr(i+1,6).find_first_not_of("0123456789abcdefABCDEF")==std::string::npos){colors.push_back(Hex(text.substr(i+1,6),color[3]));i+=7;continue;}
            // Skyrim display names are UTF-8; decode codepoints before bitmap glyph lookup.
            if(code>=192){int count=code<224?1:code<240?2:3;code&=(1u<<(6-count))-1;for(int k=0;k<count&&i+1<text.size();++k)code=(code<<6)|(static_cast<unsigned char>(text[++i])&63);}
            auto it=f.glyphs.find(int(code));if(it==f.glyphs.end())it=f.glyphs.find('?');if(it==f.glyphs.end())continue;auto& g=it->second;auto kerning=g.kerning.find(previous);if(kerning!=g.kerning.end())cursor+=kerning->second;if(code!=' ')glyphs.push_back({&g,cursor,line,colors.back()});cursor+=g.advance+f.spacing;previous=int(code);
        }
        widths.push_back(cursor);int pw=*std::max_element(widths.begin(),widths.end()),ph=int(widths.size())*(f.size+f.spacingY);
        float x=cx+(w.x+shiftX)*rowScale+(state&&name!="LabelScore"?state->StylishX:0),y=cy-w.y*rowScale-rowY-(state&&name!="LabelScore"?state->StylishY:0);
        x-=(w.pivot%3)*.5f*pw*sx;y-=(w.pivot/3)*.5f*ph*sy;
        auto pass=[&](float dx,float dy,std::array<float,4> tint,bool original=false){for(auto& item:glyphs){auto& g=*item.g;float alignment=w.pivot%3==0?0:w.pivot%3==2?float(pw-widths[item.line]):std::nearbyint((pw-widths[item.line])*.5f)+((pw&1)?.5f:0);add(w,f.texture,float(g.x),float(g.y),float(g.w),float(g.h),x+(item.x+g.ox+alignment+dx)*sx,y+(item.line*(f.size+f.spacingY)+g.oy+dy)*sy,g.w*sx,g.h*sy,original?item.c:tint);}};
        if(w.effect){auto shadow=w.shadow;shadow[3]*=color[3];pass(w.ex,w.ey,shadow);if(w.effect==2){pass(-w.ex,-w.ey,shadow);pass(w.ex,-w.ey,shadow);pass(-w.ex,w.ey,shadow);}}pass(0,0,color,true);
    };
    auto sprite=[&](const std::string& name,float fill,const std::string& tint="",bool vertical=false,bool radial=false,float rotation=0){
        auto& w=layout.at(name);auto& sp=sprites.at(w.sprite);float wx=std::abs(w.sx),wy=std::abs(w.sy);
        float px=w.pivot%3==0?sp.left:w.pivot%3==1?(sp.left-sp.right-1)*.5f:-1-sp.right;
        float py=w.pivot/3==0?-sp.top:w.pivot/3==1?(sp.bottom-sp.top+1)*.5f:1+sp.bottom;
        float x=cx+(state?state->StylishX:0)+(w.x+px*wx)*rowScale,y=cy-(state?state->StylishY:0)-rowY-(w.y+py*wy)*rowScale;
        auto c=w.color;c[3]*=std::clamp(rowAlpha,0.f,1.f);if(tint=="warning")c={1,1,0,c[3]};if(tint=="red")c={1,0,0,c[3]};if(tint=="empty")c={1,0,0,c[3]};
        auto piece=[&](float u,float v,float uw,float vh){auto before=ordered.size();float su=w.sx<0?1-u:u,sv=w.sy<0?1-v:v;
            add(w,sp.texture,su*sp.w,sv*sp.h,uw*sp.w,vh*sp.h,x+u*wx*rowScale,y+v*wy*rowScale,uw*wx*rowScale,vh*wy*rowScale,c);
            if(ordered.size()>before){auto& q=ordered.back().second;if(w.sx<0)q.SourceWidth=-q.SourceWidth;if(w.sy<0)q.SourceHeight=-q.SourceHeight;q.Rotation=rotation;}};
        if(radial && fill<1){constexpr int slices=128;for(int row=0;row<slices;row++){int begin=-1;for(int col=0;col<=slices;col++){
            double angle=col==slices?0:std::atan2((col+.5)/slices-.5,.5-(row+.5)/slices);if(angle<0)angle+=2*cs_pi;
            bool inside=col<slices && angle<fill*2*cs_pi;if(inside && begin<0)begin=col;
            if(!inside && begin>=0){piece(float(begin)/slices,float(row)/slices,float(col-begin)/slices,1.f/slices);begin=-1;}
        }}}else if(vertical)piece(0,1-fill,1,fill);else piece(0,0,fill,1);
    };
    if(hero){
        if(!hero->Visible)return {};
        auto tint=[](float f){return f<=0?"empty":f<.3f?"warning":"";};
        auto place=[&](const std::string& n,float x,float y){layout.at(n).x=x;layout.at(n).y=y;};
        if(hero->ResourcesVisible){
        rowY=-height*.5f+5;
        for(const std::string side:{"L","R"}){
            sprite("gas"+side,1,hero->Guns?"":tint(hero->GasFill));sprite("gas"+side+"1",hero->GasFill,"",true);
            if(!hero->Guns){sprite("bladeC"+side,hero->BladeFill,"",true);for(int n=1;n<=std::min(5,hero->BladePairs);n++)sprite(std::string("blade")+(side=="L"?"l":"r")+std::to_string(n),1,n==1?tint(hero->BladeFill):"");}
            else {if(side=="L"?hero->LeftLoaded:hero->RightLoaded)sprite("bullet"+side,1);for(int n=1;n<=std::min(7,side=="L"?hero->LeftReserve:hero->RightReserve);n++)sprite("bullet"+side+std::to_string(n),1);}
        }
        place("skill_cd_bottom",0,0);sprite("skill_cd_bottom",1);auto skill="skill_cd_"+hero->Skill;
        if(layout.contains(skill)){place(skill,0,0);sprite(skill,hero->SpecialFill,"",false,true);}}rowY=0;
        if(hero->AimVisible){const char* styles[]={"CursorDefault","CursorSquare","CursorPlus","CursorTarget","CursorDot"};auto cross=styles[std::clamp(int(hero->CrosshairStyle),0,4)];place(cross,hero->AimX*width-cx,cy-hero->AimY*height);sprite(cross,1,hero->InRange()?"":"red");
            place("CrosshairText",hero->AimX*width-cx,cy-hero->AimY*height-18);label("CrosshairText",hero->AimText(),hero->InRange()?"":"FF0000");
            if(hero->ShowArrows)for(auto [arrow,side]:{std::pair{hero->LeftArrow,"L"},std::pair{hero->RightArrow,"R"}}){if(!arrow.Visible)continue;auto n=std::string("cross")+side+(arrow.InRange?"1":"2");place(n,arrow.X*width-cx,cy-arrow.Y*height);sprite(n,1,"",false,false,arrow.Rotation);}
        }
    }
    else    if(state){sprite("Sprite",1);sprite("bar",state->FillAmount);label("LabelTotal",state->PointsText);label("LabelHits",state->HitsText);label("LabelChain",state->ChainText);label("LabelS",state->RankLetter(),state->RankColor());label("Labelsub",state->RankSuffix());if(state->DisplayRank==5||state->DisplayRank==6)label("LabelS2","S",state->RankColor());if(state->DisplayRank==6)label("LabelS1","S",state->RankColor());label("LabelScore",state->ScoreText,"",state->ScoreScale);}
    else for(auto& e:feed->Entries){rowScale=e.Scale;rowY=e.Y;rowAlpha=e.Alpha;std::string p=e.Compact?"s":"";if(e.KillerIsTitan){sprite(roles.at(p+"leftTitan"),1);sprite(roles.at(p+"spriteSkeleton"),1);}else sprite(roles.at(p+"spriteSword"),1);if(e.VictimIsTitan)sprite(roles.at(p+"rightTitan"),1);shiftX=e.KillerIsTitan?0:e.Compact?16.f:18.f;label(roles.at(p+"labelNameLeft"),e.Killer);shiftX=e.KillerIsTitan?(e.Compact?-16.f:-18.f):0;label(roles.at(p+"labelNameRight"),e.Victim);shiftX=0;if(e.Damage)label(roles.at(p+"labelScore"),std::to_string(e.Damage),e.Damage>1000?"FF0000":"");}
    std::stable_sort(ordered.begin(),ordered.end(),[](auto& a,auto& b){return a.first<b.first;});float scale=height/std::clamp(height,320.f,1536.f);std::vector<DamageHudQuad> result;for(auto& [_,q]:ordered){q.X=cx+(q.X-cx)*scale;q.Y=cy+(q.Y-cy)*scale;q.Width*=scale;q.Height*=scale;result.push_back(q);}return result;
}
}
