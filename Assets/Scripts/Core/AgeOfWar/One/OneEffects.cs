using System;
using System.Collections.Generic;
namespace XanaduYonder.Core.AgeOfWar.One
{
    public sealed partial class AgeOfWarOneBattle
    {
        private sealed class Projectile
        {
            internal long Uid,BornTick;
            internal string Id;
            internal int Side,Age,NativeAge,Counter,Timer=40,Flame;
            internal double X,Y,Xs,Ys,Gravity,GravityFactor=.05,Rotation,Spin,Damage,Scale=1;
            internal bool Removed,Initialized;
        }
        private sealed class Particle
        {
            internal long Uid,BornTick;
            internal int Id,Side,Age,NativeAge,Variant,Flame;
            internal double X,Y,Xs,Ys,Gravity,GravityFactor,Spin,Rotation,Damage,Scale=1,Alpha=1;
            internal bool Removed,Initialized;
        }
        private sealed class Special
        {
            internal int Age,Counter,Timer,Frame=1;
            internal long Uid;
            internal bool Initialized;
            internal double X,Y;
        }
        private readonly List<Projectile> _projectiles=new List<Projectile>();
        private readonly List<Particle> _particles=new List<Particle>();
        private Special _special;
        private void StartSpecial()
        {
            _special=new Special{Uid=_nextVisual++,Age=_age};
            Event("special",0,0,_age);
        }
        private void InitializeSpecial()
        {
            var special=_special;if(special==null||special.Initialized)return;
            special.Timer=special.Age==3?300:200;
            special.X=special.Age==4?-100:special.Age==5?150:0;
            special.Y=special.Age==4?200:special.Age==5?425:0;
            special.Initialized=true;
            if(special.Age==3)_healing=true;
            if(special.Age==4)Event("sound",special.X,special.Y,0,"sound/972");
        }
        private void TickSpecial()
        {
            var special=_special;if(special==null)return;
            ++special.Counter;--special.Timer;
            switch(special.Age)
            {
                case 1: case 2:
                    if(special.Counter==(special.Age==1?9:5)){CreateProjectile(special.X,special.Y,0,200,"special"+special.Age,1);special.Counter=0;}
                    int range=special.Age==1?10:4;
                    Event("camera-shake",_random.Next(range)-range/2,_random.Next(range)-range/2);
                    if(special.Timer==0){Event("camera-shake",0,0);_special=null;}break;
                case 3:if(special.Timer==0){_healing=false;_special=null;}break;
                case 4:
                    if(special.Counter==15&&special.X<700){CreateProjectile(special.X,special.Y,0,400,"special4",1);special.Counter=0;}
                    special.X+=4;Event("camera-shake",_random.Next(3)-1,_random.Next(3)-1);
                    if(special.X>1000){Event("camera-shake",0,0);_special=null;}break;
                case 5:
                    if(special.Counter==5&&special.X<700){CreateProjectile(special.X+30,special.Y-5,0,1000,"special5",1);special.X+=50;special.Counter=0;}
                    Event("camera-shake",_random.Next(3)-1,_random.Next(3)-1);
                    if(special.Frame>=56){Event("camera-shake",0,0);_special=null;}break;
            }
            ++special.Frame;
            if(special.Age==5&&special.Frame>=2&&special.Frame<=52&&(special.Frame-2)%5==0)Event("sound",special.X,special.Y,0,"sound/680");
        }
        private Projectile CreateProjectile(double x,double y,double rotation,double damage,string id,int side)
        {
            var p=new Projectile{Uid=_nextVisual++,BornTick=_tick,Id=id,Side=side,X=DisplayPosition(x),Y=DisplayPosition(y),Rotation=DisplayRotation(rotation),Damage=damage};
            _projectiles.Add(p);return p;
        }
        private void InitializeProjectile(Projectile p)
        {
            string id=p.Id;int side=p.Side;double rotation=p.Rotation;
            int n;int.TryParse(id,out n);
            if(id=="special1"||id=="special2")
            {
                p.Rotation=80+_random.Next(20);p.Xs=10*Math.Cos(p.Rotation*0.017453292519943295);p.Ys=10*Math.Sin(p.Rotation*0.017453292519943295);
                p.X=_random.Next(500)+200;p.Y=-100;p.Scale=(_random.Next(70)+120)/100.0;
            }
            else if(id=="special4"||id=="special5")
            {p.Rotation=0;p.Xs=4;p.Ys=1;p.GravityFactor=.2;p.Spin=_random.Next(10)-5;}
            else if(n==5){p.X=DisplayPosition(p.X+(side==1?116:-116));p.Y=DisplayPosition(p.Y+7);}
            else
            {
                double speed=n>=9?.2:7;p.Xs=speed*Math.Cos(rotation*0.017453292519943295);p.Ys=speed*Math.Sin(rotation*0.017453292519943295);
                if(n==3||n==4)p.Y=DisplayPosition(p.Y+(side==1?-1:1)*p.Xs*5);
                else {double offset=n==2?4:n==9?200:n==10||n==11?42:n==12?100:5;p.X=DisplayPosition(p.X+p.Xs*offset);p.Y=DisplayPosition(p.Y+p.Ys*offset);}
                p.GravityFactor=n>=6?.02:.05;p.Spin=n==2?_random.Next(6)-3:n==4?6:n==1||n==3?2:0;
            }
            if(id=="special1"||id=="special2")p.Flame=NewFlame();
            p.Initialized=true;
        }
        private void TickProjectiles()
        {
            foreach(var projectile in _projectiles){if(projectile.BornTick>=_tick)continue;if(!projectile.Initialized)InitializeProjectile(projectile);++projectile.NativeAge;if(projectile.Flame>0)projectile.Flame=NextFlame(projectile.Flame);}
            if(_paused)return;
            int count=_projectiles.Count;
            for(int i=count-1;i>=0;--i)
            {
                var p=_projectiles[i];if(!p.Initialized)continue;++p.Age;int n;int.TryParse(p.Id,out n);
                if(n==5)
                {
                    --p.Timer;
                    if(++p.Counter==2){CreateParticle(p.X,p.Y,8,p.Side,p.Damage);p.Counter=0;}
                    if(p.Timer<=0)p.Removed=true;
                    continue;
                }
                int substeps=n>=9?8:n>=6?4:1;
                for(int step=0;step<substeps;++step)
                {
                    // Ruffle traces: a removed AVM1 clip no longer supplies collision coordinates to later substeps.
                    if(n>=9)
                    {
                        if(n==9||p.Xs<15){p.X=DisplayPosition(p.X+p.Xs);p.Y=DisplayPosition(p.Y+p.Ys);p.Xs*=1.01;p.Ys*=1.01;}
                    }
                    else if(p.Id=="special4"||p.Id=="special5")
                    {if(p.Gravity<15)p.Gravity+=.2;p.Xs/=1.02;p.X=DisplayPosition(p.X+p.Xs);p.Y=DisplayPosition(p.Y+(p.Ys+p.Gravity));p.Rotation=DisplayRotation(p.Rotation+p.Spin);}
                    else
                    {p.Gravity+=p.GravityFactor;p.X=DisplayPosition(p.X+p.Xs);p.Y=DisplayPosition(p.Y+(p.Ys+p.Gravity));p.Rotation=DisplayRotation(p.Rotation+p.Spin);}
                    if(p.Id=="special1")CreateParticle(p.X,p.Y,4,0,p.Rotation);
                    if(p.Y>425){Impact(p,null,true);p.Removed=true;break;}
                    foreach(var actor in Roster())
                    {
                        if(actor.Side==p.Side||!Contains(actor,p.X,p.Y))continue;
                        actor.Health-=p.Damage;Impact(p,actor,false);p.Removed=true;break;
                    }
                    if(p.Id=="special5")p.Removed=true;
                    if(p.Removed)break;
                }
            }
            _projectiles.RemoveAll(p=>p.Removed);
        }
        private static bool Contains(Actor actor,double x,double y)
        {return x>=actor.X+actor.Left&&x<=actor.X+actor.Right&&y>=actor.Y+actor.Top&&y<=actor.Y+actor.Bottom;}
        private void Impact(Projectile p,Actor target,bool ground)
        {
            int n;int.TryParse(p.Id,out n);
            if(n==1||n==3||p.Id=="special1")
            {int amount=ground?7:4;for(int i=0;i<amount;++i)CreateParticle(p.X,p.Y-(ground&&p.Id=="special1"?10:0),3,0,0);}
            else if(n==4)
            {for(int i=0;i<3;++i)CreateParticle(p.X,p.Y,5,p.Side,p.Damage);if(ground)CreateParticle(p.X,p.Y,6,p.Side,p.Damage);CreateParticle(p.X,p.Y,7,p.Side,p.Damage);}
            else if(n==6){for(int i=0;i<5;++i)CreateParticle(p.X,p.Y,10,0,0);}
            else if(n==7){for(int i=0;i<(ground?4:3);++i)CreateParticle(p.X,p.Y,11,p.Side,p.Damage);CreateParticle(p.X,p.Y,7,p.Side,p.Damage);}
            else if(n==9)CreateParticle(p.X,p.Y,7,0,0);
            else if(p.Id=="special4")CreateParticle(p.X,p.Y,12,0,0);
            if(target!=null&&n!=2)Blood(target,5);
            if(target!=null)Event("projectile-hit",p.X,p.Y,n);
        }
        private void Blood(Actor actor,int count)
        {for(int i=0;i<count;++i)CreateParticle(actor.X,actor.Y-actor.Height/1.5,2,0,0);}
        private Particle CreateParticle(double x,double y,int id,int side,double parameter)
        {
            var p=new Particle{Uid=_nextVisual++,BornTick=_tick,Id=id,Side=side,X=DisplayPosition(x),Y=DisplayPosition(y),Damage=parameter};
            _particles.Add(p);return p;
        }
        private void InitializeParticle(Particle p)
        {
            int id=p.Id;double parameter=p.Damage;
            switch(id)
            {
                case 1:p.Ys=3;break;
                case 2:
                    p.GravityFactor=.2;p.Xs=_random.Next()*2-1;p.Gravity=_random.Next()*1.5-1;p.X=DisplayPosition(p.X+(_random.Next()*6-3));p.Y=DisplayPosition(p.Y+(_random.Next()*6-3));p.Spin=_random.Next(4)-1.5;
                    break;
                case 3:case 9:case 10:
                    p.GravityFactor=.15;p.Scale=(_random.Next(40)+100)/100.0;p.Xs=_random.Next()*6-3;p.Gravity=-_random.Next()*3+1;p.X=DisplayPosition(p.X+(_random.Next()*9-4));p.Y=DisplayPosition(p.Y+(_random.Next()*9-4));p.Spin=_random.Next(4)-1.5;
                    break;
                case 4:
                    p.Scale=(_random.Next(20)+100)/100.0;p.Rotation=DisplayRotation(parameter);p.X=DisplayPosition(p.X+(_random.Next()*9-4));p.Y=DisplayPosition(p.Y+(_random.Next()*9-4));p.Spin=_random.Next(4)-1.5;break;
                case 5:case 11:
                    p.Rotation=-90+_random.Next(60)-30;p.Xs=7*Math.Cos(p.Rotation*0.017453292519943295);p.Ys=7*Math.Sin(p.Rotation*0.017453292519943295)+_random.Next();p.GravityFactor=.2;p.Spin=_random.Next(20)-10;p.Damage=id==5?10:30;break;
                case 6:p.X=DisplayPosition(p.X+(_random.Next()*6-3));p.Y=DisplayPosition(p.Y+(_random.Next()*6-3));break;
                case 8:
                    p.GravityFactor=.2;p.Gravity=1;p.X=DisplayPosition(p.X+(_random.Next()*2-1));p.Y=DisplayPosition(p.Y+(_random.Next()*2-1));p.Spin=_random.Next(4)-1.5;
                    // part8 never initializes xs. AVM1 writes NaN to _x are ignored by the display object.
                    break;
            }
            if(id==2||id==8)p.Variant=_random.Next(12);
            if(id==7||id==12)Event("sound",p.X,p.Y,0,"sound/591");
            if(id==6)p.Flame=NewFlame();
            p.Initialized=true;
        }
        private void TickParticles()
        {
            int count=_particles.Count;
            for(int i=count-1;i>=0;--i)
            {
                var p=_particles[i];if(p.BornTick>=_tick)continue;if(!p.Initialized)InitializeParticle(p);++p.NativeAge;if(p.Flame>0)p.Flame=NextFlame(p.Flame);
                // These two clips remove themselves on frame 28 even while PAUSED.
                if(p.Id==7||p.Id==12){if(++p.Age>=27)p.Removed=true;continue;}
                if(_paused)continue;
                ++p.Age;
                switch(p.Id)
                {
                    case 1:p.Y=DisplayPosition(p.Y-p.Ys);p.Ys/=1.1;if(p.Ys<=.2)p.Alpha=DisplayAlphaPercent(p.Alpha*100-2);if(p.Alpha<=0)p.Removed=true;break;
                    case 4:p.Y=DisplayPosition(p.Y-1);p.Scale-=.04;p.Alpha=DisplayAlphaPercent(p.Alpha*100-4);if(p.Y>425||p.Alpha<=0||p.Scale<=0)p.Removed=true;break;
                    case 6:p.Alpha=DisplayAlphaPercent(p.Alpha*100-1);if(p.Alpha<=0)p.Removed=true;break;
                    case 5:case 11:
                        p.Gravity+=p.GravityFactor;p.X=DisplayPosition(p.X+p.Xs);p.Y=DisplayPosition(p.Y+(p.Ys+p.Gravity));p.Rotation=p.Id==11?0:DisplayRotation(p.Rotation+p.Spin);
                        if(p.Ys+p.Gravity>0)
                        {
                            if(p.Y>425){CreateParticle(p.X,p.Y,7,0,0);CreateParticle(p.X,p.Y,6,0,0);p.Removed=true;}
                            foreach(var actor in Roster())if(actor.Side!=p.Side&&Contains(actor,p.X,p.Y))
                            {actor.Health-=p.Damage;CreateParticle(p.X,p.Y,7,0,0);Blood(actor,5);p.Removed=true;}
                        }break;
                    default:
                        p.Gravity+=p.GravityFactor;p.Xs/=1.1;p.X=DisplayPosition(p.X+p.Xs);p.Y=DisplayPosition(p.Y+p.Gravity);p.Rotation=DisplayRotation(p.Rotation+p.Spin);
                        if(p.Id==8)foreach(var actor in Roster())if(actor.Side!=p.Side&&Contains(actor,p.X,p.Y))
                        {actor.Health-=p.Damage;for(int j=0;j<3;++j)CreateParticle(p.X,p.Y,9,0,0);Blood(actor,2);p.Removed=true;}
                        if(p.Y>425)p.Removed=true;break;
                }
            }
            _particles.RemoveAll(p=>p.Removed);
        }
        private void AddEffectsVisuals(List<AgeOfWarVisual> visuals)
        {
            foreach(var p in _projectiles)visuals.Add(new AgeOfWarVisual{InstanceId=p.Uid,Clip="projectile/"+p.Id,X=p.X,Y=p.Y,Frame=p.Id=="10"||p.Id=="11"||p.Id=="12"?p.NativeAge%2:p.Age,Rotation=p.Rotation,ScaleX=p.Scale,ScaleY=p.Scale,Depth=50});
            foreach(var p in _particles)visuals.Add(new AgeOfWarVisual{InstanceId=p.Uid,Clip="part/"+p.Id,X=p.X,Y=p.Y,Frame=p.Id==2||p.Id==8?p.Variant:p.Id==1?p.NativeAge%40:p.Age,Rotation=p.Rotation,ScaleX=p.Scale,ScaleY=p.Scale,Alpha=p.Alpha,Text=p.Id==1?"+ "+Round(p.Damage):"",TextScene=p.Id==1?"one-reward":"",TextField=p.Id==1?"cash":"",Depth=10000000});
            if(_special!=null)visuals.Add(new AgeOfWarVisual{InstanceId=_special.Uid,Clip="special/"+_special.Age,Frame=_special.Age==4?(_special.Frame-1)%3:_special.Frame-1,X=_special.X,Y=_special.Y,Depth=99999999});
        }
    }
}
