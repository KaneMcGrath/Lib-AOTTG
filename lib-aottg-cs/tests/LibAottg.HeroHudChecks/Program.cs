// SPDX-License-Identifier: MIT
using LibAottg;
using LibAottg.Assets;
using System.Globalization;
using System.Text;
int checks=0;
void Require(bool value,string name){checks++;if(!value)throw new Exception(name);}
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var package=HeroHudPackage.Load(Path.Combine(root,"assets/ui/aottg-hero-hud"));
var state=new HeroHud();
var motor=new CharacterMotor(new CharacterConfig{WorldScale=70});
motor.SetPose(Vec3.Zero,new Vec3(2100,0,2800),0);state.Read(motor);
Require(state.Speed==50,"Speed must use source units");
Require(state.GasFill==1 && state.BladeFill==1 && state.BladePairs==5,"Initial resources");
state.HasTarget=true;state.Distance=120;Require(state.InRange,"120 inclusive");state.Distance=120.01f;Require(!state.InRange,"Beyond 120");
state.Distance=1001;Require(state.AimText=="???\n50.0 u/s","Long distance text");state.Speedometer=HeroSpeedometer.Damage;Require(state.AimText=="???\n0.5K","Damage mode");
state.Speedometer=HeroSpeedometer.Off;state.ShowDistance=false;Require(state.AimText=="","Off mode");
var aimWorld=new AimWorld(7000);
var view=new HeroCameraPose{Position=new Vec3(0,140,-700),Forward=new Vec3(0,0,1),Up=Vec3.Up,FieldOfView=50};
state.Aim(motor,new CharacterInput{HasAimOrigin=true,AimOrigin=view.Position,AimDirection=view.Forward},aimWorld,view,16f/9);
Require(state.HasTarget && state.InRange && Math.Abs(state.Distance-100.02f)<.01f,"Source distance and range");
Require(state.LeftArrow.Visible && state.RightArrow.Visible && state.LeftArrow.X<.5f && state.RightArrow.X>.5f,"Projected left/right arrows");
aimWorld.Enabled=false;state.Aim(motor,new CharacterInput{HasAimOrigin=true,AimOrigin=view.Position,AimDirection=view.Forward},aimWorld,view,16f/9);
Require(!state.HasTarget && !state.LeftArrow.Visible && !state.RightArrow.Visible,"No-hit clears arrows");
state=new HeroHud{GasFill=.2f,BladeFill=0,BladePairs=1,SpecialFill=.125f};
var quads=package.Compose(state,1280,720);
Require(quads.Any(q=>q.Widget=="gasL" && q.R==1 && q.G==1 && q.B==0),"Gas warning");
Require(quads.Any(q=>q.Widget=="bladel1" && q.R==1 && q.G==0),"Empty blade warning");
Require(!quads.Any(q=>q.Widget=="bladel2"),"Reserve visibility");
Require(!quads.Any(q=>q.Widget=="bladeCL"),"Empty durability");
var fill=quads.Single(q=>q.Widget=="gasL1");Require(Math.Abs(fill.Height-5.6f)<.001f,"Bottom up gas fill");
Require(quads.Where(q=>q.Widget=="skill_cd_mikasa").All(q=>q.X>=639.4f && q.Y<698.5f),"Clockwise radial begins at top");
state.Visible=false;Require(package.Compose(state,1280,720).Count==0,"Death hiding");
var output=new StringBuilder();
for(int i=0;i<30;i++){
    state=new HeroHud{GasFill=(i%5)/4f,BladeFill=(i%4)/3f,SpecialFill=(i%9)/8f,BladePairs=i%6,
        Guns=i>=15,LeftLoaded=i%2==0,RightLoaded=i%3==0,LeftReserve=i%8,RightReserve=(i+2)%8,
        HasTarget=i%2==0,Distance=i%3==0?120:1001,Speed=i%2==0?75.25f:75.35f,Skill=new[]{"mikasa","levi","armin","petra","sasha","jean","marco","eren"}[i%8],
        CrosshairStyle=(HeroCrosshairStyle)(i%5),Speedometer=(HeroSpeedometer)(i%3)};
    state.ResourcesVisible=i!=28;state.AimVisible=i!=29;
    state.LeftArrow=new HeroHudArrow{Visible=true,InRange=i%2==0,X=.4f,Y=.45f,Rotation=.3f};
    state.RightArrow=new HeroHudArrow{Visible=true,InRange=i%2!=0,X=.6f,Y=.45f,Rotation=-.3f};
    float width=i%2==0?1280:1920,height=i%2==0?720:1080;
    quads=package.Compose(state,width,height);
    Require(quads.All(q=>float.IsFinite(q.X) && q.Width>0 && q.Height>0),"Finite geometry "+i);
    Require(quads.Any(q=>q.Widget=="Cursor"+state.CrosshairStyle)==state.AimVisible,"Original cursor style "+i);
    output.AppendLine(quads.Count.ToString(CultureInfo.InvariantCulture));
    foreach(var q in quads)output.AppendLine(q.Texture+" "+string.Join(" ",new[]{q.X,q.Y,q.Width,q.Height,q.SourceX,q.SourceY,q.SourceWidth,q.SourceHeight,q.R,q.G,q.B,q.A,q.Rotation}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture))));
}
if(args.Length>0)File.WriteAllText(args[0],output.ToString());
Console.WriteLine($"HERO HUD checks passed: {checks}; 30 native comparison cases.");

sealed class AimWorld(float plane):ICharacterWorld {
    public bool Enabled=true;
    public bool TryHook(Vec3 origin,Vec3 direction,float distance,HookSide side,out HookHit hit){
        float t=(plane-origin.Z)/direction.Z;
        hit=new HookHit(origin+direction*t);return Enabled && t>0 && t<=distance;
    }
    public void OnCharacterEvent(CharacterEvent kind){}
}
