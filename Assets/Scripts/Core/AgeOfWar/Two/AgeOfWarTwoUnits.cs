using System;
using System.Linq;
using System.Xml.Linq;
namespace XanaduYonder.Core.AgeOfWar.Two
{
    public sealed partial class AgeOfWarTwoBattle
    {
        internal Unit NextUnit(double x,int multi,int side,bool enemiesOnly,double limit)
        {
            Unit found=null;
            foreach(var u in Units) { double distance=(u.X-x)*multi; if(distance>0&&distance<limit&&u.Health>0&&(!enemiesOnly||u.Side!=side)) { found=u; limit=distance; } }
            return found;
        }
        internal sealed class Unit
        {
            readonly AgeOfWarTwoBattle game;
            internal readonly int TypeId,Side,Multi,Pop,Reward,DamageMin,DamageMax;
            internal readonly double Width,Height,MaxHealth,OriginalSpeed,Range,Scale;
            internal readonly string Type,ProjectileName;
            readonly string meleeSound,rangedSound,deathSound;
            readonly bool shootProjectile;
            internal readonly long InstanceId;
            internal readonly Animation Animation;
            private double x;
            internal double X {get{return x;}set{x=DisplayPosition(value);}}
            private double healthAlpha;
            internal double HealthAlpha {get{return healthAlpha;}set{healthAlpha=DisplayAlpha(value);}}
            internal double Health,Speed,DisplayHealthFraction=1,Wait=.2;
            internal bool ShowHealth;
            internal bool Alive=true,Dead,Stopped,OnBaseMelee,OnBaseRanged,OnUnitMelee,OnUnitRange,OnMelee,CanWalk;
            string state="idle";
            internal Unit(AgeOfWarTwoBattle battle,int id,int side)
            {
                game=battle; TypeId=id; Side=side; InstanceId=game.Id(); Multi=side==1?1:-1;
                XElement d=game.Definition("units",id); Type=S(d,"unitType"); Pop=I(d,"pop");
                Reward=Round(I(d,"cost")*(1+game.Bonus("Income",side)));
                OriginalSpeed=Speed=N(d,"speed")/3.2; Width=N(d,"sizeW"); Height=N(d,"sizeH");
                double armor=Type=="Infantry"||Type=="Anti-Armor"||Type=="Armored"?game.Bonus(Type+" Armor",side):0;
                Health=Round(I(d,"health")*(1+armor)); if(side==2) Health=Round(game.Difficulty*Health); MaxHealth=Health;
                Range=Round(N(d,"range")*(1+(Type=="Support"?game.Bonus("Support Range",side):0)));
                DamageMin=I(d,"damageMin"); DamageMax=I(d,"damageMax"); Scale=N(d,"scale"); shootProjectile=I(d,"shootProjectile")==1; ProjectileName=S(d,"projectileName");
                meleeSound=S(d,"hitMeleeSound"); rangedSound=S(d,"hitRangedSound"); deathSound=S(d,"deathSound");
                X=side==1?-580:580; Animation=new Animation("u"+id); Animation.Play("idle"); game.Animations.Add(Animation);
            }
            internal void Render(double dt)
            {
                if(Health>0)
                {
                    if(Wait>0) Wait-=dt;
                    DisplayHealthFraction=Health/MaxHealth;
                    if(ShowHealth) { if(HealthAlpha<1) HealthAlpha+=dt*2; else HealthAlpha=1; }
                    else if(HealthAlpha>0) HealthAlpha-=dt*2; else HealthAlpha=0;
                    if(OnUnitMelee) HealthAlpha=1;
                    Unit next=game.NextUnit(X,Multi,Side,false,1200);
                    CanWalk=false; OnBaseMelee=OnBaseRanged=OnUnitMelee=OnUnitRange=false;
                    if(Side==1&&X+Width>580||Side==2&&X-Width< -580) OnBaseMelee=true; else CanWalk=true;
                    if(Side==1&&X+Width+Range>580||Side==2&&X-Width-Range< -580) OnBaseRanged=true;
                    if(next==null) { if(Speed!=OriginalSpeed) Speed=OriginalSpeed; }
                    else
                    {
                        double distance=Math.Abs((next.X-X)*Multi);
                        if(distance<Width+next.Width)
                        {
                            if(Side!=next.Side) { OnUnitMelee=true; CanWalk=false; }
                            else if(next.Stopped) CanWalk=false;
                            else { if(next.Speed<Speed) Speed=next.Speed; Wait=.2; CanWalk=false; }
                        }
                        else CanWalk=true;
                        if(Range>0) { Unit enemy=game.NextUnit(X,Multi,Side,true,1200); if(enemy!=null&&Math.Abs((enemy.X-X)*Multi)<Range) OnUnitRange=true; }
                    }
                    if(OnMelee) CanWalk=false;
                    if(CanWalk&&Wait<=0) X+=Speed*dt*100*Multi;
                    Stopped=!CanWalk;
                }
                else if(!Dead)
                {
                    Dead=true; HealthAlpha=0;
                    if(Side==1) { game.Population-=Pop; game.Experience+=Round(Reward/2.0); }
                    else { game.Gold+=Reward; game.Experience+=Reward; game.EnemyPopulation-=Pop; }
                    game.Sound(deathSound,X); game.Emit("death",X,0,TypeId);
                }
                CheckAnimation();
            }
            void CheckAnimation()
            {
                string desired="idle"; bool walk=CanWalk;
                if(OnMelee) { walk=false; desired="attackMelee"; OnUnitRange=false; }
                if(Wait>0) walk=false;
                if(Health<=0) desired="die";
                else if(!walk&&OnUnitMelee) desired="attackMelee";
                else if(!walk&&OnBaseMelee) desired="attackMelee";
                else if(!walk&&OnUnitRange) desired="attackRanged";
                else if(!walk&&OnBaseRanged) desired="attackRanged";
                else if(walk&&OnBaseRanged||walk&&OnUnitRange) desired="walkAttack";
                else if(walk) desired="walk";
                if(state==desired) return;
                switch(desired)
                {
                    case "walk": Animation.Play("walkStart",false,()=>Animation.Play("walkCycle")); break;
                    case "attackMelee": OnMelee=false; PlayMelee(false); break;
                    case "attackRanged": OnMelee=false; PlayRanged(); break;
                    case "walkAttack": PlayWalkAttack(); break;
                    case "idle": Animation.Play("idle"); break;
                    case "die": Animation.Play("die",false,()=>Alive=false); break;
                }
                state=desired;
            }
            void PlayMelee(bool alternate)
            {
                string label=alternate?"attackMelee2":"attackMelee";
                Animation.Play(label,false,()=> { OnMelee=true; HitMelee(); Animation.Play(label+"Hit",false,()=> { OnMelee=false; CheckMelee(); }); });
            }
            void CheckMelee() { OnMelee=false; double n=game.Random(); PlayMelee(Animation.Has("attackMelee2")&&n>=.5); }
            void PlayRanged() { Animation.Play("attackRanged",false,()=> { HitRanged(); Animation.Play("attackRangedHit",false,PlayRanged); }); }
            void PlayWalkAttack()
            {
                Animation.Play("walkAttack",false,()=> { HitRanged(); Animation.Play("walkAttackHit1",false,()=> { if(Animation.Has("walkAttackHit2")) { HitRanged(); Animation.Play("walkAttackHit2",false,PlayWalkAttack); } else PlayWalkAttack(); }); });
            }
            void HitBase() { game.DamageBase(Side,game.Damage(Type,"base",DamageMin,DamageMax,Side)); }
            void HitMelee()
            {
                Unit enemy=game.NextUnit(X,Multi,Side,true,1200);
                if(!OnBaseMelee) { if(enemy!=null) { game.ApplyDamage(enemy,game.Damage(Type,enemy.Type,DamageMin,DamageMax,Side),Counter(Type,enemy.Type)); game.Sound(meleeSound,X); } }
                else { game.Sound(meleeSound,X); HitBase(); }
            }
            void HitRanged()
            {
                if(shootProjectile)
                {
                    Timeline.Point p;
                    if(!Animation.Timeline.Points.TryGetValue(Animation.AbsoluteFrame*2,out p)) throw new InvalidOperationException("Missing source bullet point for u"+TypeId+" frame "+Animation.AbsoluteFrame);
                    game.Projectiles.Add(new Projectile(game,ProjectileName,Side,X-p.X*Scale*Multi,p.Y*Scale,p.Rotation+180,p.Velocity,DamageMin,DamageMax,Type));
                    game.Sound(rangedSound,X);
                }
                else
                {
                    Unit enemy=game.NextUnit(X,Multi,Side,true,1200);
                    if(enemy!=null) game.ApplyDamage(enemy,game.Damage(Type,enemy.Type,DamageMin,DamageMax,Side),Counter(Type,enemy.Type)); else if(OnBaseRanged) HitBase();
                    game.Sound(rangedSound,X);
                }
            }
            internal AgeOfWarVisual Visual() { return new AgeOfWarVisual {InstanceId=InstanceId,Clip="unit/"+TypeId,Animation=Animation.Label,Frame=Animation.Frame,X=X,Y=0,ScaleX=-Multi*Scale,ScaleY=Scale,Depth=100,HealthFraction=DisplayHealthFraction,HealthAlpha=HealthAlpha,HealthY=-Height-7,HitWidth=Width*2,HitHeight=Height}; }
            internal void Destroy() { game.Animations.Remove(Animation); }
        }
        internal sealed class Turret
        {
            readonly AgeOfWarTwoBattle game;
            internal readonly int TypeId,Side,Spot,Multi;
            internal readonly long InstanceId;
            internal readonly Animation Animation;
            internal readonly double X,Y,Range,Scale;
            readonly int min,max;
            readonly string projectile,sounds;
            internal bool Alive=true;
            private double rotation;
            internal double Rotation {get{return rotation;}set{rotation=DisplayRotation(value);}}
            internal double SellFrame;
            string state;
            internal Turret(AgeOfWarTwoBattle battle,int id,int side,int spot)
            {
                game=battle; TypeId=id; Side=side; Spot=spot; Multi=side==1?1:-1; InstanceId=game.Id();
                X=side==1?-624:620; Y=-146-(spot-1)*47;
                XElement d=game.Definition("turrets",id); Range=Round(N(d,"range")*(1+game.Bonus("Turret Range",side)));
                Scale=N(d,"scale"); min=I(d,"damageMin"); max=I(d,"damageMax"); projectile=S(d,"projectileName"); sounds=S(d,"shootSound");
                Animation=new Animation("t"+id); Animation.Play("idle"); game.Animations.Add(Animation); Rotation=side==1?0:180;
            }
            internal void Render()
            {
                Unit next=game.NextUnit(X,Multi,Side,true,2300);
                bool inRange=next!=null&&Math.Abs((next.X-X)*Multi)<Range;
                if(inRange) Rotation=Math.Atan2(-next.Height/2-Y,next.X-X)*180/Math.PI;
                string desired=inRange?"shoot":"idle";
                if(state!=desired)
                {
                    if(desired=="shoot") Animation.Play("shoot",false,()=> { Hit(); Animation.Play("shootLaunch",false,()=>state="idle"); });
                    else Animation.Play("idle");
                    state=desired;
                }
            }
            void Hit()
            {
                for(int point=0;point<2;point++)
                {
                    Timeline.Point p; if(!Animation.Timeline.Points.TryGetValue(Animation.AbsoluteFrame*2+point,out p)) { if(point==0) throw new InvalidOperationException("Missing turret bullet point"); continue; }
                    double radians=Rotation*Math.PI/180, px=p.X*Animation.Timeline.CannonScaleX,py=p.Y*Animation.Timeline.CannonScaleY*Multi;
                    double x=X+Scale*(Animation.Timeline.CannonX+Math.Cos(radians)*px-Math.Sin(radians)*py);
                    double y=Y+Scale*(Animation.Timeline.CannonY+Math.Sin(radians)*px+Math.Cos(radians)*py);
                    game.Projectiles.Add(new Projectile(game,projectile,Side,x,y,p.Rotation-Rotation*Multi+(Side==2?180:0),p.Velocity,min,max,"Turret"));
                }
                game.Sound(sounds,X);
            }
            // GameScreen adds the whole turrets manager after units. Equal depths retain each turret's base/cannon insertion order.
            internal AgeOfWarVisual BaseVisual() { return new AgeOfWarVisual {InstanceId=-1000-InstanceId,Clip="turret/"+TypeId,Animation="base",X=X,Y=Y,ScaleX=Scale*Multi,ScaleY=Scale,Depth=150}; }
            internal AgeOfWarVisual Visual() { return new AgeOfWarVisual {InstanceId=InstanceId,Clip="turret/"+TypeId,Animation=Animation.Label,Frame=Animation.Frame,X=X,Y=Y,ScaleX=Scale,ScaleY=Scale*Multi,Rotation=Rotation,Depth=150}; }
        }
    }
}
