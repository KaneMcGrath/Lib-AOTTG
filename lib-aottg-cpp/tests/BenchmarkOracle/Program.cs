// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Text.Json;
using LibAottg;
var results=new Dictionary<string,object>();
double Run(bool full,int count){
    var world=new HeroWorld();var hero=new HeroController(world:world);var m=hero.Motor;
    hero.Camera=new HeroCamera();hero.Respawn(Vec3.Zero,true);
    var input=new CharacterInput{MoveY=1,LookDirection=new Vec3(0,0,1)};
    var raw=new HeroInputFrame{MoveY=1,Held=HeroButtons.Jump,MouseDeltaX=0.1f,AspectRatio=16f/9};
    var smoke=new HeroSmokeTrail();var rope=new HeroRope();
    var start=Stopwatch.GetTimestamp();
    for(int i=0;i<count;i++){
        if(full){hero.Advance(0.016f,raw);smoke.Advance(0.016f,m,hero.RenderPose);rope.Sample(m,HookSide.Left,m.Position,i*0.016f);}
        else m.Step(0.02f,input,new CharacterContacts{Grounded=true},world);
    }
    double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    results[full?"runtimeChecksum":"motorChecksum"]=m.Position.Z+m.Gas+smoke.Particles.Count;
    return ms;
}
// Warm both workloads before measuring; suppress all I/O inside the timed region.
Run(false,50000);Run(true,10000);
foreach(bool full in new[]{false,true}){
    int count=full?20000:200000;var times=Enumerable.Range(0,7).Select(_=>Run(full,count)).ToArray();
    results[full?"runtime":"motor"]=new{iterations=count,elapsedMilliseconds=times,medianMilliseconds=times.Order().ElementAt(3)};
}
results["runtimeVersion"]=Environment.Version.ToString();results["scope"]="Single-thread simulation only; no Skyrim, rendering, input device, host physics, marshalling or startup";
File.WriteAllText(args[0],JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(results));
