using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.AircraftFire;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.AircraftFire;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    [InitializeOnLoad]
    public static class AircraftFireUiChecks
    {
        private const string Pending = "Xanadu.AircraftFireUiChecks.Pending";
        private static int checks;
        private static string output;
        static AircraftFireUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            new GameObject("Aircraft UI checks").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private sealed class Runner : MonoBehaviour { }
        private static IEnumerator Execute()
        {
            yield return null;
            output = Path.GetFullPath("artifacts/aircraft-fire-ui"); Directory.CreateDirectory(output);
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                foreach (var size in new[] { new Vector2Int(720, 960), new Vector2Int(720, 1320), new Vector2Int(720, 1600) }) Check(size);
                var report = "AIRCRAFT_FIRE_UI_PASS: " + checks + " assertions; production UGUI + Camera.Render; isolated in-memory profile.\n";
                File.WriteAllText(Path.Combine(output, "report.txt"), report); Debug.Log(report); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "report.txt"), "AIRCRAFT_FIRE_UI_FAIL: " + error);
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
        private static void Check(Vector2Int size)
        {
            using (var f = new Fixture(size))
            {
                var home = 0; var allowed = true; Action closeSettings = null;
                var page = f.Root.gameObject.AddComponent<AircraftFirePage>();
                page.Initialize(f.Root, () => home++, done => { allowed = false; closeSettings = done; },
                    () => allowed, () => false, () => false, null, new AircraftFireSaveData());
                page.enabled = false; page.Enter(); page.LayoutNow();
                Require(page.Game == null, "Lobby entry opens the original menu without creating a game");
                foreach (var name in new[] { "Aircraft help", "Aircraft settings", "Aircraft home", "经典模式", "关卡模式", "历史成绩" }) f.CheckHit(name);
                f.Capture("entry");
                f.Click("Aircraft help");
                Require(page.HasDialog && page.Game == null, "Entry help opens without starting gameplay");
                foreach (var text in f.Root.GetComponentsInChildren<Text>())
                    if (text.text.Length > 100) Require(text.preferredHeight <= 468, "Help body fits above its close button");
                page.Advance(.1f);
                Require(page.Game == null && !page.Pointer.HasOwner, "Help keeps the entry idle and releases pointer");
                f.Capture("help"); f.Click("知道了");
                f.Click("Aircraft settings");
                Require(closeSettings != null && page.Game == null, "Entry settings uses shared callback without starting gameplay");
                allowed = true; closeSettings();
                Require(!page.HasDialog && page.Game == null, "Closing settings returns to the entry");
                f.Click("历史成绩");
                Require(page.HasDialog && page.Game == null, "Records opens without creating a game");
                f.Click("返回");
                f.Click("经典模式");
                Require(page.Game.Mode == AircraftMode.Classic && page.Game.Phase == AircraftPhase.Running, "Classic menu action starts actual gameplay");
                var bounds = f.Bounds(page.Surface);
                Require(Mathf.Abs(bounds.width - size.x) < 1 && Mathf.Abs(bounds.height - size.y) < 1, "Battle fills full width and height");
                f.CheckHit("Aircraft pause");
                var heroX = page.Game.HeroX;
                var contact = f.Down(page.Surface, 10);
                contact.position += new Vector2(60, 0);
                ExecuteEvents.Execute(page.Surface.gameObject, contact, ExecuteEvents.dragHandler);
                Require(page.Game.HeroX > heroX, "Real UGUI drag moves the hero relatively");
                var other = f.Down(page.Surface, 11); other.position -= new Vector2(120, 0);
                var afterOwner = page.Game.HeroX;
                ExecuteEvents.Execute(page.Surface.gameObject, other, ExecuteEvents.dragHandler);
                Require(page.Game.HeroX == afterOwner, "Second touch cannot steal movement");
                ExecuteEvents.Execute(page.Surface.gameObject, contact, ExecuteEvents.pointerUpHandler);
                for (var i = 0; i < 300; i++) page.Advance(1f / 60);
                Require(page.Game.ElapsedTime > 4, "Simulation advances through gameplay");
                f.Capture("playing");
                page.SetForeground(false); page.SetForeground(true); page.Advance(.01f);
                Require(page.Game.Phase == AircraftPhase.Paused && page.HasDialog, "Background return remains paused");
                f.Capture("paused"); f.Click("主菜单"); f.Click("关卡模式");
                Require(page.Game.Mode == AircraftMode.Level && page.Game.Stage == 1 && page.Game.Phase == AircraftPhase.Running, "Level menu action starts seven-stage game");
                f.Click("Aircraft pause"); f.Click("主菜单"); f.Click("经典模式");
                f.Click("Aircraft pause"); f.Click("疯狂进攻");
                Require(page.Game.Mode == AircraftMode.Classic && page.Game.Crazy, "Pause choice starts crazy classic difficulty");
                // Set up the final life, then use the real collision path. The page
                // must finish the original death frames before offering actions.
                typeof(AircraftGame).GetProperty("Lives").SetValue(page.Game, 1);
                typeof(AircraftGame).GetProperty("HeroInvulnerability").SetValue(page.Game, 0f);
                typeof(AircraftGame).GetMethod("SpawnEnemy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(page.Game, new object[] { AircraftEnemyKind.Medium, page.Game.HeroX, page.Game.HeroY, 0f });
                page.Advance(1f / 60);
                Require(page.Game.Phase == AircraftPhase.GameOver && !page.HasDialog, "Terminal collision has a noninteractive animation tail");
                var firstDeath = f.Find("Hero explosion").GetComponent<Image>().sprite;
                page.Advance(.1f); page.Advance(.1f);
                Require(f.Find("Hero explosion").GetComponent<Image>().sprite != firstDeath, "Terminal explosion advances original sprite frames");
                for (var i = 0; i < 8; i++) page.Advance(.1f);
                Require(page.HasDialog, "Result appears after terminal animation"); f.Capture("result");
                f.Click("继续挑战 · 本局复活一次"); Require(page.Game.Phase == AircraftPhase.Running && page.Game.Lives == 3, "Offline revive starts real gameplay");
                f.Click("Aircraft pause"); f.Click("主菜单"); f.Click("Aircraft home"); Require(home == 1, "Entry home reaches host exactly once");
                page.Leave(); Require(!page.gameObject.activeSelf, "Leave deactivates full-screen page");
            }
        }
        private static void Require(bool pass, string reason) { if (!pass) throw new InvalidOperationException(reason); checks++; }
        private sealed class Fixture : IDisposable
        {
            internal readonly RectTransform Root;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;
            private readonly Vector2Int size;
            internal Fixture(Vector2Int size)
            {
                this.size = size; owner = new GameObject("Aircraft fixture");
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                canvas.SetParent(owner.transform, false); canvas.sizeDelta = size;
                camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
                camera.orthographicSize = size.y / 2f; camera.aspect = (float)size.x / size.y;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.clearFlags = CameraClearFlags.SolidColor;
                target = new RenderTexture(size.x, size.y, 24); target.Create(); camera.targetTexture = target;
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; canvas.GetComponent<Canvas>().worldCamera = camera;
                Root = UiFactory.Rect(canvas, "Page", 0, 0, size.x, size.y); UiFactory.Stretch(Root);
                events = new GameObject("Events", typeof(EventSystem)).GetComponent<EventSystem>(); events.transform.SetParent(owner.transform, false);
            }
            internal RectTransform Find(string name)
            {
                foreach (RectTransform rect in Root.GetComponentsInChildren<RectTransform>()) if (rect.name == name) return rect;
                throw new InvalidOperationException("Missing active UI: " + name);
            }
            private void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }
            internal Rect Bounds(RectTransform rect)
            {
                Flush(); var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            private PointerEventData Pointer(RectTransform rect, int id)
            {
                Flush(); var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), pointerId = id, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)), "Raycast reaches " + rect.name);
                data.pointerCurrentRaycast = data.pointerPressRaycast = hits[0]; return data;
            }
            internal PointerEventData Down(RectTransform rect, int id)
            { var data = Pointer(rect, id); ExecuteEvents.ExecuteHierarchy(data.pointerCurrentRaycast.gameObject, data, ExecuteEvents.pointerDownHandler); return data; }
            internal void CheckHit(string name) { Pointer(Find(name), -1); }
            internal void Click(string name)
            {
                var data = Pointer(Find(name), -1); var hit = data.pointerCurrentRaycast.gameObject;
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                ExecuteEvents.ExecuteHierarchy(hit, data, ExecuteEvents.pointerDownHandler);
                if (clicked != null) { ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler); }
            }
            internal void Capture(string name)
            {
                Flush(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name + "-" + size.x + "x" + size.y + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose() { camera.targetTexture = null; Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
