// SPDX-License-Identifier: MIT
using LibAottg;
using System.Text.Json;
using Stride.Graphics;
namespace StrideAottg;
public sealed partial class SandboxController
{
    private bool heroHudSmoke;
    private readonly Dictionary<string,bool> heroHudChecks=new();
    private void UpdateHeroHudSmoke()
    {
        DebugText.Visible=false;
        var buffer=GraphicsDevice.Presenter.BackBuffer;
        int tick=frames-1,scenario=tick/45,phase=tick%45;
        if(scenario<8){
            var s=heroHudView.State;
            s.Visible=true;s.Guns=scenario>=5;s.GasFill=scenario==1?.2f:scenario==2?0:1;
            s.BladeFill=scenario==2?0:scenario==1?.2f:1;s.BladePairs=scenario==2?0:scenario==1?2:5;
            s.SpecialFill=scenario/7f;s.Skill=scenario==3?"levi":scenario==4?"armin":"mikasa";
            s.LeftLoaded=scenario!=6;s.RightLoaded=scenario!=7;s.LeftReserve=7;s.RightReserve=4;
            s.HasTarget=true;s.Distance=scenario%2==0?100:1001;s.Speed=75.25f;
            s.CrosshairStyle=(HeroCrosshairStyle)(scenario%5);s.Speedometer=(HeroSpeedometer)(scenario%3);
            s.LeftArrow=new HeroHudArrow{Visible=true,InRange=true,X=.46f,Y=.48f,Rotation=.4f};
            s.RightArrow=new HeroHudArrow{Visible=true,InRange=false,X=.54f,Y=.48f,Rotation=-.4f};
            heroHudView.Render(true,buffer.Width,buffer.Height);
            if(phase==35){
                heroHudChecks["scenario"+scenario]=heroHudView.VisibleQuads>5;
                string capture=Path.Combine(Path.GetDirectoryName(reportPath),"hero-hud-"+scenario+".png");
                using(var output=File.Create(capture)) buffer.Save(Game.GraphicsContext.CommandList,output,ImageFileType.Png);
                using var stream=File.OpenRead(capture);
                using var decoded=Image.Load(stream);
                var pixels=decoded.PixelBuffer[0].GetPixels<Stride.Core.Mathematics.Color>();
                bool bgra=decoded.Description.Format==PixelFormat.B8G8R8A8_UNorm || decoded.Description.Format==PixelFormat.B8G8R8A8_UNorm_SRgb;
                bool Contains(float nx,float ny,Func<int,int,int,bool> test) {
                    int cx=(int)(nx*buffer.Width),cy=(int)(ny*buffer.Height);
                    for(int y=Math.Max(0,cy-15);y<Math.Min(buffer.Height,cy+15);y++)
                        for(int x=Math.Max(0,cx-15);x<Math.Min(buffer.Width,cx+15);x++) {
                            var p=pixels[y*buffer.Width+x];if(test(bgra?p.B:p.R,p.G,bgra?p.R:p.B))return true;
                        }
                    return false;
                }
                heroHudChecks["renderedMirroredArrow"+scenario]=Contains(.46f,.48f,(r,g,b)=>r>220 && g>220 && b>220);
                heroHudChecks["renderedRedArrow"+scenario]=Contains(.54f,.48f,(r,g,b)=>r>180 && g<90 && b<90);
                if(scenario==1)heroHudChecks["renderedGasWarning"]=Contains(.5f-23f/buffer.Width,1f-21f/buffer.Height,(r,g,b)=>r>180 && g>180 && b<90);
                if(scenario==2)heroHudChecks["renderedEmptyGas"]=Contains(.5f-23f/buffer.Width,1f-21f/buffer.Height,(r,g,b)=>r>180 && g<90 && b<90);
            }
            return;
        }
        var hud=heroHudView.State;hud.Visible=false;heroHudView.Render(true,buffer.Width,buffer.Height);
        heroHudChecks["deadHidden"]=heroHudView.VisibleQuads==0;
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{passed=heroHudChecks.Values.All(x=>x),checks=heroHudChecks,width=buffer.Width,height=buffer.Height,frames},new JsonSerializerOptions{WriteIndented=true}));
        ((Stride.Engine.Game)Game).Exit();
    }
}
