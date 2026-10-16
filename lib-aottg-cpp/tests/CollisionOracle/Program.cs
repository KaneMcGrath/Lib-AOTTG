// SPDX-License-Identifier: MIT
using System.Globalization;
using LibAottg;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
using var file=new StreamWriter(args[0]);
uint random=177;
float Next(float min,float max){random=unchecked(random*1664525u+1013904223u);return min+(max-min)*((random>>8)/16777216f);}
int count=0;
foreach(float scale in new[]{0.001f,0.1f,1f,10f,1000f}) {
    var world=new KinematicCollisionWorld(worldScale:scale);
    world.Boxes.Add(new WorldBox(new Vec3(-4,0,4)*scale,new Vec3(4,8,5)*scale));
    world.Boxes.Add(new WorldBox(new Vec3(6,0,-5)*scale,new Vec3(8,4,5)*scale));
    world.Boxes.Add(new WorldBox(new Vec3(-3,5,-3)*scale,new Vec3(3,6,3)*scale));
    for(int n=0;n<2000;n++) {
        var start=new Vec3(Next(-10,10),Next(-2,12),Next(-10,10))*scale;
        var v=new Vec3(Next(-80,80),Next(-60,60),Next(-80,80))*scale;
        var target=start+v*0.02f;
        world.Friction=n%2==0?0:0.6f;
        var result=world.Move(start,target,v);
        var contacts=world.Contacts(result.Position,new Vec3(0,0,1));
        var direction=new Vec3(Next(-1,1),Next(-1,1),Next(-1,1));
        bool hit=world.TryHook(start,direction,30*scale,HookSide.Left,out var h);
        object[] row={scale,start.X,start.Y,start.Z,target.X,target.Y,target.Z,v.X,v.Y,v.Z,world.Friction,
            direction.X,direction.Y,direction.Z,
            result.Position.X/scale,result.Position.Y/scale,result.Position.Z/scale,
            result.Velocity.X/scale,result.Velocity.Y/scale,result.Velocity.Z/scale,
            result.Collided?1:0,result.Grounded?1:0,contacts.Grounded?1:0,contacts.FrontWall?1:0,contacts.UpperFrontClear?1:0,
            hit?1:0,h.Point.X/scale,h.Point.Y/scale,h.Point.Z/scale};
        file.WriteLine(string.Join(' ',row.Select(v=>v is float f?f.ToString("R"):v)));count++;
    }
}
Console.WriteLine($"Generated {count} independent collision queries.");
