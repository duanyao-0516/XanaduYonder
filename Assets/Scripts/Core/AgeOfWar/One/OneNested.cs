using System.Collections.Generic;
namespace XanaduYonder.Core.AgeOfWar.One
{
    public sealed partial class AgeOfWarOneBattle
    {
        // DefineSprite589: gotoAndPlay(random(25)); frame31 gotoAndPlay(3).
        private int NewFlame(){int frame=_random.Next(25);return frame<1?1:frame;}
        private static int NextFlame(int frame){return frame>=30?3:frame+1;}
        private void AdvanceUnitFlames(Actor unit)
        {
            if(unit.Animation!="die")return;
            if(unit.Id==12)
            {
                unit.Flame1=unit.Flame1==0?NewFlame():NextFlame(unit.Flame1);
                unit.Flame2=unit.Flame2==0?NewFlame():NextFlame(unit.Flame2);
            }
            else if(unit.Id==15&&unit.AnimationFrame>=4)unit.Flame1=unit.Flame1==0?NewFlame():NextFlame(unit.Flame1);
        }
        private static AgeOfWarVisual Flame(long uid,string clip,int frame,double x,double y,double scaleX,double scaleY,double rotation,double alpha,int depth)
        {return new AgeOfWarVisual{InstanceId=uid,Clip=clip,Frame=frame-1,X=x,Y=y,ScaleX=scaleX,ScaleY=scaleY,Rotation=rotation,Alpha=alpha,Depth=depth};}
        private void AddNestedVisuals(List<AgeOfWarVisual> visuals)
        {
            foreach(var unit in _units)
            {
                if(unit.Flame1==0)continue;
                int depth=100000+(int)unit.Uid*10;
                if(unit.Id==12)
                {
                    visuals.Add(Flame(-2000000-unit.Uid*2,"nested/607/1",unit.Flame1,unit.X,unit.Y,unit.Side==1?1:-1,1,0,1,depth-1));
                    visuals.Add(Flame(-2000001-unit.Uid*2,"nested/607/3",unit.Flame2,unit.X,unit.Y,unit.Side==1?1:-1,1,0,1,depth+1));
                }
                else if(unit.Id==15)
                {
                    double[] alpha={0,21,43,64,85,107,128,149,171,192,213,235,256};
                    double opacity=unit.AnimationFrame>=16?1:alpha[unit.AnimationFrame-4]/256.0;
                    visuals.Add(Flame(-2000000-unit.Uid*2,"nested/685/12",unit.Flame1,unit.X,unit.Y,unit.Side==1?1:-1,1,0,opacity,depth+1));
                }
            }
            foreach(var p in _projectiles)if(p.Flame>0)visuals.Add(Flame(-3000000-p.Uid,"nested/"+(p.Id=="special1"?"896/2":"894/3"),p.Flame,p.X,p.Y,p.Scale,p.Scale,p.Rotation,1,50));
            foreach(var p in _particles)if(p.Flame>0)visuals.Add(Flame(-4000000-p.Uid,"nested/933/2",p.Flame,p.X,p.Y,p.Scale,p.Scale,p.Rotation,p.Alpha,10000000));
        }
    }
}
