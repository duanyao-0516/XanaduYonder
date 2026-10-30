using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.Tetris;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Tetris;
using Object = UnityEngine.Object;
namespace XanaduYonder.EditorTools
{
    [InitializeOnLoad]
    public static class TetrisUiChecks
    {
        private const string Pending = "Xanadu.TetrisChecks.Pending";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int checks;
        private static string output;
        static TetrisUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }
        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            new GameObject("Tetris UI runner").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private static IEnumerator Execute()
        {
            yield return null;
            int exit = 0; output = Path.GetFullPath("artifacts/validation/tetris-ui");
            Directory.CreateDirectory(output); Report.Clear(); checks = 0;
            var oldFont = UiFactory.Font;
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled CJK font available");
                foreach (int height in new[] { 784, 688 })
                using (var fixture = new Fixture(height)) { fixture.CheckLayout(); fixture.CheckStatesAndPersistence(); }
                CheckHostWiring();
                Report.AppendLine("TETRIS_UI_CHECKS_PASS: " + checks + " assertions");
            }
            catch (Exception error) { exit = 1; Report.AppendLine("TETRIS_UI_CHECKS_FAIL\n" + error); Debug.LogException(error); }
            finally { UiFactory.Font = oldFont; File.WriteAllText(Path.Combine(output,"report.txt"), Report.ToString()); Debug.Log(Report.ToString()); EditorApplication.Exit(exit); }
        }
        private static void Require(bool value,string reason) { if (!value) throw new InvalidOperationException(reason); checks++; Report.AppendLine("PASS " + reason.TrimEnd()); }
        private static object Invoke(object target,string method,params object[] args) { return target.GetType().GetMethod(method,Flags).Invoke(target,args); }
        private static object Get(object target,string field) { return target.GetType().GetField(field,Flags).GetValue(target); }
        private static void CheckHostWiring()
        {
            // An inactive shell lets us inspect the real lobby without executing Awake or touching user saves.
            var host = new GameObject("Isolated lobby fixture"); host.SetActive(false);
            try
            {
                var shell = host.AddComponent<AppController>();
                var frame = UiFactory.Rect(host.transform,"Safe frame",0,0,720,1455);
                typeof(AppController).GetField("frame",Flags).SetValue(shell,frame);
                typeof(AppController).GetField("canvasRoot",Flags).SetValue(shell,frame);
                Invoke(shell,"BuildLobby");
                LobbyDesignChecks.SetModeForCheck(shell,2);
                var cards = frame.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name.EndsWith(" game card") && LobbyDesignChecks.ActiveWithin(frame,r)).ToArray();
                Require(cards.Length==17 && cards.Last().name=="Idiom game card","Lobby retains all seventeen entries in category order");
                var tetrisCard=cards.Single(r=>r.name=="Tetris game card");
                Require(cards[10]==tetrisCard && tetrisCard.GetComponentsInChildren<Text>(true).Any(t=>t.text.StartsWith("15")),"Tetris preserves ordinal 15 within the synthesis category");
                Require(tetrisCard.GetComponentsInChildren<TetrisPage>(true).Length==0,"Lobby preview creates no live session");
                Require(tetrisCard.GetComponent<Button>()!=null && tetrisCard.GetComponentsInChildren<Button>(true).Length==1,
                    "Tetris uses its whole card as the sole entry");
                LobbyInputTestDriver.CheckBinding(tetrisCard.GetComponent<Button>(),shell,"EnterTetris",Require);
                var content=frame.GetComponentsInChildren<ScrollRect>(true).Single().content;
                Require(content.rect.height>LobbyDesignChecks.CardTop(content,cards.Last())+cards.Last().rect.height,"Lobby content includes the last compact card and footer");
                typeof(AppController).GetField("tetrisPreview",Flags).SetValue(shell,new TetrisLoadResult{Status=TetrisLoadStatus.UnsupportedVersion,CanWrite=false});
                Invoke(shell,"RefreshTetrisRecord");
                Require(LobbyDesignChecks.RecordForCheck(shell,"Tetris").text.Contains("存档暂不可用"),"Lobby preserves unreadable record semantics");
                typeof(AppController).GetField("tetrisPreview",Flags).SetValue(shell,new TetrisLoadResult{Status=TetrisLoadStatus.Missing,CanWrite=true,Data=new TetrisSaveData()});
                Invoke(shell,"RefreshTetrisRecord");
                CaptureLobby(frame);
                string[] help = { "左右移动，双向旋转，填满横行即可消除。\n每消除 10 行提速，堆到顶部结束。\n左侧暂存每块一次，右侧预览 3 块。\n描边是落点；软降慢落，底部按钮直落。", "单 / 双 / 三 / 四行：\n100 / 300 / 500 / 800 × 当前等级。\n软降每格 1 分，直落每格 2 分。\n不加算 T-spin、连击、B2B、全清分。" };
                foreach (var body in help)
                {
                    Invoke(shell,"ShowDialog","俄罗斯方块",body,"知道了",(Action)(()=>{}),"返回",(Action)(()=>{}));
                    var modal=(GameObject)Get(shell,"modal");
                    var text=modal.GetComponentsInChildren<Text>(true).Single(t=>t.text==body);
                    Require(text.preferredHeight<=text.rectTransform.rect.height+.1,"Host help body fits fixed dialog without overflow");
                    Invoke(shell,"CloseModal");
                }
            }
            finally { Object.DestroyImmediate(host); }
        }
        private static void CaptureLobby(RectTransform frame)
        {
            var originalParent=frame.parent;
            var canvasObject=new GameObject("Lobby render canvas",typeof(RectTransform),typeof(Canvas));
            var rect=(RectTransform)canvasObject.transform;rect.sizeDelta=new Vector2(720,784*720f/388);
            var cameraObject=new GameObject("Lobby render camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;
            camera.orthographicSize=rect.rect.height/2;camera.aspect=388f/784;camera.nearClipPlane=.1f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=UiFactory.Paper;camera.cullingMask=1<<5;
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            var texture=new RenderTexture(388,784,24);texture.Create();camera.targetTexture=texture;
            try
            {
                frame.SetParent(rect,false);UiFactory.Stretch(frame);
                SetLayerForRender(rect);Canvas.ForceUpdateCanvases();
                var scroll=frame.GetComponentInChildren<ScrollRect>();
                var card=LobbyDesignChecks.FindCard(scroll.content,"Tetris");
                scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(LobbyDesignChecks.CardTop(scroll.content,card)-(scroll.viewport.rect.height-card.rect.height)/2,
                    0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
                Canvas.ForceUpdateCanvases();camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=texture;
                var image=new Texture2D(388,784,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,388,784),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,"lobby-card-388x784.png"),image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=previous;
            }
            finally { frame.SetParent(originalParent,false);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(canvasObject);texture.Release();Object.DestroyImmediate(texture); }
        }
        private static void SetLayerForRender(Transform root){root.gameObject.layer=5;foreach(Transform child in root)SetLayerForRender(child);}
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly Camera camera;
            private readonly RenderTexture texture;
            private readonly RectTransform frame;
            private readonly TetrisPage page;
            private readonly string directory;
            private readonly int height;
            private readonly float designHeight;
            public Fixture(int height)
            {
                this.height=height; designHeight=height*720f/388;
                directory=Path.Combine(output,"store-"+Guid.NewGuid().ToString("N"));
                root=new GameObject("Tetris fixture");
                camera=new GameObject("UI camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);
                camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=UiFactory.Paper;
                camera.cullingMask=1<<5;camera.orthographic=true;camera.enabled=false;camera.orthographicSize=designHeight*.5f;camera.aspect=388f/height;
                camera.nearClipPlane=.1f;camera.farClipPlane=100;
                texture=new RenderTexture(388,height,24);texture.Create();camera.targetTexture=texture;
                var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));canvasObject.transform.SetParent(root.transform);
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
                var rect=(RectTransform)canvasObject.transform;rect.sizeDelta=new Vector2(720,designHeight);
                frame=UiFactory.Rect(rect,"Tetris page",0,0,720,designHeight);UiFactory.Stretch(frame);
                if(EventSystem.current==null)new GameObject("Events",typeof(EventSystem),typeof(StandaloneInputModule)).transform.SetParent(root.transform);
                page=frame.gameObject.AddComponent<TetrisPage>();
                page.Initialize(frame,()=>{},after=>after(),()=>true,()=>false,()=>false,null,_=>{},(a,b,c,d,e,f)=>{},new TetrisSaveStore(directory));
                SetLayer(canvasObject.transform);Canvas.ForceUpdateCanvases();page.Enter();
            }
            private static void SetLayer(Transform t){t.gameObject.layer=5;foreach(Transform child in t)SetLayer(child);}
            public void CheckLayout()
            {
                var view=page.View;var layout=view.Layout;
                Require(Math.Abs(layout.BoardHeight/layout.BoardWidth-2)<.0001,"10 x 20 square-cell board at "+height);
                Require(Math.Abs(layout.BoardWidth*388/720-(height==784?237:194))<1,"Expected board width at "+height);
                Require(view.BoardRect.rect.height>0 && view.BoardRect.rect.width>0,"Board has real UGUI geometry");
                var targets=view.ActionTargets.Values.ToArray();
                Require(targets.Length==7,"Seven touch targets including Hold");
                foreach(var t in targets)Require(t.rect.width>=layout.MinimumTarget-.1 && t.rect.height>=layout.MinimumTarget-.1,"48px target "+t.name);
                for(int i=0;i<targets.Length;i++)for(int j=i+1;j<targets.Length;j++)Require(!Bounds(targets[i]).Overlaps(Bounds(targets[j])),"No gameplay hit overlap "+i+"/"+j);
                var icons=frame.GetComponentsInChildren<UiIconGraphic>(true);
                foreach(var kind in new[]{UiIconKind.Home,UiIconKind.Book,UiIconKind.Settings})Require(icons.Count(i=>i.Kind==kind)==1,"Shared navigation icon "+kind);
                Require(!File.Exists(Path.Combine(directory,"tetris-v1.json")),"Preview creates no active save");
                Capture("ready");
            }
            private Rect Bounds(RectTransform r)
            {
                var corners=new Vector3[4];r.GetWorldCorners(corners);var a=frame.InverseTransformPoint(corners[0]);var b=frame.InverseTransformPoint(corners[2]);
                return new Rect(a.x+.001f,a.y+.001f,b.x-a.x-.002f,b.y-a.y-.002f);
            }
            public void CheckStatesAndPersistence()
            {
                Invoke(page,"Begin");Require(page.Session.Phase==TetrisSessionPhase.Running,"Start runs only on explicit action");
                var touch=(TetrisTouchInput)Get(page,"touch");
                Canvas.ForceUpdateCanvases();camera.Render();
                foreach(var pair in page.View.ActionTargets)
                {
                    var position=RectTransformUtility.WorldToScreenPoint(camera,pair.Value.TransformPoint(pair.Value.rect.center));
                    var pointer=(TetrisPointer)Invoke(touch,"Pointer",1,position,TetrisPointerPhase.Began);
                    Require(pointer.Action==pair.Key,"Actual UGUI raycast acquires "+pair.Key);
                }
                page.Session.Input.ProcessFrame(new TetrisPointer[0],true,page.Session.Snapshot.PieceSerial);
                page.Session.Input.ProcessFrame(new[]{new TetrisPointer(7,TetrisAction.Hold,TetrisPointerPhase.Began,true)},true,page.Session.Snapshot.PieceSerial);
                page.Session.Advance(0);Invoke(page,"Refresh");Canvas.ForceUpdateCanvases();
                var holdTarget=page.View.ActionTargets[TetrisAction.Hold];
                var holdPoint=RectTransformUtility.WorldToScreenPoint(camera,holdTarget.TransformPoint(holdTarget.rect.center));
                Require(!page.Session.Snapshot.CanHold,"Real Hold command consumes permission");
                Require(((TetrisPointer)Invoke(touch,"Pointer",7,holdPoint,TetrisPointerPhase.Began)).Action==TetrisAction.None,"Used Hold cannot acquire another touch");
                Capture("hold-used");
                var state=page.Session.Snapshot;state.CanHold=true;state.HoldPiece=TetrisPiece.O;state.NextQueue=new[]{TetrisPiece.I,TetrisPiece.L,TetrisPiece.Z};state.Bag=new[]{TetrisPiece.J,TetrisPiece.S};state.Score=1240;state.Lines=8;state.X=3;state.Y=8;state.ActivePiece=TetrisPiece.T;
                for(int y=18;y<22;y++)for(int x=0;x<10;x++)if((x+y)%4!=0)state.Cells[y*10+x]=(TetrisPiece)(1+(x+y)%7);
                Require(state.TryValidate(out var reason),"Reference state valid: "+reason);
                Invoke(page,"SetSession",new TetrisGame(state),true);Invoke(page,"Begin");
                for(int i=0;i<30;i++)page.Session.Advance(.1);
                Invoke(page,"Refresh");Capture("game");
                var beforeFlash=page.Session.Snapshot;
                page.View.FlashLines(new[]{20,21});Require(page.View.HasClearFlash,"Clear feedback starts on visible rows");Capture("clear-feedback");
                page.View.TickVisuals(.12f);Require(!page.View.HasClearFlash,"Clear flash ends within120ms");
                Require(page.Session.Snapshot.ElapsedTicks==beforeFlash.ElapsedTicks,"Feedback advances no rule time");
                page.View.FlashLines(new[]{21});page.Suspend();Require(!page.View.HasClearFlash,"Pause cancels clear feedback");
                page.Session.Advance(.1);page.Suspend();var ticks=page.Session.Snapshot.ElapsedTicks;
                page.Session.Advance(.2);Require(page.Session.Snapshot.ElapsedTicks==ticks,"Pause freezes real model");Capture("paused");
                Invoke(page,"Begin");Require(page.Session.Phase==TetrisSessionPhase.Countdown,"Continue uses three-second countdown");Capture("countdown");
                page.SetForeground(false);page.SetForeground(true);Require(page.Session.Phase==TetrisSessionPhase.Paused,"Foreground return remains paused");
                var loaded=new TetrisSaveStore(directory).Load();Require(loaded.Status==TetrisLoadStatus.Loaded,"Page commits valid independent save");
                Require(loaded.Data.Game.ElapsedTicks==ticks && loaded.Data.Game.Score==1240,"Page saves full current dynamics");
                Invoke(page,"OpenHelp");Invoke(page,"OpenSettings");Require(page.Session.Phase==TetrisSessionPhase.Paused,"Help/settings preserve pause");
                page.SetHostSaveWarning("公共设置暂未保存，请检查剩余空间。");page.View.RefreshLayout();Invoke(page,"Refresh");Capture("warning");
                Require(frame.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("公共设置暂未保存")),"Shared warning remains visible");
                page.SetHostSaveWarning(null);page.View.RefreshLayout();
                var max=page.Session.Snapshot;max.Score=long.MaxValue;((TetrisSaveData)Get(page,"data")).BestScore=long.MaxValue;Invoke(page,"SetSession",new TetrisGame(max),true);Invoke(page,"Refresh");Capture("max-score");
                foreach(var text in frame.GetComponentsInChildren<Text>(true).Where(t=>t.text.Contains(long.MaxValue.ToString())))Require(text.horizontalOverflow!=HorizontalWrapMode.Wrap || text.resizeTextForBestFit,"Maximum score fits a single line");
                var ended=new TetrisGame(11);int tries=0;while(!ended.GameOver && tries++<100)ended.Execute(TetrisCommand.HardDrop);
                Require(ended.GameOver,"Real core produces terminal board");Invoke(page,"SetSession",ended,true);Invoke(page,"Refresh");Capture("game-over");
                Require(page.Session.Phase==TetrisSessionPhase.Ended,"Terminal snapshot shows ended phase");
                page.Leave();Require(!page.gameObject.activeSelf,"Leave deactivates page");page.Enter();Require(page.Session.Phase==TetrisSessionPhase.Ended,"Reentry cannot resume terminal game");
                string file=Path.Combine(directory,"tetris-v1.json");
                string future="{\"schemaVersion\":2}";File.WriteAllText(file,future);Invoke(page,"Load");Invoke(page,"Begin");page.Save();Capture("future-save");
                Require(File.ReadAllText(file)==future,"Future save UI cannot start or overwrite file");
                Require(frame.GetComponentsInChildren<Text>(true).Any(t=>t.text=="存档暂不可用"),"Unsupported save has dedicated UI");
                Require(page.View.OverlayRoot.GetComponentsInChildren<Text>(true).Any(t=>t.text=="最高纪录暂不可读"),"Unsupported save does not invent zero best score");
                var errorLabel=frame.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Tetris warning text");
                Require(errorLabel.text==page.SaveError,"Load failure appears only once in persistent warning");
                File.Delete(file+".bak");File.WriteAllText(file,"{");Invoke(page,"Load");page.Save();Capture("corrupt-save");
                Require(File.ReadAllText(file)=="{","Corrupt save stays intact before explicit reset");
                Require(frame.GetComponentsInChildren<Text>(true).Any(t=>t.text=="重置损坏存档"),"Corrupt save exposes explicit reset action");
            }
            private void Capture(string name)
            {
                page.View.RefreshLayout();Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;
                var image=new Texture2D(388,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,388,height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,name+"-388x"+height+".png"),image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=old;
            }
            public void Dispose(){page.Leave();Object.DestroyImmediate(root);texture.Release();Object.DestroyImmediate(texture);if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
