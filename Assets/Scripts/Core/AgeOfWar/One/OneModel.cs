using System;
namespace XanaduYonder.Core.AgeOfWar.One
{
    internal sealed class UnitDefinition
    {
        internal readonly int Id, Cost, Training;
        internal readonly string Name;
        internal readonly double Health, Damage, RangedDamage, MeleeRange, ShootRange, Left, Top, Right, Bottom;
        internal UnitDefinition(int id,string name,int cost,int training,double health,double damage,double ranged,double meleeRange,double shootRange,double left,double top,double right,double bottom)
        { Id=id;Name=name;Cost=cost;Training=training;Health=health;Damage=damage;RangedDamage=ranged;MeleeRange=meleeRange;ShootRange=shootRange;Left=left;Top=top;Right=right;Bottom=bottom; }
    }
    internal sealed class TurretDefinition
    {
        internal readonly int Id, Cost, Interval, Bullet, Damage, Range, Timeline;
        internal readonly string Name;
        internal TurretDefinition(int id,string name,int cost,int interval,int bullet,int damage,int range,int timeline)
        {Id=id;Name=name;Cost=cost;Interval=interval;Bullet=bullet;Damage=damage;Range=range;Timeline=timeline;}
    }
    internal sealed class TimelineEvent
    {
        internal readonly int Frame, A, B;
        internal readonly string Kind;
        internal TimelineEvent(int frame,string kind,int a,int b) {Frame=frame;Kind=kind;A=a;B=b;}
    }
    internal sealed class Timeline
    {
        internal readonly int Frames;
        internal readonly TimelineEvent[] Events;
        internal Timeline(int frames,TimelineEvent[] events) {Frames=frames;Events=events;}
    }
    // Explicit stream makes replays deterministic. Flash Math.random has no portable seed contract.
    internal sealed class OneRandom
    {
        private uint _state;
        internal uint State { get { return _state; } }
        internal OneRandom(uint seed) { _state=seed==0?0x6D2B79F5u:seed; }
        internal double Next() {uint x=_state;x^=x<<13;x^=x>>17;x^=x<<5;_state=x;return x/4294967296.0;}
        internal int Next(int maximum) {return (int)(Next()*maximum);}
    }
}
