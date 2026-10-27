// Derived from Zuma-Deluxe-HD, Copyright (c) 2023 GalaxyShad, MIT.
// See THIRD_PARTY_NOTICES.md for the full license and fixed upstream revision.
using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Zuma
{
    /// <summary>
    /// Fixed 60 Hz port of Game.c, BallChain.c, Bullet.c and Frog.c at upstream 28001e0.
    /// Positions are DAT sample indices, while collision coordinates are reference-canvas pixels.
    /// The caller owns pause and wall-clock accumulation; Tick never reads a clock or Unity state.
    /// Rules v1 consumes accepted Aim/Fire/Swap commands at the last completed-step boundary;
    /// the next Tick advances the original chain, generation, bullet and coin phases in order.
    /// Draw/clock causality is explicit: 10 recoil ticks, 30 explosion ticks, 28 intro trail ticks,
    /// 34 outro trail ticks; the original strict 10/3-second coin boundaries become 601/181 ticks.
    /// </summary>
    public sealed class ZumaGame
    {
        public const int TickRate=60, MaxBalls=256, MaxBullets=32;
        // Result/profile time is an Int32 count of whole seconds. Keep every phase restorable
        // at the final representable second, while TotalTicks remains an independent Int64 clock.
        public const long MaximumPlayTicks=(long)int.MaxValue*TickRate+(TickRate-1);
        public const float BallSpacing=32, RoughSpacing=34;
        private readonly ZumaLevelDefinition level;
        private readonly ZumaDifficulty difficulty;
        private readonly List<ZumaEvent> events=new List<ZumaEvent>();
        private ZumaSnapshot state;

        public ZumaGame(ZumaLevelDefinition level,string difficultyId,uint seed,int aceTimeSeconds=0)
        {
            string error;
            if(!ValidateLevel(level,out error))throw new ArgumentException(error,nameof(level));
            if(aceTimeSeconds<0)throw new ArgumentOutOfRangeException(nameof(aceTimeSeconds));
            this.level=level.Copy();difficulty=ZumaDifficulty.Get(difficultyId);
            state=new ZumaSnapshot{SessionId=Guid.NewGuid().ToString("N"),MapId=level.Id,DifficultyId=difficultyId,
                RandomState=seed==0?0x6D2B79F5u:seed,ChainSpeed=difficulty.Speed,
                AceTimeSeconds=aceTimeSeconds==0?difficulty.AceTimeSeconds:aceTimeSeconds};
            state.Frog.NextColor=1;
            var first=NewBall(0,Random(0,difficulty.ColorCount-1));
            state.Balls=new[]{first};
        }
        private ZumaGame(ZumaLevelDefinition level,ZumaSnapshot snapshot)
        {
            this.level=level.Copy();difficulty=ZumaDifficulty.Get(snapshot.DifficultyId);state=snapshot.Copy();
            // Cached coordinates/flags are derived from the verified path, never trusted from disk.
            for(int i=0;i<state.Balls.Length;i++)Place(state.Balls[i]);
        }
        public static ZumaGame Restore(ZumaLevelDefinition level,ZumaSnapshot snapshot)
        {
            string error;
            if(!ValidateLevel(level,out error)||!Validate(level,snapshot,out error))throw new ArgumentException(error,nameof(snapshot));
            return new ZumaGame(level,snapshot);
        }
        public ZumaSnapshot Snapshot() { return state.Copy(); }
        public ZumaEvent[] DrainEvents() { var copy=events.ToArray();events.Clear();return copy; }
        public bool CanFire { get { return state.Phase=="play"&&!state.InitialRoll&&!state.EndReached&&state.Frog.RecoilTicks==0&&state.Balls.Length>0&&state.Balls.Length<MaxBalls&&state.Bullets.Length<MaxBullets; } }
        public bool Aim(float x,float y)
        {
            if(!Finite(x)||!Finite(y))return false;
            double dx=(double)x-level.FrogX,dy=(double)y-level.FrogY;
            double length=Math.Sqrt(dx*dx+dy*dy);if(length<0.00001)return false;
            state.Frog.DirectionX=(float)(dx/length);state.Frog.DirectionY=(float)(dy/length);BumpRevision();return true;
        }
        public bool TryFire()
        {
            if(!CanFire)
            {
                if(state.Phase=="play"&&(state.Balls.Length>=MaxBalls||state.Bullets.Length>=MaxBullets))CapacityRejected();
                return false;
            }
            var frog=state.Frog;
            int slot=0;while(slot<state.Bullets.Length&&state.Bullets[slot].Slot==slot)slot++;
            var bullet=new ZumaBulletSnapshot{X=level.FrogX+frog.DirectionX*35,Y=level.FrogY+frog.DirectionY*35,
                DirectionX=frog.DirectionX,DirectionY=frog.DirectionY,Color=frog.CurrentColor,Slot=slot};
            // C reuses the first inactive array slot. That order matters when several bullets hit
            // in one step, so preserve slots in the compact active snapshot instead of appending.
            var bullets=new ZumaBulletSnapshot[state.Bullets.Length+1];
            Array.Copy(state.Bullets,0,bullets,0,slot);Array.Copy(state.Bullets,slot,bullets,slot+1,state.Bullets.Length-slot);
            bullets[slot]=bullet;state.Bullets=bullets;
            frog.RecoilTicks=10;frog.CurrentColor=frog.NextColor;frog.NextColor=RandomChainColor();
            Emit("fire",bullet.X,bullet.Y,0,bullet.Color);BumpRevision();return true;
        }
        public bool Swap()
        {
            if(state.Phase!="play"||state.EndReached)return false;
            int old=state.Frog.CurrentColor;state.Frog.CurrentColor=state.Frog.NextColor;state.Frog.NextColor=old;
            Emit("swapped",level.FrogX,level.FrogY);BumpRevision();return true;
        }
        public bool MarkResultCommitted()
        {
            if((state.Phase!="won"&&state.Phase!="lost")||state.ResultCommitted)return false;
            state.ResultCommitted=true;BumpRevision();return true;
        }
        public bool AcknowledgeResult()
        {
            if(!state.ResultCommitted||state.ResultAcknowledged)return false;
            state.ResultAcknowledged=true;BumpRevision();return true;
        }
        public void Tick()
        {
            if(state.Phase=="won"||state.Phase=="lost")return;
            state.TotalTicks=Add(state.TotalTicks,1);BumpRevision();
            if(state.Frog.RecoilTicks>0)state.Frog.RecoilTicks--;
            if(state.GlowTicks>0)state.GlowTicks--;
            if(state.Coin.FadeTicks>0)state.Coin.FadeTicks--;
            if(state.Coin.TimerTicks<601)state.Coin.TimerTicks++;
            if(state.Phase=="intro"){UpdateIntro();return;}
            if(state.Phase=="winOutro"){UpdateOutro();return;}
            if(state.Phase=="play"&&state.PlayTicks<MaximumPlayTicks)state.PlayTicks++;

            UpdateChain();
            if(state.Balls.Length>0)UpdateBullets();
            if(state.Generating&&state.Score>difficulty.GaugeScore)
            {state.Generating=false;state.GlowTicks=31;Emit("threshold",level.FrogX,level.FrogY);}
            GenerateChain();
            if(state.EndReached)
            {
                state.Generating=false;
                if(state.Phase!="lossDrain"){state.Phase="lossDrain";Emit("loss",level.FrogX,level.FrogY);}
                if(state.Balls.Length==0){Finish("lost");return;}
            }
            else if(state.Balls.Length==0&&state.Score>difficulty.GaugeScore)
            {
                state.Phase="winOutro";state.Generating=false;state.Bullets=new ZumaBulletSnapshot[0];
                state.OutroPosition=state.LastClearedPosition;state.OutroPreviousPosition=state.LastClearedPosition;
                state.TrailParticles=new ZumaTrailParticleSnapshot[0];Emit("win",level.FrogX,level.FrogY);return;
            }
            if(!state.EndReached)CorrectFrogColors();
            UpdateChainSpeed();
            CollideBullets();
            UpdateCoin();
        }

        private void UpdateChain()
        {
            // Pass one transfers momentum from tail to front, including gaps and matching-end reversal.
            for(int i=state.Balls.Length-1;i>=0;i--)
            {
                var ball=state.Balls[i];
                if(i==state.Balls.Length-1)
                {ball.Speed=state.ChainSpeed==32?32:Math.Min(ball.Speed+.05f,state.ChainSpeed);continue;}
                var back=state.Balls[i+1];
                if(CollidesBack(i,RoughSpacing))
                {
                    if(ball.GoBack)
                    {
                        // The C loop uses the updated i speed again for each subsequent ball.
                        // Preserve that momentum sharing (including the /2.5 factor) deliberately.
                        for(int j=i;j<state.Balls.Length;j++)
                        {state.Balls[j].Speed=state.Balls[i].Speed/2.5f;if(!CollidesBack(j,RoughSpacing))break;}
                        ball.Speed=0;
                        if(!ball.Exploding&&!state.EndReached)
                        {
                            int start,end;FindSubChain(i,out start,out end);
                            if(end-start+1>=3)ResolveCombo(start,end);
                            else back.Combo=0;
                        }
                        ball.GoBack=false;
                    }
                    else ball.Speed=back.Speed;
                }
                else if(ball.Color==back.Color&&!state.EndReached)
                {ball.Speed=Math.Max(ball.Speed-.4f,-10);ball.GoBack=true;}
                else ball.Speed-=Math.Min(Math.Abs(ball.Speed),.04f)*Math.Sign(ball.Speed);
            }

            // Pass two advances and pushes balls. The list is in descending path-position order.
            for(int i=state.Balls.Length-1;i>=0;i--)
            {
                var ball=state.Balls[i];
                if(ball.PathPosition<0)
                {if(state.Balls.Length==1)ball.PathPosition=0;else{Destroy(i,i);continue;}}
                if(ball.PathPosition>=level.PathLength)
                {Destroy(i,i);state.Generating=false;state.EndReached=true;continue;}
                ball.PathPosition+=ball.Speed;
                if(i<state.Balls.Length-1)
                {
                    var back=state.Balls[i+1];
                    if(!back.Inserted)
                    {
                        // Equivalent to upstream's pos++ loop, without an unbounded loop on hostile input.
                        float missing=BallSpacing-(ball.PathPosition-back.PathPosition);
                        if(missing>0)ball.PathPosition+=(float)Math.Ceiling(missing);
                    }
                    else if(CollidesBack(i,BallSpacing))ball.PathPosition+=4;
                    else
                    {
                        if(!state.EndReached&&!back.Exploding)ResolveInsertion(i+1);
                        back.Inserted=false;
                    }
                }
                if(i==0&&ball.Inserted&&!ball.Exploding)
                {if(!state.EndReached)ResolveInsertion(i);ball.Inserted=false;}
                // Clamp sampling only; keep position so the next tick can apply the original pit test.
                Place(ball);
                if(ball.FadeTicks>0)ball.FadeTicks--;
            }
            for(int i=0;i<state.Balls.Length;)
            {
                var ball=state.Balls[i];
                if(ball.Exploding&&ball.ExplosionTicks>=30)
                {
                    // Exploding groups may meet while their animations are at different ages.
                    // The source removes the whole connected exploding subchain at the first expiry.
                    int start,end;FindSubChain(i,out start,out end);Destroy(start,end);i=start;continue;
                }
                i++;
            }
            for(int i=0;i<state.Balls.Length;i++)if(state.Balls[i].Exploding)state.Balls[i].ExplosionTicks++;
            if(state.Balls.Length>0)state.Balls[0].Combo=0;
            for(int i=1;i<state.Balls.Length;i++)
            {
                var front=state.Balls[i-1];
                if(!front.Exploding&&(CollidesFront(i,BallSpacing)||front.Color!=state.Balls[i].Color))state.Balls[i].Combo=0;
            }
        }
        private void ResolveInsertion(int index)
        {
            int start,end;FindSubChain(index,out start,out end);
            if(end-start+1<3){state.ChainBonus=0;return;}
            state.ChainBonus=Increment(state.ChainBonus);state.MaxChainBonus=Math.Max(state.MaxChainBonus,state.ChainBonus);
            long score=(end-start+1)*10L;if(state.ChainBonus>=5)score=Add(score,100L+(state.ChainBonus-5L)*10);
            Explode(start,end,score,0);
            if(end+1<state.Balls.Length)state.Balls[end+1].Combo=1;
        }
        private void ResolveCombo(int start,int end)
        {
            int combo=0;for(int i=start;i<=end;i++)if(state.Balls[i].Combo>0){combo=state.Balls[i].Combo;break;}
            if(end+1<state.Balls.Length)state.Balls[end+1].Combo=Increment(combo);
            if(combo>0)state.TotalCombos=Increment(state.TotalCombos);state.MaxCombo=Math.Max(state.MaxCombo,combo);
            Explode(start,end,(end-start+1)*10L+combo*100L,combo);
        }
        private void Explode(int start,int end,long score,int combo)
        {
            for(int i=start;i<=end;i++){state.Balls[i].Exploding=true;state.Balls[i].ExplosionTicks=0;}
            state.Score=Add(state.Score,score);var center=state.Balls[end-(end-start+1)/2];
            Emit(combo>0?"combo":"explode",center.X,center.Y,score,center.Color,combo);
        }
        private bool CollidesBack(int i,float spacing) { return i+1<state.Balls.Length&&state.Balls[i].PathPosition-state.Balls[i+1].PathPosition<spacing; }
        private bool CollidesFront(int i,float spacing) { return i>0&&state.Balls[i-1].PathPosition-state.Balls[i].PathPosition<spacing; }
        private void FindSubChain(int index,out int start,out int end)
        {
            start=index;end=index;
            for(int i=index;i+1<state.Balls.Length;i++)
            {if(CollidesBack(i,RoughSpacing)&&SameGroup(i,i+1))end=i+1;else break;}
            for(int i=index;i>0;i--)
            {if(CollidesFront(i,RoughSpacing)&&SameGroup(i,i-1))start=i-1;else break;}
        }
        private bool SameGroup(int a,int b) { return state.Balls[a].Color==state.Balls[b].Color&&state.Balls[a].Exploding==state.Balls[b].Exploding; }
        private void Destroy(int start,int end)
        {
            // Capture before removing the final chain; no reference to stale array memory in the outro.
            if(start==0&&end==state.Balls.Length-1)state.LastClearedPosition=Math.Max(0,Math.Min(level.PathLength,state.Balls[0].PathPosition));
            if(state.Balls.Length==1)state.LastClearedPosition=Math.Max(0,Math.Min(level.PathLength,state.Balls[0].PathPosition));
            RemoveRange(ref state.Balls,start,end-start+1);
        }
        private void GenerateChain()
        {
            if(!state.Generating)return;
            if(state.Balls.Length==0){AppendGenerated();return;}
            if(state.InitialRoll&&state.Balls.Length<difficulty.StartCount)
            {if(state.Balls[state.Balls.Length-1].PathPosition>=.5f)AppendGenerated();}
            else
            {
                if(state.Balls[state.Balls.Length-1].PathPosition>=BallSpacing)
                {if(AppendGenerated())state.Balls[state.Balls.Length-1].FadeTicks=17;}
                state.InitialRoll=false;
            }
        }
        private bool AppendGenerated()
        {
            if(state.Balls.Length>=MaxBalls){CapacityRejected();return false;}
            bool single=false;int color;
            if(state.Balls.Length==0)color=Random(0,difficulty.ColorCount-1);
            else
            {
                var tail=state.Balls[state.Balls.Length-1];
                if(tail.IsSingle)color=DifferentColor(tail.Color);
                else if(Random(1,100)<difficulty.SingleChance){color=DifferentColor(tail.Color);single=true;}
                else if(Random(1,100)<difficulty.RepeatChance)
                {
                    int start,end;FindSubChain(state.Balls.Length-1,out start,out end);
                    color=end-start+1<4?tail.Color:DifferentColor(tail.Color);
                }
                else color=Random(0,difficulty.ColorCount-1);
            }
            var ball=NewBall(0,color);ball.IsSingle=single;
            if(state.Balls.Length>0)ball.Speed=state.Balls[state.Balls.Length-1].Speed;
            Append(ref state.Balls,ball);return true;
        }
        private int DifferentColor(int previous)
        {int color=previous;while(color==previous)color=Random(0,difficulty.ColorCount-1);return color;}
        private int RandomChainColor()
        {
            if(state.Balls.Length==0)return state.Frog.NextColor;
            int color;do{color=Random(0,difficulty.ColorCount-1);}while(!ContainsColor(color));return color;
        }
        private bool ContainsColor(int color)
        {for(int i=0;i<state.Balls.Length;i++)if(state.Balls[i].Color==color)return true;return false;}
        private void CorrectFrogColors()
        {
            if(state.Balls.Length==0)return;
            if(!ContainsColor(state.Frog.NextColor))state.Frog.NextColor=RandomChainColor();
            if(!ContainsColor(state.Frog.CurrentColor))state.Frog.CurrentColor=RandomChainColor();
        }
        private void UpdateChainSpeed()
        {
            if(state.EndReached){state.ChainSpeed=32;return;}
            int last=state.Balls.Length-1;for(int i=last;i>=0;i--)if(!CollidesFront(i,RoughSpacing)){last=i;break;}
            state.ChainSpeed=last>=0&&state.Balls[last].PathPosition>level.PathLength*.8f?difficulty.Speed/difficulty.SlowFactor:difficulty.Speed;
        }
        private void UpdateBullets()
        {
            for(int i=state.Bullets.Length-1;i>=0;i--)
            {
                var b=state.Bullets[i];
                if(b.X>1330||b.X< -50||b.Y>770||b.Y< -50)
                {RemoveRange(ref state.Bullets,i,1);state.ChainBonus=0;continue;}
                b.X+=b.DirectionX*15;b.Y+=b.DirectionY*15;
            }
        }
        private void CollideBullets()
        {
            for(int i=0;i<state.Bullets.Length;)
            {
                var bullet=state.Bullets[i];int hit=-1;
                for(int j=0;j<state.Balls.Length;j++)
                {var ball=state.Balls[j];if(!ball.Hidden&&!ball.Exploding&&DistanceSquared(bullet.X,bullet.Y,ball.X,ball.Y)<2300){hit=j;break;}}
                if(hit<0){i++;continue;}
                if(state.Balls.Length>=MaxBalls){CapacityRejected();i++;continue;}
                bool back=InsertInBack(bullet,state.Balls[hit]);Insert(hit+(back?1:0),bullet.Color,back);
                RemoveRange(ref state.Bullets,i,1);
            }
        }
        private bool InsertInBack(ZumaBulletSnapshot bullet,ZumaBallSnapshot ball)
        {
            float frontX=ball.X,frontY=ball.Y,backX=ball.X,backY=ball.Y;
            // Upstream probes 32 samples in each direction with half a delta per sample.
            // Clamp the old out-of-range reads at either path end, without changing valid probes.
            for(int step=0;step<32;step++)
            {
                int index=(int)(ball.PathPosition+step);if(index>=0&&index<level.PathLength)
                {frontX+=(level.Points[index+1].X-level.Points[index].X)*.5f;frontY+=(level.Points[index+1].Y-level.Points[index].Y)*.5f;}
            }
            for(int step=0;step<32;step++)
            {
                float p=ball.PathPosition-step;if(p<0)return false;int index=(int)p;
                if(index<level.PathLength){backX-=(level.Points[index+1].X-level.Points[index].X)*.5f;backY-=(level.Points[index+1].Y-level.Points[index].Y)*.5f;}
            }
            return DistanceSquared(frontX,frontY,bullet.X,bullet.Y)>DistanceSquared(backX,backY,bullet.X,bullet.Y);
        }
        private void Insert(int index,int color,bool back)
        {
            // Allocate a distinct ball; copying the displaced ball lost isSingle in the original C.
            var reference=state.Balls[Math.Min(index,state.Balls.Length-1)];
            var inserted=reference.Copy();inserted.Color=color;inserted.Inserted=true;inserted.Exploding=false;
            inserted.ExplosionTicks=0;inserted.IsSingle=false;inserted.FadeTicks=0;
            int previousLength=state.Balls.Length;
            var array=new ZumaBallSnapshot[previousLength+1];
            Array.Copy(state.Balls,0,array,0,index);Array.Copy(state.Balls,index,array,index+1,state.Balls.Length-index);
            array[index]=inserted;state.Balls=array;
            if(back)
            {
                var previous=state.Balls[index-1];
                // Original CollidesBack observes the old length until the final len++. In particular
                // an insertion at old len-1 is placed 32 behind; an append at old len was UB, and is
                // safely given the same spacing here.
                bool touching=index<previousLength-1&&inserted.PathPosition-state.Balls[index+1].PathPosition<BallSpacing;
                inserted.PathPosition=touching?previous.PathPosition:previous.PathPosition-BallSpacing;
            }
            Place(inserted);Emit("insert",inserted.X,inserted.Y,0,color);
        }
        private void UpdateCoin()
        {
            if(level.Coins.Length==0)return;
            var coin=state.Coin;
            if(coin.Active&&coin.Blinking)
            {if(coin.TimerTicks>180){coin.Active=false;coin.Blinking=false;coin.TimerTicks=0;EmitCoin("coinDisappear");}}
            else if(coin.Active)
            {if(coin.TimerTicks>600){coin.Blinking=true;coin.TimerTicks=0;}}
            else if(coin.TimerTicks>600)
            {coin.Active=true;coin.PositionIndex=Random(0,level.Coins.Length-1);coin.TimerTicks=0;EmitCoin("coinAppear");}
            if(!coin.Active)return;
            var p=level.Coins[coin.PositionIndex];
            for(int i=0;i<state.Bullets.Length;i++)
            {
                var b=state.Bullets[i];if(DistanceSquared(b.X,b.Y,p.X,p.Y)>=2900)continue;
                RemoveRange(ref state.Bullets,i,1);state.ChainBonus=0;coin.Active=false;coin.Blinking=false;coin.FadeTicks=16;coin.TimerTicks=0;
                state.Score=Add(state.Score,500);state.TotalCoins=Increment(state.TotalCoins);EmitCoin("coinCollect",500);break;
            }
        }
        private void UpdateIntro()
        {
            if(!state.IntroInitialized)
            {state.IntroInitialized=true;state.IntroPosition=0;state.IntroPreviousPosition=0;AddTrail(0);return;}
            AgeTrail(28);
            if(state.IntroPosition>level.PathLength)
            {if(state.TrailParticles.Length==0){state.Phase="play";Emit("introEnd",level.FrogX,level.FrogY);}return;}
            if(state.IntroPosition>state.IntroPreviousPosition)
            {AddTrail(state.IntroPosition);state.IntroPreviousPosition=state.IntroPosition;}
            state.IntroPosition+=15;
        }
        private void UpdateOutro()
        {
            if(!state.OutroInitialized)
            {state.OutroInitialized=true;OutroPop();return;}
            AgeTrail(34);
            if(state.OutroPosition>level.PathLength)
            {if(state.TrailParticles.Length==0)Finish("won");return;}
            if(state.OutroPosition-state.OutroPreviousPosition>50)
            {OutroPop();state.OutroPreviousPosition=state.OutroPosition;}
            state.OutroPosition+=12;
        }
        private void OutroPop()
        {
            state.Score=Add(state.Score,100);AddTrail(state.OutroPosition);
            float x,y,angle;bool hidden,priority;Sample(state.OutroPosition,out x,out y,out angle,out hidden,out priority);
            Emit("outroPop",x,y,100,2);
        }
        private void AgeTrail(int lifetime)
        {
            for(int i=state.TrailParticles.Length-1;i>=0;i--)
            {state.TrailParticles[i].AgeTicks++;if(state.TrailParticles[i].AgeTicks>=lifetime)RemoveRange(ref state.TrailParticles,i,1);}
        }
        private void AddTrail(float position)
        {
            float x,y,angle;bool hidden,priority;Sample(position,out x,out y,out angle,out hidden,out priority);
            if(state.TrailParticles.Length>=256)RemoveRange(ref state.TrailParticles,0,1);
            Append(ref state.TrailParticles,new ZumaTrailParticleSnapshot{X=x,Y=y});
        }
        private void Finish(string phase)
        {
            state.Phase=phase;state.Generating=false;state.Bullets=new ZumaBulletSnapshot[0];
            state.TrailParticles=new ZumaTrailParticleSnapshot[0];Emit("result",level.FrogX,level.FrogY,state.Score);
        }
        private ZumaBallSnapshot NewBall(float position,int color)
        {var ball=new ZumaBallSnapshot{PathPosition=position,Color=color};Place(ball);return ball;}
        private void Place(ZumaBallSnapshot ball)
        {Sample(ball.PathPosition,out ball.X,out ball.Y,out ball.Angle,out ball.Hidden,out ball.DrawPriority);}
        private void Sample(float position,out float x,out float y,out float angle,out bool hidden,out bool priority)
        {
            float p=Math.Max(0,Math.Min(level.PathLength,position));int index=Math.Min((int)p,level.PathLength-1);float fraction=p-index;
            var a=level.Points[index];var b=level.Points[index+1];x=a.X+(b.X-a.X)*fraction;y=a.Y+(b.Y-a.Y)*fraction;
            // BallChain_Update keeps the angle/flags of the last fully traversed DAT delta.
            int tangent=Math.Max(0,Math.Min(level.PathLength-1,(int)p-1));var t=level.Points[tangent];var next=level.Points[tangent+1];
            angle=(float)(Math.Atan2(next.Y-t.Y,next.X-t.X)*180/Math.PI);hidden=t.Hidden;priority=t.DrawPriority;
        }
        private uint NextRandom()
        {uint x=state.RandomState;x^=x<<13;x^=x>>17;x^=x<<5;state.RandomState=x;return x;}
        private int Random(int min,int max) { return min+(int)(NextRandom()%(uint)(max-min+1)); }
        private void CapacityRejected() { state.CapacityRejections=Increment(state.CapacityRejections);BumpRevision(); }
        private void BumpRevision() { state.Revision=Add(state.Revision,1); }
        private static int Increment(int n) { return n==int.MaxValue?n:n+1; }
        private static long Add(long a,long b) { return a>long.MaxValue-b?long.MaxValue:a+b; }
        private static float DistanceSquared(float x1,float y1,float x2,float y2) { float x=x1-x2,y=y1-y2;return x*x+y*y; }
        private static bool Finite(float f) { return !float.IsNaN(f)&&!float.IsInfinity(f); }
        private static void Append<T>(ref T[] array,T item) { int n=array.Length;Array.Resize(ref array,n+1);array[n]=item; }
        private static void RemoveRange<T>(ref T[] array,int start,int count)
        {Array.Copy(array,start+count,array,start,array.Length-start-count);Array.Resize(ref array,array.Length-count);}
        private void Emit(string kind,float x,float y,long score=0,int color=0,int combo=0)
        {if(events.Count==256)events.RemoveAt(0);events.Add(new ZumaEvent{Kind=kind,X=x,Y=y,Score=score,Color=color,Combo=combo});}
        private void EmitCoin(string kind,long score=0)
        {var p=level.Coins[state.Coin.PositionIndex];Emit(kind,p.X,p.Y,score,2);}

        public static bool ValidateLevel(ZumaLevelDefinition level,out string reason)
        {
            reason=null;
            if(level==null||string.IsNullOrEmpty(level.Id)||string.IsNullOrEmpty(level.DisplayName)||!Finite(level.FrogX)||!Finite(level.FrogY))reason="Invalid level identity or frog position.";
            else if(level.Points==null||level.Points.Length<2||level.Points.Length>100000||level.Coins==null||level.Coins.Length>256)reason="Invalid path/coin arrays.";
            else
            {
                foreach(var p in level.Points)if(p==null||!Finite(p.X)||!Finite(p.Y)||Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000){reason="Invalid path coordinate.";break;}
                if(reason==null)foreach(var p in level.Coins)if(p==null||!Finite(p.X)||!Finite(p.Y)||Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000){reason="Invalid coin coordinate.";break;}
            }
            return reason==null;
        }
        public static bool Validate(ZumaLevelDefinition level,ZumaSnapshot s,out string reason)
        {
            reason=null;
            if(s==null){reason="Missing snapshot.";return false;}
            ZumaDifficulty d;
            try{d=ZumaDifficulty.Get(s.DifficultyId);}catch(ArgumentException){reason="Unknown difficulty.";return false;}
            if(level!=null&&(!ValidateLevel(level,out reason)||s.MapId!=level.Id)){if(reason==null)reason="Map mismatch.";return false;}
            bool terminal=s.Phase=="won"||s.Phase=="lost";
            if(s.RulesVersion!=1||s.ContentVersion!=1||s.RandomAlgorithm!="xorshift32-v1"||s.RandomState==0)reason="Unsupported rules/content/random state.";
            else if(string.IsNullOrEmpty(s.SessionId)||s.SessionId.Length>128||string.IsNullOrEmpty(s.MapId)||s.MapId.Length>64)reason="Invalid session/map identity.";
            else if(s.Phase!="intro"&&s.Phase!="play"&&s.Phase!="lossDrain"&&s.Phase!="winOutro"&&!terminal)reason="Invalid phase.";
            else if(s.Score<0||s.TotalTicks<0||s.PlayTicks<0||s.PlayTicks>MaximumPlayTicks||s.PlayTicks>s.TotalTicks||s.Revision<0||s.AceTimeSeconds<=0||s.ChainBonus<0||s.MaxChainBonus<s.ChainBonus||s.MaxCombo<0||s.TotalCombos<0||s.TotalCoins<0||s.CapacityRejections<0)reason="Invalid counters.";
            else if(s.ResultAcknowledged&&!s.ResultCommitted||s.ResultCommitted&&!terminal)reason="Invalid result markers.";
            else if(s.Balls==null||s.Bullets==null||s.TrailParticles==null||s.Frog==null||s.Coin==null||s.Balls.Length>MaxBalls||s.Bullets.Length>MaxBullets||s.TrailParticles.Length>256)reason="Invalid object arrays.";
            else if(!InRange(s.ChainSpeed,0,32)||!InRange(s.LastClearedPosition,0,100032)||!InRange(s.IntroPosition,0,100032)||!InRange(s.IntroPreviousPosition,0,s.IntroPosition)||!InRange(s.OutroPosition,0,100032)||!InRange(s.OutroPreviousPosition,0,s.OutroPosition)||s.GlowTicks<0||s.GlowTicks>31)reason="Invalid chain/trail counters.";
            else if(s.Frog.CurrentColor<0||s.Frog.CurrentColor>=d.ColorCount||s.Frog.NextColor<0||s.Frog.NextColor>=d.ColorCount||s.Frog.RecoilTicks<0||s.Frog.RecoilTicks>10||!Unit(s.Frog.DirectionX,s.Frog.DirectionY))reason="Invalid frog.";
            else if(s.Coin.PositionIndex<0||s.Coin.TimerTicks<0||s.Coin.TimerTicks>601||s.Coin.FadeTicks<0||s.Coin.FadeTicks>16||s.Coin.Blinking&&!s.Coin.Active)reason="Invalid coin.";
            if(reason!=null)return false;
            if((s.Phase=="intro"||s.Phase=="play"||s.Phase=="winOutro"||s.Phase=="won")&&s.EndReached)
                reason="Non-loss phase has reached the pit.";
            else if((s.Phase=="lossDrain"||s.Phase=="lost")&&(!s.EndReached||s.Generating))
                reason="Loss phase must stop generation after reaching the pit.";
            else if(!s.EndReached&&!s.Generating&&s.Score<=d.GaugeScore)
                reason="Generation can stop only above the strict score threshold or after reaching the pit.";
            else if((s.Phase=="winOutro"||s.Phase=="won")&&(s.Score<=d.GaugeScore||s.Generating||s.Balls.Length!=0||s.Bullets.Length!=0))
                reason="Win phase requires cleared chain above the score threshold.";
            else if(terminal&&(s.Generating||s.Balls.Length!=0||s.Bullets.Length!=0||s.TrailParticles.Length!=0))
                reason="Terminal phase retains active objects.";
            else if(s.Phase=="won"&&!s.OutroInitialized)reason="Won phase has not initialized the outro.";
            if(reason!=null)return false;
            if(level!=null)
            {
                if((s.Coin.Active||s.Coin.FadeTicks>0)&&(s.Coin.PositionIndex>=level.Coins.Length))reason="Coin position outside map.";
                if(s.LastClearedPosition>level.PathLength||s.IntroPosition>level.PathLength+15||s.OutroPosition>level.PathLength+12)reason="Trail outside map.";
            }
            for(int i=0;reason==null&&i<s.Balls.Length;i++)
            {
                // Spacing pushes cascade through overlapping insertions before the next tick's pit
                // removal. Bound that overshoot by the number of live balls, not just ball speed.
                var b=s.Balls[i];float max=level==null?100000+MaxBalls*BallSpacing:level.PathLength+s.Balls.Length*BallSpacing;
                if(b==null||b.Color<0||b.Color>=d.ColorCount||!InRange(b.PathPosition,-64,max)||!InRange(b.Speed,-32,32)||!Finite(b.X)||!Finite(b.Y)||!Finite(b.Angle)||b.Combo<0||b.ExplosionTicks<0||b.ExplosionTicks>30||b.FadeTicks<0||b.FadeTicks>17||!b.Exploding&&b.ExplosionTicks!=0)reason="Invalid ball.";
                else if(i>0&&s.Balls[i-1].PathPosition+32<b.PathPosition)reason="Invalid chain order.";
            }
            int previousSlot=-1;
            foreach(var b in s.Bullets)
            {
                if(reason!=null)break;
                if(b==null||b.Slot<=previousSlot||b.Slot>=MaxBullets||b.Color<0||b.Color>=d.ColorCount||!InRange(b.X,-66,1346)||!InRange(b.Y,-66,786)||!Unit(b.DirectionX,b.DirectionY))reason="Invalid bullet.";
                else previousSlot=b.Slot;
            }
            foreach(var p in s.TrailParticles)
            {if(reason!=null)break;if(p==null||!Finite(p.X)||!Finite(p.Y)||p.AgeTicks<0||p.AgeTicks>34)reason="Invalid trail particle.";}
            return reason==null;
        }
        private static bool InRange(float f,float min,float max) { return Finite(f)&&f>=min&&f<=max; }
        private static bool Unit(float x,float y) { return Finite(x)&&Finite(y)&&Math.Abs(x*x+y*y-1)<.001f; }
    }
}
