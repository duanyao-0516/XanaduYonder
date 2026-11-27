using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using XanaduYonder.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.BakeryBasket;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.BakeryBasket;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    [InitializeOnLoad]
    public static class BakeryBasketChecks
    {
        private const string Pending = "Xanadu.BakeryChecks.Pending";
        private const string BakePending = "Xanadu.BakeryChecks.BakeOpenings";
        private static readonly string Output = Path.GetFullPath("artifacts/validation/bakery-basket/v11");

        static BakeryBasketChecks() { EditorApplication.playModeStateChanged += StateChanged; }

        public static void Run() => EnterPlayer(false);
        public static void BakeOpeningPoses() => EnterPlayer(true);

        private static void EnterPlayer(bool bake)
        {
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(BakePending, bake);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            bool bake = SessionState.GetBool(BakePending, false);
            SessionState.SetBool(Pending, false);
            SessionState.SetBool(BakePending, false);
            new GameObject("Bakery check runner").AddComponent<Runner>().StartCoroutine(Execute(bake));
        }

        private static IEnumerator Execute(bool bake)
        {
            yield return null;
            Directory.CreateDirectory(Output);
            try
            {
                if (bake) BakeOpenings();
                else Check();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
                yield break;
            }
            for (int i = 0; i < 30 && LoadedBasketScenes() > 0; i++) yield return null;
            try
            {
                Require(LoadedBasketScenes() == 0, "all comparison physics scenes unload after exit");
                Debug.Log(bake ? "BAKERY_OPENINGS_BAKE_PASS" : "BAKERY_BASKET_CHECKS_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void BakeOpenings()
        {
            using var data = new MemoryStream();
            using var writer = new BinaryWriter(data);
            writer.Write(BakeryOpeningPoses.Format);
            writer.Write(BakeryOpeningPoses.Seed);
            writer.Write(BakeryThemeCatalog.All.Count * 2);
            var preview = new GameObject("Bakery pose target", typeof(RectTransform), typeof(RawImage));
            var image = preview.GetComponent<RawImage>();
            foreach (var theme in BakeryThemeCatalog.All)
                foreach (bool tapered in new[] { false, true })
                {
                    var elapsed = System.Diagnostics.Stopwatch.StartNew();
                    using var world = new BakeryWorld(image, new BakeryRound(BakeryOpeningPoses.Seed),
                        BakeryOpeningPoses.Seed, tapered, theme.Theme, bake: true);
                    Debug.Log($"BAKERY_SIMULATED_OPENING {theme.Theme} tapered={tapered}: {elapsed.ElapsedMilliseconds} ms");
                    Require(world.Recoveries == 0, "build-time deal remains in its basket");
                    foreach (var piece in world.Pieces)
                    {
                        Vector3 position = piece.Rigidbody.position - piece.Root.parent.position;
                        Quaternion rotation = piece.Rigidbody.rotation;
                        writer.Write(position.x);
                        writer.Write(position.y);
                        writer.Write(position.z);
                        writer.Write(rotation.x);
                        writer.Write(rotation.y);
                        writer.Write(rotation.z);
                        writer.Write(rotation.w);
                    }
                }
            Object.Destroy(preview);
            File.WriteAllBytes("Assets/Resources/" + BakeryOpeningPoses.ResourcePath + ".bytes", data.ToArray());
        }

        private static int LoadedBasketScenes()
        {
            int count = 0;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name.StartsWith("Bakery basket ", StringComparison.Ordinal)) count++;
            return count;
        }

        private static void Check()
        {
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(UiFactory.Font != null, "shared font loads");
            CheckPreferences();
            CheckJadeFinishes();
            var cam = new GameObject("Bakery UI camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 0, -9);
            cam.cullingMask = 1 << 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiFactory.Paper;
            cam.orthographic = true;
            cam.orthographicSize = 660;
            cam.enabled = false;
            cam.targetTexture = new RenderTexture(720, 1320, 24);
            var canvas = new GameObject("Bakery UI fixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            var eventSystem = new GameObject("Bakery event system", typeof(EventSystem)).GetComponent<EventSystem>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(720, 1320);
            var page = canvas.gameObject.AddComponent<BakeryPage>();
            bool foreground = true;
            var preferences = new BakeryPreferences { RandomOnEntry = false };
            page.Initialize(rect, () => { }, () => foreground, () => false, () => false, null, preferences, () => { }, () => { });
            foreach (var node in canvas.GetComponentsInChildren<Transform>()) node.gameObject.layer = 5;
            CheckPickingDepth();
            Require(BakeryThemeCatalog.All.All(theme => new[] { false, true }.All(tapered =>
                BakeryOpeningPoses.Get(BakeryOpeningPoses.Seed, theme.Theme, tapered)?.Length == BakeryRound.PieceCount)),
                "all eight opening poses are available to the normal runtime path");
            CheckEntrance(page, cam);
            CheckPastryPhysics(page);
            page.StartRound(false);
            CheckRapidTaps(page, canvas, eventSystem);
            CheckReserve(page, canvas, cam, eventSystem);
            page.StartRound(false);
            Capture(cam, "straight.png");
            CheckMenus(page, preferences, canvas);
            CheckContactShadows(page.World, "pastry");
            var view = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(item => item.targetTexture == page.World.Texture);
            Require(!view.orthographic && view.depthTextureMode == DepthTextureMode.None && view.commandBufferCount == 0,
                "straight wall uses perspective and the contact outline pass is gone");
            int[] kinds = Enumerable.Range(0, BakeryRound.PieceCount).Select(page.Round.KindOf).ToArray();
            Require(page.World.Pieces.Length == BakeryRound.PieceCount && page.World.Pieces.All(piece => piece.Surfaces.Length > 0), "every dealt model has a rendered surface");
            Require(Mathf.Approximately(page.World.FloorRadius, BakeryBasketGeometry.MouthRadius) &&
                Mathf.Approximately(page.World.Depth, 2.2f), "straight wall retains its original depth and full floor width");
            CheckRingContact(page.World);
            CheckOpening(page.World, "straight");
            Debug.Log("BAKERY_VISIBLE straight opening " + Enumerable.Range(0, BakeryRound.PieceCount).Count(id => page.World.TryFindVisiblePoint(id, out _)));
            Require(CheckShake(page, "straight") > 0, "straight shake reveals previously hidden small pieces");
            CheckDisplay(page, canvas, preferences);
            CheckAfterSixty(page, cam, false, "straight");
            page.StartRound(true);
            Capture(cam, "tapered.png");
            Require(page.World.FloorRadius <= .90f && page.World.Depth >= 2.8f &&
                (BakeryBasketGeometry.MouthRadius - page.World.FloorRadius) / page.World.Depth > .79f &&
                kinds.SequenceEqual(Enumerable.Range(0, BakeryRound.PieceCount).Select(page.Round.KindOf)),
                "deeper tapered basket has a steeper inward wall and the same deal");
            CheckOpening(page.World, "tapered");
            Debug.Log("BAKERY_VISIBLE tapered opening " + Enumerable.Range(0, BakeryRound.PieceCount).Count(id => page.World.TryFindVisiblePoint(id, out _)));
            CheckShake(page, "tapered");
            Capture(cam, "tapered-after-shake.png");

            int visibleLoaf = Enumerable.Range(0, BakeryRound.PieceCount).First(candidate => page.Round.KindOf(candidate) == 0 &&
                page.World.TryFindVisiblePoint(candidate, out _));
            Require(page.World.TryFindVisiblePoint(visibleLoaf, out var loafPoint) && page.Press(loafPoint), "visible loaf selects");
            Capture(cam, "highlight-visible.png");
            page.Release(false);

            int id = FindPartiallyCoveredSmall(page.World, out var at);
            Require(id >= 0, "comparison provides a partly visible small piece with a blocked region");
            var viewport = canvas.GetComponentInChildren<BakeryPointer>();
            var tap = MakePointer(canvas, viewport, eventSystem, at);
            ExecuteEvents.Execute(viewport.gameObject, tap, ExecuteEvents.pointerDownHandler);
            Require(page.World.Selected == id, "UGUI press on the exposed surface highlights the partly covered model");
            Capture(cam, "highlight.png");
            ExecuteEvents.Execute(viewport.gameObject, tap, ExecuteEvents.cancelHandler);
            Require(page.World.Selected == -1 && page.Round.Tray.Count == 0, "cancel clears highlight without taking an object");
            ExecuteEvents.Execute(viewport.gameObject, tap, ExecuteEvents.pointerDownHandler);
            Require(page.World.Selected == id, "piece can be selected again after cancel");
            ExecuteEvents.Execute(viewport.gameObject, tap, ExecuteEvents.pointerUpHandler);
            Advance(page, Mathf.CeilToInt(.16f / BakeryWorld.Step));
            Require(page.Round.Tray.SequenceEqual(new[] { id }) && page.DisplayedTray.Count == 0 && !page.World.Pieces[id].InBasket,
                "flying object is accepted at release and enters the visible tray on arrival");
            Capture(cam, "flight.png");
            Advance(page, Mathf.CeilToInt(.62f / BakeryWorld.Step));
            Capture(cam, "slot.png");
            Require(page.DisplayedTray.SequenceEqual(new[] { id }) && page.Round.Remaining == BakeryRound.PieceCount - 1, "model finishes its flight in the first tray slot");
            CheckAfterSixty(page, cam, true, "tapered");
            CheckTaperedFloorMovement(page);
            foreach (var theme in BakeryThemeCatalog.All.Skip(1))
                CheckTheme(page, cam, kinds, theme);
            CheckEntryTheme(page, preferences);
            foreground = false;
            Require(!page.Press(at), "background rejects picks");
            page.Leave();
            Require(page.World == null && !page.gameObject.activeSelf, "return releases the view and physics scene");
            Object.Destroy(cam.targetTexture);
            Object.Destroy(cam.gameObject);
            Object.Destroy(canvas.gameObject);
            Object.Destroy(eventSystem.gameObject);
        }

        private static void CheckMenus(BakeryPage page, BakeryPreferences preferences, Canvas canvas)
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
            var host = new GameObject("Bakery dialog host");
            host.SetActive(false);
            var shell = host.AddComponent<AppController>();
            var type = typeof(AppController);
            void Set(string field, object value) => type.GetField(field, members).SetValue(shell, value);
            void Call(string method) => type.GetMethod(method, members).Invoke(shell, null);
            Set("canvasRoot", (RectTransform)canvas.transform);
            Set("bakeryPage", page);
            Set("bakeryPreferences", preferences);
            Set("data", new SaveData());
            try
            {
                foreach (var method in new[] { "ShowBakeryHelp", "OpenBakerySettings" })
                {
                    Call(method);
                    var modal = (GameObject)type.GetField("modal", members).GetValue(shell);
                    foreach (var child in modal.GetComponentsInChildren<Transform>()) child.gameObject.layer = canvas.gameObject.layer;
                    Require(modal.GetComponentsInChildren<Button>().Length > 0 &&
                        (modal.GetComponentInChildren<DialogSheetLayout>() != null) == (method != "ShowBakeryHelp"),
                        "Bakery routes use their shared dialog or bottom sheet.");
                    if (method == "OpenBakerySettings")
                    {
                        var layout = modal.GetComponentInChildren<DialogSheetLayout>();
                        Require(layout.Content.Find("Section 数量显示") && layout.Content.Find("Section 3D 主题") &&
                            layout.Content.Find("Section 声音与触感"), "All basket settings are exposed in one sheet");
                        var selection = layout.Content.Find(BakeryThemeCatalog.Get(page.ActiveTheme).Title).GetComponent<Button>();
                        layout.Reveal((RectTransform)selection.transform);
                        selection.onClick.Invoke();
                        Require((GameObject)type.GetField("modal", members).GetValue(shell) == modal,
                            "Selecting the current basket theme keeps the same settings sheet");
                    }
                    DialogScreenshotAudit.CaptureCurrent(modal.transform);
                    Call("CloseModal");
                    Object.DestroyImmediate(modal);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void CheckEntrance(BakeryPage page, Camera camera)
        {
            page.Enter();
            var world = page.World;
            var positions = world.Pieces.Select(piece => piece.Rigidbody.position).ToArray();
            Require(world.IsEntering, "lobby entry starts the fountain");
            Capture(camera, "entrance-start.png");
            Advance(page, 23);
            Require(world.IsEntering && world.Pieces.Any(piece => piece.Visual.gameObject.activeSelf &&
                Vector3.Distance(piece.Visual.localPosition, piece.Centered) > .15f), "the fountain moves visible meshes up from the basin");
            Capture(camera, "entrance-fountain.png");
            Advance(page, 40);
            Require(!world.IsEntering && world.Pieces.Select(piece => piece.Rigidbody.position).SequenceEqual(positions) &&
                world.Pieces.All(piece => piece.Visual.gameObject.activeSelf && piece.Visual.localPosition == piece.Centered),
                "the fountain finishes at the cached poses without simulating the pile");
            page.Enter();
            Require(!page.Press(new Vector2(.5f, .5f)) && !page.World.IsEntering && page.Round.Remaining == BakeryRound.PieceCount,
                "touching during the fountain skips it without taking a piece");
        }

        private static void CheckReserve(BakeryPage page, Canvas canvas, Camera camera, EventSystem eventSystem)
        {
            page.StartRound(false);
            var world = page.World;
            var types = new HashSet<int>();
            var ids = new List<int>();
            for (int i = 0; i < BakeryRound.SlotCount; i++)
            {
                int id = Enumerable.Range(0, world.Pieces.Length).First(candidate =>
                    !types.Contains(page.Round.KindOf(candidate)) && world.TryFindVisiblePoint(candidate, out _));
                Require(world.TryFindVisiblePoint(id, out var point) && page.Press(point), "reserve test picks a new kind");
                types.Add(page.Round.KindOf(id));
                ids.Add(id);
                page.Release(true);
                Advance(page, 54, false);
            }
            var buttons = canvas.GetComponentsInChildren<Button>(true);
            var move = buttons.Single(button => button.name == "移出 3 件");
            var first = buttons.Single(button => button.name == "Return reserve 1");
            var click = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            int remaining = page.Round.Remaining;
            Require(page.Round.Phase == BakeryPhase.Full && move.interactable && !first.interactable,
                "a full elimination tray can be rescued by the reserve");
            ExecuteEvents.Execute(move.gameObject, click, ExecuteEvents.pointerClickHandler);
            Require(page.Round.Reserve.SequenceEqual(ids.Take(3)) && page.DisplayedTray.SequenceEqual(ids.Skip(3)) &&
                page.Round.Phase == BakeryPhase.Playing, "move out transfers the first three and compacts the tray");
            ExecuteEvents.Execute(move.gameObject, click, ExecuteEvents.pointerClickHandler);
            Require(page.Round.Reserve.SequenceEqual(ids.Take(6)) && !move.interactable && first.interactable &&
                page.DisplayedTray.SequenceEqual(ids.Skip(6)) && page.Round.Remaining == remaining, "the six reserve cells fill without changing basket count");
            world.Render();
            Capture(camera, "reserve-full.png");
            Canvas.ForceUpdateCanvases();
            var cell = (RectTransform)first.transform;
            click.position = camera.WorldToScreenPoint(cell.TransformPoint(cell.rect.center));
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(click, hits);
            Require(hits.Count > 0 && hits[0].gameObject == first.gameObject, "the 3D viewport keeps a reserve cell tappable");
            ExecuteEvents.Execute(first.gameObject, click, ExecuteEvents.pointerClickHandler);
            Require(page.Round.Reserve.SequenceEqual(ids.Skip(1).Take(5)) && page.Round.Remaining == remaining,
                "tapping an occupied reserve cell sends that model back to the tray");
            Advance(page, 54, false);
            Require(page.DisplayedTray.SequenceEqual(new[] { ids[6], ids[0] }), "the returned 3D model lands in the next tray slot");
            page.StartRound(false);
            Require(page.Round.Reserve.Count == 0 && !first.interactable, "restarting clears the reserve");
        }

        private static void CheckPastryPhysics(BakeryPage page)
        {
            foreach (bool tapered in new[] { false, true })
            {
                if (tapered) page.StartRound(true);
                var toast = page.World.Pieces.Where(piece => piece.Kind == BakeryFood.Toast).ToArray();
                var contact = toast[0].Root.GetComponent<Collider>();
                float opening = toast.Max(piece => piece.Rigidbody.angularVelocity.magnitude);
                float tail = 0, tailTurn = 0, tailTravel = 0;
                var restingRotation = new Quaternion[toast.Length];
                var restingPosition = new Vector3[toast.Length];
                for (int i = 0; i < 270; i++)
                {
                    page.World.Simulate();
                    if (i == 179)
                        for (int id = 0; id < toast.Length; id++)
                        {
                            restingRotation[id] = toast[id].Rigidbody.rotation;
                            restingPosition[id] = toast[id].Rigidbody.position;
                        }
                    if (i < 180) continue;
                    for (int id = 0; id < toast.Length; id++)
                    {
                        var body = toast[id].Rigidbody;
                        tail = Mathf.Max(tail, body.angularVelocity.magnitude);
                        tailTurn = Mathf.Max(tailTurn, Quaternion.Angle(restingRotation[id], body.rotation));
                        tailTravel = Mathf.Max(tailTravel, Vector3.Distance(restingPosition[id], body.position));
                    }
                }
                Debug.Log($"BAKERY_TOAST {(tapered ? "tapered" : "straight")}: collider={contact.GetType().Name}, " +
                    $"bounds={contact.bounds.size}, sleep={toast.Count(piece => piece.Rigidbody.IsSleeping())}/{toast.Length}, " +
                    $"openingSpin={opening:F2}, tailSpin={tail:F2}, tailTurn={tailTurn:F2}, tailTravel={tailTravel:F3}");
                Require(opening <= .95f && tail <= .95f && tailTurn <= 7f && tailTravel <= .06f,
                    "thin toast opens and settles without sustained spin");
            }

            var world = page.World;
            for (int id = 2; id < world.Pieces.Length; id++)
            {
                world.RemoveFromBasket(id);
                world.Pieces[id].Root.gameObject.SetActive(false);
            }
            for (int id = 0; id < 2; id++)
            {
                var piece = world.Pieces[id];
                foreach (var collider in piece.Root.GetComponents<Collider>()) collider.enabled = false;
                piece.Root.gameObject.AddComponent<BoxCollider>().size = new Vector3(.8f, .28f, .8f);
                var body = piece.Rigidbody;
                body.position = piece.Root.parent.position + Vector3.up * (.15f + id * .29f);
                body.rotation = Quaternion.identity;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
            }
            Advance(page, 110, false);
            var upper = world.Pieces[1].Rigidbody;
            Require(upper.IsSleeping(), "stacked fixture has a sleeping piece supported above the floor");
            float height = upper.position.y;
            world.RemoveFromBasket(0);
            Advance(page, 55, false);
            Debug.Log($"BAKERY_REFLOW fall={height - upper.position.y:F3}, sleeping={upper.IsSleeping()}");
            Require(height - upper.position.y > .20f, "removing a support wakes a sleeping piece and it falls under gravity");
        }

        private static void CheckEntryTheme(BakeryPage page, BakeryPreferences preferences)
        {
            preferences.RandomOnEntry = true;
            page.Enter();
            var drawn = page.ActiveTheme;
            preferences.Theme = BakeryThemeCatalog.All.First(theme => theme.Theme != drawn).Theme;
            page.StartRound(true);
            Require(page.ActiveTheme == drawn, "changing the basket keeps the entry theme");
            preferences.RandomOnEntry = false;
            page.Enter();
            Require(page.ActiveTheme == preferences.Theme, "re-entering with random disabled loads the last manual theme");
        }

        private static void CheckJadeFinishes()
        {
            var jadeShader = Resources.Load<Shader>("BakeryBasket/BakeryJade");
            Require(jadeShader != null && jadeShader.isSupported, "the jade finish has a supported shader");
            var paints = new Color[BakeryRound.KindCount];
            var transmission = new float[paints.Length];
            var metal = new float[paints.Length];
            bool completeOptics = true;
            for (int kind = 0; kind < paints.Length; kind++)
            {
                var shape = BakeryFood.CreateJade(kind);
                var colors = shape.Surface.colors;
                var optics = shape.Surface.uv2;
                paints[kind] = colors.Aggregate(Color.clear, (sum, color) => sum + color) / colors.Length;
                completeOptics &= optics.Length == colors.Length &&
                    shape.Surface.normals.All(normal => normal.sqrMagnitude > .5f);
                transmission[kind] = optics.Select(value => value.x).DefaultIfEmpty().Average();
                metal[kind] = optics.Select(value => value.y).DefaultIfEmpty().Average();
                Object.Destroy(shape.Surface);
                foreach (var part in shape.ContactParts) Object.Destroy(part);
            }
            Require(paints.Max(paint => paint.a) - paints.Min(paint => paint.a) >= .45f &&
                paints.Max(paint => paint.grayscale) - paints.Min(paint => paint.grayscale) >= .45f,
                "jade kinds include visibly different polished and matte, pale and dark finishes");
            Require(completeOptics && transmission[3] > .6f && transmission[0] < .2f && metal[5] > .6f,
                "jade caps are lit and translucent jade, matte stone and brass have separate optics");
        }

        private static void CheckPreferences()
        {
            string[] keys = { "xanadu.bakery.v1.showBottom", "xanadu.bakery.v1.showRemaining", "xanadu.bakery.v1.theme", "xanadu.bakery.v1.randomOnEntry" };
            var original = keys.Select(key => PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null).ToArray();
            try
            {
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                var defaults = BakeryPreferences.Load();
                Require(defaults.ShowBottom && defaults.ShowRemaining && defaults.Theme == BakeryTheme.Pastry && defaults.RandomOnEntry,
                    "upgrading keeps existing display defaults and enables random entry");
                new BakeryPreferences { ShowBottom = false, ShowRemaining = true, Theme = BakeryTheme.Jade, RandomOnEntry = false }.Save();
                var saved = BakeryPreferences.Load();
                Require(!saved.ShowBottom && saved.ShowRemaining && saved.Theme == BakeryTheme.Jade && !saved.RandomOnEntry,
                    "settings save and reload the independent preferences");
                var random = new System.Random(112);
                Require(saved.ChooseForEntry(random) == BakeryTheme.Jade, "disabled random entry restores the manually selected theme");
                saved.RandomOnEntry = true;
                Require(Enumerable.Range(0, 64).Select(_ => saved.ChooseForEntry(random)).Distinct().Count() == BakeryThemeCatalog.All.Count &&
                    saved.Theme == BakeryTheme.Jade, "random entry reaches every theme while retaining the manual choice");
                foreach (var theme in BakeryThemeCatalog.All)
                {
                    saved.Theme = theme.Theme;
                    saved.Save();
                    Require(BakeryPreferences.Load().Theme == theme.Theme, theme.Title + " theme survives a settings reload");
                }
                PlayerPrefs.SetInt(keys[2], 100);
                Require(BakeryPreferences.Load().Theme == BakeryTheme.Pastry, "unknown saved theme falls back to pastry");
                PlayerPrefs.SetInt(keys[2], 0);
                Require(BakeryPreferences.Load().Theme == BakeryTheme.Pastry, "old stored value 0 keeps pastry");
                PlayerPrefs.SetInt(keys[2], 1);
                Require(BakeryPreferences.Load().Theme == BakeryTheme.Jade, "old stored value 1 keeps jade");
            }
            finally
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (original[i].HasValue) PlayerPrefs.SetInt(keys[i], original[i].Value);
                    else PlayerPrefs.DeleteKey(keys[i]);
                }
                PlayerPrefs.Save();
            }
        }

        private static void CheckRapidTaps(BakeryPage page, Canvas canvas, EventSystem eventSystem)
        {
            var world = page.World;
            var viewport = canvas.GetComponentInChildren<BakeryPointer>();
            var visible = Enumerable.Range(0, world.Pieces.Length).Where(id => world.TryFindVisiblePoint(id, out _)).ToArray();
            var groups = visible.GroupBy(page.Round.KindOf).Where(group => group.Count() >= 3).Take(2).Select(group => group.Key).ToArray();
            int otherKind = visible.Select(page.Round.KindOf).First(kind => !groups.Contains(kind));
            var order = Enumerable.Repeat(groups[0], 3).Concat(Enumerable.Repeat(groups[1], 3)).Append(otherKind);
            var picked = order.Select(Tap).ToArray();
            Require(page.DisplayedTray.Count == 0 && page.Round.Tray.SequenceEqual(picked.Skip(6)),
                "both triples and the next pick can be accepted before the first piece lands");
            Advance(page, 120, false);
            Require(page.DisplayedTray.SequenceEqual(picked.Skip(6)) && page.Round.Remaining == BakeryRound.PieceCount - 7 &&
                picked.Take(6).All(id => !world.Pieces[id].Root.gameObject.activeSelf), "rapid consecutive matches resolve in tap order");

            page.StartRound(false);
            world = page.World;
            int first = Tap(groups[0]);
            var differentKinds = new HashSet<int> { groups[0] };
            var kept = Enumerable.Range(0, 4).Select(_ => TapDifferent()).ToList();
            int second = Tap(groups[0]);
            int third = Tap(groups[0]);
            kept.Add(TapDifferent());
            Require(page.Round.Phase == BakeryPhase.Playing && page.Round.Tray.SequenceEqual(kept),
                "a seventh pick that matches frees slots for the next rapid pick");
            Advance(page, 120, false);
            Require(page.DisplayedTray.SequenceEqual(kept) && new[] { first, second, third }.All(id => !world.Pieces[id].Root.gameObject.activeSelf),
                "the seven-slot tray compacts before the following piece lands");

            int TapDifferent()
            {
                int kind = Enumerable.Range(0, world.Pieces.Length).Where(id => world.TryFindVisiblePoint(id, out _))
                    .Select(page.Round.KindOf).First(candidate => !differentKinds.Contains(candidate));
                differentKinds.Add(kind);
                return Tap(kind);
            }

            int Tap(int kind)
            {
                int id = Enumerable.Range(0, world.Pieces.Length).First(candidate => page.Round.KindOf(candidate) == kind && world.TryFindVisiblePoint(candidate, out _));
                Require(world.TryFindVisiblePoint(id, out var point), "rapid test target is visible at tap time");
                var tap = MakePointer(canvas, viewport, eventSystem, point);
                ExecuteEvents.Execute(viewport.gameObject, tap, ExecuteEvents.pointerDownHandler);
                Require(world.Selected == id, "rapid tap highlights the intended piece while earlier picks animate");
                ExecuteEvents.Execute(viewport.gameObject, tap, ExecuteEvents.pointerUpHandler);
                Require(!world.Pieces[id].InBasket, "rapid tap removes the selected piece immediately");
                Require(world.Pick(point) == -1, "a departing piece still occludes its original pixels while it is visible there");
                Advance(page, 3, false);
                return id;
            }
        }

        private static void CheckDisplay(BakeryPage page, Canvas canvas, BakeryPreferences preferences)
        {
            var labels = canvas.GetComponentsInChildren<Text>(true);
            var bottom = labels.Single(label => label.text.StartsWith("篮底小件", StringComparison.Ordinal));
            var remaining = labels.Single(label => label.text.StartsWith("篮中 ", StringComparison.Ordinal));
            var shakeResult = labels.Single(label => label.text.StartsWith("颠前篮底", StringComparison.Ordinal));
            var round = page.Round;
            preferences.ShowBottom = false;
            page.ApplyDisplay();
            Require(!bottom.gameObject.activeSelf && remaining.gameObject.activeSelf && !shakeResult.text.Contains("件"),
                "the bottom count switch also hides the count in the shake summary");
            preferences.ShowBottom = true;
            preferences.ShowRemaining = false;
            page.ApplyDisplay();
            Require(bottom.gameObject.activeSelf && !remaining.gameObject.activeSelf && page.Round == round,
                "both displays can be switched independently during a round");
            preferences.ShowRemaining = true;
            page.ApplyDisplay();
        }

        private static void CheckTheme(BakeryPage page, Camera camera, int[] pastryDeal, BakeryThemeCatalog theme)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            page.ChangeTheme(theme.Theme);
            Debug.Log($"BAKERY_THEME_SWITCH {theme.Theme}: {elapsed.ElapsedMilliseconds} ms");
            string stem = theme.Theme.ToString().ToLowerInvariant();
            foreach (bool tapered in new[] { false, true })
            {
                elapsed.Restart();
                page.StartRound(tapered);
                Debug.Log($"BAKERY_BASKET_SWITCH {theme.Theme} tapered={tapered}: {elapsed.ElapsedMilliseconds} ms");
                string name = stem + (tapered ? "-tapered" : "-straight");
                Capture(camera, name + ".png");
                if (tapered && theme.Theme == BakeryTheme.Jade) CheckContactShadows(page.World, "jade");
                var pieces = page.World.Pieces;
                Require(pieces.Select(piece => piece.Surfaces[0].sharedMesh).Distinct().Count() == BakeryRound.KindCount &&
                    pieces.All(piece => piece.Root.name.StartsWith(theme.Names[piece.Kind] + " ", StringComparison.Ordinal)) &&
                    pastryDeal.SequenceEqual(Enumerable.Range(0, BakeryRound.PieceCount).Select(page.Round.KindOf)),
                    name + " loads 15 theme-specific shapes with the same deal");
                if (theme.Theme == BakeryTheme.Produce)
                {
                    float apple = pieces.First(piece => piece.Kind == 3).Length;
                    float pineapple = pieces.First(piece => piece.Kind == 2).Length;
                    float grapes = pieces.First(piece => piece.Kind == 8).Length;
                    Require(pineapple >= 1.8f * apple && grapes >= 1.3f * apple,
                        "a pineapple and a full grape bunch are bigger than an apple in the basket");
                }
                if (!tapered) CaptureModels(pieces, stem);
                float coverage = Coverage(page.World, 2.7f, page.World.Depth * .5f);
                float highest = pieces.Max(piece => piece.Renderers[0].bounds.max.y - piece.Root.parent.position.y);
                Debug.Log($"BAKERY_COVERAGE {name} opening: {coverage:P1}, highest={highest:F2}, recoveries={page.World.Recoveries}");
                Require(coverage >= .86f && page.World.Recoveries == 0, name + " starts with a dense physical pile");
                CheckShake(page, name);
                int visible = Enumerable.Range(0, pieces.Length).First(id => page.World.TryFindVisiblePoint(id, out _));
                Require(page.World.TryFindVisiblePoint(visible, out var point) && page.Press(point) && page.World.Selected == visible,
                    name + " picks the visible model surface");
                page.Release(true);
                Advance(page, 55, false);
                Require(page.DisplayedTray.SequenceEqual(new[] { visible }), name + " flies into the tray");
                CheckAfterSixty(page, camera, tapered, name);
            }
        }

        private static void CaptureModels(BakeryWorld.Piece[] pieces, string stem)
        {
            var gallery = new GameObject("Theme model capture");
            var camera = new GameObject("Theme model camera").AddComponent<Camera>();
            camera.transform.SetParent(gallery.transform, false);
            camera.transform.rotation = Quaternion.Euler(62, 0, 0);
            camera.transform.position = -camera.transform.forward * 15;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2.44f;
            camera.cullingMask = 1 << pieces[0].Root.gameObject.layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiFactory.Hex("EEE9DF");
            camera.targetTexture = new RenderTexture(1000, 600, 24) { antiAliasing = 2 };
            try
            {
                var models = new Transform[BakeryRound.KindCount];
                foreach (var piece in pieces.GroupBy(piece => piece.Kind).Select(group => group.First()))
                {
                    int kind = piece.Kind;
                    var model = new GameObject(piece.Root.name).transform;
                    model.SetParent(gallery.transform, false);
                    model.position = camera.transform.right * ((kind % 5 - 2) * 1.56f) +
                        camera.transform.up * ((1 - kind / 5) * 1.52f);
                    model.rotation = Quaternion.Euler(0, 22, 8);
                    model.localScale = Vector3.one * (stem == "produce" ? 1 : 1.20f / piece.Length);
                    Object.Instantiate(piece.Root.GetChild(0).gameObject, model, false);
                    models[kind] = model;
                }
                Capture(camera, stem + "-models.png");
                foreach (var model in models) model.Rotate(0, 0, 84);
                Capture(camera, stem + "-models-rotated.png");
            }
            finally
            {
                gallery.SetActive(false);
                Object.Destroy(camera.targetTexture);
                Object.Destroy(gallery);
            }
        }

        private static PointerEventData MakePointer(Canvas canvas, BakeryPointer target, EventSystem system, Vector2 viewport)
        {
            var rect = (RectTransform)target.transform;
            var local = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, viewport.x),
                Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, viewport.y));
            return new PointerEventData(system)
            {
                pointerId = -1,
                position = canvas.worldCamera.WorldToScreenPoint(rect.TransformPoint(local)),
                button = PointerEventData.InputButton.Left,
                pointerPressRaycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() }
            };
        }

        private static int FindPartiallyCoveredSmall(BakeryWorld world, out Vector2 visiblePoint)
        {
            var camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(view => view.targetTexture == world.Texture);
            visiblePoint = default;
            foreach (int id in Enumerable.Range(0, world.Pieces.Length).Where(id => world.Pieces[id].IsSmall))
            {
                if (!world.TryFindVisiblePoint(id, out var open)) continue;
                foreach (var model in world.Pieces[id].Surfaces)
                {
                    var mesh = new BakeryMesh(model.sharedMesh);
                    var bounds = model.sharedMesh.bounds;
                    for (int y = -3; y <= 3; y++)
                        for (int x = -3; x <= 3; x++)
                        {
                            var local = bounds.center + new Vector3(x * bounds.extents.x / 3, 0, y * bounds.extents.z / 3);
                            Vector3 screen = camera.WorldToViewportPoint(model.transform.TransformPoint(local));
                            Ray ray = camera.ViewportPointToRay(screen);
                            float distance = camera.farClipPlane;
                            if (!mesh.Intersect(ray, model.transform, ref distance)) continue;
                            int hit = world.Pick(screen);
                            if (hit < 0 || hit == id) continue;
                            visiblePoint = open;
                            Debug.Log($"BAKERY_OCCLUSION #{id} exposed at {open}, #{hit} blocks another point");
                            return id;
                        }
                }
            }
            return -1;
        }

        private static void CheckPickingDepth()
        {
            var triangle = new Mesh { vertices = new[] { new Vector3(-1,-1,0), new Vector3(0,1,0), new Vector3(1,-1,0) }, triangles = new[] { 0, 1, 2 } };
            var mesh = new BakeryMesh(triangle);
            var near = new GameObject("Near surface");
            near.transform.position = Vector3.forward * 2;
            near.transform.localScale = Vector3.one * .5f;
            var far = new GameObject("Far surface");
            far.transform.position = Vector3.forward * 5;
            far.transform.localScale = Vector3.one * 3;
            var ray = new Ray(Vector3.zero, Vector3.forward);
            float distance = 10;
            Require(mesh.Intersect(ray, near.transform, ref distance) && Mathf.Approximately(distance, 2) &&
                !mesh.Intersect(ray, far.transform, ref distance), "pick depth stays in world units across differently scaled meshes");
            Object.Destroy(near);
            Object.Destroy(far);
            Object.Destroy(triangle);
        }

        private static void CheckRingContact(BakeryWorld world)
        {
            var ring = world.Pieces.First(piece => piece.Kind == BakeryFood.Doughnut);
            var colliders = ring.Root.GetComponents<Collider>();
            Ray center = new Ray(ring.Root.TransformPoint(Vector3.down), ring.Root.up);
            Ray dough = new Ray(ring.Root.TransformPoint(new Vector3(.44f, -1, 0)), ring.Root.up);
            Require(colliders.Length > 1 && !colliders.Any(collider => collider.Raycast(center, out _, 2)) &&
                colliders.Any(collider => collider.Raycast(dough, out _, 2)), "glazed ring has contacts on its dough and an unobstructed center hole");
        }

        private static void CheckOpening(BakeryWorld world, string name)
        {
            Transform pan = world.Pieces[0].Root.parent.Find("Bakery basket");
            var piecesOverRim = world.Pieces.Count(piece => piece.Renderers.Max(renderer => renderer.bounds.max.y) - pan.position.y >= world.Depth);
            float highest = world.Pieces.Max(piece => piece.Renderers.Max(renderer => renderer.bounds.max.y) - pan.position.y);
            Debug.Log($"BAKERY_HEIGHT {name}: highest={highest:F2}, overRim={piecesOverRim}, recoveries={world.Recoveries}");
            float coverage = Coverage(world, 2.7f, world.Depth * .5f);
            Debug.Log($"BAKERY_COVERAGE {name} opening: {coverage:P1}");
            Require(coverage >= .88f && piecesOverRim >= 3, name + " opening fills the basket projection and reaches the rim");
        }

        private static void CheckAfterSixty(BakeryPage page, Camera camera, bool tapered, string name)
        {
            page.StartRound(tapered);
            var world = page.World;
            var ids = Enumerable.Range(0, BakeryRound.PieceCount).ToArray();
            int shakes = 0;
            while (page.Round.Remaining > 120)
            {
                int kind = page.Round.Tray.Count > 0 ? page.Round.KindOf(page.Round.Tray[0]) : -1;
                var visible = ids.Where(id => (kind < 0 || page.Round.KindOf(id) == kind) &&
                    world.TryFindVisiblePoint(id, out _));
                int selected = kind >= 0 ? visible.DefaultIfEmpty(-1).First() : visible.GroupBy(page.Round.KindOf)
                    .Where(group => group.Count() >= 3)
                    .OrderByDescending(group => world.Pieces[group.First()].Length)
                    .Select(group => group.First()).DefaultIfEmpty(-1).First();
                if (selected < 0)
                {
                    Require(shakes++ < 8, name + " still offers playable triples before 60 removals");
                    page.Shake();
                    Advance(page, 120, false);
                    continue;
                }
                int remaining = page.Round.Remaining;
                Require(world.TryFindVisiblePoint(selected, out var point) && page.Press(point) && world.Selected == selected,
                    name + " surface tap takes the intended piece");
                page.Release(true);
                Advance(page, 55, false);
                if (page.Round.Remaining != remaining - 1) throw new InvalidOperationException("Tap did not reach the tray");
            }
            Advance(page, 90, false);
            world.Render();
            Capture(camera, name + "-after-60.png");
            float coverage = Coverage(world, 1.5f, .6f);
            Debug.Log($"BAKERY_COVERAGE {name} after 60: {coverage:P1}, shakes={shakes}, recoveries={world.Recoveries}");
            Require(page.Round.Remaining == 120 && page.Round.Tray.Count == 0 && world.Pieces.Count(piece => piece.InBasket) == 120 &&
                coverage >= .86f && world.Recoveries == 0, name + " has a crowded center after 20 complete triples");
        }

        private static float Coverage(BakeryWorld world, float radius, float height)
        {
            var view = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(camera => camera.targetTexture == world.Texture);
            var pan = world.Pieces[0].Root.parent.Find("Bakery basket");
            int hit = 0, sample = 0;
            for (int z = -12; z <= 12; z++)
                for (int x = -12; x <= 12; x++)
                {
                    if (x * x + z * z > 144) continue;
                    Vector3 point = pan.TransformPoint(new Vector3(x * radius / 12, height, z * radius / 12));
                    if (world.Pick(view.WorldToViewportPoint(point)) >= 0) hit++;
                    sample++;
                }
            return (float)hit / sample;
        }

        private static int CheckShake(BakeryPage page, string name)
        {
            var world = page.World;
            var before = world.BottomSmall();
            int[] hidden = before.Where(id => !world.TryFindVisiblePoint(id, out _)).ToArray();
            int recoveries = world.Recoveries;
            Transform pan = world.Pieces[0].Root.parent.Find("Bakery basket");
            Vector3 start = pan.position;
            var positions = world.Pieces.Select(piece => piece.Root.position).ToArray();
            page.Shake();
            float top = 0, drift = 0, flight = 0, tilt = 0;
            for (int frame = 0; frame < Mathf.CeilToInt(1.32f / BakeryWorld.Step); frame++)
            {
                Advance(page, 1);
                Vector3 travel = pan.position - start;
                top = Mathf.Max(top, travel.y);
                drift = Mathf.Max(drift, new Vector2(travel.x, travel.z).magnitude);
                tilt = Mathf.Max(tilt, Quaternion.Angle(pan.rotation, Quaternion.identity));
                for (int id = 0; id < positions.Length; id++)
                    flight = Mathf.Max(flight, world.Pieces[id].Root.position.y - positions[id].y);
            }
            Debug.Log($"BAKERY_TOSS {name}: panLift={top:F3}, panDrift={drift:F3}, tilt={tilt:F2}, maximumPieceLift={flight:F3}");
            Require(top > .15f && drift < .025f && tilt < 3 && flight > .36f && flight < world.Depth * .8f &&
                Vector3.Distance(start, pan.position) < .01f,
                name + " pan lifts the pieces vertically and returns to its starting position");
            int revealed = hidden.Count(id => world.TryFindVisiblePoint(id, out _));
            Require(before.Count > 0 && page.Round.Remaining == BakeryRound.PieceCount && world.Pieces.All(piece => piece.InBasket) && world.Recoveries == 0,
                name + " shake preserves the entire deal without repositioning escaped objects");
            Debug.Log($"BAKERY_MEASURE {name}: bottom={before.Count}, visible={before.Count - hidden.Length}->{world.VisibleCount(before)}, newlyVisible={revealed}, recoveries={recoveries}+{world.Recoveries - recoveries}");
            Debug.Log($"BAKERY_PEAK {name}: {page.NewlyVisibleDuringShake}");
            return page.NewlyVisibleDuringShake;
        }

        private static void CheckTaperedFloorMovement(BakeryPage page)
        {
            var world = page.World;
            var bottom = world.BottomSmall();
            var positions = bottom.Select(id => world.Pieces[id].Root.position).ToArray();
            int recoveries = world.Recoveries;
            page.Shake();
            Advance(page, 170, false);
            float displacement = bottom.Select((id, i) => Vector3.Distance(positions[i], world.Pieces[id].Root.position)).DefaultIfEmpty(0).Max();
            Debug.Log($"BAKERY_FLOOR after 60: bottom={bottom.Count}, displacement={displacement:F3}, exposed={page.NewlyVisibleDuringShake}");
            Require(bottom.Count > 0 && displacement > .07f && world.Recoveries == recoveries,
                "the deeper basket redistributes floor pieces after 20 complete triples without forcing any position");
        }

        private static void Advance(BakeryPage page, int steps, bool render = true)
        { for (int i = 0; i < steps; i++) page.Advance(BakeryWorld.Step, render); }

        private static void CheckContactShadows(BakeryWorld world, string name)
        {
            var stage = world.Pieces[0].Root.parent;
            var materials = stage.GetComponentsInChildren<MeshRenderer>().SelectMany(renderer => renderer.sharedMaterials)
                .Distinct().Where(material => material.HasProperty("_BakeryShadowMap")).ToArray();
            var maps = materials.Select(material => material.GetTexture("_BakeryShadowMap")).ToArray();
            var shaded = CapturePixels(world.Texture);
            Color32[] unshaded;
            try
            {
                foreach (var material in materials) material.SetTexture("_BakeryShadowMap", Texture2D.whiteTexture);
                world.Render();
                unshaded = CapturePixels(world.Texture);
            }
            finally
            {
                for (int i = 0; i < materials.Length; i++) materials[i].SetTexture("_BakeryShadowMap", maps[i]);
                world.Render();
            }
            int covered = 0, shadowed = 0, directlyLit = 0;
            for (int i = 0; i < shaded.Length; i++)
            {
                if (shaded[i].a < 250 || unshaded[i].a < 250) continue;
                covered++;
                int darkening = unshaded[i].r + unshaded[i].g + unshaded[i].b - shaded[i].r - shaded[i].g - shaded[i].b;
                if (darkening >= 42) shadowed++;
                if (Mathf.Abs(darkening) <= 3) directlyLit++;
            }
            Debug.Log($"BAKERY_LIGHT {name}: surface={covered}, visiblyShadowed={shadowed}, unchanged={directlyLit}");
            Require(covered > 10000 && shadowed > covered * .08f && directlyLit > covered * .2f,
                name + " has localized cast shadows and directly lit surfaces");
            var map = maps.OfType<RenderTexture>().Distinct().Single();
            map.Release();
            world.Render();
            Require(map.IsCreated() && shaded.SequenceEqual(CapturePixels(world.Texture)),
                name + " restores the same shadows when the render texture is lost");
        }

        private static Color32[] CapturePixels(RenderTexture target)
        {
            var active = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                return pixels.GetPixels32();
            }
            finally
            {
                RenderTexture.active = active;
                Object.Destroy(pixels);
            }
        }

        private static void Capture(Camera camera, string filename)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var active = RenderTexture.active;
            var texture = camera.targetTexture;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, filename), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active;
                Object.Destroy(pixels);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("BAKERY_PASS " + message);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
