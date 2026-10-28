using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.FlappyBird;

namespace XanaduYonder.Runtime.FlappyBird
{
    internal sealed class FlappyScene : IDisposable
    {
        private readonly RectTransform world, bird, ground;
        private readonly Image birdImage;
        private readonly Sprite birdRest, birdWing, cap, grass;
        private readonly Dictionary<int, PipeView> pipes=new Dictionary<int, PipeView>();
        private readonly List<int> removed=new List<int>();
        private float flapRemaining, deathElapsed;
        private bool wasDead;
        private double deathY;
        internal Sprite BirdSprite { get { return birdRest; } }

        internal FlappyScene(RectTransform surface)
        {
            world=surface;
            birdRest=FlappyArt.LoadSprite("BirdRest");
            // Only the two right-facing poses are included in the runtime pack.
            birdWing=FlappyArt.LoadSprite("BirdWing");
            cap=FlappyArt.LoadSprite("PipeCap");
            grass=FlappyArt.LoadSprite("Ground");
            Cloud(43.2f,130.8f,.84f,.70f); Cloud(282,219,1.14f,.82f); Cloud(133.92f,58.08f,.552f,.55f);
            Ellipse("Far hill left",-84,440.4f,252,220,UiFactory.Hex("BFD8BE")*new Color(1,1,1,.42f));
            Ellipse("Far hill right",146.4f,476.4f,252,180,UiFactory.Hex("A9CCA9")*new Color(1,1,1,.32f));
            Ellipse("Front hill",24,500.4f,222,134.4f,UiFactory.Hex("A8C99D")*new Color(1,1,1,.38f));
            birdImage=SpriteImage(world,"Flappy bird",birdRest,0,0,63.984f,57.792f);
            // The visible round body is centred near (66,64) in this 124x112
            // top-left crop. Align the fixed collision circle to that body,
            // rather than to the crop's tail/beak and transparent padding.
            bird=birdImage.rectTransform; bird.pivot=new Vector2(66f/124,1-64f/112);
            ground=UiFactory.Rect(world,"Flappy scrolling ground",0,(float)FlappyRules.PlayHeight,600,56.4f);
            // Grass tips are decorative above the fixed ground boundary. This
            // offset preserves the approved atlas scale and visible soil strip.
            for(int i=0;i<5;i++) SpriteImage(ground,"Grass tile "+i,grass,i*147.6f,-10.8f,148.8f,56.4f);
        }

        internal static Image SpriteImage(Transform parent,string name,Sprite sprite,float x,float y,float w,float h)
        {
            var image=UiFactory.Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();
            image.sprite=sprite; image.color=Color.white; image.raycastTarget=false; return image;
        }
        private void Cloud(float x,float y,float scale,float opacity)
        {
            var cloud=UiFactory.Rect(world,"Cloud",x,y,80,20); cloud.localScale=Vector3.one*scale;
            // Preblend the cloud shade so overlapping lobes form one even
            // silhouette instead of showing darker translucent seams.
            var color=Color.Lerp(UiFactory.Hex("DAECE6"),UiFactory.Hex("F7FAEE"),opacity);
            Shape(cloud,"Cloud base",0,0,80,20,10,color);
            Shape(cloud,"Cloud left",11,-13,33,33,17,color);
            Shape(cloud,"Cloud right",27,-23,43,43,22,color);
        }
        private void Ellipse(string name,float x,float y,float w,float h,Color color)
        { var item=Shape(world,name,x,y,w,h,0,color); item.Kind=FlappyGraphicKind.Ellipse; }
        internal static FlappyGraphic Shape(Transform parent,string name,float x,float y,float w,float h,float radius,Color color)
        {
            var shape=UiFactory.Rect(parent,name,x,y,w,h).gameObject.AddComponent<FlappyGraphic>();
            shape.Radius=radius; shape.color=color; shape.raycastTarget=false; return shape;
        }
        internal void Flap() { flapRemaining=.14f; }
        internal void Reset()
        {
            flapRemaining=deathElapsed=0; wasDead=false;
            foreach(var pipe in pipes.Values) pipe.Dispose(); pipes.Clear();
        }
        internal void Render(FlappySession session,float elapsed,bool animate)
        {
            var game=session.Game;
            bool ready=session.Phase==FlappyPhase.Ready;
            if(game.GameOver&&!wasDead) { wasDead=true; deathElapsed=0; deathY=game.BirdY; }
            if(animate)
            {
                flapRemaining=Mathf.Max(0,flapRemaining-elapsed);
                if(session.Phase==FlappyPhase.Dying) deathElapsed+=elapsed;
            }
            float y=(float)game.BirdY;
            if(game.GameOver) y=Mathf.Max((float)FlappyRules.BirdRadius,(float)deathY-300*deathElapsed*deathElapsed);
            bird.anchoredPosition=new Vector2(ready?180:(float)FlappyRules.BirdX,-((float)FlappyRules.PlayHeight-y));
            if(ready) bird.anchoredPosition=new Vector2(182.064f,-290.928f);
            float flightPose = Mathf.Clamp01((float)((game.BirdVelocity + FlappyRules.TerminalVelocity)
                / (FlappyRules.TerminalVelocity + FlappyRules.FlapVelocity)));
            float tilt=ready?0:game.GameOver?-Mathf.Lerp(15,80,Mathf.Clamp01(deathElapsed/.35f)):Mathf.Lerp(-65,22,flightPose);
            bird.localRotation=Quaternion.Euler(0,0,tilt);
            birdImage.sprite=flapRemaining>.055f?birdWing:birdRest;
            bird.localScale=new Vector3(1,flapRemaining>.09f?.95f:1,1);
            removed.Clear();
            foreach(int id in pipes.Keys)
            {
                bool exists=false; foreach(var pipe in game.Pipes) if(pipe.Id==id) { exists=true; break; }
                if(!exists) removed.Add(id);
            }
            foreach(int id in removed) { pipes[id].Dispose(); pipes.Remove(id); }
            foreach(var pipe in game.Pipes)
            {
                PipeView view;
                if(!pipes.TryGetValue(pipe.Id,out view))
                {
                    view=new PipeView(world,cap,pipe.Id); pipes.Add(pipe.Id,view);
                    view.Top.SetSiblingIndex(bird.GetSiblingIndex()); view.Bottom.SetSiblingIndex(bird.GetSiblingIndex());
                }
                view.Render((float)pipe.X,(float)pipe.GapCenter);
            }
            ground.anchoredPosition=new Vector2(-(float)(game.ElapsedTime*FlappyRules.Speed%147.6),-(float)FlappyRules.PlayHeight);
        }
        public void Dispose()
        {
            foreach(var pipe in pipes.Values) pipe.Dispose(); pipes.Clear();
            UnityEngine.Object.Destroy(birdRest); UnityEngine.Object.Destroy(birdWing); UnityEngine.Object.Destroy(cap); UnityEngine.Object.Destroy(grass);
        }

        private sealed class PipeView : IDisposable
        {
            internal readonly RectTransform Top,Bottom;
            private readonly RectTransform topStem,bottomStem,topCap,bottomCap;
            private const float Width=(float)FlappyRules.PipeWidth;
            private const float CapHeight=(float)FlappyRules.PipeCapHeight;
            private const float StemWidth=(float)FlappyRules.PipeStemWidth;
            private const float StemInset=(float)FlappyRules.PipeStemInset;
            private const float StemCapOffset=(float)FlappyRules.PipeStemCapOffset;
            internal PipeView(Transform parent,Sprite sprite,int id)
            {
                Top=UiFactory.Rect(parent,"Upper pipe "+id,0,0,Width,1);
                Bottom=UiFactory.Rect(parent,"Lower pipe "+id,0,0,Width,1);
                topStem=Stem(Top,true); bottomStem=Stem(Bottom,false);
                topCap=SpriteImage(Top,"Upper cap",sprite,0,0,Width,CapHeight).rectTransform;
                topCap.pivot=new Vector2(.5f,.5f); topCap.localRotation=Quaternion.Euler(0,0,180);
                bottomCap=SpriteImage(Bottom,"Lower cap",sprite,0,0,Width,CapHeight).rectTransform;
            }
            private static RectTransform Stem(Transform parent,bool inverted)
            {
                var stem=UiFactory.Rect(parent,"Pipe stem",StemInset,0,StemWidth,1);
                // Match the atlas cap's outer geometry; simple color bands avoid
                // stretching transparent atlas margins into a false obstacle.
                Color[] colors={UiFactory.Hex("255916"),UiFactory.Hex("58A723"),UiFactory.Hex("A5EB6D"),UiFactory.Hex("58A723")};
                float[] starts={0,.05f,.80f,.92f},ends={.05f,.80f,.92f,1};
                for(int i=0;i<4;i++)
                {
                    float min=inverted?1-ends[i]:starts[i],max=inverted?1-starts[i]:ends[i];
                    var stripe=UiFactory.Panel(stem,"Stem band",0,0,1,1,colors[i],false).rectTransform;
                    stripe.anchorMin=new Vector2(min,0); stripe.anchorMax=new Vector2(max,1); stripe.offsetMin=stripe.offsetMax=Vector2.zero;
                }
                return stem;
            }
            internal void Render(float x,float gap)
            {
                float upper=(float)FlappyRules.PlayHeight-gap-(float)FlappyRules.GapHeight*.5f;
                float lower=gap-(float)FlappyRules.GapHeight*.5f;
                Top.anchoredPosition=new Vector2(x,0); Top.sizeDelta=new Vector2(Width,upper);
                Bottom.anchoredPosition=new Vector2(x,-((float)FlappyRules.PlayHeight-lower)); Bottom.sizeDelta=new Vector2(Width,lower);
                topStem.sizeDelta=new Vector2(StemWidth,Mathf.Max(0,upper-StemCapOffset));
                bottomStem.anchoredPosition=new Vector2(StemInset,-StemCapOffset); bottomStem.sizeDelta=new Vector2(StemWidth,Mathf.Max(0,lower-StemCapOffset));
                topCap.anchoredPosition=new Vector2(Width*.5f,-upper+CapHeight*.5f);
            }
            public void Dispose() { if(Top!=null) { Top.gameObject.SetActive(false); UnityEngine.Object.Destroy(Top.gameObject); } if(Bottom!=null) { Bottom.gameObject.SetActive(false); UnityEngine.Object.Destroy(Bottom.gameObject); } }
        }
    }
}
