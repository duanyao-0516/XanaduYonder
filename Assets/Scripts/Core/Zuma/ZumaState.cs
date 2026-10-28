using System;

namespace XanaduYonder.Core.Zuma
{
    // Import-time coordinates are already in the original 1280 x 720 reference canvas.
    // Points are NOT resampled: N original DAT deltas produce N+1 vertices.
    [Serializable] public sealed class ZumaPoint { public float X; public float Y; }
    [Serializable] public sealed class ZumaPathPoint
    {
        public float X; public float Y; public bool Hidden; public bool DrawPriority;
    }
    [Serializable] public sealed class ZumaLevelDefinition
    {
        public string Id; public string DisplayName;
        public float FrogX; public float FrogY;
        public ZumaPathPoint[] Points;
        public ZumaPoint[] Coins;
        public int PathLength { get { return Points == null ? 0 : Points.Length - 1; } }
        public ZumaLevelDefinition Copy()
        {
            var copy = (ZumaLevelDefinition)MemberwiseClone();
            if (Points != null) { copy.Points = new ZumaPathPoint[Points.Length]; for (int i=0;i<Points.Length;i++) if(Points[i]!=null) copy.Points[i]=new ZumaPathPoint{X=Points[i].X,Y=Points[i].Y,Hidden=Points[i].Hidden,DrawPriority=Points[i].DrawPriority}; }
            if (Coins != null) { copy.Coins = new ZumaPoint[Coins.Length]; for(int i=0;i<Coins.Length;i++) if(Coins[i]!=null) copy.Coins[i]=new ZumaPoint{X=Coins[i].X,Y=Coins[i].Y}; }
            return copy;
        }
    }
    public sealed class ZumaDifficulty
    {
        public readonly string Id;
        public readonly int ColorCount, StartCount, GaugeScore, RepeatChance, SingleChance, AceTimeSeconds;
        public readonly float Speed, SlowFactor;
        private ZumaDifficulty(string id,int colors,int start,int gauge,int repeat,int ace,float speed,float slow)
        { Id=id;ColorCount=colors;StartCount=start;GaugeScore=gauge;RepeatChance=repeat;SingleChance=6;AceTimeSeconds=ace;Speed=speed;SlowFactor=slow; }
        public static ZumaDifficulty Get(string id)
        {
            switch(id)
            {
                case "rabbit": return new ZumaDifficulty(id,4,40,3000,40,70,.8f,4);
                case "eagle": return new ZumaDifficulty(id,5,50,5000,40,100,.95f,3.5f);
                case "jaguar": return new ZumaDifficulty(id,6,60,5000,25,120,.75f,1);
                case "sun_god": return new ZumaDifficulty(id,6,60,5000,40,150,1.25f,1);
                default: throw new ArgumentException("Unknown Zuma difficulty.",nameof(id));
            }
        }
    }
    [Serializable] public sealed class ZumaBallSnapshot
    {
        public float PathPosition, X, Y, Angle, Speed;
        public int Color, Combo, ExplosionTicks, FadeTicks;
        public bool Inserted, Exploding, IsSingle, GoBack, Hidden, DrawPriority;
        public ZumaBallSnapshot Copy() { return (ZumaBallSnapshot)MemberwiseClone(); }
    }
    [Serializable] public sealed class ZumaBulletSnapshot
    {
        public float X, Y, DirectionX, DirectionY;
        public int Color, Slot;
        public ZumaBulletSnapshot Copy() { return (ZumaBulletSnapshot)MemberwiseClone(); }
    }
    [Serializable] public sealed class ZumaFrogSnapshot
    {
        public int CurrentColor, NextColor, RecoilTicks;
        public float DirectionX, DirectionY=1;
        public ZumaFrogSnapshot Copy() { return (ZumaFrogSnapshot)MemberwiseClone(); }
    }
    [Serializable] public sealed class ZumaCoinSnapshot
    {
        public bool Active, Blinking;
        public int PositionIndex, TimerTicks, FadeTicks;
        public ZumaCoinSnapshot Copy() { return (ZumaCoinSnapshot)MemberwiseClone(); }
    }
    [Serializable] public sealed class ZumaTrailParticleSnapshot
    {
        public float X, Y;
        public int AgeTicks;
        public ZumaTrailParticleSnapshot Copy() { return (ZumaTrailParticleSnapshot)MemberwiseClone(); }
    }
    [Serializable] public sealed class ZumaSnapshot
    {
        public int RulesVersion=1, ContentVersion=1;
        public string SessionId, MapId, DifficultyId, Phase="intro", RandomAlgorithm="xorshift32-v1";
        public uint RandomState;
        public long Score, TotalTicks, PlayTicks, Revision;
        public int AceTimeSeconds, ChainBonus, MaxChainBonus, MaxCombo, TotalCombos, TotalCoins, GlowTicks;
        public bool Generating=true, InitialRoll=true, EndReached, IntroInitialized, OutroInitialized;
        public bool ResultCommitted, ResultAcknowledged;
        public float ChainSpeed, LastClearedPosition, IntroPosition, IntroPreviousPosition, OutroPosition, OutroPreviousPosition;
        public int CapacityRejections;
        public ZumaFrogSnapshot Frog=new ZumaFrogSnapshot();
        public ZumaCoinSnapshot Coin=new ZumaCoinSnapshot();
        public ZumaBallSnapshot[] Balls=new ZumaBallSnapshot[0];
        public ZumaBulletSnapshot[] Bullets=new ZumaBulletSnapshot[0];
        public ZumaTrailParticleSnapshot[] TrailParticles=new ZumaTrailParticleSnapshot[0];
        public ZumaSnapshot Copy()
        {
            var copy=(ZumaSnapshot)MemberwiseClone();
            copy.Frog=Frog==null?null:Frog.Copy();copy.Coin=Coin==null?null:Coin.Copy();
            if(Balls!=null){copy.Balls=new ZumaBallSnapshot[Balls.Length];for(int i=0;i<Balls.Length;i++)copy.Balls[i]=Balls[i]==null?null:Balls[i].Copy();}
            if(Bullets!=null){copy.Bullets=new ZumaBulletSnapshot[Bullets.Length];for(int i=0;i<Bullets.Length;i++)copy.Bullets[i]=Bullets[i]==null?null:Bullets[i].Copy();}
            if(TrailParticles!=null){copy.TrailParticles=new ZumaTrailParticleSnapshot[TrailParticles.Length];for(int i=0;i<TrailParticles.Length;i++)copy.TrailParticles[i]=TrailParticles[i]==null?null:TrailParticles[i].Copy();}
            return copy;
        }
        public bool TryValidate(out string reason) { return ZumaGame.Validate(null,this,out reason); }
        public bool TryValidate(ZumaLevelDefinition level,out string reason) { return ZumaGame.Validate(level,this,out reason); }
    }
    // Ephemeral presentation events are deliberately excluded from persistence; their causal results are in Snapshot.
    public sealed class ZumaEvent
    {
        public string Kind; public float X,Y; public long Score; public int Color,Combo;
    }
}
