using System;
using System.Linq;
using System.Xml.Linq;
namespace XanaduYonder.Core.AgeOfWar.Two
{
    public sealed partial class AgeOfWarTwoBattle
    {
        internal Unit Collision(double x,double y,int side,double widthFactor)
        {
            Unit result=null;
            // Original collision deliberately returns the last matching unit in insertion order.
            foreach(var u in Units) if(Math.Abs(u.X-x)<u.Width*widthFactor&&Math.Abs(y)<u.Height&&u.Health>0&&u.Side!=side) result=u;
            return result;
        }
        internal sealed partial class Projectile
        {
            readonly AgeOfWarTwoBattle game;
            internal readonly string Name,Type;
            readonly string sounds;
            internal readonly int Side,Multi,Min,Max;
            internal readonly long InstanceId;
            readonly long nativeStart;
            private double x,y;
            internal double X {get{return x;}set{x=DisplayPosition(value);}}
            internal double Y {get{return y;}set{y=DisplayPosition(value);}}
            private double rotation;
            internal double Rotation {get{return rotation;}set{rotation=DisplayRotation(value);}}
            internal double XSpeed,YSpeed,RotationSpeed,Mass,Scale,ExplosionScale;
            internal bool Alive=true,Remove,Follow;
            internal Projectile(AgeOfWarTwoBattle battle,string name,int side,double x,double y,double angle,double velocity,int min,int max,string type)
            {
                game=battle; Name=name; Side=side; Multi=side==1?-1:1; X=x; Y=y; Min=min; Max=max; Type=type; InstanceId=game.Id(); nativeStart=game.NativeTick;
                XElement d=game.Data.Element("projectiles").Elements().First(v=>S(v,"name")==name);
                Mass=N(d,"mass"); Scale=N(d,"size"); sounds=S(d,"hitSound"); ExplosionScale=N(d,"explosion");
                string rotation=S(d,"rotSpeed");
                if(rotation=="random") RotationSpeed=(1-game.Random()*2)*10;
                else if(rotation=="follow") { Follow=true; Rotation=angle*Multi; }
                else RotationSpeed=N(d,"rotSpeed");
                XSpeed=Math.Cos(angle*Math.PI/180)*velocity*3; YSpeed=Math.Sin(angle*Math.PI/180)*velocity*3;
            }
            internal void Render(double dt)
            {
                X+=XSpeed*dt*100*Multi; Y+=YSpeed*dt*100;
                if(Follow) Rotation=Math.Atan2(YSpeed*Multi,XSpeed)*180/Math.PI; else Rotation+=RotationSpeed*dt*100;
                YSpeed+=Mass*dt/4;
                Unit target=game.Collision(X,Y,Side,.6);
                if(target!=null) { game.ApplyDamage(target,game.Damage(Type,target.Type,Min,Max,Side),Counter(Type,target.Type)); target.HealthAlpha=2; Impact(); }
                else if(Side==1&&X>580||Side==2&&X< -580) { int damage=game.Damage(Type,"base",Min,Max,Side); Impact(); game.DamageBase(Side,damage); }
                else if(Y>0) Impact();
            }
            void Impact() { Alive=false; game.Sound(sounds,X); if(ExplosionScale>0) game.CreateCosmetic("explosion",X,Y,ExplosionScale,0); }
            internal AgeOfWarVisual Visual() { return new AgeOfWarVisual {InstanceId=InstanceId,Clip="projectile/"+Name,Animation="idle",Frame=(game.NativeTick-nativeStart)%(Name=="p4"?9:Name=="tp5"?8:1),X=X,Y=Y,Rotation=Rotation,ScaleX=Scale*Multi,ScaleY=Scale*Multi,Depth=200}; }
        }
        internal void CreateSpecial(int side,int age) { Effects.Add(new Effect(this,age,side)); Emit("special",0,0,age); }
        internal Effect CreateStrike(int age,int side,double x,double y) { var e=new Effect(this,age,side,x,y); Effects.Add(e); return e; }
        internal void CreateCosmetic(string clip,double x,double y,double scale,double rotation) { Effects.Add(new Effect(this,clip,x,y,scale,rotation)); }
        internal sealed partial class Effect : IAnimationOwner
        {
            readonly AgeOfWarTwoBattle game;
            internal readonly long InstanceId;
            internal readonly int Age,Side,Multi;
            internal bool Alive=true,Removed,Emitter,Ground;
            private double x,y;
            internal double X {get{return x;}set{x=DisplayPosition(value);}}
            internal double Y {get{return y;}set{y=DisplayPosition(value);}}
            private double rotation,alpha=1;
            internal double Rotation {get{return rotation;}set{rotation=DisplayRotation(value);}}
            internal double Alpha {get{return alpha;}set{alpha=DisplayAlpha(value);}}
            internal double Scale=1,Timer,Interval,IntervalTimer,TrailTimer,XSpeed,YSpeed;
            internal int Frame;
            internal string Clip;
            internal bool DamageText,Critical;
            internal int Amount;
            internal Animation Animation;
            internal Effect(AgeOfWarTwoBattle battle,int age,int side)
            {
                game=battle; InstanceId=game.Id(); Age=age; Side=side; Multi=side==1?1:-1; Emitter=true; Timer=10;
                Interval=age==2||age==3?.3:age==4?.05:age==7?.1:.2;
                if(age>=6) { X=-1080*Multi; Y=age==6?-250:0; Timer=double.NaN; if(age==6) { Clip="special/6_plane"; Emitter=false; } }
            }
            internal Effect(AgeOfWarTwoBattle battle,int age,int side,double x,double y)
            {
                game=battle; InstanceId=game.Id(); Age=age; Side=side; Multi=side==1?1:-1; X=x; Y=y;
                Clip=age==6?"special/6_bomb":"special/"+age;
                if(age==2||age==7)
                {
                    Frame=Round(game.Random()*7); Frame=age==2?Math.Min(Frame,6):0; Timer=.5;
                    int sound=Round(game.Random()*2); game.Emit("sound",X,0,0,sound==0?"lightning":"lightning"+(sound+1));
                    Unit target=game.Collision(X,Y,Side,1); if(target!=null) { game.ApplyDamage(target,age==2?500:1500); target.HealthAlpha=2; }
                }
                else
                {
                    double angle=age==6?90:80+game.Random()*20;
                    double velocity=age==5?3:age==6?1:1.4;
                    XSpeed=Math.Cos(angle*Math.PI/180)*velocity*3; YSpeed=Math.Sin(angle*Math.PI/180)*velocity*3;
                    // Meteor's display does not follow its trajectory; FireBall/Arrow/CanonBall do.
                    Rotation=age==1||age==6?0:angle; Interval=angle;
                    if(age==3) { Animation=new Animation("special3",this); Animation.Play("anim"); game.Animations.Add(Animation); }
                }
            }
            internal Effect(AgeOfWarTwoBattle battle,double x,double y,int amount,bool critical)
            {
                game=battle; InstanceId=game.Id(); X=x; Y=y; Amount=amount; Critical=critical; DamageText=true; Timer=2;
            }
            internal Effect(AgeOfWarTwoBattle battle,string clip,double x,double y,double scale,double rotation)
            {
                game=battle; InstanceId=game.Id(); Clip=clip; X=x; Y=y; Scale=scale; Rotation=rotation;
                Animation=new Animation(clip,this); Animation.Play("anim",false,CallbackForLabel("anim")); game.Animations.Add(Animation);
                if(clip=="explosion") game.Emit("sound",X,Y,0,"explosion6");
            }
            public Action CallbackForLabel(string label) { return Age==0&&!DamageText&&label=="anim"?(Action)(()=>Alive=false):null; }
            internal void Render(double dt)
            {
                if(DamageText) { Timer-=dt; Y-=Timer*dt*20; Alpha=Timer/2; if(Timer<=0) Alive=false; return; }
                if(Age==0) return;
                if(Emitter||Clip=="special/6_plane")
                {
                    IntervalTimer+=dt;
                    if(Age>=6) X+=dt*(Age==7?450:300)*Multi; else Timer-=dt;
                    if(IntervalTimer>=Interval)
                    {
                        if(Age<=5) game.CreateStrike(Age,Side,game.Random()*1160-580,Age==2?0:-400);
                        else if(X> -580&&X<580) game.CreateStrike(Age,Side,X,Y);
                        IntervalTimer=0;
                    }
                    if(Timer<=0||Age>=6&&(Side==1?X>1080:X< -1080)) Alive=false;
                    return;
                }
                if(Age==2||Age==7) { Timer-=dt; Alpha=Timer/.5; if(Timer<=0) Alive=false; return; }
                if(Ground) { Timer+=dt; if(Timer>=4) Alive=false; return; }
                TrailTimer+=dt;
                if(TrailTimer>=.05)
                {
                    TrailTimer=0;
                    if(Age==1||Age==3) game.CreateCosmetic("firetrail",X,Y,.5,Interval+245+game.Random()*50);
                }
                if(Age==6) Rotation+=dt*100*Multi;
                X+=XSpeed*dt*100; Y+=YSpeed*dt*100;
                Unit target=game.Collision(X,Y,Side,1);
                if(target!=null)
                {
                    game.ApplyDamage(target,Age==1?200:Age==5?1000:Age==6?600:800); target.HealthAlpha=2;
                    if(Age!=4) game.CreateCosmetic("explosion",X,Y,1,0);
                    Alive=false;
                }
                else if(Y>=0) { if(Age==4) Ground=true; else { game.CreateCosmetic("explosion",X,Y,1,0); Alive=false; } }
            }
            internal void Destroy() { if(Animation!=null) game.Animations.Remove(Animation); Removed=true; }
            internal AgeOfWarVisual Visual() { return new AgeOfWarVisual {InstanceId=InstanceId,Text=DamageText?Amount.ToString(System.Globalization.CultureInfo.InvariantCulture):"",TextScene=DamageText?(Critical?"damage-critical":"damage-normal"):"",TextField=DamageText?"damageTXT":"",Clip=DamageText?"":Age==0?"part/"+Clip:Clip,Animation=Animation==null?"idle":Animation.Label,Frame=Animation==null?Frame:Animation.Frame,X=X,Y=Y,Rotation=Rotation,ScaleX=Scale*(Age==6?Multi:1),ScaleY=Scale,Alpha=Alpha,Depth=300}; }
        }
    }
}
