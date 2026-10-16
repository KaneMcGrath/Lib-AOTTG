// SPDX-License-Identifier: MIT
using System.Globalization;
using LibAottg;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Directory.CreateDirectory(args[0]);
using var commands = new StreamWriter(Path.Combine(args[0],"commands.txt"));
using var expected = new StreamWriter(Path.Combine(args[0],"csharp.tsv"));
int row = 0;
void Write(params object[] values) => commands.WriteLine(string.Join(" ",values.Select(v=>v is float f?f.ToString("R"):v)));
var presets=HeroStats.RcModPresets();
foreach(var skill in new[]{"mikasa","levi","armin","marco","jean","eren","petra","sasha"})
foreach(float scale in new[]{0.001f,0.1f,1f,10f,1000f})
foreach(int equip in new[]{0,1})
foreach(int route in new[]{0,1,2}) {
    var cfg=presets.Values.Single(s=>s.SkillId==skill).ToConfig();
    cfg.WorldScale=scale;cfg.DoubleTapDash=route!=1;cfg.NoStopJump=route==2;
    cfg.AutoWallJump=route==1;cfg.NoForcedToRoof=route==2;
    var world=new TestWorld {Scale=scale};
    var hero=new HeroController(cfg,world);var motor=hero.Motor;
    var camera=new HeroCamera(new HeroCameraConfig {Mode=(HeroCameraMode)route});hero.Camera=camera;
    var rig=new TestRig();hero.Presentation=rig;rig.Sample(motor,hero.Pose,default,0);
    var ropeL=new HeroRope();var ropeR=new HeroRope();var smoke=new HeroSmokeTrail(seed:77);
    motor.SetEquipment((EquipmentMode)equip);hero.Respawn(new Vec3(0,5,0)*scale);
    int flags=(cfg.DoubleTapDash?1:0)|(cfg.NoStopJump?2:0)|(cfg.AutoWallJump?4:0)|(cfg.NoForcedToRoof?8:0);
    hero.SoundRequested+=cue=>world.Sounds.Add((int)cue.Sound);
    Write("reset",skill,scale,equip,route,flags);
    HeroButtons previous=0;
    for(int n=0;n<900;n++) {
        int phase=n%300;
        bool ground=phase<45||phase>=250;
        world.Contacts=new CharacterContacts {Grounded=ground,GroundNormal=Vec3.Up,FrontWall=phase>=205&&phase<225,UpperFrontClear=phase>=218};
        world.Target=new Vec3(0,6,20);world.Anchor=new Vec3((n%17)*0.02f,6,20);world.Exists=phase<195;
        world.Events.Clear();world.Hits.Clear();world.Sounds.Clear();
        Write("begin");
        HeroButtons held=0;
        if(phase>=10&&phase<40||phase>=70&&phase<150)held|=HeroButtons.Jump;
        if(phase>=75&&phase<185)held|=HeroButtons.HookLeft;
        if(phase>=100&&phase<180)held|=HeroButtons.HookRight;
        if(phase>=135&&phase<170)held|=HeroButtons.HookBoth;
        if(phase>=47&&phase<62||phase>=240&&phase<260)held|=HeroButtons.Attack;
        if(phase>=190&&phase<204)held|=HeroButtons.Skill;
        if(phase==230)held|=HeroButtons.Reload;
        if(phase==280)held|=HeroButtons.Supply;
        if(phase==285)held|=HeroButtons.Flare;
        if(phase==62||phase==222)held|=HeroButtons.Dash;
        if(phase==30)held|=HeroButtons.Dodge;
        float x=phase>=155&&phase<190?-1:phase>=190&&phase<205?1:0;
        float y=phase<230?1:0;
        float reel=phase%3==0?-1:phase%3==1?1:0;
        float dt=n%4==0?0.013f:n%4==1?0.027f:n%4==2?0.02f:0.033f;
        var raw=new HeroInputFrame {Held=held,Pressed=held&~previous,Released=previous&~held,MoveX=x,MoveY=y,Reel=reel,
            HasView=true,ViewForward=new Vec3(0,0,1),AimOrigin=motor.Position+Vec3.Up*scale,AimDirection=(world.Target*scale-motor.Position).Normalized,
            HasPointer=true,PointerX=0.35f,PointerY=0.45f,AspectRatio=16f/9,MouseDeltaX=n%3-1,MouseDeltaY=n%5-2,OrbitHeld=phase<200};
        previous=held;
        int mask=(ground?1:0)|(world.Contacts.FrontWall?2:0)|(world.Contacts.UpperFrontClear?4:0);
        // Explicit state corrections are host teleports, not oracle feedback: identical inputs in both languages.
        if(n%300==0) {Write("pose",0,5*scale,0,0,0,10*scale,0);motor.SetPose(new Vec3(0,5,0)*scale,new Vec3(0,0,10)*scale,0);hero.Synchronize();}
        if(n%300==205) {Write("pose",0,3*scale,0,0,0,10*scale,0);motor.SetPose(new Vec3(0,3,0)*scale,new Vec3(0,0,10)*scale,0);hero.Synchronize();}
        if(n==710){Write("grab");hero.Grab();}
        if(n==720){Write("escape",0,5*scale,0);hero.EscapeGrab(new Vec3(0,5,0)*scale);}
        if(n==860){Write("die");hero.Die();}
        if(n==870){Write("respawn",0,5*scale,0);hero.Respawn(new Vec3(0,5,0)*scale);}
        Write("tick",dt,route,mask,(int)held,(int)raw.Pressed,(int)raw.Released,x,y,reel,
            raw.AimOrigin.X,raw.AimOrigin.Y,raw.AimOrigin.Z,raw.AimDirection.X,raw.AimDirection.Y,raw.AimDirection.Z,
            raw.MouseDeltaX,raw.MouseDeltaY,world.Anchor.X,world.Anchor.Y,world.Anchor.Z,world.Exists?1:0);
        if(route==0) {
            var i=new HeroInputMapper().Sample(raw,raw.ViewForward,raw.AimOrigin,raw.AimDirection);i.SourceAttackSelection=true;
            motor.Step(dt,i,world.Contacts,world);rig.Sample(motor,HeroBodyPose.Evaluate(motor),i,dt);
        } else if(route==1) {
            var i=hero.Input.Sample(raw,raw.ViewForward,raw.AimOrigin,raw.AimDirection);hero.Advance(dt,i);
        } else hero.Advance(dt,raw);
        var pose=HeroBodyPose.Evaluate(motor);
        ropeL.Sample(motor,HookSide.Left,rig.HookOrigin(motor,HookSide.Left),n*0.02f);
        ropeR.Sample(motor,HookSide.Right,rig.HookOrigin(motor,HookSide.Right),n*0.02f);
        smoke.Advance(dt,motor,pose);
        object[] fields={row++,skill,scale,equip,route,n,motor.Position.X/scale,motor.Position.Y/scale,motor.Position.Z/scale,
            motor.Velocity.X/scale,motor.Velocity.Y/scale,motor.Velocity.Z/scale,motor.Gas,motor.Blade,motor.SpareBladePairs,
            (int)motor.State,motor.Grounded?1:0,motor.FacingDegrees,motor.FacingTargetDegrees,motor.AttackHitboxActive?1:0,
            motor.Animation.Clip,motor.Animation.Time,motor.Animation.Speed,motor.Animation.Revision,motor.SkillCooldownRemaining,motor.FlareCooldownRemaining,
            (int)motor.LeftHookPhase,(int)motor.RightHookPhase,motor.LeftHookAnchor.X/scale,motor.LeftHookAnchor.Y/scale,motor.LeftHookAnchor.Z/scale,
            motor.RightHookAnchor.X/scale,motor.RightHookAnchor.Y/scale,motor.RightHookAnchor.Z/scale,
            motor.LeftGunLoaded?1:0,motor.RightGunLoaded?1:0,motor.LeftGunReserve,motor.RightGunReserve,
            pose.Pitch,pose.Roll,pose.Leaning?1:0,pose.LeanLeft?1:0,
            camera.Pose.Position.X/scale,camera.Pose.Position.Y/scale,camera.Pose.Position.Z/scale,camera.Pose.Yaw,camera.Pose.Pitch,camera.Pose.Roll,
            ropeL.Points.Count,ropeR.Points.Count,smoke.Particles.Count,
            smoke.Particles.Count>0?smoke.Particles[0].Age:0,smoke.Particles.Count>0?smoke.Particles[0].Size/scale:0,
            smoke.Particles.Count>0?smoke.Particles[0].Position.X/scale:0,
            string.Join(',',world.Events.Select(e=>(int)e)),string.Join(',',world.Hits),string.Join(',',world.Sounds),hero.PhysicsTicks};
        expected.WriteLine(string.Join('\t',fields.Select(v=>v is float f?f.ToString("R"):v)));
    }
}
Console.WriteLine($"Generated {row} C# oracle frames.");

sealed class TestWorld:HeroWorld {
    public float Scale;public Vec3 Target=new(0,6,20),Anchor=new(0,6,20);public bool Exists=true;
    public CharacterContacts Contacts;public List<CharacterEvent> Events=new();public List<int> Hits=new();public List<int> Sounds=new();
    public override CharacterContacts GetContacts(CharacterMotor m,HeroPose p)=>Contacts;
    public override bool TryHook(Vec3 o,Vec3 d,float distance,HookSide s,out HookHit h){h=new HookHit(Target*Scale);return true;}
    public override bool SweepHook(Vec3 a,Vec3 b,HookSide s,out HookHit h){
        Vec3 target=Target*Scale,d=b-a;float t=d.LengthSquared>0?Vec3.Dot(target-a,d)/d.LengthSquared:-1;
        h=new HookHit(target){TargetId=7,LocalPoint=new(1,2,3)};
        return t>=0&&t<=1&&(a+d*t-target).Length<0.001f*Scale;
    }
    public override bool ResolveHookAnchor(int id,Vec3 local,out Vec3 p){p=Anchor*Scale;return Exists;}
    public override void OnCharacterEvent(CharacterEvent e)=>Events.Add(e);
    public override void CollectWeaponHits(HeroWeaponSweep s,IList<HeroHit> hits){hits.Add(new HeroHit{TargetId=1,Point=s.End});hits.Add(new HeroHit{TargetId=1,Point=s.End});hits.Add(new HeroHit{TargetId=2,Point=s.Start});}
    public override void OnHeroHit(HeroHit hit)=>Hits.Add(hit.TargetId);
}
sealed class TestRig:IHeroPresentation {
    CharacterMotor? m;HeroPose p;
    public void Sample(CharacterMotor motor,HeroPose pose,CharacterInput input,float dt){m=motor;p=pose;}
    public Vec3 Head=>p.Position+Vec3.Up*(1.5f*(m?.Scale??1));
    public Vec3 HookOrigin(CharacterMotor motor,HookSide side)=>motor.Position+Vec3.Up*(0.85f*motor.Scale);
    public bool WeaponSegment(HookSide side,bool gun,out Vec3 start,out Vec3 end){start=p.Position+p.Rotate(new((side==HookSide.Left?-0.4f:0.4f)*m!.Scale,m.Scale,0));end=start+p.Forward*m.Scale;return true;}
}
