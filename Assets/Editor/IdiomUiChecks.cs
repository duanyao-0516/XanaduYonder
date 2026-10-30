using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.Idiom;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Idiom;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Uses the production page, UGUI geometry, generated icon meshes and real
    // GraphicRaycaster. Layout fixtures have no stores; learning fixtures use
    // isolated temporary folders. None call the app's Awake or use player saves.
    public static class IdiomUiChecks
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int assertions;
        private static readonly StringBuilder report = new StringBuilder();

        public static void Run()
        {
            var oldFont = UiFactory.Font;
            var exitCode = 0;
            assertions = 0;
            report.Clear();
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled CJK font must load");
                CheckNativeKeyboardSamples();
                foreach (var size in new[] { new Vector2Int(720, 1280), new Vector2Int(720, 1582), new Vector2Int(900, 1582) })
                {
                    using (var fixture = new Fixture(size.x, size.y))
                    {
                        CheckSharedIcons(fixture);
                        foreach (IdiomMatchKind match in Enum.GetValues(typeof(IdiomMatchKind)))
                        foreach (IdiomMode mode in Enum.GetValues(typeof(IdiomMode)))
                        foreach (var first in new[] { IdiomActor.Left, IdiomActor.Right })
                            CheckPage(fixture, match, mode, first);
                        CheckModalRaycasts(fixture);
                        CheckSourceDefinition(fixture);
                        CheckConversationPreferences(fixture);
                        CheckViewportAnchors(fixture);
                        foreach (IdiomMatchKind match in Enum.GetValues(typeof(IdiomMatchKind)))
                        foreach (var closeAfterSend in new[] { true, false })
                            CheckSendFocus(fixture, match, closeAfterSend);
                        foreach (IdiomMatchKind match in Enum.GetValues(typeof(IdiomMatchKind)))
                        foreach (IdiomMode mode in Enum.GetValues(typeof(IdiomMode)))
                            CheckThirdHintContinues(fixture, match, mode);
                    }
                    report.AppendLine("PASS " + size.x + "x" + size.y + ": page states, keyboard transitions, send focus, saved preferences, adaptive bubbles, scroll anchors, mirrored turns, notebook, modal raycasts and full multi-source details");
                }
                report.AppendLine("IDIOM_UI_CHECKS_PASS: " + assertions + " assertions");
                Debug.Log(report.ToString());
            }
            catch (Exception exception)
            {
                exitCode = 1;
                report.AppendLine("IDIOM_UI_CHECKS_FAIL after " + assertions + " assertions\n" + exception);
                Debug.LogException(exception);
            }
            finally
            {
                UiFactory.Font = oldFont;
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/logs"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "idiom-ui-checks.txt"), report.ToString());
            }
            EditorApplication.Exit(exitCode);
        }

        private static void CheckNativeKeyboardSamples()
        {
            bool visible, foreground; float occlusion;
            Require(IdiomKeyboardViewport.TryConvertNativeSample(new[] { 1, 600, 2400, 1, 1080 }, 540, 1200,
                out visible, out foreground, out occlusion), "Same-aspect native geometry supports scaled rendering");
            Require(visible && foreground, "Visibility and foreground come from the same native packet");
            Near(occlusion, 300, "Native keyboard overlap is converted once into rendering pixels");
            Require(IdiomKeyboardViewport.TryConvertNativeSample(new[] { 1, 0, 1800, 1, 1080 }, 1080, 1800,
                out visible, out foreground, out occlusion) && visible && occlusion == 0,
                "An already-resized surface does not subtract the keyboard a second time");
            Require(!IdiomKeyboardViewport.TryConvertNativeSample(new[] { 1, 600, 2400, 1, 1080 }, 1080, 1800,
                out visible, out foreground, out occlusion), "Old native geometry is rejected after Unity's surface resize");
            Require(IdiomKeyboardViewport.TryConvertNativeSample(new[] { 0, 600, 2400, 0, 1080 }, 1080, 2400,
                out visible, out foreground, out occlusion) && !visible && !foreground && occlusion == 0,
                "A closed/background packet clears stale overlap");
            foreach (var sample in new int[][] { null, new[] { 1, 600, 2400, 1 }, new[] { 1, -1, 2400, 1, 1080 },
                new[] { 1, 2401, 2400, 1, 1080 }, new[] { 2, 600, 2400, 1, 1080 }, new[] { 1, 600, 0, 1, 1080 } })
                Require(!IdiomKeyboardViewport.TryConvertNativeSample(sample, 1080, 2400, out visible, out foreground, out occlusion),
                    "Malformed native geometry cannot produce a layout inset");
        }

        private static void CheckSharedIcons(Fixture fixture)
        {
            var group = UiFactory.Rect(fixture.Root, "Icon checks", 0, 0, 500, 100);
            try
            {
                var x = 0f;
                foreach (UiIconKind kind in Enum.GetValues(typeof(UiIconKind)))
                {
                    var button = UiFactory.IconButton(group, kind, x, 0, 58, 58, () => { });
                    var rect = (RectTransform)button.transform;
                    var icon = button.GetComponentInChildren<UiIconGraphic>();
                    Near(rect.rect.width, 58, kind + " hit width");
                    Near(rect.rect.height, 58, kind + " hit height");
                    Require(button.GetComponentsInChildren<Text>().Length == 0, kind + " must not use a font glyph");
                    Near(Center(icon.rectTransform), Center(rect), kind + " centered icon rect");
                    Require(icon.color == UiFactory.Ink, kind + " retains old foreground color");
                    CheckMesh(icon, kind == UiIconKind.Close);
                    x += 78;
                }
            }
            finally { Object.DestroyImmediate(group.gameObject); }
        }

        private static void CheckPage(Fixture fixture, IdiomMatchKind match, IdiomMode mode, IdiomActor first)
        {
            var pageRoot = UiFactory.Rect(fixture.Root, "Page under test", 0, 0, fixture.Width, fixture.Height);
            UiFactory.Stretch(pageRoot);
            try
            {
                var page = pageRoot.gameObject.AddComponent<IdiomPage>();
                var lobbyVisits = 0;
                page.Initialize(pageRoot, () => lobbyVisits++, () => true, _ => { },
                    (title, height, back) => { throw new InvalidOperationException("Unexpected modal in layout fixture: " + title); }, () => { });
                var lexicon = TestLexicon();
                Set(page, "lexicon", lexicon);
                var save = (IdiomSaveData)Invoke(page, "NewSave");
                save.ActiveMatchKind = match;
                save.ActiveMode = mode;
                Set(page, "data", save);
                Set(page, "notebook", new IdiomNotebookData());
                Invoke(page, "RestoreGames");
                Set(page, "loaded", true);
                Set(page, "visible", true);
                Invoke(page, "Refresh", true);
                fixture.Flush();

                var label = match + "/" + mode + "/" + first;
                var gameRoot = Get<RectTransform>(page, "gameRoot");
                CheckGameNavigation(fixture, page, label);
                var title = (RectTransform)gameRoot.Find("Idiom centered title");
                var count = Get<Text>(page, "countText");
                Near(Center(title).x, Center(fixture.Root).x, label + " title centered in actual canvas");
                Near(Center(count.rectTransform).x, Center(fixture.Root).x, label + " count centered in actual canvas");
                Require(title.GetComponent<Text>().alignment == TextAnchor.MiddleCenter, label + " centered title alignment");
                var ruleSwitch = Get<IdiomRuleSwitch>(page, "ruleSwitch");
                Require(WorldRect(count.rectTransform).xMax < WorldRect((RectTransform)ruleSwitch.transform).xMin,
                    label + " count does not overlap rule switch");
                Require(count.preferredWidth <= count.rectTransform.rect.width + .5f, label + " rendered count fits");
                var normalCount = count.text;
                count.text = "本轮完成 42851 个";
                Require(count.preferredWidth <= count.rectTransform.rect.width + .5f, label + " full dictionary count fits");
                count.text = normalCount;
                CheckRuleSwitch(ruleSwitch, label);
                ruleSwitch.SetState(mode == IdiomMode.SameSound, true, false);

                var left = Get<Button>(page, "leftStart");
                var right = Get<Button>(page, "rightStart");
                Require(left.GetComponentInChildren<Text>().text == (match == IdiomMatchKind.Local ? "左方先开始" : "机器开局"), label + " left starter caption");
                Require(right.GetComponentInChildren<Text>().text == (match == IdiomMatchKind.Local ? "右方先开始" : "人先开始"), label + " right starter caption");
                (first == IdiomActor.Left ? left : right).onClick.Invoke();
                var game = (IdiomGame)typeof(IdiomPage).GetProperty("Game", PrivateInstance).GetValue(page);
                Require(game.IsStarted && game.FirstActor == first, label + " real starter callback");
                CheckComposer(fixture, page, game, label);

                var submissions = match == IdiomMatchKind.Local ? 2 : 1;
                for (var turn = 0; turn < submissions; turn++)
                {
                    var previousActor = game.NextActor;
                    var previousCount = game.History.Count;
                    var candidate = NextWord(lexicon, game);
                    Get<InputField>(page, "input").text = candidate.Word;
                    Get<Button>(page, "send").onClick.Invoke();
                    Require(game.History.Count > previousCount, label + " real send callback advances history");
                    if (match == IdiomMatchKind.Local)
                        Require(game.NextActor != previousActor, label + " local actor alternates");
                    CheckComposer(fixture, page, game, label + " turn " + turn);
                    CheckTranscript(page, game, label);
                }
                CheckKeyboardNavigation(fixture, page, game, label);
                var historyBeforeNotebook = game.History.Count;
                fixture.Click(Center((RectTransform)Get<Button>(page, "bookButton").transform));
                fixture.Flush();
                var book = Get<RectTransform>(page, "bookRoot");
                Require(book.gameObject.activeInHierarchy && !gameRoot.gameObject.activeSelf, label + " notebook page opens");
                var bookTitle = (RectTransform)book.Find("生词本");
                Near(Center(bookTitle).x, Center(fixture.Root).x, label + " notebook title centered in actual canvas");
                var back = (RectTransform)book.Find("‹ 返回");
                CheckScreenRect(fixture, back, 56, 34, 130, 58, label + " notebook back");
                CheckScreenRect(fixture, bookTitle, 210, 30, 300, 66, label + " notebook title");
                CheckScreenRect(fixture, Get<Text>(page, "bookCount").rectTransform, 500, 40, 188, 50,
                    label + " notebook count");
                Require(bookTitle.GetComponent<Text>().fontSize == 35 && Get<Text>(page, "bookCount").fontSize == 22,
                    label + " notebook title and count retain their font sizes");
                Require(!ScreenRect(fixture, back).Overlaps(ScreenRect(fixture, bookTitle)),
                    label + " notebook back does not overlap title");
                fixture.Click(Center(back));
                Require(gameRoot.gameObject.activeInHierarchy, label + " return preserves page");
                Require(!book.gameObject.activeSelf && !Get<bool>(page, "notebookVisible"), label + " actual back click closes notebook");
                Require(ReferenceEquals(game, typeof(IdiomPage).GetProperty("Game", PrivateInstance).GetValue(page)) &&
                    game.History.Count == historyBeforeNotebook, label + " notebook round trip preserves active game and history");
                CheckGameNavigation(fixture, page, label + " returned");
                fixture.Click(Center((RectTransform)gameRoot.Find("Home button")));
                Require(lobbyVisits == 1, label + " aligned home hit target invokes lobby once");
                Require(Get<IdiomSaveStore>(page, "store") == null,
                    "Layout checks must never own persistent stores");
            }
            finally { Object.DestroyImmediate(pageRoot.gameObject); }
        }

        private static void CheckGameNavigation(Fixture fixture, IdiomPage page, string label)
        {
            fixture.Flush();
            var gameRoot = Get<RectTransform>(page, "gameRoot");
            var home = (RectTransform)gameRoot.Find("Home button");
            var notebook = (RectTransform)Get<Button>(page, "bookButton").transform;
            var settings = (RectTransform)gameRoot.Find("Settings button");
            var title = (RectTransform)gameRoot.Find("Idiom centered title");
            CheckScreenRect(fixture, home, 56, 34, 58, 58, label + " home");
            CheckScreenRect(fixture, notebook, 536, 34, 58, 58, label + " notebook");
            CheckScreenRect(fixture, settings, 606, 34, 58, 58, label + " settings");
            CheckScreenRect(fixture, title, 200, 30, 320, 66, label + " title");
            Require(title.GetComponent<Text>().fontSize == 35 && Get<Text>(page, "countText").fontSize == 23,
                label + " game title and count retain their font sizes");
            var titleBounds = ScreenRect(fixture, title);
            Near(titleBounds.center.x, fixture.Width * .5f, label + " title stays centered in screen");
            foreach (var button in new[] { home, notebook, settings })
            {
                var bounds = ScreenRect(fixture, button);
                Near(bounds.center.y, titleBounds.center.y, label + " navigation shares title row");
                Require(!bounds.Overlaps(titleBounds), label + " navigation does not overlap title");
                foreach (var mode in new[] { Get<Button>(page, "computerMatch"), Get<Button>(page, "localMatch") })
                {
                    var modeBounds = ScreenRect(fixture, (RectTransform)mode.transform);
                    Near(fixture.Height - modeBounds.yMax, 108, label + " mode row retains top position");
                    Require(bounds.yMin > modeBounds.yMax && titleBounds.yMin > modeBounds.yMax,
                        label + " navigation and title stay above mode row");
                }
            }
        }

        private static void CheckKeyboardNavigation(Fixture fixture, IdiomPage page, IdiomGame game, string label)
        {
            var keyboard = Get<IdiomKeyboardViewport>(page, "keyboard");
            var insetSetter = typeof(IdiomKeyboardViewport).GetProperty("BottomInset").GetSetMethod(true);
            var visibleSetter = typeof(IdiomKeyboardViewport).GetProperty("IsVisible").GetSetMethod(true);
            var readySetter = typeof(IdiomKeyboardViewport).GetProperty("LayoutMeasurementReady").GetSetMethod(true);
            var footer = Get<RectTransform>(page, "footer");
            var originalBottom = ScreenRect(fixture, footer).yMin;
            var viewport = Get<IdiomVirtualList>(page, "chat").Viewport;
            var title = (RectTransform)Get<RectTransform>(page, "gameRoot").Find("Idiom centered title");
            var originalTitle = ScreenRect(fixture, title).center;
            var originalChatBottom = ScreenRect(fixture, viewport).yMin;
            // Supply a measured IME inset at the existing platform boundary. The
            // real page layout and raycaster run; this is not a native IME test.
            insetSetter.Invoke(keyboard, new object[] { 360f });
            visibleSetter.Invoke(keyboard, new object[] { true });
            try
            {
                Invoke(page, "Layout");
                Near(ScreenRect(fixture, footer).yMin, originalBottom, label + " new inset starts a transition without snapping");
                Invoke(page, "AdvanceComposerTransition", .06f);
                var middleBottom = ScreenRect(fixture, footer).yMin;
                Require(middleBottom > originalBottom && middleBottom < 374,
                    label + " composer has an actual intermediate keyboard position");
                Near(ScreenRect(fixture, viewport).yMin - originalChatBottom, middleBottom - originalBottom,
                    label + " conversation and input move by the same amount");
                Near(ScreenRect(fixture, title).center, originalTitle, label + " title remains fixed during keyboard animation");
                // A native packet from the previous surface must freeze both the
                // new target and an already-running transition until geometry agrees.
                var heldPosition = footer.anchoredPosition;
                var heldHeight = viewport.rect.height;
                readySetter.Invoke(keyboard, new object[] { false });
                insetSetter.Invoke(keyboard, new object[] { 420f });
                Invoke(page, "Layout"); Invoke(page, "AdvanceComposerTransition", .2f);
                Near(footer.anchoredPosition, heldPosition, label + " stale native geometry freezes the in-progress composer transition");
                Near(viewport.rect.height, heldHeight, label + " stale native geometry also freezes the chat viewport");
                readySetter.Invoke(keyboard, new object[] { true });
                Invoke(page, "Layout");
                Invoke(page, "AdvanceComposerTransition", .25f);
                Near(ScreenRect(fixture, footer).yMin, 434, label + " fresh native geometry resumes toward the current keyboard inset");
                CheckGameNavigation(fixture, page, label + " keyboard open");
                CheckComposer(fixture, page, game, label + " keyboard open");
                Require(ScreenRect(fixture, footer).yMin > originalBottom,
                    label + " keyboard sample actually moves composer");
                Require(ScreenRect(fixture, footer).yMin >= 360,
                    label + " composer remains above keyboard inset");
            }
            finally
            {
                readySetter.Invoke(keyboard, new object[] { true });
                insetSetter.Invoke(keyboard, new object[] { 0f });
                visibleSetter.Invoke(keyboard, new object[] { false });
                Invoke(page, "Layout");
                var openBottom = ScreenRect(fixture, footer).yMin;
                Invoke(page, "AdvanceComposerTransition", .06f);
                var middleBottom = ScreenRect(fixture, footer).yMin;
                Require(middleBottom > originalBottom && middleBottom < openBottom,
                    label + " dismissal also has an intermediate position");
                Near(ScreenRect(fixture, title).center, originalTitle, label + " title stays fixed while dismissing keyboard");
                Invoke(page, "AdvanceComposerTransition", .25f);
            }
            fixture.Flush();
            Near(ScreenRect(fixture, footer).yMin, originalBottom, label + " keyboard dismissal restores composer");
            CheckGameNavigation(fixture, page, label + " keyboard closed");
        }

        private static void CheckScreenRect(Fixture fixture, RectTransform rect, float x, float y, float width, float height, string label)
        {
            Require(rect != null, label + " exists");
            var bounds = ScreenRect(fixture, rect);
            Near(bounds.xMin, (fixture.Width - 720) * .5f + x, label + " actual screen left");
            Near(fixture.Height - bounds.yMax, y, label + " actual screen top");
            Near(bounds.width, width, label + " actual screen width");
            Near(bounds.height, height, label + " actual screen height");
        }

        private static Rect ScreenRect(Fixture fixture, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(fixture.Camera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(fixture.Camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }

        private static void CheckComposer(Fixture fixture, IdiomPage page, IdiomGame game, string label)
        {
            fixture.Flush();
            var send = (RectTransform)Get<Button>(page, "send").transform;
            var hint = (RectTransform)Get<Button>(page, "hint").transform;
            var input = Get<InputField>(page, "input");
            var field = (RectTransform)input.transform;
            Require(send.gameObject.activeInHierarchy && hint.gameObject.activeInHierarchy && input.interactable,
                label + " active composer remains usable");
            var sendBounds = WorldRect(send);
            var hintBounds = WorldRect(hint);
            var fieldBounds = WorldRect(field);
            Require(!sendBounds.Overlaps(fieldBounds) && !hintBounds.Overlaps(fieldBounds) && !sendBounds.Overlaps(hintBounds),
                label + " real composer rectangles never overlap");
            var leftTurn = game.MatchKind == IdiomMatchKind.Local && game.NextActor == IdiomActor.Left;
            Require(leftTurn ? sendBounds.xMax < fieldBounds.xMin && hintBounds.xMin > fieldBounds.xMax :
                hintBounds.xMax < fieldBounds.xMin && sendBounds.xMin > fieldBounds.xMax, label + " send and hint mirror current actor");
            Near(Center(send).y, Center(field).y, label + " send aligned vertically");
            Near(Center(hint).y, Center(field).y, label + " hint aligned vertically");
            Require(WorldRect(Get<RectTransform>(page, "footer")).yMin >= WorldRect(fixture.Root).yMin,
                label + " composer stays inside canvas");
            var expectedCount = game.MatchKind == IdiomMatchKind.Local ? game.History.Count : game.PlayerCount;
            Require(Get<Text>(page, "countText").text == "本轮完成 " + expectedCount + " 个", label + " count semantics");
            if (game.MatchKind == IdiomMatchKind.Local)
            {
                var actor = game.NextActor == IdiomActor.Left ? "左方" : "右方";
                Require(((Text)input.placeholder).text.StartsWith(actor, StringComparison.Ordinal), label + " active actor placeholder");
            }
        }

        private static void CheckTranscript(IdiomPage page, IdiomGame game, string label)
        {
            var list = Get<IdiomVirtualList>(page, "chat");
            var rows = Get<List<IdiomVirtualList.Row>>(list, "rows");
            var found = 0;
            foreach (var text in Get<RectTransform>(page, "gameRoot").GetComponentsInChildren<Text>())
                Require(!text.text.StartsWith("请接", StringComparison.Ordinal), label + " permanent next-letter prompt is removed");
            foreach (var row in rows)
            {
                if (row.Index < 0 || !row.Root.gameObject.activeInHierarchy) continue;
                var right = row.Index % 2 == 0 ? game.FirstActor == IdiomActor.Right : game.FirstActor != IdiomActor.Right;
                var expected = game.MatchKind == IdiomMatchKind.Local ? right ? "右方" : "左方" : right ? "你" : "APP";
                Require(row.Actor.text == expected, label + " transcript actor " + row.Index);
                Require(right ? Center((RectTransform)row.Button.transform).x > Center(row.Root).x :
                    Center((RectTransform)row.Button.transform).x < Center(row.Root).x, label + " transcript side " + row.Index);
                Require(!row.Pinyin.gameObject.activeSelf, label + " pinyin is hidden by default");
                Require(row.Word.fontSize < 32, label + " smaller conversation type");
                Require(row.Button.GetComponent<RectTransform>().rect.width <= row.Word.preferredWidth + 48,
                    label + " bubble fits its idiom rather than a fixed column");
                found++;
            }
            Require(found > 0, label + " production virtual rows rendered");
        }

        private static void CheckConversationPreferences(Fixture fixture)
        {
            var inert = new GameObject("Inactive preferences shell");
            inert.SetActive(false); inert.transform.SetParent(fixture.Owner.transform, false);
            var shell = inert.AddComponent<AppController>();
            Set(shell, "canvasRoot", fixture.Root);
            var pageRoot = UiFactory.Rect(fixture.Root, "Preferences owner", 0, 0, fixture.Width, fixture.Height);
            UiFactory.Stretch(pageRoot);
            var directory = Path.Combine(Path.GetTempPath(), "xanadu-idiom-preferences-ui-" + Guid.NewGuid().ToString("N"));
            try
            {
                var page = pageRoot.gameObject.AddComponent<IdiomPage>();
                page.Initialize(pageRoot, () => { }, () => true, _ => { },
                    (title, height, back) => (RectTransform)Invoke(shell, "OpenIdiomModal", title, height, back),
                    () => DestroyModal(shell));
                var lexicon = TestLexicon(); Set(page, "lexicon", lexicon);
                var data = (IdiomSaveData)Invoke(page, "NewSave");
                Set(page, "data", data); Set(page, "notebook", data.Notebook);
                Invoke(page, "RestoreGames"); Set(page, "loaded", true); Set(page, "visible", true);
                var game = Get<IdiomGame>(page, "characterGame");
                Require(game.Start(IdiomActor.Player).Success && game.Submit("一心一意").Success, "Preferences fixture starts a live transcript");
                var disk = new IdiomSaveStore(directory, lexicon); string warning; disk.Load(out warning); Set(page, "store", disk);
                Invoke(page, "Refresh", true); page.Save();
                CheckTranscript(page, game, "default preferences");
                var chat = Get<IdiomVirtualList>(page, "chat");
                var firstRow = Get<List<IdiomVirtualList.Row>>(chat, "rows")[0];
                var compactHeight = firstRow.Root.rect.height;
                var historyCount = game.History.Count;
                fixture.Click(Center((RectTransform)Get<RectTransform>(page, "gameRoot").Find("Settings button")));
                var show = FindNamedButton(Get<GameObject>(shell, "modal"), "是否显示拼音 switch");
                var close = FindNamedButton(Get<GameObject>(shell, "modal"), "发送后收起键盘 switch");
                Require(!WorldRect((RectTransform)show.transform).Overlaps(WorldRect((RectTransform)close.transform)),
                    "Preference switches have separate touch targets");
                fixture.Click(Center((RectTransform)show.transform));
                Require(data.ShowPinyin, "Real settings switch enables pinyin immediately");
                fixture.Flush();
                foreach (var row in Get<List<IdiomVirtualList.Row>>(chat, "rows"))
                {
                    if (row.Index < 0 || !row.Root.gameObject.activeInHierarchy) continue;
                    Require(row.Pinyin.gameObject.activeSelf && !string.IsNullOrEmpty(row.Pinyin.text), "Enabled pinyin appears on existing conversation rows");
                    Require(row.Root.rect.height > compactHeight, "Showing pinyin gives it real vertical space");
                    Require(row.Pinyin.preferredWidth <= row.Pinyin.rectTransform.rect.width + .5f, "Displayed pinyin fits its adaptive bubble");
                    var right = row.Index % 2 == 0;
                    Require(row.Word.alignment == (right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft) &&
                        row.Pinyin.alignment == row.Word.alignment, "Word and pinyin retain their side alignment");
                }
                close = FindNamedButton(Get<GameObject>(shell, "modal"), "发送后收起键盘 switch");
                fixture.Click(Center((RectTransform)close.transform));
                Require(!data.CloseKeyboardAfterSend, "Real settings switch keeps keyboard after sending");
                var persisted = new IdiomSaveStore(directory, lexicon).Load(out warning);
                Require(persisted != null && persisted.ShowPinyin && !persisted.CloseKeyboardAfterSend,
                    "Both real settings clicks persist through a fresh disk read");
                show = FindNamedButton(Get<GameObject>(shell, "modal"), "是否显示拼音 switch");
                fixture.Click(Center((RectTransform)show.transform));
                Require(!data.ShowPinyin, "Pinyin can be switched off again");
                CheckTranscript(page, game, "pinyin disabled again");
                Near(firstRow.Root.rect.height, compactHeight, "Disabling pinyin restores compact row height");
                Require(game.History.Count == historyCount && !game.IsEnded, "Settings toggles preserve the live game");
                Invoke(page, "ReturnFromDialog");
            }
            finally
            {
                DestroyModal(shell); Object.DestroyImmediate(pageRoot.gameObject); Object.DestroyImmediate(inert);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static Button FindNamedButton(GameObject root, string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>()) if (button.name == name) return button;
            throw new InvalidOperationException("Missing actual setting switch: " + name);
        }

        private static void CheckViewportAnchors(Fixture fixture)
        {
            var owner = UiFactory.Rect(fixture.Root, "List anchor checks", 0, 0, 672, 600);
            try
            {
                var list = new IdiomVirtualList(owner, "Anchor transcript", 120, (row, index) => row.Word.text = "历史 " + index, _ => { });
                IdiomUi.Place(list.Viewport, 0, 0, 672, 240);
                list.SetCount(60, true); list.Tick();
                var historicalOffset = 60 * 120 - 240 - 100;
                list.SetOffset(historicalOffset);
                list.BeginViewportTransition();
                for (var height = 250; height <= 300; height += 10)
                {
                    IdiomUi.Place(list.Viewport, 0, 0, 672, height); list.Tick();
                    Near(list.Offset, historicalOffset, "Growing viewport must not acquire bottom following after crossing the 90-unit threshold");
                }
                list.EndViewportTransition();
                Near(list.Offset, historicalOffset, "History reading position is stable after viewport transition");
                list.GoBottom(); list.BeginViewportTransition();
                for (var height = 310; height <= 420; height += 10)
                {
                    IdiomUi.Place(list.Viewport, 0, 0, 672, height); list.Tick();
                    Near(list.Offset, 60 * 120 - height, "A transcript already at bottom stays pinned throughout keyboard movement");
                }
                list.EndViewportTransition();
                list.SetOffset(12 * 120 + 37);
                list.SetRowHeight(150); list.Tick();
                Near(list.Offset, 12 * 150 + 37, "Pinyin row-height change retains message index and pixel offset");
                list.SetRowHeight(120); list.Tick();
                Near(list.Offset, 12 * 120 + 37, "Repeated pinyin toggles return to the same history anchor");
                list.GoBottom(); list.SetRowHeight(150); list.Tick();
                Near(list.Offset, 60 * 150 - 420, "Pinyin changes keep an already-following transcript at bottom");
            }
            finally { Object.DestroyImmediate(owner.gameObject); }
        }

        private static void CheckSendFocus(Fixture fixture, IdiomMatchKind match, bool closeAfterSend)
        {
            var root = UiFactory.Rect(fixture.Root, "Send focus checks", 0, 0, fixture.Width, fixture.Height);
            UiFactory.Stretch(root);
            try
            {
                var page = root.gameObject.AddComponent<IdiomPage>();
                page.Initialize(root, () => { }, () => true, _ => { },
                    (title, height, back) => { throw new InvalidOperationException("Unexpected focus-check modal: " + title); }, () => { });
                Set(page, "lexicon", TestLexicon());
                var data = (IdiomSaveData)Invoke(page, "NewSave"); data.ActiveMatchKind = match; data.CloseKeyboardAfterSend = closeAfterSend;
                Set(page, "data", data); Set(page, "notebook", data.Notebook); Invoke(page, "RestoreGames");
                Set(page, "loaded", true); Set(page, "visible", true); Invoke(page, "Refresh", true);
                Get<Button>(page, "rightStart").onClick.Invoke();
                var game = (IdiomGame)typeof(IdiomPage).GetProperty("Game", PrivateInstance).GetValue(page);
                var input = Get<InputField>(page, "input"); var send = Get<Button>(page, "send");
                input.text = "一心一意"; Invoke(page, "Update");
                FocusInput(input);
                var pointer = fixture.Press(Center((RectTransform)send.transform));
                Require(input.isFocused, match + " send pointer-down retains input ownership for close=" + closeAfterSend);
                fixture.Release(pointer);
                Require(game.History.Count > 0, "Real send release submits the idiom");
                Require(input.isFocused == !closeAfterSend, "Only successful send obeys close-after-send preference");
                Require(input.text.Length == 0, "Successful send clears the committed word while keeping the configured focus state");
                var historyCount = game.History.Count;
                input.text = "不合法词"; Invoke(page, "Update"); FocusInput(input);
                pointer = fixture.Press(Center((RectTransform)send.transform)); fixture.Release(pointer);
                Require(input.isFocused && input.text == "不合法词" && game.History.Count == historyCount,
                    "Rejected input retains focus and draft even when successful sends normally dismiss the keyboard");
                FocusInput(input);
                var sameMode = Get<Button>(page, match == IdiomMatchKind.Local ? "localMatch" : "computerMatch");
                pointer = fixture.Press(Center((RectTransform)sameMode.transform));
                Require(!input.isFocused, "Tapping a non-send control still releases input focus normally");
                fixture.Release(pointer);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static void FocusInput(InputField input)
        {
            typeof(InputField).GetMethod("ActivateInputFieldInternal", PrivateInstance).Invoke(input, null);
            Require(input.isFocused, "Production InputField is focused before the pointer event");
        }

        private static void CheckRuleSwitch(IdiomRuleSwitch ruleSwitch, string label)
        {
            var thumb = Get<RectTransform>(ruleSwitch, "thumb");
            var character = Get<Text>(ruleSwitch, "character");
            var sound = Get<Text>(ruleSwitch, "sound");
            var characterEnd = (RectTransform)ruleSwitch.transform.Find("Character end");
            var soundEnd = (RectTransform)ruleSwitch.transform.Find("Sound end");
            Near(thumb.rect.width, 52, label + " rule thumb width");
            Near(thumb.rect.height, 52, label + " rule thumb height");
            Near(Center(character.rectTransform), Center(characterEnd), label + " 字 centered in endpoint");
            Near(Center(sound.rectTransform), Center(soundEnd), label + " 音 centered in endpoint");
            Require(character.text == "字" && sound.text == "音", label + " rule labels");
            ruleSwitch.SetState(false, true, false);
            Near(Center(thumb), Center(characterEnd), label + " character selected disc center");
            ruleSwitch.SetState(true, true, false);
            Near(Center(thumb), Center(soundEnd), label + " sound selected disc center");
        }

        private static void CheckModalRaycasts(Fixture fixture)
        {
            var inert = new GameObject("Inactive shell under test");
            inert.SetActive(false);
            inert.transform.SetParent(fixture.Owner.transform, false);
            var shell = inert.AddComponent<AppController>();
            Set(shell, "canvasRoot", fixture.Root);
            var backCount = 0;
            var contentCount = 0;
            var closeCount = 0;
            var panel = (RectTransform)Invoke(shell, "OpenIdiomModal", "点击范围检查", 560f, (Action)(() => backCount++));
            try
            {
                var content = UiFactory.Button(panel, "内容按钮", 32, 180, 532, 70, () => contentCount++);
                var close = UiFactory.CircleButton(panel, UiIconKind.Close, panel.rect.width - 72, 43, 48, () => closeCount++);
                var closeRect = (RectTransform)close.transform;
                var icon = close.GetComponentInChildren<UiIconGraphic>();
                Near(closeRect.rect.width, closeRect.rect.height, "Close button is circular, not an ellipse");
                Near(Center(icon.rectTransform), Center(closeRect), "Close glyph rect center");
                CheckMesh(icon, true);
                fixture.Flush();
                fixture.Click(fixture.Root.TransformPoint(new Vector2(fixture.Root.rect.xMin + 10, fixture.Root.rect.yMin + 10)));
                Require(backCount == 1, "Actual outside click invokes supplied idiom back action exactly once");
                fixture.Click(panel.TransformPoint(new Vector2(panel.rect.xMin + 16, panel.rect.yMax - 130)));
                Require(backCount == 1, "Panel whitespace does not dismiss or bubble to backdrop");
                fixture.Click(Center((RectTransform)content.transform));
                Require(contentCount == 1 && backCount == 1, "Content button receives click without outside dismissal");
                fixture.Click(closeRect.TransformPoint(new Vector2(closeRect.rect.xMin + 1, closeRect.rect.yMax - 1)));
                Require(closeCount == 0 && backCount == 1, "Transparent corner of circular close is not clickable");
                fixture.Click(Center(closeRect));
                Require(closeCount == 1 && backCount == 1, "Circle center invokes close exactly once");
                var image = close.GetComponent<Image>();
                var centerScreen = RectTransformUtility.WorldToScreenPoint(fixture.Camera, Center(closeRect));
                var cornerScreen = RectTransformUtility.WorldToScreenPoint(fixture.Camera,
                    closeRect.TransformPoint(new Vector2(closeRect.rect.xMin + 1, closeRect.rect.yMax - 1)));
                Require(image.IsRaycastLocationValid(centerScreen, fixture.Camera), "Circle image accepts center");
                Require(!image.IsRaycastLocationValid(cornerScreen, fixture.Camera), "Circle image rejects corner");
            }
            finally { DestroyModal(shell); }
            // Shared confirmations retain their original modal semantics.
            Invoke(shell, "CreateModal", "普通确认", 488f, null);
            try
            {
                fixture.Flush();
                var modal = Get<GameObject>(shell, "modal");
                Require(modal.transform.Find("Dismiss outside dialog") == null, "Generic dialog has no outside dismiss handler");
                fixture.Click(fixture.Root.TransformPoint(new Vector2(fixture.Root.rect.xMin + 10, fixture.Root.rect.yMin + 10)));
                Require(backCount == 1, "Generic outside click cannot trigger previous idiom back action");
            }
            finally { DestroyModal(shell); }

            var actualPageRoot = UiFactory.Rect(fixture.Root, "Dialog owner page", 0, 0, fixture.Width, fixture.Height);
            var learningDirectory = Path.Combine(Path.GetTempPath(), "xanadu-idiom-ui-" + Guid.NewGuid().ToString("N"));
            UiFactory.Stretch(actualPageRoot);
            try
            {
                var page = actualPageRoot.gameObject.AddComponent<IdiomPage>();
                page.Initialize(actualPageRoot, () => { }, () => true, _ => { },
                    (title, height, back) => (RectTransform)Invoke(shell, "OpenIdiomModal", title, height, back),
                    () => DestroyModal(shell));
                Set(page, "visible", true);
                var returned = 0;
                var actualPanel = (RectTransform)Invoke(page, "Dialog", "真实成语弹窗", 560f, (Action)(() => returned++));
                var actualClose = (RectTransform)actualPanel.Find("Close button");
                Require(actualClose != null, "Production idiom Dialog creates shared circular close");
                var title = actualPanel.GetComponentInChildren<Text>();
                Near(Center(actualClose).y, Center(title.rectTransform).y, "Production dialog close aligns with title row");
                CheckMesh(actualClose.GetComponentInChildren<UiIconGraphic>(), true);
                fixture.Click(Center(actualClose));
                Require(returned == 1 && Get<GameObject>(shell, "modal") == null,
                    "Production X closes the dialog and returns through its back callback");
                Invoke(page, "Dialog", "真实外侧点击", 560f, (Action)(() => returned++));
                fixture.Click(fixture.Root.TransformPoint(new Vector2(fixture.Root.rect.xMin + 10, fixture.Root.rect.yMin + 10)));
                Require(returned == 2 && Get<GameObject>(shell, "modal") == null,
                    "Production outside click follows the same dialog close/back chain");

                var lexicon = TestLexicon();
                Set(page, "lexicon", lexicon);
                var learningData = (IdiomSaveData)Invoke(page, "NewSave");
                Set(page, "data", learningData);
                Invoke(page, "RestoreGames");
                Set(page, "loaded", true);
                var saved = new IdiomNotebookItem { Id = "ui-0", SenseId = "sense-0", Word = "一心一意",
                    Pinyin = "yī xīn yī yì", Meaning = "专心专意。", AddedAt = 1, DictionaryVersion = lexicon.Version };
                var book = new IdiomNotebookData { Entries = new List<IdiomNotebookItem> { saved } };
                Set(page, "notebook", book);
                var profile = Get<IdiomLearningProfile>(page, "learning");
                profile.AddNotebook(saved.Id, saved.AddedAt);
                var disk = new IdiomSaveStore(learningDirectory, lexicon);
                string warning;
                disk.Load(out warning);
                Set(page, "store", disk);
                Invoke(page, "ShowDefinition", saved.Id, null, saved, false);
                fixture.Flush();
                Button remembered = null, remove = null;
                foreach (var button in Get<GameObject>(shell, "modal").GetComponentsInChildren<Button>())
                {
                    var caption = button.GetComponentInChildren<Text>();
                    if (caption == null) continue;
                    if (caption.text == "我记住了") remembered = button;
                    if (caption.text == "移出生词本") remove = button;
                }
                Require(remembered != null, "Notebook definition exposes explicit remembered action");
                Require(remove != null, "Notebook keeps removal separate from remembered action");
                var rememberedBounds = WorldRect((RectTransform)remembered.transform);
                var removeBounds = WorldRect((RectTransform)remove.transform);
                Require(!rememberedBounds.Overlaps(removeBounds), "Remembered and removal hit targets must not overlap");
                Require(((RectTransform)remembered.transform).rect.height >= 48, "Remembered action retains accessible hit height");
                fixture.Click(Center((RectTransform)remembered.transform));
                Require(profile.IsFamiliar(saved.Id) && !profile.IsNotebook(saved.Id), "Remember click updates shared learner");
                Require(Get<IdiomNotebookData>(page, "notebook").Entries.Count == 0, "Remember click removes saved notebook row");
                var persisted = new IdiomSaveStore(learningDirectory, lexicon).Load(out warning);
                Require(persisted != null && persisted.Notebook.Entries.Count == 0 &&
                    new IdiomLearningProfile(lexicon, persisted.Profile).IsFamiliar(saved.Id),
                    "Remember action persists notebook and familiar state in one save");

                // A failed edit must not leave the shared profile half changed.
                var failedWord = new IdiomNotebookItem { Id = "ui-2", SenseId = "sense-2", Word = "发扬光大",
                    Pinyin = "fā yáng guāng dà", Meaning = "使优点得到发展。", AddedAt = 2, DictionaryVersion = lexicon.Version };
                profile.AddNotebook(failedWord.Id, failedWord.AddedAt);
                book = new IdiomNotebookData { Entries = new List<IdiomNotebookItem> { failedWord } };
                Set(page, "notebook", book);
                page.Save();
                var savedBytes = File.ReadAllBytes(disk.SavePath);
                Directory.CreateDirectory(disk.SavePath + ".tmp");
                var changed = (bool)Invoke(page, "SaveNotebook", new IdiomNotebookData(),
                    (Action)(() => profile.Remember(failedWord.Id)));
                Require(!changed && profile.IsNotebook(failedWord.Id) && !profile.IsFamiliar(failedWord.Id) &&
                    Get<IdiomNotebookData>(page, "notebook").Entries.Count == 1,
                    "Failed notebook edit rolls back the same shared learner instance and notebook");
                Require(Convert.ToBase64String(savedBytes) == Convert.ToBase64String(File.ReadAllBytes(disk.SavePath)),
                    "Failed edit keeps complete old disk snapshot");
                page.Save();
                Require(Get<bool>(page, "saveFailed"), "Fixture observes an ordinary save failure before recovery");
                var removed = new IdiomNotebookItem { Id = "ui-1", SenseId = "sense-1", Word = "意气风发",
                    Pinyin = "yì qì fēng fā", Meaning = "精神振奋。", AddedAt = 2, DictionaryVersion = lexicon.Version };
                book.Entries.Add(removed);
                profile.AddNotebook(removed.Id, removed.AddedAt);
                Invoke(page, "ShowDefinition", removed.Id, null, removed, false);
                fixture.Flush();
                Directory.Delete(disk.SavePath + ".tmp");
                foreach (var button in Get<GameObject>(shell, "modal").GetComponentsInChildren<Button>())
                {
                    var caption = button.GetComponentInChildren<Text>();
                    if (caption != null && caption.text == "移出生词本") { remove = button; break; }
                }
                fixture.Click(Center((RectTransform)remove.transform));
                Require(!profile.IsNotebook(removed.Id) && !profile.IsFamiliar(removed.Id),
                    "Plain removal does not grant familiar evidence");
                Require(page.SaveError == null && !Get<bool>(page, "saveFailed"),
                    "A successful combined notebook transaction clears all stale save errors");

                File.WriteAllText(disk.SavePath, "broken during UI transaction test");
                page.Save();
                Require(disk.NeedsRecovery, "Protected file triggers recovery flow");
                var recoveryReturns = 0;
                Invoke(page, "ConfirmNotebookRecovery", (Action)(() => recoveryReturns++));
                Invoke(page, "ReturnFromDialog");
                Require(recoveryReturns == 1, "Cancel notebook recovery returns to its originating detail dialog");
                Invoke(page, "ConfirmNotebookRecovery", (Action)(() => recoveryReturns++));
                foreach (var button in Get<GameObject>(shell, "modal").GetComponentsInChildren<Button>())
                {
                    var caption = button.GetComponentInChildren<Text>();
                    if (caption != null && caption.text == "保留并保存") { fixture.Click(Center((RectTransform)button.transform)); break; }
                }
                Require(recoveryReturns == 2 && !disk.NeedsRecovery && page.SaveError == null,
                    "Successful notebook recovery returns to its detail dialog with saving restored");

                var activeGame = Get<IdiomGame>(page, "characterGame");
                activeGame.Start(IdiomActor.Player);
                Invoke(page, "ShowDefinition", failedWord.Id, null, failedWord, false);
                Invoke(page, "ReturnFromDialog");
                Require(activeGame.GetSnapshot().AssistedEntryIds.Count == 1 &&
                    activeGame.GetSnapshot().AssistedEntryIds[0] == failedWord.Id,
                    "Viewing saved notebook definition records assistance in a live human turn");
                Require(Get<IdiomGame>(page, "soundGame").GetSnapshot().AssistedEntryIds.Count == 0 &&
                    Get<IdiomGame>(page, "localCharacterGame").GetSnapshot().AssistedEntryIds.Count == 0,
                    "Viewing assistance is isolated to the active game slot");
                persisted = new IdiomSaveStore(learningDirectory, lexicon).Load(out warning);
                Set(page, "data", persisted); Set(page, "notebook", persisted.Notebook);
                Invoke(page, "RestoreGames"); Invoke(page, "Refresh", true);
                profile = Get<IdiomLearningProfile>(page, "learning");
                Get<InputField>(page, "input").text = failedWord.Word;
                Get<Button>(page, "send").onClick.Invoke();
                IdiomUseCount observed = null;
                foreach (var use in profile.GetSnapshot().Uses) if (use.EntryId == failedWord.Id) observed = use;
                Require(observed != null && observed.AssistedUses == 1 && observed.IndependentUses == 0 &&
                    !profile.IsFamiliar(failedWord.Id), "Restored detail-assisted submit never creates independent familiarity");
            }
            finally
            {
                DestroyModal(shell);
                Object.DestroyImmediate(actualPageRoot.gameObject);
                Object.DestroyImmediate(inert);
                if (Directory.Exists(learningDirectory)) Directory.Delete(learningDirectory, true);
            }
        }

        private static void CheckThirdHintContinues(Fixture fixture, IdiomMatchKind match, IdiomMode mode)
        {
            var inert = new GameObject("Inactive hint shell");
            inert.SetActive(false); inert.transform.SetParent(fixture.Owner.transform, false);
            var shell = inert.AddComponent<AppController>(); Set(shell, "canvasRoot", fixture.Root);
            var pageRoot = UiFactory.Rect(fixture.Root, "Hint continuation owner", 0, 0, fixture.Width, fixture.Height);
            UiFactory.Stretch(pageRoot);
            try
            {
                var page = pageRoot.gameObject.AddComponent<IdiomPage>();
                page.Initialize(pageRoot, () => { }, () => true, _ => { },
                    (title, height, back) => (RectTransform)Invoke(shell, "OpenIdiomModal", title, height, back), () => DestroyModal(shell));
                var entries = new List<IdiomEntry>();
                var middles = "甲乙丙丁戊己庚辛壬癸子丑寅卯辰巳午未申酉";
                for (var i = 0; i < middles.Length; i++)
                    entries.Add(new IdiomEntry("hint-" + i, "一" + middles[i] + "字一", "yī zì yī", "yi", "yi", "sense-hint-" + i, "fixture", true));
                Set(page, "lexicon", new IdiomLexicon(entries, "hint-fixture"));
                var data = (IdiomSaveData)Invoke(page, "NewSave"); data.ActiveMatchKind = match; data.ActiveMode = mode;
                Set(page, "data", data); Set(page, "notebook", new IdiomNotebookData()); Invoke(page, "RestoreGames");
                Set(page, "loaded", true); Set(page, "visible", true); Invoke(page, "Refresh", true);
                var game = (IdiomGame)typeof(IdiomPage).GetProperty("Game", PrivateInstance).GetValue(page);
                Invoke(page, "StartGame", match == IdiomMatchKind.Local ? IdiomActor.Left : IdiomActor.Player);
                var turns = match == IdiomMatchKind.Local ? 6 : 3;
                for (var turn = 0; turn < turns; turn++)
                {
                    fixture.Click(Center((RectTransform)Get<Button>(page, "hint").transform));
                    var candidate = game.CurrentHint;
                    Require(candidate != null && candidate.EntryIds.Count > 0 && !game.IsEnded, match + "/" + mode + " hint remains playable at turn " + turn);
                    IdiomEntry chosen; Get<IdiomLexicon>(page, "lexicon").TryGetById(candidate.EntryIds[0], out chosen);
                    var modal = Get<GameObject>(shell, "modal");
                    Button choose = null;
                    foreach (var button in modal.GetComponentsInChildren<Button>())
                    foreach (var text in button.GetComponentsInChildren<Text>())
                        if (text.text == chosen.Word) choose = button;
                    Require(choose != null, "Hint result retains actual selectable word row");
                    fixture.Click(Center((RectTransform)choose.transform));
                    Require(Get<GameObject>(shell, "modal") == null && Get<InputField>(page, "input").text == chosen.Word,
                        "Hint row fills the draft without ending the turn");
                    var count = game.HintCountFor(game.NextActor);
                    if (count == 3)
                        Require(Get<Button>(page, "hint").GetComponentInChildren<Text>().text == "认输" && !game.IsEnded,
                            "After the third hint the composer offers surrender while keeping the selected word");
                    var before = game.History.Count;
                    Get<Button>(page, "send").onClick.Invoke();
                    Require(game.History.Count > before && !game.IsEnded, "The third hinted word can be submitted and the next turn continues");
                }
                if (match == IdiomMatchKind.Local)
                    Require(game.HintCountFor(IdiomActor.Left) == 3 && game.HintCountFor(IdiomActor.Right) == 3,
                        "Both local players independently finish three playable hints");
                var surrender = Get<Button>(page, "hint");
                Require(surrender.GetComponentInChildren<Text>().text == "认输", "Hint quota exhaustion exposes explicit surrender");
                fixture.Click(Center((RectTransform)surrender.transform));
                Require(game.IsEnded && game.EndReason == IdiomEndReason.Surrendered, "Only the explicit surrender click ends this playable game");
                var summary = Get<GameObject>(shell, "modal");
                Require(summary != null && FindButton(summary, "再来一局") != null, "Surrender opens a round summary");
                var explained = false;
                foreach (var text in summary.GetComponentsInChildren<Text>()) if (text.text.Contains("选择认输")) explained = true;
                Require(explained, "Summary names the actual surrender reason");
                Invoke(page, "ReturnFromDialog");
                var finalEntry = new IdiomEntry("last", "一心一意", "yī xīn yī yì", "yi", "yi", "sense-last", "fixture", true);
                Set(page, "lexicon", new IdiomLexicon(new[] { finalEntry }, "last-word-fixture"));
                data = (IdiomSaveData)Invoke(page, "NewSave"); data.ActiveMatchKind = match; data.ActiveMode = mode;
                Set(page, "data", data); Invoke(page, "RestoreGames"); Invoke(page, "Refresh", true);
                game = (IdiomGame)typeof(IdiomPage).GetProperty("Game", PrivateInstance).GetValue(page);
                Invoke(page, "StartGame", match == IdiomMatchKind.Local ? IdiomActor.Left : IdiomActor.Player);
                Get<InputField>(page, "input").text = finalEntry.Word;
                Get<Button>(page, "send").onClick.Invoke();
                Require(game.IsEnded && game.EndReason == IdiomEndReason.DictionaryExhausted, "Final legal word ends only when the next player has no unused legal word");
                summary = Get<GameObject>(shell, "modal"); explained = false;
                foreach (var text in summary.GetComponentsInChildren<Text>())
                    if (text.text.Contains("没有可接的成语") && (text.text.Contains("获胜") || text.text.Contains("赢了"))) explained = true;
                Require(explained, "Dictionary exhaustion summary identifies the losing side and winner");
                if (match == IdiomMatchKind.Local)
                foreach (var first in new[] { IdiomActor.Left, IdiomActor.Right })
                {
                    Invoke(page, "ReturnFromDialog");
                    var move = new IdiomEntry("first", "甲乙丙丁", "jiǎ yǐ bǐng dīng", "jia", "ding", "s-first", "fixture", true);
                    var spare = new IdiomEntry("spare", "戊己庚辛", "wù jǐ gēng xīn", "wu", "xin", "s-spare", "fixture", true);
                    Set(page, "lexicon", new IdiomLexicon(new[] { move, spare }, "either-no-moves"));
                    data = (IdiomSaveData)Invoke(page, "NewSave"); data.ActiveMatchKind = match; data.ActiveMode = mode;
                    Set(page, "data", data); Invoke(page, "RestoreGames"); Invoke(page, "Refresh", true);
                    game = (IdiomGame)typeof(IdiomPage).GetProperty("Game", PrivateInstance).GetValue(page);
                    Invoke(page, "StartGame", first);
                    Get<InputField>(page, "input").text = move.Word;
                    Get<Button>(page, "send").onClick.Invoke();
                    Require(game.IsEnded && game.EndReason == (first == IdiomActor.Left ? IdiomEndReason.PlayerNoMoves : IdiomEndReason.AppNoMoves),
                        "Either local side ends automatically only after no matching word remains");
                    summary = Get<GameObject>(shell, "modal"); explained = false;
                    var loser = first == IdiomActor.Left ? "右方" : "左方";
                    var winner = first == IdiomActor.Left ? "左方" : "右方";
                    foreach (var label in summary.GetComponentsInChildren<Text>())
                        if (label.text.Contains(loser + "已没有可接的成语") && label.text.Contains(winner + "获胜")) explained = true;
                    Require(explained, "No-move summary names both the losing and winning local side");
                }
            }
            finally { DestroyModal(shell); Object.DestroyImmediate(pageRoot.gameObject); Object.DestroyImmediate(inert); }
        }

        private static void CheckSourceDefinition(Fixture fixture)
        {
            var inert = new GameObject("Inactive source-detail shell");
            inert.SetActive(false);
            inert.transform.SetParent(fixture.Owner.transform, false);
            var shell = inert.AddComponent<AppController>();
            Set(shell, "canvasRoot", fixture.Root);
            var pageRoot = UiFactory.Rect(fixture.Root, "Source detail owner", 0, 0, fixture.Width, fixture.Height);
            UiFactory.Stretch(pageRoot);
            var directory = Path.Combine(Path.GetTempPath(), "xanadu-idiom-sources-" + Guid.NewGuid().ToString("N"));
            try
            {
                var page = pageRoot.gameObject.AddComponent<IdiomPage>();
                page.Initialize(pageRoot, () => { }, () => true, _ => { },
                    (title, height, back) => (RectTransform)Invoke(shell, "OpenIdiomModal", title, height, back), () => DestroyModal(shell));
                var lexicon = TestLexicon();
                Set(page, "lexicon", lexicon);
                var data = (IdiomSaveData)Invoke(page, "NewSave");
                Set(page, "data", data); Set(page, "notebook", new IdiomNotebookData());
                Invoke(page, "RestoreGames");
                Set(page, "loaded", true); Set(page, "visible", true);
                var store = new IdiomSaveStore(directory, lexicon);
                string warning; store.Load(out warning); Set(page, "store", store);
                var sourceStory = new string('故', 18000) + "\n典故末尾原文。";
                var definition = new IdiomDefinition
                {
                    Id = "ui-0", SenseId = "sense-0", Word = "一心一意", Pinyin = "UI fixture", Meaning = "根摘要不得替换源原文。",
                    SourceLabel = "两个词库", Stories = new string[0], Origins = new IdiomCitation[0], Examples = new IdiomCitation[0],
                    Sources = new[]
                    {
                        new IdiomDefinitionSource { SourceKey = "a", SourceRecordId = "a-1", SourceLabel = "资料库甲",
                            Pinyin = new string('p', 160) + " 原拼音甲", Meaning = " 甲释义原文。 ", Stories = new[] { sourceStory },
                            Origins = new[] { new IdiomCitation { Text = "甲出处原文。", Reference = "甲书" } }, Examples = new IdiomCitation[0] },
                        new IdiomDefinitionSource { SourceKey = "b", SourceRecordId = "b-1", SourceLabel = "资料库乙", Pinyin = "乙原拼音",
                            Meaning = "乙词库不同释义。", Stories = new string[0], Origins = new IdiomCitation[0],
                            Examples = new[] { new IdiomCitation { Text = "乙例句末尾。", Reference = "" } } }
                    }
                };
                var loader = Get<IdiomCatalogLoader>(page, "loader");
                Set(loader, "lexicon", lexicon);
                Get<Dictionary<string, Dictionary<string, IdiomDefinition>>>(loader, "cache").Add("fixture",
                    new Dictionary<string, IdiomDefinition> { { definition.Id, definition } });
                Invoke(page, "ShowDefinition", definition.Id, null, null, false);
                fixture.Flush();
                var modal = Get<GameObject>(shell, "modal");
                Text pinyinHeader = null;
                foreach (var text in modal.GetComponentsInChildren<Text>())
                    if (text.text == "拼音见各词库资料") pinyinHeader = text;
                Require(pinyinHeader != null && pinyinHeader.preferredHeight <= pinyinHeader.rectTransform.rect.height + .5f,
                    "Multi-source header is short enough for its actual 42px row");
                CheckSourceScroll(fixture, modal, IdiomDefinitionText.Format(definition, false));
                fixture.Click(Center((RectTransform)FindButton(modal, "展开典故").transform));
                modal = Get<GameObject>(shell, "modal");
                fixture.Flush();
                var fullText = IdiomDefinitionText.Format(definition, true);
                var scroll = CheckSourceScroll(fixture, modal, fullText);
                Require(scroll.content.GetComponentsInChildren<Text>().Length > 1,
                    "An 18000-character story uses multiple real Text meshes");
                scroll.verticalNormalizedPosition = 0;
                fixture.Flush();
                var segments = scroll.content.GetComponentsInChildren<Text>();
                Require(WorldRect(segments[segments.Length - 1].rectTransform).yMin >= WorldRect(scroll.viewport).yMin - .5f,
                    "The final original source line is reachable at the scroll bottom");
                fixture.Click(Center((RectTransform)FindButton(modal, "加入生词本").transform));
                var notebook = Get<IdiomNotebookData>(page, "notebook");
                Require(notebook.Entries.Count == 1 && notebook.Entries[0].Meaning == fullText,
                    "Actual add button saves all source text, including the unfolded long story");
                var persisted = new IdiomSaveStore(directory, lexicon).Load(out warning);
                Require(persisted.Notebook.Entries.Count == 1 && persisted.Notebook.Entries[0].Meaning == fullText,
                    "Real notebook store round-trips full multi-source text");
                // A failed resource read must show the exact previously saved complete text.
                loader.ClearDetails(); Set(loader, "lexicon", null);
                Invoke(page, "ShowDefinition", definition.Id, null, notebook.Entries[0], false);
                modal = Get<GameObject>(shell, "modal");
                var fallback = modal.GetComponentInChildren<ScrollRect>();
                var fallbackText = new StringBuilder();
                foreach (var text in fallback.content.GetComponentsInChildren<Text>()) fallbackText.Append(text.text);
                Require(fallbackText.ToString().Contains(fullText), "Resource failure fallback retains every saved source character");
                Require(FindButton(modal, "重新读取资料") != null, "Failed detail still exposes retry");
            }
            finally
            {
                DestroyModal(shell); Object.DestroyImmediate(pageRoot.gameObject); Object.DestroyImmediate(inert);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static ScrollRect CheckSourceScroll(Fixture fixture, GameObject modal, string expectedText)
        {
            fixture.Flush();
            var scroll = modal.GetComponentInChildren<ScrollRect>();
            Require(scroll != null, "Source details have a real scroll viewport");
            var actual = new StringBuilder();
            foreach (var segment in scroll.content.GetComponentsInChildren<Text>())
            {
                actual.Append(segment.text);
                Require(!segment.supportRichText && segment.text.Length <= 2048, "Source segments are literal text with bounded mesh size");
                Require(segment.preferredHeight <= segment.rectTransform.rect.height + .5f, "Every segment fits its measured height");
            }
            Require(actual.ToString() == expectedText, "Rendered Text segments exactly reproduce formatter output");
            foreach (var button in modal.GetComponentsInChildren<Button>())
            {
                var caption = button.GetComponentInChildren<Text>();
                if (caption == null || string.IsNullOrEmpty(caption.text)) continue;
                var rect = (RectTransform)button.transform;
                Near(Center(caption.rectTransform), Center(rect), "Definition button label remains centered: " + caption.text);
                Require(caption.alignment == TextAnchor.MiddleCenter && caption.preferredWidth <= caption.rectTransform.rect.width + .5f,
                    "Definition button text fits its centered bounds: " + caption.text);
                Require(!WorldRect(scroll.viewport).Overlaps(WorldRect(rect)), "Definition scroll does not overlap footer action: " + caption.text);
            }
            return scroll;
        }

        private static Button FindButton(GameObject modal, string caption)
        {
            foreach (var button in modal.GetComponentsInChildren<Button>())
            {
                var text = button.GetComponentInChildren<Text>();
                if (text != null && text.text == caption) return button;
            }
            throw new InvalidOperationException("Missing detail button: " + caption);
        }

        private static void DestroyModal(AppController shell)
        {
            var modal = Get<GameObject>(shell, "modal");
            Set(shell, "modal", null);
            Set(shell, "dismissModal", null);
            if (modal != null) Object.DestroyImmediate(modal);
        }

        private static void CheckMesh(UiIconGraphic icon, bool centered)
        {
            using (var vertices = new VertexHelper())
            {
                typeof(UiIconGraphic).GetMethod("OnPopulateMesh", PrivateInstance, null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(icon, new object[] { vertices });
                Require(vertices.currentVertCount > 0, icon.Kind + " actual mesh contains vertices");
                var mesh = new Mesh();
                try
                {
                    vertices.FillMesh(mesh);
                    mesh.RecalculateBounds();
                    Require(mesh.bounds.size.x > 0 && mesh.bounds.size.y > 0, icon.Kind + " actual mesh has area");
                    Require(mesh.bounds.size.x <= icon.rectTransform.rect.width && mesh.bounds.size.y <= icon.rectTransform.rect.height,
                        icon.Kind + " actual mesh fits icon rect");
                    if (centered) Near((Vector2)mesh.bounds.center, icon.rectTransform.rect.center, "Actual X mesh is centered");
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        private static IdiomLexicon TestLexicon()
        {
            // A closed five-word chain avoids random app-opening dead ends in either rule.
            var words = new[] { "一心一意", "意气风发", "发扬光大", "大快人心", "心口如一" };
            var keys = new[] { "yi", "yi", "fa", "da", "xin", "yi" };
            var entries = new List<IdiomEntry>();
            for (var i = 0; i < words.Length; i++)
                entries.Add(new IdiomEntry("ui-" + i, words[i], "UI fixture", keys[i], keys[i + 1], "sense-" + i, "fixture", true));
            return new IdiomLexicon(entries, "ui-check-fixture");
        }

        private static IdiomEntry NextWord(IdiomLexicon lexicon, IdiomGame game)
        {
            var used = new HashSet<string>(game.History);
            foreach (var entry in lexicon.Entries)
            {
                if (used.Contains(entry.Id)) continue;
                if (game.LastEntry == null || (game.Mode == IdiomMode.SameCharacter ?
                    game.LastEntry.LastCharacter == entry.FirstCharacter : game.LastEntry.LastKey == entry.FirstKey)) return entry;
            }
            throw new InvalidOperationException("UI fixture chain unexpectedly has no continuation");
        }

        private static object Invoke(object target, string name, params object[] values)
        {
            var method = target.GetType().GetMethod(name, PrivateInstance);
            if (method == null) throw new MissingMethodException(target.GetType().Name, name);
            return method.Invoke(target, values);
        }

        private static T Get<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        }

        private static Vector2 Center(RectTransform rect) { return rect.TransformPoint(rect.rect.center); }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void Near(float actual, float expected, string message)
        {
            Require(Mathf.Abs(actual - expected) <= .1f, message + " (actual " + actual + ", expected " + expected + ")");
        }

        private static void Near(Vector2 actual, Vector2 expected, string message)
        {
            Require(Vector2.Distance(actual, expected) <= .1f, message + " (actual " + actual + ", expected " + expected + ")");
        }

        private static void Require(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Owner;
            public readonly RectTransform Root;
            public readonly Camera Camera;
            public readonly int Width, Height;
            private readonly RenderTexture target;
            private readonly GraphicRaycaster raycaster;
            private readonly EventSystem events;
            private readonly EventSystem previousEvents;
            private readonly bool registeredEvents;

            public Fixture(int width, int height)
            {
                Width = width; Height = height;
                Owner = new GameObject("Idiom UI fixture") { hideFlags = HideFlags.HideAndDontSave };
                var canvasObject = new GameObject("UI fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(Owner.transform, false);
                Root = (RectTransform)canvasObject.transform;
                Root.sizeDelta = new Vector2(width, height);
                Root.pivot = new Vector2(.5f, .5f);
                var cameraObject = new GameObject("UI fixture camera", typeof(Camera));
                cameraObject.transform.SetParent(Owner.transform, false);
                Camera = cameraObject.GetComponent<Camera>();
                Camera.enabled = false;
                Camera.transform.position = new Vector3(0, 0, -10);
                Camera.orthographic = true;
                Camera.orthographicSize = height * .5f;
                Camera.aspect = width / (float)height;
                Camera.nearClipPlane = .1f;
                Camera.farClipPlane = 100;
                target = new RenderTexture(width, height, 24);
                target.Create();
                Camera.targetTexture = target;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera;
                Root.sizeDelta = new Vector2(width, height);
                Near(Root.rect.width, width, "Actual fixture canvas width");
                Near(Root.rect.height, height, "Actual fixture canvas height");
                raycaster = canvasObject.GetComponent<GraphicRaycaster>();
                var eventObject = new GameObject("UI fixture events", typeof(EventSystem));
                eventObject.transform.SetParent(Owner.transform, false);
                events = eventObject.GetComponent<EventSystem>();
                previousEvents = EventSystem.current;
                // These checks run outside Play Mode. Register the production
                // event system lifecycle explicitly so InputField owns real focus.
                var systems = (List<EventSystem>)typeof(EventSystem).GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                if (!systems.Contains(events)) { Invoke(events, "OnEnable"); registeredEvents = true; }
                EventSystem.current = events;
            }

            public void Flush()
            {
                Canvas.ForceUpdateCanvases();
                // Actual rendering establishes Graphic.depth; testing only rectangle
                // math would miss graphics rejected by the production raycaster.
                Camera.Render();
                Canvas.ForceUpdateCanvases();
            }

            public void Click(Vector2 worldPosition)
            {
                Release(Press(worldPosition));
            }

            public PointerEventData Press(Vector2 worldPosition)
            {
                Flush();
                var pointer = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(Camera, worldPosition),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                raycaster.Raycast(pointer, hits);
                Require(hits.Count > 0, "Production GraphicRaycaster finds a drawn target at " + pointer.position);
                pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                // Mirror StandaloneInputModule's DeselectIfSelectionChanged before
                // pointerDown. Omitting this would hide the native keyboard bug.
                var selected = ExecuteEvents.GetEventHandler<ISelectHandler>(hits[0].gameObject);
                if (selected != events.currentSelectedGameObject) events.SetSelectedGameObject(null, pointer);
                var pressed = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                pointer.pointerPress = pressed != null ? pressed : clicked;
                return pointer;
            }

            public void Release(PointerEventData pointer)
            {
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(pointer.pointerCurrentRaycast.gameObject);
                if (pointer.pointerPress != null) ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                if (clicked != null && pointer.pointerPress == clicked)
                    ExecuteEvents.Execute(clicked, pointer, ExecuteEvents.pointerClickHandler);
            }

            public void Dispose()
            {
                Camera.targetTexture = null;
                if (registeredEvents) Invoke(events, "OnDisable");
                if (previousEvents != null) EventSystem.current = previousEvents;
                Object.DestroyImmediate(Owner);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
