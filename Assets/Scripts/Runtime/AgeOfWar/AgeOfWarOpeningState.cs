using System;
using System.Collections.Generic;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarOpeningCue
    {
        internal string Kind,Key;
        internal int SourceFrame;
    }

    // Source MovieClip clocks, independent of Unity and of the battle simulation.
    internal sealed class AgeOfWarOpeningState : IDisposable
    {
        private enum Phase { Loading,LoadedLead,Ready,OneRoot5,OneLead,OneIntro,SponsorInitial,Sponsor,SponsorDone,Author,Done }
        private sealed class Fire { internal int Birth,Start; }
        private readonly int edition;
        private readonly Func<int,int> random;
        private readonly AgeOfWarClock clock;
        private readonly List<AgeOfWarOpeningCue> cues=new List<AgeOfWarOpeningCue>();
        private readonly Dictionary<string,Fire> fires=new Dictionary<string,Fire>();
        private Phase phase;
        private bool playRequested,suspended,disposed,transitionPending;
        private string stream;
        internal event Action FrameAdvanced;
        internal string SceneKey {get;private set;}
        internal string OverlaySceneKey {get;private set;}
        internal int SourceFrame {get;private set;}
        internal int UiTicks {get;private set;}
        internal double Progress {get;private set;}
        internal bool Done {get{return phase==Phase.Done;}}
        internal bool CanPressPlay {get{return phase==Phase.Ready&&!playRequested&&!disposed;}}
        internal bool IsLoading {get{return phase==Phase.Loading;}}
        internal int Edition {get{return edition;}}
        internal AgeOfWarOpeningState(int edition,Func<int,int> randomInteger)
        {
            if(edition!=1&&edition!=2)throw new ArgumentOutOfRangeException("edition");
            if(randomInteger==null)throw new ArgumentNullException("randomInteger");
            this.edition=edition;random=randomInteger;clock=new AgeOfWarClock(edition==1?40:30);
            phase=Phase.Loading;SceneKey="preloader";OverlaySceneKey="";SourceFrame=1;
        }
        internal void ResourceProgress(long loadedBytes,long totalBytes,bool ready)
        {
            if(disposed||phase!=Phase.Loading)return;
            if(loadedBytes<0||totalBytes<0)throw new ArgumentOutOfRangeException("loadedBytes");
            Progress=totalBytes>0?Math.Max(0,Math.Min(1,loadedBytes/(double)totalBytes)):0;
            if(ready)
            {
                Progress=1;
                if(edition==1){phase=Phase.LoadedLead;SceneKey="loaded-lead";SourceFrame=3;}
                else phase=Phase.Ready;
            }
            Present();
        }
        internal bool PressPlay()
        {
            if(!CanPressPlay||suspended)return false;
            if(edition==1)playRequested=true;
            else
            {
                Cue("music","music/2");Transition("checkers");
                phase=Phase.SponsorInitial;SceneKey="sponsor-initial";SourceFrame=1;Present();
            }
            return true;
        }
        internal void TransitionComplete(){transitionPending=false;}
        internal void Suspend(bool value){suspended=value;clock.Reset();}
        internal void Advance(double activeSeconds){clock.Advance(activeSeconds,!suspended&&!disposed,Tick);}
        internal void Tick()
        {
            if(suspended||disposed||Done)return;
            ++UiTicks;
            switch(phase)
            {
                case Phase.LoadedLead:phase=Phase.Ready;SceneKey="loaded";SourceFrame=4;break;
                case Phase.Ready:
                    if(edition==1&&playRequested){phase=Phase.OneRoot5;SourceFrame=5;}
                    break;
                case Phase.OneRoot5:phase=Phase.OneLead;SceneKey="intro-lead";SourceFrame=6;break;
                case Phase.OneLead:
                    phase=Phase.OneIntro;SceneKey="intro-background";SourceFrame=1;OverlaySceneKey="intro/1";OneIntroCues();break;
                case Phase.OneIntro:
                    if(SourceFrame==160){StopStream();phase=Phase.Done;SourceFrame=8;OverlaySceneKey="";}
                    else{++SourceFrame;OverlaySceneKey="intro/"+SourceFrame;OneIntroCues();}
                    break;
                case Phase.SponsorInitial:phase=Phase.Sponsor;SceneKey="sponsor";SourceFrame=1;break;
                case Phase.Sponsor:
                    if(SourceFrame==186){StopStream();phase=Phase.SponsorDone;SceneKey="sponsor-initial";SourceFrame=3;}
                    else{++SourceFrame;if(SourceFrame==2)StartStream("sponsor-stream");}
                    break;
                case Phase.SponsorDone:
                    if(!transitionPending){StopStream();Transition("fade");phase=Phase.Author;SceneKey="author/1";SourceFrame=1;}
                    break;
                case Phase.Author:
                    if(SourceFrame<66){++SourceFrame;SceneKey="author/"+SourceFrame;}
                    else if(!transitionPending){Transition("fade");phase=Phase.Done;}
                    break;
            }
            Present();
        }
        private void OneIntroCues()
        {
            if(SourceFrame==1||SourceFrame==5||SourceFrame==14||SourceFrame==18||SourceFrame==22)Cue("sound","sound/1002");
            if(SourceFrame==9)StartStream("intro-stream");
        }
        private void Present(){if(FrameAdvanced!=null)FrameAdvanced();}
        private void Cue(string kind,string key){cues.Add(new AgeOfWarOpeningCue{Kind=kind,Key=key,SourceFrame=SourceFrame});}
        private void Transition(string kind){transitionPending=true;Cue("transition",kind);}
        private void StartStream(string key){if(stream==key)return;StopStream();stream=key;Cue("stream-start",key);}
        private void StopStream(){if(stream==null)return;Cue("stream-stop",stream);stream=null;}
        internal AgeOfWarOpeningCue[] DrainCues(){var result=cues.ToArray();cues.Clear();return result;}
        internal int ResolveFrame(string key,string layerClock,int fallback,int birthFrame)
        {
            if(layerClock!="random-fire")return fallback;
            // Original root7 attachment first presents all children on raw source frame1.
            if(SourceFrame==1&&birthFrame==0)return 0;
            Fire fire;
            if(!fires.TryGetValue(key,out fire)||fire.Birth!=birthFrame)
            {
                int value=random(25);if(value<0||value>=25)throw new InvalidOperationException("randomInteger must return [0, bound).");
                fire=new Fire{Birth=birthFrame,Start=Math.Max(1,value)};fires[key]=fire;
            }
            int frame=fire.Start+Math.Max(0,SourceFrame-1-birthFrame);
            if(frame>=31)frame=3+(frame-31)%28;
            return frame-1;
        }
        public void Dispose(){if(disposed)return;disposed=true;clock.Reset();StopStream();fires.Clear();}
    }
    internal sealed class AgeOfWarOpeningHover
    {
        private bool hovered;
        private double from,target,started;
        internal void Hover(bool value,double now)
        {
            if(value==hovered)return;
            from=Sample(now);hovered=value;target=value?1:0;started=now;
        }
        internal double Sample(double now)
        {
            double t=Math.Max(0,Math.Min(1,(now-started)/.3));
            return from+(target-from)*(1-(1-t)*(1-t));
        }
    }

}
