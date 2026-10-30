using System;
using System.Collections.Generic;
using System.Globalization;
namespace XanaduYonder.Core.AgeOfWar.Two
{
    internal sealed class Timeline
    {
        internal sealed class Section { internal string Name; internal int Start, Length; }
        internal struct Point { internal double X,Y,Rotation,Velocity; }
        internal readonly Dictionary<string,Section> Labels = new Dictionary<string,Section>();
        internal readonly Dictionary<int,Point> Points = new Dictionary<int,Point>();
        internal readonly double CannonScaleX,CannonScaleY,CannonX,CannonY;
        internal Timeline(string labels,string points,double a,double b,double c,double d,double x,double y)
        {
            foreach(var s in labels.Split(';')) { var v=s.Split(','); Labels.Add(v[0],new Section{Name=v[0],Start=int.Parse(v[1]),Length=int.Parse(v[2])}); }
            foreach(var s in points.Split(';')) if(s.Length>0) { var v=s.Split(','); Points.Add(int.Parse(v[0])*2+int.Parse(v[1]),new Point{X=D(v[2]),Y=D(v[3]),Rotation=D(v[4]),Velocity=D(v[5])}); }
            CannonScaleX=Math.Sqrt(a*a+b*b); CannonScaleY=Math.Sqrt(c*c+d*d); CannonX=x; CannonY=y;
        }
        static double D(string s) { return double.Parse(s,CultureInfo.InvariantCulture); }
    }
    internal interface IAnimationOwner
    {
        Action CallbackForLabel(string label);
    }
    internal sealed partial class Animation
    {
        internal readonly Timeline Timeline;
        internal readonly string Key;
        internal IAnimationOwner Owner;
        internal string Label="";
        internal int Frame;
        internal double Current;
        internal int AbsoluteFrame { get { return section==null?0:section.Start+Frame; } }
        Timeline.Section section;
        Action callback;
        bool loop, stopped=true;
        internal Animation(string key,IAnimationOwner owner=null) { Key=key; Owner=owner; Timeline=SourceTimelines.All[key]; }
        internal bool Has(string label) { return Timeline.Labels.ContainsKey(label); }
        internal void Play(string label,bool looping=true,Action end=null)
        {
            Timeline.Section found; if(!Timeline.Labels.TryGetValue(label,out found)) return;
            section=found; Label=label; Frame=0; Current=0; loop=looping; callback=end; stopped=false;
        }
        internal void Render(double dt)
        {
            if(section==null) return;
            Current+=dt*30;
            if((int)Current==Frame) return;
            if(stopped||!loop&&(int)Current>=section.Length)
            {
                Frame=section.Length-1; Current=Frame;
                var end=callback; callback=null; stopped=true; if(end!=null) end();
            }
            else { Frame=(int)Current%section.Length; Current%=section.Length; }
        }
    }
}
