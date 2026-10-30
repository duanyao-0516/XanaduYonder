using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.FlappyBird;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.FlappyBird;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Real production UGUI, isolated profiles and real rendered raycasts. No player files are opened.
    [InitializeOnLoad]
    public static class FlappyBirdUiChecks
    {
        private const string Pending = "Xanadu.FlappyBirdUiChecks.Pending";
        private static readonly StringBuilder report = new StringBuilder();
        private static int checks;
        private static string output;
        static FlappyBirdUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }
        public static void Run()
        {
            FlappyAssetPreparation.Ensure();
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            new GameObject("FlappyBird checks runner").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private sealed class Runner : MonoBehaviour { }
        private static IEnumerator Execute()
        {
            yield return null;
            checks = 0; report.Clear();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/flappy-bird-ui"));
            Directory.CreateDirectory(output);
            report.AppendLine("Unity " + Application.unityVersion + "; production UGUI; Camera.Render; in-memory profiles");
            var run = Scenarios();
            while (true)
            {
                bool next = false; object current = null; Exception failure = null;
                try { next = run.MoveNext(); if (next) current = run.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null || !next)
                {
                    (run as IDisposable)?.Dispose();
                    if (failure == null) report.AppendLine("FLAPPY_BIRD_UI_CHECKS_PASS: " + checks + " assertions");
                    else report.AppendLine("FLAPPY_BIRD_UI_CHECKS_FAIL after " + checks + " assertions\n" + failure);
                    File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
                    Debug.Log(report.ToString()); EditorApplication.Exit(failure == null ? 0 : 1); yield break;
                }
                yield return current;
            }
        }
        private static IEnumerator Scenarios()
        {
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(UiFactory.Font != null, "CJK font loads");
            var atlas = Resources.Load<UnityEngine.U2D.SpriteAtlas>("FlappyBird/Atlas");
            Require(atlas != null && atlas.spriteCount == 4, "Runtime atlas contains only four selected sprites");
            var packed=atlas.GetSprite("BirdRest");
            Require(packed.texture.width<=512&&packed.texture.height<=512,"Used sprites occupy a compact runtime texture");Object.Destroy(packed);
            foreach (var size in new[] { new Vector2Int(388,784), new Vector2Int(320,640), new Vector2Int(388,896), new Vector2Int(720,1455) })
            using (var f = new Fixture(size.y,size.x))
            {
                bool allowed=true; Action finishSettings=null; int home=0;
                var page=f.Root.gameObject.AddComponent<FlappyBirdPage>();
                page.Initialize(f.Root,()=>home++, done=>{allowed=false;finishSettings=done;},()=>allowed,()=>false,()=>false,
                    f.Root.gameObject.AddComponent<GameFeedback>(), _=>{}, initialData:new FlappySaveData());
                page.enabled=false; page.Enter(); page.LayoutNow();
                yield return null;
                Require(page.Session.Phase==FlappyPhase.Ready,"Entry stays Ready");
                Rect bounds=f.Bounds(page.Surface);
                Near(bounds.width/bounds.height,.6f,"World keeps 3:5");
                Require(bounds.xMin>=0&&bounds.xMax<=size.x+1&&bounds.yMin>=0&&bounds.yMax<=size.y+1,"World fits usable area");
                f.Capture("ready");
                foreach(string name in new[]{"Home","Book","Settings","Pause"}) f.CheckHit(f.Find(name));
                f.Click(f.Find("Book")); Require(page.HasDialog&&page.Session.Phase==FlappyPhase.Ready,"Help preserves Ready");
                f.Capture("help"); page.Back(); Require(!page.HasDialog,"Back closes help");
                f.Click(f.Find("Settings")); Require(page.SettingsBlocked&&finishSettings!=null,"Settings blocks world");
                page.Pointer.Press(4); page.Pointer.Release(4); Require(page.Session.Phase==FlappyPhase.Ready,"Settings prevents flap");
                allowed=true;finishSettings();Require(!page.SettingsBlocked&&page.Session.Phase==FlappyPhase.Ready,"Settings restores Ready");
                var heldControl=f.Down(f.Find("Home"),41);
                var secondTouch=f.Down(page.Surface,42);
                Require(page.Session.Phase==FlappyPhase.Ready,"A held chrome touch excludes second world finger");
                f.Up(page.Surface,secondTouch);f.Up(f.Find("Home"),heldControl);
                f.Click(page.Surface); page.Advance(1f/60); Require(page.Session.Phase==FlappyPhase.Running&&page.Session.Game.BirdVelocity>0,"Real UGUI tap starts and flaps");
                double velocity=page.Session.Game.BirdVelocity;
                page.Pointer.Press(1);page.Advance(1f/60);page.Pointer.Press(1);page.Pointer.Press(2);page.Advance(1f/60);
                Require(page.Session.Game.BirdVelocity<velocity,"Held and second fingers do not repeat flap");
                page.Pointer.Release(1);page.Pointer.Release(2);
                f.Capture("flying");
                f.Click(f.Find("Pause")); Require(page.Session.Phase==FlappyPhase.Paused&&page.HasDialog,"Pause opens panel");
                double y=page.Session.Game.BirdY;page.Advance(.05f);Require(page.Session.Game.BirdY==y,"Pause freezes model");
                f.Capture("paused");
                f.Click(f.Find("继续飞行"));Require(page.Session.Phase==FlappyPhase.Countdown,"Continue starts countdown");
                f.Capture("countdown");
                page.SetForeground(false);page.SetForeground(true);Require(page.Session.Phase==FlappyPhase.Paused,"Interrupted countdown returns Paused");
                f.Click(f.Find("继续飞行"));for(int i=0;i<180;i++) page.Advance(1f/60);
                Require(page.Session.Phase==FlappyPhase.Running,"Full three seconds resumes");
                for(int i=0;i<180&&page.Session.Phase!=FlappyPhase.Result;i++) page.Advance(1f/60);
                Require(!f.Find("再飞一次").GetComponent<Button>().interactable,"Result button initially guards accidental touch");
                page.Restart();Require(page.Session.Phase==FlappyPhase.Result,"Guard blocks direct restart too");
                for(int i=0;i<22;i++)page.Advance(1f/60);
                Require(page.Session.Phase==FlappyPhase.Result,"Death reaches result"); f.Capture("result");
                f.Click(f.Find("再飞一次"));Require(page.Session.Phase==FlappyPhase.Ready,"Restart opens Ready without flap");
                f.Click(page.Surface);page.Advance(1f/60);page.Advance(.2f);
                Require(page.Session.Phase==FlappyPhase.Paused,"Long frame pauses");
                page.RequestHome(); Require(page.HasDialog&&home==0,"Active Home confirms");
                f.Click(f.Find("结束并返回大厅"));Require(home==1,"Confirm exits exactly once");
                page.Leave();page.Enter();Require(page.Session.Game.Score==0&&page.Session.Phase==FlappyPhase.Ready,"Reentry discards active run");
                page.Leave();
            }
            using(var f=new Fixture(784,388))
            {
                var page=f.Root.gameObject.AddComponent<FlappyBirdPage>();
                page.Initialize(f.Root,()=>{},_=>{},()=>true,()=>false,()=>false,f.Root.gameObject.AddComponent<GameFeedback>(),_=>{},initialData:new FlappySaveData{BestScore=long.MaxValue});
                page.enabled=false;page.Enter();f.Capture("max-score");
                Require(page.BestScore==long.MaxValue,"Long record remains exact");
            }
            string temporary=Path.Combine(Path.GetTempPath(),"flappy-ui-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                using(var f=new Fixture(784,388))
                {
                    string blocked=Path.Combine(temporary,"blocked"); File.WriteAllText(blocked,"occupied");
                    var store=new FlappySaveStore(blocked);
                    var page=f.Root.gameObject.AddComponent<FlappyBirdPage>();
                    page.Initialize(f.Root,()=>{},_=>{},()=>true,()=>false,()=>false,f.Root.gameObject.AddComponent<GameFeedback>(),_=>{},store,new FlappySaveData());
                    page.enabled=false;page.Enter();page.Save();
                    Require(page.SaveError==null,"Fresh entry does not attempt a zero-record write");
                    // Controlled score fixture exercises the real page-to-store path, without simulating art as rules.
                    typeof(FlappyGame).GetProperty("Score").SetValue(page.Session.Game,7L,null);page.Save();
                    Require(page.BestScore==7&&page.StoredBestScore==0&&page.SaveError!=null,"Failed write preserves memory best separately");
                    Rect bounds=f.Bounds(page.Surface);f.Capture("unsaved");
                    page.Leave();page.Enter();Require(page.BestScore==7&&page.SaveError!=null,"Unsaved high survives page reentry");
                    File.Delete(blocked);Directory.CreateDirectory(blocked);f.Click(f.Find("Retry save"));
                    Require(page.SaveError==null&&page.StoredBestScore==7,"Retry persists and clears warning");
                    Require(f.Bounds(page.Surface)==bounds,"Retry does not resize world");
                    string warning;Require(new FlappySaveStore(blocked).Load(out warning).BestScore==7&&warning==null,"Fresh store reads persisted high");
                    page.Leave();
                }
                using(var f=new Fixture(784,388))
                {
                    string bad=Path.Combine(temporary,"bad");Directory.CreateDirectory(bad);File.WriteAllText(Path.Combine(bad,"flappy-bird-v1.json"),"broken");
                    var page=f.Root.gameObject.AddComponent<FlappyBirdPage>();
                    page.Initialize(f.Root,()=>{},_=>{},()=>true,()=>false,()=>false,f.Root.gameObject.AddComponent<GameFeedback>(),_=>{},new FlappySaveStore(bad));
                    page.enabled=false;page.Enter();page.Save();page.Leave();page.Enter();
                    Require(page.SaveError!=null&&File.ReadAllText(Path.Combine(bad,"flappy-bird-v1.json"))=="broken","Untrusted load warning survives zero-score lifecycle without overwrite");
                }
            }
            finally {Directory.Delete(temporary,true);}
            using(var f=new Fixture(1455))
            {
                var inert=new GameObject("Isolated host");inert.SetActive(false);
                try
                {
                    var host=inert.AddComponent<AppController>();
                    Set(host,"canvasRoot",(RectTransform)f.Root.parent);Set(host,"frame",f.Root);Set(host,"data",new SaveData());
                    Set(host,"feedback",inert.AddComponent<GameFeedback>());
                    Invoke(host,"BuildLobby");Invoke(host,"RefreshFlappyRecord");
                    var scroll=f.Root.GetComponentInChildren<ScrollRect>();f.Flush();
                    var card=f.Find("FlappyBird game card");
                    scroll.StopMovement();
                    scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(LobbyDesignChecks.CardTop(scroll.content,card)-(scroll.viewport.rect.height-card.rect.height)/2,
                        0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));f.Flush();
                    Require(f.Bounds(card).yMin>=f.Bounds(scroll.viewport).yMin && f.Bounds(card).yMax<=f.Bounds(scroll.viewport).yMax,
                        "Flappy card is fully visible for its lobby entry check");
                    Require(card.GetComponent<Button>()!=null && card.GetComponentsInChildren<Button>().Length==1,
                        "Flappy enters through its whole card without a separate CTA");
                    LobbyInputTestDriver.CheckBinding(card.GetComponent<Button>(),host,"EnterFlappyBird",Require);
                    f.CheckHit(card);f.Capture("lobby-flappy-card");
                    Get<GameObject>(host,"lobbyPage").SetActive(false);
                    var root=UiFactory.Rect(f.Root,"Host Flappy",0,0,720,1455);UiFactory.Stretch(root);
                    var page=root.gameObject.AddComponent<FlappyBirdPage>();Set(host,"flappyBirdPage",page);
                    Set(host,"activePage",Enum.Parse(typeof(AppController).GetNestedType("Page",BindingFlags.NonPublic),"FlappyBird"));
                    page.Initialize(root,()=>{},done=>Invoke(host,"OpenSettings",done),()=>Get<GameObject>(host,"modal")==null&&!Get<bool>(host,"paused")&&Get<bool>(host,"focused"),()=>false,()=>false,Get<GameFeedback>(host,"feedback"),message=>Invoke(host,"SetStatus",message),initialData:new FlappySaveData());
                    page.enabled=false;page.Enter();f.Click(f.Find("Settings"));
                    Require(Get<GameObject>(host,"modal")!=null&&page.SettingsBlocked,"Actual shared settings blocks page");
                    Invoke(host,"OnApplicationPause",true);Invoke(host,"OnApplicationPause",false);
                    Require(Get<GameObject>(host,"modal")==null&&!page.SettingsBlocked&&page.Session.Phase==FlappyPhase.Ready,"Actual foreground clears stale settings and restores Ready");
                    f.Click(page.Surface);page.Advance(1f/60);Invoke(host,"OnApplicationFocus",false);Invoke(host,"OnApplicationFocus",true);
                    Require(page.Session.Phase==FlappyPhase.Paused,"Actual host focus suspends running game");
                    Set(host,"saveFailed",true);Set(host,"saveWarning","公共设置尚未保存");page.Save();
                    Require(Array.Exists(root.GetComponentsInChildren<Text>(),t=>t.text.Contains("公共设置尚未保存")),"Host warning reaches hidden-footer page");
                    page.Leave();
                }
                finally {Object.DestroyImmediate(inert);}
            }
        }
        private static void Near(float value,float expected,string reason) {Require(Math.Abs(value-expected)<.01f,reason+": "+value+" vs "+expected);}
        private static void Require(bool value,string reason) {if(!value)throw new InvalidOperationException(reason);checks++;}
        private const BindingFlags Members=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        private static T Get<T>(object value,string field) {return (T)value.GetType().GetField(field,Members).GetValue(value);}
        private static void Set(object value,string field,object data) {value.GetType().GetField(field,Members).SetValue(value,data);}
        private static void Invoke(object value,string method,params object[] args)
        {foreach(var candidate in value.GetType().GetMethods(Members))if(candidate.Name==method&&candidate.GetParameters().Length==args.Length){candidate.Invoke(value,args);return;}throw new MissingMethodException(method);}
        private sealed class Fixture : IDisposable
        {
            public readonly RectTransform Root;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;
            private readonly int height, width;
            public Fixture(int height, int width = 720)
            {
                this.height = height; this.width = width; owner = new GameObject("FlappyBird UI fixture");
                var canvas = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                canvas.SetParent(owner.transform, false); canvas.sizeDelta = new Vector2(width, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = height * .5f; camera.aspect = (float)width / height; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = camera;
                Root = UiFactory.Rect(canvas, "FlappyBird fixture page", 0, 0, width, height); UiFactory.Stretch(Root);
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>(); events.transform.SetParent(owner.transform, false);
            }
            public RectTransform Find(string name)
            {
                foreach (RectTransform rect in Root.GetComponentsInChildren<RectTransform>()) if (rect.name == name) return rect;
                throw new InvalidOperationException("Missing visible production element: " + name);
            }
            public void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }
            public Rect Bounds(RectTransform rect)
            {
                Flush(); var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            private PointerEventData Pointer(RectTransform rect,int id=-1)
            {
                Flush(); var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), pointerId = id, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)), "Rendered raycast reaches " + rect.name);
                data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0]; return data;
            }
            public PointerEventData Down(RectTransform rect,int id)
            {var data=Pointer(rect,id);ExecuteEvents.ExecuteHierarchy(data.pointerCurrentRaycast.gameObject,data,ExecuteEvents.pointerDownHandler);return data;}
            public void Up(RectTransform rect,PointerEventData data)
            {ExecuteEvents.ExecuteHierarchy(rect.gameObject,data,ExecuteEvents.pointerUpHandler);}
            public void CheckHit(RectTransform rect) { Pointer(rect); }
            public void Click(RectTransform rect)
            {
                var data = Pointer(rect); var hit = data.pointerCurrentRaycast.gameObject;
                var pressed = ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit); data.pointerPress = pressed != null ? pressed : clicked;
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                if (clicked != null && data.pointerPress == clicked) ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
            }
            public void Capture(string name)
            {
                Flush(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name + "-" + width + "x" + height + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose() { camera.targetTexture = null; if (RenderTexture.active == target) RenderTexture.active = null; Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
