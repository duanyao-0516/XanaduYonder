using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;
using XanaduYonder.Core.AgeOfWar.Two;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarView
    {
        private readonly AgeOfWarAssets assets;
        private readonly RectTransform stage,world,actors,worldControls,ui,dynamicHud,overlay;
        private AgeOfWarSceneView scene,hud,buttons,pause,tooltip;
        private AgeOfWarImage background;
        private AgeOfWarTransition screenTransition;
        private AgeOfWarTwoMenuEffects menuEffects;
        private AgeOfWarWideHud wideHud;
        private RectTransform pauseButton;
        private Image pauseButtonBackground;
        private RectTransform menuHelpButton,menuSettingsButton;
        private Image menuHelpBackground,menuSettingsBackground;
        private bool continueAvailable;
        private readonly AgeOfWarHotspot menuHelpHit=new AgeOfWarHotspot{action="mobile-help",name="mobile-help",id=-210};
        private readonly AgeOfWarHotspot menuSettingsHit=new AgeOfWarHotspot{action="mobile-settings",name="mobile-settings",id=-211};
        private RectTransform saveWarningRoot;
        private Text saveWarningLabel;
        private readonly AgeOfWarHotspot saveWarningHit=new AgeOfWarHotspot{action="mobile-save-retry",name="save-warning",id=-201};
        private readonly AgeOfWarHotspot pauseButtonHit=new AgeOfWarHotspot{action="mobile-pause",name="mobile-pause",id=-200};
        private readonly int[] resourceIndices={-1,-1,-1};
        private readonly float[] resourceWidths=new float[3];
        private float logicalWidth,buttonSourceX;
        internal float LogicalWidth{get{return logicalWidth;}}
        internal float PauseOffsetX{get{return pause==null?(logicalWidth-assets.Stage.width)/2:pause.Root.anchoredPosition.x;}}
        private double sceneTime;
        internal bool Transitioning{get{return screenTransition!=null;}}
        private readonly Dictionary<long,AgeOfWarImage> objects=new Dictionary<long,AgeOfWarImage>();
        private readonly HashSet<long> present=new HashSet<long>();
        private readonly List<long> removed=new List<long>();
        private AgeOfWarFrame frame;
        private IAgeOfWarBattle battle;
        private AgeOfWarOneHud oneHud;
        private AgeOfWarTwoHud twoHud;
        private AgeOfWarTwoWorldControls twoControls;
        private AgeOfWarSceneView tutorial;
        private CanvasGroup tutorialAlpha;
        private bool tutorialActive;
        private float tutorialElapsed;
        internal bool HasTutorial{get{return tutorialActive;}}
        private readonly Dictionary<long,AgeOfWarSceneView> textObjects=new Dictionary<long,AgeOfWarSceneView>();
        private readonly Dictionary<long,AgeOfWarHealthBar> healthObjects=new Dictionary<long,AgeOfWarHealthBar>();
        private string mode="main",titleOverride;
        internal string MenuMode{get{return mode;}}
        private int buttonAge;
        private bool sell,pointerVisible,pointerDown;
        private Vector2 pointer;
        internal string SceneKey{get;private set;}
        internal bool Selling { get{return sell;} }
        internal float CameraX,CameraY;
        internal Action<AgeOfWarHotspot> Activated;
        internal Action<AgeOfWarHotspot> Hovered;
        internal AgeOfWarView(RectTransform parent,AgeOfWarAssets source,bool startMenu=true)
        {
            assets=source;stage=parent;logicalWidth=Mathf.Max(source.Stage.width,parent.rect.width);world=AgeOfWarDrawing.Rect(stage,"World");
            background=new AgeOfWarImage(world,assets,"Background");actors=AgeOfWarDrawing.Rect(world,"Actors");
            worldControls=AgeOfWarDrawing.Rect(stage,"World controls");ui=AgeOfWarDrawing.Rect(stage,"Original UI");dynamicHud=AgeOfWarDrawing.Rect(stage,"Dynamic HUD");overlay=AgeOfWarDrawing.Rect(stage,"Overlay");
            if(startMenu)ShowScene("menu");else world.gameObject.SetActive(false);
        }
        internal void ShowScene(string key)
        {
            ClearUi();sceneTime=0;world.gameObject.SetActive(false);scene=new AgeOfWarSceneView(ui,assets,key);SceneKey=key;frame=null;
            if(assets.Edition==2){menuEffects=new AgeOfWarTwoMenuEffects(scene);if(key=="difficulty")scene.Named("btnInsane",false);}
            if(key=="menu")
            {
                scene.SetTextButtonEnabled("mobile-continue",continueAvailable);
                CreateMenuControls();
            }
            ApplyLayout();
        }
        internal void SetContinueAvailable(bool value)
        {
            continueAvailable=value;
            if(scene!=null&&SceneKey=="menu")scene.SetTextButtonEnabled("mobile-continue",value);
            if(pointerVisible)ApplyHover();
        }
        private void ClearUi()
        {
            if(menuEffects!=null){menuEffects.Dispose();menuEffects=null;}
            if(twoControls!=null){twoControls.Dispose();twoControls=null;}
            AgeOfWarDrawing.Clear(ui);AgeOfWarDrawing.Clear(dynamicHud);AgeOfWarDrawing.Clear(overlay);scene=hud=buttons=pause=tooltip=tutorial=null;oneHud=null;twoHud=null;wideHud=null;pauseButton=null;pauseButtonBackground=null;tutorialActive=false;
            menuHelpButton=menuSettingsButton=null;menuHelpBackground=menuSettingsBackground=null;
        }
        internal void StartBattle(AgeOfWarFrame value,IAgeOfWarBattle source=null)
        {
            ClearUi();battle=source;SceneKey="battle";titleOverride=null;mode="main";sell=false;frame=value;buttonAge=0;
            foreach(var image in objects.Values)UnityEngine.Object.Destroy(image.Root.gameObject);objects.Clear();foreach(var text in textObjects.Values)text.Dispose();textObjects.Clear();foreach(var bar in healthObjects.Values)bar.Dispose();healthObjects.Clear();
            world.gameObject.SetActive(true);hud=new AgeOfWarSceneView(ui,assets,"battle-ui");
            if(assets.Edition==1)
            {
                hud.Named("bf1",false);hud.Named("bt1",false);hud.Named("m1",false);
            }
            else hud.Named("buttons",false);
            wideHud=new AgeOfWarWideHud(hud,assets);
            for(int i=0;i<resourceIndices.Length;i++)resourceIndices[i]=-1;
            if(assets.Edition==2)for(int i=0;i<hud.Texts.Count;i++)
            {int index=ResourceIndex(hud.Definition.texts[i].name);if(index>=0)resourceIndices[index]=i;}
            CreatePauseButton();
            if(source is AgeOfWarOneBattle)oneHud=new AgeOfWarOneHud(dynamicHud,assets);
            if(source is AgeOfWarTwoBattle){twoHud=new AgeOfWarTwoHud(dynamicHud,assets,overlay);twoControls=new AgeOfWarTwoWorldControls(worldControls,assets);}
            ApplyLayout();
            Render(value);
        }
        internal void SetViewportWidth(float width)
        {
            logicalWidth=Mathf.Max(assets.Stage.width,width);ApplyLayout();
            if(frame!=null&&SceneKey=="battle")Render(frame);
        }
        private void ApplyLayout()
        {
            float extra=logicalWidth-assets.Stage.width;
            if(scene!=null){scene.Root.anchoredPosition=new Vector2(extra/2,0);FillSceneBackground(scene,extra);}
            if(pause!=null)
            {
                pause.Root.anchoredPosition=new Vector2(assets.Edition==1?logicalWidth/2:extra/2,assets.Edition==1?-225:0);
                FillSceneBackground(pause,extra);
            }
            if(tutorial!=null){tutorial.Root.anchoredPosition=new Vector2(extra/2,0);FillSceneBackground(tutorial,extra);}
            if(hud!=null)
            {
                for(int i=0;i<hud.Images.Count;i++)
                {
                    var layer=hud.Definition.layers[i];bool right=assets.Edition==1?layer.x>=430:layer.x>=230;
                    hud.SetLayerOffset(i,new Vector2(right?extra:0,0));
                    if(assets.Edition==2&&layer.key=="ui/hud/9")hud.Images[i].Root.localScale=new Vector3(logicalWidth/assets.Stage.width,layer.scaleY,1);
                }
                for(int i=0;i<hud.Texts.Count;i++)
                {
                    var field=hud.Definition.texts[i];if(assets.Edition==2&&ResourceIndex(field.name)>=0)continue;
                    bool right=assets.Edition==1?field.x>=430:field.name=="base2Hp";
                    hud.SetTextOffset(i,new Vector2(right?extra:0,0));
                }
                if(assets.Edition==2)LayoutTwoResources();
            }
            if(buttons!=null)
            {
                if(assets.Edition==1)buttons.Root.anchoredPosition=new Vector2(buttonSourceX+extra,-40.5f);
                else for(int i=0;i<buttons.Images.Count;i++)buttons.SetLayerOffset(i,new Vector2(buttons.Definition.layers[i].x>=230?extra:0,0));
            }
            if(wideHud!=null)wideHud.SetWidth(extra);
            if(oneHud!=null)oneHud.SetViewportWidth(logicalWidth);
            if(twoHud!=null)twoHud.SetViewportWidth(logicalWidth);
            LayoutMenuControls();
            LayoutPauseButton();
            LayoutSaveWarning();
        }
        internal void SaveWarning(string message,bool retry=true)
        {
            if(string.IsNullOrEmpty(message)){if(saveWarningRoot!=null)saveWarningRoot.gameObject.SetActive(false);return;}
            if(saveWarningRoot==null)
            {
                saveWarningRoot=UiFactory.Panel(stage,"Save notice",0,0,560,58,new Color(.12f,.15f,.13f,.97f)).rectTransform;
                saveWarningLabel=UiFactory.Label(saveWarningRoot,"",12,4,536,50,14,Color.white,TextAnchor.MiddleCenter);
                saveWarningLabel.supportRichText=false;
            }
            saveWarningHit.action=retry?"mobile-save-retry":"mobile-save-dismiss";
            saveWarningLabel.text=message+(retry?"\n点按重试":"\n点按关闭");
            saveWarningRoot.gameObject.SetActive(true);LayoutSaveWarning();
        }
        private void LayoutSaveWarning()
        {
            if(saveWarningRoot==null||!saveWarningRoot.gameObject.activeSelf)return;
            float width=Mathf.Min(560,logicalWidth-24),top=assets.Stage.height-92;
            saveWarningRoot.sizeDelta=new Vector2(width,58);saveWarningRoot.anchoredPosition=new Vector2((logicalWidth-width)/2,-top);
            saveWarningLabel.rectTransform.sizeDelta=new Vector2(width-24,50);
            saveWarningHit.x=(logicalWidth-width)/2;saveWarningHit.y=top;saveWarningHit.w=width;saveWarningHit.h=58;
            saveWarningRoot.SetAsLastSibling();
        }
        private static int ResourceIndex(string name)
        {return name=="popTXT"?0:name=="xpTXT"?1:name=="moneyTXT"?2:-1;}
        private void LayoutTwoResources()
        {
            const float padding=4,gap=18;
            float total=gap*2;
            for(int slot=0;slot<resourceIndices.Length;slot++)
            {
                int index=resourceIndices[slot];if(index<0)return;
                var text=hud.Texts[index];text.horizontalOverflow=HorizontalWrapMode.Overflow;
                text.alignment=TextAnchor.UpperLeft;text.resizeTextForBestFit=false;
                resourceWidths[slot]=Mathf.Ceil(text.preferredWidth)+padding*2;
                total+=resourceWidths[slot];
            }
            float controlRight=0;
            for(int i=0;i<hud.Images.Count;i++)
            {
                string name=hud.Definition.layers[i].name;
                if((name!="btnPause"&&name!="btnGameSpeed")||!hud.Images[i].Root.gameObject.activeSelf)continue;
                var image=hud.Images[i];var rect=image.Image.rectTransform;
                controlRight=Mathf.Max(controlRight,image.Root.anchoredPosition.x+(rect.anchoredPosition.x+rect.sizeDelta.x)*image.Root.localScale.x);
            }
            // Keep the whole group centred; only unusually long values move it enough
            // to clear the original bottom-left controls. Glyph size and scale stay intact.
            float left=Mathf.Max(controlRight+12,(logicalWidth-total)/2);
            left=Mathf.Min(left,logicalWidth-total-12);
            for(int slot=0;slot<resourceIndices.Length;slot++)
            {
                int index=resourceIndices[slot];var field=hud.Definition.texts[index];var text=hud.Texts[index];
                text.rectTransform.sizeDelta=new Vector2(resourceWidths[slot]-padding*2,Mathf.Max(1,field.h-4));
                hud.SetTextOffset(index,new Vector2(left+padding-(field.x+2),0));
                left+=resourceWidths[slot]+gap;
            }
        }
        private void CreatePauseButton()
        {
            pauseButton=AgeOfWarDrawing.Rect(ui,"Pause battle",0,0,76,30);
            pauseButtonBackground=pauseButton.gameObject.AddComponent<Image>();
            pauseButtonBackground.color=new Color(.12f,.09f,.04f,.9f);pauseButtonBackground.raycastTarget=false;
            var border=pauseButton.gameObject.AddComponent<Outline>();border.effectColor=new Color(.8f,.65f,.33f,.9f);border.effectDistance=new Vector2(1,-1);
            AgeOfWarDrawing.Text(pauseButton,assets,new AgeOfWarTextField{name="mobile-pause",variable="",initial="PAUSE",font=assets.Edition==1?"font/one/962":"font/windows/2",
                size=assets.Edition==1?14:10,x=0,y=2,w=76,h=26,color="#fff2cf",align=2,bold=true});
        }
        private void CreateMenuControls()
        {
            menuHelpBackground=CreateMenuIcon("Menu help",UiIconKind.Book);
            menuSettingsBackground=CreateMenuIcon("Menu settings",UiIconKind.Settings);
            menuHelpButton=menuHelpBackground.rectTransform;menuSettingsButton=menuSettingsBackground.rectTransform;
        }
        private Image CreateMenuIcon(string name,UiIconKind kind)
        {
            // Match UiFactory.IconButton's artwork, while Page remains the sole input dispatcher.
            var image=UiFactory.Panel(ui,name,0,0,40,40,UiFactory.Hex("E7EDE4"));image.raycastTarget=false;
            var iconRect=UiFactory.Rect(image.transform,name+" icon",0,0,22,22);UiFactory.Center(iconRect);
            var icon=iconRect.gameObject.AddComponent<UiIconGraphic>();icon.Kind=kind;icon.color=UiFactory.Ink;icon.raycastTarget=false;
            return image;
        }
        private void LayoutMenuControls()
        {
            if(menuHelpButton==null||menuSettingsButton==null)return;
            bool visible=SceneKey=="menu";
            menuHelpButton.gameObject.SetActive(visible);menuSettingsButton.gameObject.SetActive(visible);
            if(!visible)return;
            float settingsX=logicalWidth-52,helpX=settingsX-50;
            menuHelpButton.anchoredPosition=new Vector2(helpX,-12);menuSettingsButton.anchoredPosition=new Vector2(settingsX,-12);
            menuHelpHit.x=helpX;menuHelpHit.y=12;menuHelpHit.w=menuHelpHit.h=40;
            menuSettingsHit.x=settingsX;menuSettingsHit.y=12;menuSettingsHit.w=menuSettingsHit.h=40;
        }
        private void HoverMenuControls(AgeOfWarHotspot hit,bool down)
        {
            Color normal=UiFactory.Hex("E7EDE4"),highlight=UiFactory.Hex(down?"B7C9B4":"D6E2D2");
            if(menuHelpBackground!=null)menuHelpBackground.color=hit==menuHelpHit?highlight:normal;
            if(menuSettingsBackground!=null)menuSettingsBackground.color=hit==menuSettingsHit?highlight:normal;
        }
        private void LayoutPauseButton()
        {
            if(pauseButton==null)return;
            bool visible=SceneKey=="battle"&&frame!=null&&!frame.Paused&&!frame.Ended&&!tutorialActive;
            pauseButton.gameObject.SetActive(visible);if(!visible)return;
            float gapLeft=assets.Edition==1?410:230,gapRight=logicalWidth-(assets.Edition==1?240:490);
            bool fits=gapRight-gapLeft>=92;
            float x=fits?(gapLeft+gapRight-76)/2:(logicalWidth-76)/2;
            float y=fits?20:assets.Edition==1?112:80;
            pauseButton.anchoredPosition=new Vector2(x,-y);
            pauseButtonHit.x=x;pauseButtonHit.y=y;pauseButtonHit.w=76;pauseButtonHit.h=30;
        }
        private void HoverPauseButton(AgeOfWarHotspot hit,bool down)
        {
            if(pauseButtonBackground!=null)pauseButtonBackground.color=hit==pauseButtonHit
                ?(down?new Color(.2f,.14f,.06f,.95f):new Color(.3f,.22f,.1f,.95f))
                :new Color(.12f,.09f,.04f,.9f);
        }
        private void FillSceneBackground(AgeOfWarSceneView source,float extra)
        {
            for(int i=0;i<Math.Min(2,source.Images.Count);i++)
            {
                var layer=source.Definition.layers[i];AgeOfWarSpriteFrame pixels;
                if(!assets.TryGetFrame(layer.key,layer.action,layer.frame,out pixels))continue;
                float left=layer.x-pixels.Origin.x*layer.scaleX,top=layer.y-pixels.Origin.y*layer.scaleY;
                if(left>1||top>1||left+pixels.Size.x*layer.scaleX<assets.Stage.width-1||top+pixels.Size.y*layer.scaleY<assets.Stage.height-1)continue;
                source.SetLayerOffset(i,new Vector2(-extra/2,0));
                source.Images[i].Root.localScale=new Vector3(layer.scaleX*logicalWidth/assets.Stage.width,layer.scaleY,1);
            }
        }
        private void RenderBackground(string key)
        {
            AgeOfWarSpriteFrame pixels;
            if(!assets.TryGetFrame(key,"idle",0,out pixels))return;
            double scale=Math.Max(1,logicalWidth/pixels.Size.x),x=0;
            double left=CameraX-pixels.Origin.x*scale,right=left+pixels.Size.x*scale;
            if(left>0)x=-left;else if(right<logicalWidth)x=logicalWidth-right;
            background.Set(key,"idle",0,x,0,scale,1);
        }
        internal void Title(string title){titleOverride=title;}
        internal void Menu(string value){titleOverride=null;mode=value;sell=value=="sell";buttonAge=0;if(frame!=null)Render(frame);}
        internal void Render(AgeOfWarFrame value)
        {
            frame=value;if(SceneKey!="battle")return;
            world.anchoredPosition=new Vector2(CameraX,-CameraY);
            RenderBackground("background/"+(assets.Edition==1?1:Math.Max(value.Age,value.EnemyAge)));
            present.Clear();
            foreach(var visual in value.Visuals.OrderBy(v=>v.Depth))
            {
                present.Add(visual.InstanceId);
                if(!string.IsNullOrEmpty(visual.TextScene))
                {
                    AgeOfWarSceneView text;
                    if(!textObjects.TryGetValue(visual.InstanceId,out text)){text=new AgeOfWarSceneView(actors,assets,visual.TextScene);text.Root.gameObject.AddComponent<CanvasGroup>();textObjects.Add(visual.InstanceId,text);}
                    text.Value(visual.TextField,visual.Text);text.Animate((int)visual.Frame);text.Root.anchoredPosition=new Vector2((float)visual.X,-(float)visual.Y);text.Root.localScale=new Vector3((float)visual.ScaleX,(float)visual.ScaleY,1);
                    text.Root.GetComponent<CanvasGroup>().alpha=(float)visual.Alpha;text.Root.SetAsLastSibling();continue;
                }
                AgeOfWarImage image;
                if(!objects.TryGetValue(visual.InstanceId,out image)){image=new AgeOfWarImage(actors,assets,visual.Clip);objects.Add(visual.InstanceId,image);}
                image.Set(visual.Clip,visual.Animation,visual.Frame,visual.X,visual.Y,visual.ScaleX,visual.ScaleY,visual.Rotation,visual.Alpha);
                image.Root.SetAsLastSibling();
                if(assets.Edition==2&&visual.HealthFraction>=0)
                {
                    AgeOfWarHealthBar bar;if(!healthObjects.TryGetValue(visual.InstanceId,out bar)){bar=new AgeOfWarHealthBar(actors);healthObjects.Add(visual.InstanceId,bar);}
                    bar.Render(visual);
                }
            }
            removed.Clear();foreach(var item in objects)if(!present.Contains(item.Key))removed.Add(item.Key);
            foreach(var id in removed){UnityEngine.Object.Destroy(objects[id].Root.gameObject);objects.Remove(id);}
            removed.Clear();foreach(var item in textObjects)if(!present.Contains(item.Key))removed.Add(item.Key);foreach(var id in removed){textObjects[id].Dispose();textObjects.Remove(id);}
            removed.Clear();foreach(var item in healthObjects)if(!present.Contains(item.Key))removed.Add(item.Key);foreach(var id in removed){healthObjects[id].Dispose();healthObjects.Remove(id);}
            if(assets.Edition==1)RenderOne();else RenderTwo();
            if(oneHud!=null){oneHud.Selling=sell;oneHud.Render((AgeOfWarOneBattle)battle,value,CameraX,CameraY);}
            if(twoControls!=null)twoControls.Render(value,sell,CameraX,CameraY);
            if(twoHud!=null){twoHud.CoveredByCancel=sell;twoHud.Root.gameObject.SetActive(!value.Paused);twoHud.Render((AgeOfWarTwoBattle)battle,value);}
            if(value.Paused&&pause==null&&!tutorialActive)
            {pause=new AgeOfWarSceneView(overlay,assets,"paused",assets.Edition==1?325:0,assets.Edition==1?225:0);}
            else if(!value.Paused&&pause!=null){pause.Dispose();pause=null;}
            if(twoHud!=null)twoHud.TooltipRoot.SetAsLastSibling();
            if(assets.Edition==1)hud.Root.gameObject.SetActive(!value.Paused);
            ApplyLayout();
            if(pointerVisible)ApplyHover();
        }
        private void RenderOne()
        {
            var pending=frame.PendingTurretId>0;
            string selected=pending?"build":sell?"sell":mode;
            if(buttonAge!=frame.Age)
            {
                if(buttons!=null)buttons.Dispose();buttons=null;
                if(selected=="main")buttons=new AgeOfWarSceneView(ui,assets,"one-main/1",469.5f,40.5f);
                else if(selected=="units")buttons=new AgeOfWarSceneView(ui,assets,"one-units/"+frame.Age,442,40.5f);
                else if(selected=="turrets")buttons=new AgeOfWarSceneView(ui,assets,"one-turrets/"+frame.Age,433,40.5f);
                if(buttons!=null)buttonSourceX=buttons.Root.anchoredPosition.x;
                buttonAge=frame.Age;
            }
            if(buttons!=null)buttons.Root.gameObject.SetActive(!pending&&!sell&&!frame.Paused);
            hud.Named("btc",pending);hud.Named("stc",sell);
            hud.Value("_root.cash",frame.Gold.ToString());hud.Value("_root.xp",frame.Experience.ToString());
            string title=selected=="sell"?"Sell a turret":"Menu"+(selected=="main"?"":selected=="units"?" - Units":" - Turrets");
            title=titleOverride??title;hud.Value("menu_text",title);hud.Value("mt",title);
            if(battle is AgeOfWarOneBattle)hud.Value("bdesc",((AgeOfWarOneBattle)battle).CaptureTooltip(AgeOfWarAction.Train,0)["bdesc"]);
            for(int i=0;i<hud.Images.Count;i++)
            {
                var l=hud.Definition.layers[i];
                if(l.name=="prog")hud.Images[i].Root.localScale=new Vector3((float)frame.TrainingProgress,1,1);
                int index;if(l.name.StartsWith("t")&&int.TryParse(l.name.Substring(1),out index))hud.Layer(i,true,index<=frame.TrainingQueue.Length?frame.TrainingQueue[index-1]:0);
                if(l.key=="ui/one/78")hud.Layer(i,oneHud==null,frame.Age-1);
            }
        }
        private static bool Offered(AgeOfWarOffer[] offers,int id){foreach(var offer in offers)if(offer.Id==id)return true;return false;}
        private void RenderTwo()
        {
            if(buttons==null)buttons=new AgeOfWarSceneView(ui,assets,"buttons/1",10,19);
            for(int i=0;i<buttons.Images.Count;i++)
            {
                var name=buttons.Definition.layers[i].name;bool visible=true;int id;
                if(name.StartsWith("btnUP")&&int.TryParse(name.Substring(5),out id))visible=Offered(frame.Upgrades,id);
                else if(name.StartsWith("btnU")&&int.TryParse(name.Substring(4),out id))visible=Offered(frame.Units,id);
                else if(name.StartsWith("btnT")&&int.TryParse(name.Substring(4),out id))visible=Offered(frame.Turrets,id);
                else if(name.StartsWith("btnS")&&int.TryParse(name.Substring(4),out id))visible=id==frame.Age;
                if(name=="btnCancel")visible=frame.PendingTurretId>0||sell;

                buttons.Layer(i,visible);
            }
            hud.Value("moneyTXT",frame.Gold+" Gold");hud.Value("xpTXT",frame.Experience+" Xp");hud.Value("popTXT",frame.Population+" / 10 Pop");
            hud.Value("base1Hp",Math.Round(frame.PlayerHealth)+"/"+Math.Round(frame.PlayerMaxHealth));
            hud.Value("base2Hp",Math.Round(frame.EnemyHealth)+"/"+Math.Round(frame.EnemyMaxHealth));
            for(int i=0;i<hud.Images.Count;i++)
            {
                var name=hud.Definition.layers[i].name;
                if(name=="base1Bar")hud.Images[i].Root.localScale=new Vector3((float)Math.Max(0,frame.PlayerHealth/frame.PlayerMaxHealth),1,1);
                if(name=="base2Bar")hud.Images[i].Root.localScale=new Vector3((float)Math.Max(0,frame.EnemyHealth/frame.EnemyMaxHealth),1,1);
                if(name=="btnGameSpeed")hud.Layer(i,true,frame.Speed-1);
            }
        }
        internal AgeOfWarHotspot Hit(Vector2 point)
        {
            if(saveWarningRoot!=null&&saveWarningRoot.gameObject.activeSelf&&new Rect(saveWarningHit.x,saveWarningHit.y,saveWarningHit.w,saveWarningHit.h).Contains(point))return saveWarningHit;
            if(Transitioning)return null;
            if(SceneKey=="menu")
            {
                if(menuSettingsButton!=null&&menuSettingsButton.gameObject.activeSelf&&new Rect(menuSettingsHit.x,menuSettingsHit.y,menuSettingsHit.w,menuSettingsHit.h).Contains(point))return menuSettingsHit;
                if(menuHelpButton!=null&&menuHelpButton.gameObject.activeSelf&&new Rect(menuHelpHit.x,menuHelpHit.y,menuHelpHit.w,menuHelpHit.h).Contains(point))return menuHelpHit;
            }
            if(tutorialActive)return new AgeOfWarHotspot{action="dismiss-tutorial"};
            if(pause!=null)
            {
                var pausedHit=pause.Hit(point);
                return pausedHit??(assets.Edition==1?new AgeOfWarHotspot{action="mobile-resume"}:null);
            }
            if(scene!=null)return scene.Hit(point);
            if(frame==null)return null;
            if(pauseButton!=null&&pauseButton.gameObject.activeSelf&&new Rect(pauseButtonHit.x,pauseButtonHit.y,pauseButtonHit.w,pauseButtonHit.h).Contains(point))return pauseButtonHit;
            var h=buttons==null||!buttons.Root.gameObject.activeSelf?null:buttons.Hit(point);
            if(h!=null)
            {
                if(assets.Edition==2)
                {
                    if(h.action=="btnEvolve"&&frame.Age==7)return null;
                    if(h.action=="btnAddTower"&&frame.NextTurretSlotCost==0)return null;
                    if(h.action=="btnSellTurret"){bool any=false;foreach(var slot in frame.TurretSlots)if(!slot.Enemy&&slot.TurretId!=0)any=true;if(!any)return null;}
                }
                return h;
            }
            h=hud.Hit(point);if(h!=null)return h;

            if(oneHud!=null){var original=oneHud.Hit(point);if(original!=null)return original;}
            if(twoControls!=null){var original=twoControls.Hit(point);if(original!=null)return original;}
            return null;
        }
        internal void Pointer(Vector2 point,bool down)
        {
            pointer=point;pointerDown=down;pointerVisible=true;
            if(twoHud!=null)twoHud.Pointer(point);
            if(oneHud!=null)oneHud.Pointer(point,true,down);
            if(twoControls!=null)twoControls.Pointer(point,down);
            if(battle is AgeOfWarTwoBattle)
            {
                long hovered=0;
                if(frame!=null&&!frame.Paused&&point.y>75&&point.y<425)
                    for(int i=frame.Visuals.Length-1;i>=0;i--){var v=frame.Visuals[i];if(v.HitWidth>0&&new Rect((float)(v.X-v.HitWidth/2)+CameraX,(float)(v.Y-v.HitHeight)+CameraY,(float)v.HitWidth,(float)v.HitHeight).Contains(point)){hovered=v.InstanceId;break;}}
                ((AgeOfWarTwoBattle)battle).HoverUnit(hovered);
            }
            var hit=Hit(point);if(scene!=null)scene.Hover(hit,down);if(buttons!=null)buttons.Hover(hit,down);if(hud!=null)hud.Hover(hit,down);if(pause!=null)pause.Hover(hit,down);
            HoverPauseButton(hit,down);
            HoverMenuControls(hit,down);
            if(menuEffects!=null)menuEffects.Hover(hit);
            Hovered?.Invoke(hit);
        }
        private void ApplyHover()
        {
            var hit=Hit(pointer);if(scene!=null)scene.Hover(hit,pointerDown);if(buttons!=null)buttons.Hover(hit,pointerDown);if(hud!=null)hud.Hover(hit,pointerDown);if(pause!=null)pause.Hover(hit,pointerDown);
            HoverPauseButton(hit,pointerDown);
            HoverMenuControls(hit,pointerDown);
        }
        internal void Click(Vector2 point){var hit=Hit(point);if(hit!=null)Activated?.Invoke(hit);}
        internal void Tooltip(string kind,Dictionary<string,string> values,float x,float y)
        {
            if(assets.Edition==1){if(hud!=null&&values!=null)foreach(var pair in values)hud.Value(pair.Key,pair.Value);return;}
            if(tooltip!=null){tooltip.Dispose();tooltip=null;}
            if(string.IsNullOrEmpty(kind)||values==null||frame==null||frame.Paused)return;
            tooltip=new AgeOfWarSceneView(overlay,assets,"window/"+kind,x,y);
            foreach(var pair in values)tooltip.Value(pair.Key,pair.Value);
        }
        internal string PauseSliderAt(Vector2 point)
        {
            if(pause==null)return "";
            point-=new Vector2(pause.Root.anchoredPosition.x,-pause.Root.anchoredPosition.y);
            for(int i=0;i<pause.Images.Count;i++)
            {
                var layer=pause.Definition.layers[i];string kind;
                switch(layer.name){case "sliderQual":kind="quality";break;case "sliderFPS":kind="fps";break;case "sliderSound":kind="sound";break;case "sliderMusic":kind="music";break;default:continue;}
                var position=pause.Images[i].Root.anchoredPosition;
                // HUD symbol308/shape307 has original bounds [-6.25,-6.25,6.25,6.25].
                if(new Rect(position.x-6.25f,-position.y-6.25f,12.5f,12.5f).Contains(point))return kind;
            }
            return "";
        }
        internal void PauseValues(float music,float sound,int fps,string quality,bool showDamage,float qualityX=float.NaN,float fpsX=float.NaN)
        {
            if(pause==null)return;
            pause.Value("musicTXT",Mathf.RoundToInt(music*100)+"%");pause.Value("soundTXT",Mathf.RoundToInt(sound*100)+"%");pause.Value("fpsTXT",fps.ToString());pause.Value("qualityTXT",quality);
            for(int i=0;i<pause.Images.Count;i++){var l=pause.Definition.layers[i];float x=l.x;if(l.name=="sliderQual")x=float.IsNaN(qualityX)?(quality=="LOW"?306:quality=="MEDIUM"?376:446):qualityX;else if(l.name=="sliderFPS")x=float.IsNaN(fpsX)?306+(fps-25)/275f*140:fpsX;else if(l.name=="sliderSound")x=306+sound*140;else if(l.name=="sliderMusic")x=306+music*140;else if(l.name=="btnShowDamage"){pause.Layer(i,true,showDamage?1:0);continue;}else continue;pause.Images[i].Root.anchoredPosition=new Vector2(x,-l.y);}
        }
        internal void ShowTutorial(){tutorial=new AgeOfWarSceneView(overlay,assets,"instructions");tutorialAlpha=tutorial.Root.gameObject.AddComponent<CanvasGroup>();tutorialActive=true;if(pause!=null){pause.Dispose();pause=null;}ApplyLayout();}
        internal void DismissTutorial(){tutorialActive=false;tutorialElapsed=0;}
        internal void Event(AgeOfWarEvent item){if(twoHud!=null&&item.Kind=="error")twoHud.ShowError(item.Message);}
        internal void HoverAction(AgeOfWarAction action,int id,Vector2 point){if(twoHud!=null)twoHud.ShowTooltip((AgeOfWarTwoBattle)battle,action,id,point);}
        internal void ClearHover(){if(twoHud!=null)twoHud.HideTooltip();if(oneHud!=null)hud.Value("desc","");}
        internal void BeginTransition(bool fade)
        {
            if(assets.Edition!=2)return;
            if(screenTransition!=null){screenTransition.Dispose();screenTransition=null;}
            int width=Mathf.RoundToInt(logicalWidth);
            var snapshot=AgeOfWarSnapshot.Capture(stage,width,assets.Stage.height);
            screenTransition=new AgeOfWarTransition(stage,snapshot,width,assets.Stage.height,fade);
        }
        internal void Advance(float dt)
        {
            if(twoHud!=null&&frame!=null)twoHud.Advance(dt,frame.Speed);
            if(tutorial!=null&&!tutorialActive)
            {
                tutorialElapsed+=dt;float remaining=Mathf.Max(0,1-tutorialElapsed/.5f);
                tutorialAlpha.alpha=remaining*remaining;
                if(remaining==0){tutorial.Dispose();tutorial=null;}
            }
            if(menuEffects!=null)menuEffects.Render();
            sceneTime+=dt;if(scene!=null)scene.Animate((int)(sceneTime*assets.Stage.fps));
            ApplyLayout();
            if(screenTransition!=null){screenTransition.Advance(dt);if(screenTransition.Done){screenTransition.Dispose();screenTransition=null;}}
        }
        internal void HidePointer(){pointerVisible=false;HoverPauseButton(null,false);HoverMenuControls(null,false);if(scene!=null)scene.Hover(null,false);if(buttons!=null)buttons.Hover(null,false);if(hud!=null)hud.Hover(null,false);if(pause!=null)pause.Hover(null,false);if(menuEffects!=null)menuEffects.Hover(null);if(twoControls!=null)twoControls.HidePointer();if(oneHud!=null)oneHud.Pointer(Vector2.zero,false);if(battle is AgeOfWarTwoBattle)((AgeOfWarTwoBattle)battle).HoverUnit(0);ClearHover();}
        internal void Dispose(){if(menuEffects!=null){menuEffects.Dispose();menuEffects=null;}if(screenTransition!=null){screenTransition.Dispose();screenTransition=null;}AgeOfWarDrawing.Clear(stage);objects.Clear();textObjects.Clear();healthObjects.Clear();}
    }
}
