using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    internal sealed class ZumaView
    {
        private readonly RectTransform root;
        private readonly ZumaAssets assets;
        private RectTransform content,dialog;
        private Texture2D objects,frog;
        private ZumaLevelDefinition level;
        private string loadedMap;
        private RectTransform lower,upper,projectiles,trails;
        private RawImage overlay,finish,finishMouth,foundation,body,tongue,eyes,current,next,gauge;
        private ZumaBitmapText score,stage,phaseLabel;
        private Text hint;
        private readonly List<RawImage> balls=new List<RawImage>(),shadows=new List<RawImage>(),bullets=new List<RawImage>(),particles=new List<RawImage>(),dots=new List<RawImage>(),dotShadows=new List<RawImage>();
        private readonly List<Floating> floats=new List<Floating>();
        private RawImage coin;
        private readonly Text warning;
        public ZumaTouchInput Touch { get; private set; }
        public bool HasDialog { get { return dialog!=null; } }
        private sealed class Floating { public ZumaBitmapText Text;public float X,Y,Age; }

        public ZumaView(RectTransform parent,ZumaAssets resources)
        {
            root=parent;assets=resources;
            warning=UiFactory.Label(root,"",170,677,940,36,19,new Color(1,.9f,.55f),TextAnchor.MiddleCenter);
        }
        public void Warning(string message)
        { warning.text=message??"";warning.transform.SetAsLastSibling(); }
        private void Clear()
        {
            CloseDialog();
            if(content!=null){content.gameObject.SetActive(false);UnityEngine.Object.Destroy(content.gameObject);}
            if(loadedMap!=null){assets.ReleaseLevelResources(loadedMap);loadedMap=null;}
            content=ZumaArt.Rect(root,"Zuma scene",0,0,1280,720);
            balls.Clear();shadows.Clear();bullets.Clear();particles.Clear();dots.Clear();dotShadows.Clear();floats.Clear();Touch=null;
            warning.transform.SetAsLastSibling();
        }
        public void ShowSelection(string id,string difficulty,ZumaRecord record,Action back,Action previous,Action play,Action nextMap,Action<int> selectDifficulty,Action resume,Action help)
        {
            Clear();var menu=assets.Texture("Images/menu");var map=assets.Map(id);
            ZumaArt.Image(content,"Sky",menu,new Rect(0,1442,1280,250),0,0,1280,250);
            var thumbnail=assets.Texture(map.thumbnail);
            // Encoded textures may have bottom padding for ETC2 block alignment.
            // Crop the original preview region so its geometry stays unchanged.
            ZumaArt.Image(content,"Map preview",thumbnail,new Rect(0,0,476,305),418,347.5f,476,305);
            ZumaArt.Image(content,"Gauntlet temple",menu,new Rect(1282,1440,1280,720),0,0,1280,720);
            ZumaArt.Button(content,menu,new Rect(1280,2160,211,84),172,632,211,84,back);
            ZumaArt.Button(content,menu,new Rect(1924,2260,139,48),426,667,139,48,previous);
            ZumaArt.Button(content,menu,new Rect(1922,2210,157,49),576,667,157,49,play);
            ZumaArt.Button(content,menu,new Rect(1922,2161,141,48),745,667,141,48,nextMap);
            var rects=new[]{new Rect(1608,2336,328,63),new Rect(1538,2399,258,49),new Rect(1485,2448,205,43),new Rect(1439,2491,159,53)};
            var positions=new[]{new Vector2(470,270),new Vector2(504,196),new Vector2(530,124),new Vector2(554,45)};
            for(var i=0;i<4;i++)
            {
                var index=i;var button=ZumaArt.Button(content,menu,rects[i],positions[i].x,positions[i].y,rects[i].width,rects[i].height,()=>selectDifficulty(index));
                button.Select(difficulty==ZumaSaveData.DifficultyIds[i]);
            }
            ZumaArt.Text(content,assets,"cancun10","BEST SCORE:",924,632,212,40,Color.white);
            ZumaArt.Text(content,assets,"cancun10",record.BestScore.ToString(),1140,632,140,40,ZumaArt.Gold);
            ZumaArt.Text(content,assets,"cancun10","BEST TIME:",924,686,212,40,Color.white);
            ZumaArt.Text(content,assets,"cancun10",FormatTime(record.BestTimeSeconds),1140,686,140,40,ZumaArt.Gold);
            UiFactory.Button(content,"‹ 返回大厅",24,24,174,56,back);
            UiFactory.Button(content,"玩法说明",210,24,174,56,help);
            if(resume!=null) StandardButton(content,"CONTINUE",940,40,276,64,resume);
        }
        public void ShowGame(ZumaLevelDefinition definition,ZumaSnapshot snapshot,Action pause,Action swap)
        {
            Clear();level=definition;loadedMap=level.Id;
            objects=assets.Texture("Images/gameobjects");frog=assets.Texture("Images/frog");
            var map=assets.Map(level.Id);var background=assets.Texture(map.background);
            ZumaArt.Image(content,"Level background",background,new Rect(0,0,background.width,background.height),0,0,1280,720);
            var field=ZumaArt.Rect(content,"Aim surface",0,52,1280,668);
            var hit=field.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            Touch=field.gameObject.AddComponent<ZumaTouchInput>();
            var end=level.Points[level.PathLength];
            finish=ZumaArt.Image(content,"Skull foundation",objects,new Rect(629,0,132,132),0,0,132,132);
            ZumaArt.Center(finish,end.X,end.Y,132,132);
            finishMouth=ZumaArt.Image(content,"Skull mouth",objects,new Rect(629,132,132,132),0,0,132,132);
            ZumaArt.Center(finishMouth,end.X,end.Y,132,132);
            foundation=FrogImage("Foundation",new Rect(162,162,162,162));
            body=FrogImage("Body",new Rect(0,0,162,162));
            tongue=FrogImage("Tongue",new Rect(162,0,162,162));
            current=ZumaArt.Image(content,"Current ball",objects,new Rect(0,1152,48,48),0,0,48,48);
            next=ZumaArt.Image(content,"Next ball",frog,new Rect(234,633,15,15),0,0,24,24);
            eyes=FrogImage("Eyes",new Rect(0,162,162,162));
            lower=ZumaArt.Rect(content,"Lower chain",0,0,1280,720);
            if(!string.IsNullOrEmpty(map.overlay))
            {
                var texture=assets.Texture(map.overlay);
                overlay=ZumaArt.Image(content,"Tunnel overlay",texture,new Rect(0,0,texture.width,texture.height),0,0,1280,720);
            }
            else overlay=null;
            upper=ZumaArt.Rect(content,"Upper chain",0,0,1280,720);
            projectiles=ZumaArt.Rect(content,"Projectiles",0,0,1280,720);
            trails=ZumaArt.Rect(content,"Effects",0,0,1280,720);
            coin=ZumaArt.Image(trails,"Treasure",objects,new Rect(340,0,54,54),0,0,54,54);
            var hud=assets.Texture("Images/gameinterface");
            // Source requests730rows fromy51, while actualbitmapendsat776: preserve clipped extent.
            ZumaArt.Image(content,"Original HUD",hud,new Rect(0,51,1280,725),0,-6,1280,725);
            score=ZumaArt.Text(content,assets,"cancun12",snapshot.Score.ToString().PadLeft(9),545,6,210,36,ZumaArt.Gold);
            stage=ZumaArt.Text(content,assets,"cancun12","lvl1-"+(Array.IndexOf(ZumaSaveData.MapIds,level.Id)+1),390,6,140,36,ZumaArt.Gold,true);
            gauge=ZumaArt.Image(content,"Progress",hud,new Rect(773,28,1,28),772,2,1,28);
            ZumaArt.Button(content,hud,new Rect(925,15,118,37),1128,4,118,44,pause);
            StandardButton(content,"SWAP",34,2,240,46,swap);
            phaseLabel=ZumaArt.Text(content,assets,"nativealienextended16","",700,520,520,100,Color.white,true);
            hint=UiFactory.Label(content,"按下拖动瞄准 · 松手发射 · SWAP 换球",300,675,680,32,18,new Color(1,1,.8f),TextAnchor.MiddleCenter);
            Render(snapshot,false);
        }
        private RawImage FrogImage(string name,Rect source)
        { return ZumaArt.Image(content,name,frog,source,0,0,162,162); }
        private RawImage Pool(List<RawImage> pool,int index,Transform parent,string name)
        {
            while(pool.Count<=index)pool.Add(ZumaArt.Image(parent,name,objects,new Rect(0,0,48,48),0,0,48,48));
            var image=pool[index];if(image.transform.parent!=parent)image.transform.SetParent(parent,false);
            image.gameObject.SetActive(true);image.color=Color.white;return image;
        }
        private static void HideRemaining(List<RawImage> pool,int used) { for(var i=used;i<pool.Count;i++)pool[i].gameObject.SetActive(false); }
        public void Render(ZumaSnapshot s,bool aiming)
        {
            if(level==null||score==null)return;
            score.SetText(s.Score.ToString().PadLeft(9));
            var difficulty=ZumaDifficulty.Get(s.DifficultyId);
            var width=Mathf.Clamp((float)((double)s.Score/difficulty.GaugeScore)*96,0,96);
            ZumaArt.SetSource(gauge,gauge.texture,new Rect(773,s.Score>=difficulty.GaugeScore?0:28,width,28));
            gauge.rectTransform.sizeDelta=new Vector2(width,28);
            var first=s.Balls.Length>0?s.Balls[0].PathPosition:0;
            var opening=Mathf.Clamp((int)(12*(first-level.PathLength*.8f)/(level.PathLength*.2f)),0,11);
            ZumaArt.SetSource(finishMouth,objects,new Rect(629,132*(1+opening),132,132));
            for(var i=0;i<s.Balls.Length;i++)
            {
                var ball=s.Balls[i];var layer=ball.DrawPriority?upper:lower;
                var image=Pool(balls,i,layer,"Chain ball");
                var shadow=Pool(shadows,i,layer,"Ball shadow");
                if(ball.Hidden){image.gameObject.SetActive(false);shadow.gameObject.SetActive(false);continue;}
                shadow.transform.SetSiblingIndex(0);ZumaArt.SetSource(shadow,objects,new Rect(134,2595,48,50));
                shadow.color=new Color(1,1,1,.5f);ZumaArt.Center(shadow,ball.X,ball.Y+16,48,50);
                if(ball.Exploding)
                {
                    ZumaArt.SetSource(image,objects,new Rect(395,Mathf.Clamp(ball.ExplosionTicks/2,0,15)*120,105,120));
                    image.color=BallTint(ball.Color);ZumaArt.Center(image,ball.X,ball.Y,105,120);
                    shadow.gameObject.SetActive(false);
                }
                else
                {
                    var f=((int)Mathf.Max(0,ball.PathPosition))%(ball.Color==0?47:50);
                    ZumaArt.SetSource(image,objects,new Rect(48*ball.Color,48*f,48,48));
                    image.color=new Color(1,1,1,ball.FadeTicks>0?1-ball.FadeTicks/17f:1);
                    ZumaArt.Center(image,ball.X,ball.Y,48,48,ball.Angle-90);
                }
            }
            HideRemaining(balls,s.Balls.Length);HideRemaining(shadows,s.Balls.Length);
            for(var i=0;i<s.Bullets.Length;i++)
            {
                var b=s.Bullets[i];var image=Pool(bullets,i,projectiles,"Fired ball");
                ZumaArt.SetSource(image,objects,new Rect(48*b.Color,1152,48,48));ZumaArt.Center(image,b.X,b.Y,48,48);
            }
            HideRemaining(bullets,s.Bullets.Length);
            for(var i=0;i<s.TrailParticles.Length;i++)
            {
                var p=s.TrailParticles[i];var image=Pool(particles,i,trails,"Trail");
                var intro=s.Phase=="intro";
                var frame=Mathf.Clamp(p.AgeTicks/2,0,15);
                ZumaArt.SetSource(image,objects,intro?new Rect(501,frame*26,26,26):new Rect(528,frame*130,100,130));
                ZumaArt.Center(image,p.X,p.Y,intro?26:150,intro?26:195);
            }
            HideRemaining(particles,s.TrailParticles.Length);
            var direction=new Vector2(s.Frog.DirectionX,s.Frog.DirectionY);
            var rotation=-Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg;
            var fx=level.FrogX-direction.x*s.Frog.RecoilTicks;var fy=level.FrogY-direction.y*s.Frog.RecoilTicks;
            ZumaArt.Center(foundation,fx,fy,162,162,rotation);ZumaArt.Center(body,fx,fy,162,162,rotation);
            var tongueShift=s.Phase=="intro"?24:s.Frog.RecoilTicks*4;
            ZumaArt.Center(tongue,fx+direction.x*(25-tongueShift),fy+direction.y*(25-tongueShift),162,162,rotation);
            ZumaArt.SetSource(current,objects,new Rect(s.Frog.CurrentColor*48,1152,48,48));
            ZumaArt.Center(current,fx+direction.x*(35-tongueShift),fy+direction.y*(35-tongueShift),48,48,rotation);
            ZumaArt.SetSource(next,frog,new Rect(234+15*s.Frog.NextColor,633,15,15));
            ZumaArt.Center(next,fx-direction.x*40,fy-direction.y*40,24,24,rotation);
            current.gameObject.SetActive(s.Phase!="intro");next.gameObject.SetActive(s.Phase!="intro");
            ZumaArt.SetSource(eyes,frog,new Rect(0,162+Mathf.Clamp(s.Frog.RecoilTicks/3,0,2)*162,162,162));
            ZumaArt.Center(eyes,fx,fy,162,162,rotation);
            var coinVisible=(s.Coin.Active&&(!s.Coin.Blinking||(s.Coin.TimerTicks/8)%2==0))||s.Coin.FadeTicks>0;
            coin.gameObject.SetActive(coinVisible);
            if(coinVisible&&s.Coin.PositionIndex>=0&&s.Coin.PositionIndex<level.Coins.Length)
            {
                var p=level.Coins[s.Coin.PositionIndex];var scale=s.Coin.FadeTicks>0?2.5f-s.Coin.FadeTicks*.1f:1;
                ZumaArt.SetSource(coin,objects,new Rect(340,54*((s.TotalTicks/4)%30),54,54));
                ZumaArt.Center(coin,p.X,p.Y,54*scale,54*scale);coin.color=new Color(1,1,1,s.Coin.FadeTicks>0?s.Coin.FadeTicks/15f:1);
            }
            const int aimDotCount=22;
            for(var i=0;i<(aiming?aimDotCount:0);i++)
            {
                var x=level.FrogX+direction.x*(78+i*14);
                var y=level.FrogY+direction.y*(78+i*14);
                var outline=Pool(dotShadows,i,projectiles,"Aim dot contrast edge");
                var dot=Pool(dots,i,projectiles,"Aim dot");
                ZumaArt.SetSource(outline,objects,new Rect(s.Frog.CurrentColor*48,1152,48,48));
                ZumaArt.SetSource(dot,objects,new Rect(s.Frog.CurrentColor*48,1152,48,48));
                ZumaArt.Center(outline,x,y,11,11);
                ZumaArt.Center(dot,x,y,8,8);
                outline.color=new Color(.04f,.04f,.04f,.7f-i*.02f);
                dot.color=new Color(1,1,1,.95f-i*.025f);
            }
            HideRemaining(dots,aiming?aimDotCount:0);
            HideRemaining(dotShadows,aiming?aimDotCount:0);
            phaseLabel.SetText(s.Phase=="intro"?"LEVEL "+(Array.IndexOf(ZumaSaveData.MapIds,level.Id)+1)+"\n"+level.DisplayName:s.Phase=="winOutro"?"ZUMA!":"");
            hint.gameObject.SetActive(s.PlayTicks<600&&s.Phase=="play");
        }
        public void Event(ZumaEvent ev)
        {
            if(ev.Score<=0||trails==null)return;
            if(floats.Count>=32){UnityEngine.Object.Destroy(floats[0].Text.gameObject);floats.RemoveAt(0);}
            var label=ZumaArt.Text(trails,assets,"cancunfloat14","+"+ev.Score+(ev.Combo>0?"\nCOMBO x"+ev.Combo:""),ev.X-100,ev.Y-50,200,80,BallTint(ev.Color),true);
            floats.Add(new Floating{Text=label,X=ev.X-100,Y=ev.Y-50});
        }
        public void Animate(float delta)
        {
            for(var i=floats.Count-1;i>=0;i--)
            {
                var f=floats[i];f.Age+=delta;
                if(f.Age>1.4f){UnityEngine.Object.Destroy(f.Text.gameObject);floats.RemoveAt(i);continue;}
                f.Text.rectTransform.anchoredPosition=new Vector2(f.X,-f.Y+f.Age*24);
                var c=f.Text.color;c.a=Mathf.Clamp01(1.4f-f.Age);f.Text.color=c;
            }
        }
        public RectTransform Dialog(string title,bool god=false,int columns=5)
        {
            CloseDialog();dialog=ZumaArt.Rect(root,"Dialog input blocker",0,0,1280,720);
            var dim=dialog.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.40f);dim.raycastTarget=true;
            var panel=ZumaArt.Panel(dialog,assets.Texture("Images/menu"),columns,4,god);
            ZumaArt.Text(panel,assets,"nativealienextended18",title,20,god?72:32,columns*121-40,60,Color.white,true);
            warning.transform.SetAsLastSibling();return panel;
        }
        public void HideAim()
        { HideRemaining(dots,0);HideRemaining(dotShadows,0); }
        public void CloseDialog()
        { if(dialog==null)return;dialog.gameObject.SetActive(false);UnityEngine.Object.Destroy(dialog.gameObject);dialog=null; }
        public void ShowHelp(Action close)
        {
            CloseDialog();
            dialog=ZumaArt.Rect(root,"Zuma help",0,0,1280,720);
            new ZumaHelpView(dialog,close);
            warning.transform.SetAsLastSibling();
        }
        public void StandardButton(Transform parent,string title,float x,float y,float width,float height,Action action)
        {
            var button=ZumaArt.Button(parent,assets.Texture("Images/menu"),new Rect(0,532,180,72),x,y,width,height,action,true);
            ZumaArt.Text(button.transform,assets,"cancun10",title,5,(height-30)/2,width-10,36,ZumaArt.Gold,true);
        }
        public void Chinese(Transform parent,string value,float x,float y,float w,float h,int size=22)
        { UiFactory.Label(parent,value,x,y,w,h,size,new Color(.95f,.95f,.86f),TextAnchor.MiddleCenter); }
        public void Toggle(Transform parent,string title,bool enabled,float x,float y,Action action)
        {
            var hit=ZumaArt.Rect(parent,"Toggle "+title,x,y,460,70);
            var image=hit.gameObject.AddComponent<Image>();image.color=Color.clear;image.raycastTarget=true;
            var button=hit.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>action());
            ZumaArt.Image(hit,"Checkbox",assets.Texture("Images/menu"),new Rect(enabled?0:75,462,75,69),0,0,75,69);
            ZumaArt.Text(hit,assets,"cancun10",title,100,22,340,36,Color.white);
        }
        public void Stats(ZumaSnapshot state,Action acknowledge)
        {
            var panel=Dialog(state.Phase=="won"?"STATS":"GAmE OvER",true,6);
            var names=new[]{"POINTS","COMBOS","COINS","MAX CHAIN","MAX COMBO","YOUR TIME","ACE TIME"};
            var values=new[]{state.Score.ToString(),state.TotalCombos.ToString(),state.TotalCoins.ToString(),state.MaxChainBonus.ToString(),state.MaxCombo.ToString(),FormatTime(state.PlayTicks/60),FormatTime(state.AceTimeSeconds)};
            for(var i=0;i<names.Length;i++)
            {
                var x=i<5?101:395;var y=142+28*(i<5?i:i-5);
                ZumaArt.Text(panel,assets,"cancun12",names[i],x,y,180,32,Color.white);
                ZumaArt.Text(panel,assets,"cancun12",values[i],x+185,y,145,32,new Color(1,.6f,1));
            }
            StandardButton(panel,"OK",66,362,594,72,acknowledge);
        }
        private static Color BallTint(int color)
        {
            switch(color){case 0:return new Color(0,.996f,.992f);case 1:return new Color(0,.98f,.267f);case 2:return new Color(.992f,.992f,.024f);case 3:return new Color(.976f,.435f,.298f);case 4:return new Color(.992f,.533f,.973f);default:return new Color(.992f,.918f,.733f);}
        }
        public static string FormatTime(long seconds) { return (seconds/60)+":"+(seconds%60).ToString("00"); }
        public void Dispose() { CloseDialog();if(content!=null)UnityEngine.Object.Destroy(content.gameObject); }
    }
}
