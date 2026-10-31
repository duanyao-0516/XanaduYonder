using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AircraftFire;

namespace XanaduYonder.Runtime.AircraftFire
{
    internal sealed partial class AircraftFirePage
    {
        private static readonly Color OriginalInk = new Color32(48, 49, 49, 255);
        private RectTransform surface, hud, entry, battleHud, titleArtwork, entryMenu;
        private Text scoreLabel, bombLabel, livesLabel, warningLabel;
        private Image bombFlash, sparePlane;
        private Button bombButton, pauseButton;
        private float flashTime;

        private void Build()
        {
            var background = UiFactory.Panel(root, "Aircraft full-width battlefield", 0, 0, 720, 1320, new Color32(194, 200, 200, 255), false);
            surface = background.rectTransform; UiFactory.Stretch(surface);
            background.raycastTarget = true;
            surface.gameObject.AddComponent<RectMask2D>();
            bombFlash = UiFactory.Panel(root, "Bomb flash", 0, 0, 720, 1320, Color.clear, false);
            UiFactory.Stretch(bombFlash.rectTransform);
            hud = UiFactory.Rect(root, "Aircraft overlay coordinates", 0, 0, 640, 1136);
            hud.anchorMin = hud.anchorMax = hud.pivot = Vector2.zero;

            entry = UiFactory.Rect(hud, "Aircraft original entry", 0, 0, 640, 1136);
            UiFactory.Stretch(entry);
            UiFactory.IconButton(entry, UiIconKind.Home, 14, 8, 60, 60, Exit).name = "Aircraft home";
            UiFactory.IconButton(entry, UiIconKind.Book, 492, 8, 60, 60, ShowRules).name = "Aircraft help";
            UiFactory.IconButton(entry, UiIconKind.Settings, 566, 8, 60, 60, Settings).name = "Aircraft settings";
            titleArtwork = UiFactory.Rect(entry, "Aircraft original title", 192, 100, 256, 64);
            var title = titleArtwork.gameObject.AddComponent<RawImage>();
            title.texture = Resources.Load<Texture2D>("AircraftFire/Title"); title.raycastTarget = false;
            entryMenu = UiFactory.Rect(entry, "Aircraft original mode menu", 170, 0, 300, 260);
            OriginalButton(entryMenu, "经典模式", 0, 0, 300, 70, () => StartRun(AircraftMode.Classic, selectedCrazy));
            OriginalButton(entryMenu, "关卡模式", 0, 95, 300, 70, () => StartRun(AircraftMode.Level, false));
            OriginalButton(entryMenu, "历史成绩", 0, 190, 300, 70, ShowRecords);
            warningLabel = UiFactory.Label(entry, "", 16, 1090, 608, 32, 16, UiFactory.Hex("853D36"), TextAnchor.MiddleCenter);
            warningLabel.name = "Aircraft save warning";

            battleHud = UiFactory.Rect(hud, "Aircraft original battle instruments", 0, 0, 640, 1136);
            UiFactory.Stretch(battleHud);
            // The original game has a pause icon and score at the upper left, with
            // no opaque header, help/settings navigation, record or mode banner.
            pauseButton = ArtworkButton(battleHud, "Aircraft pause", "game_pause", "game_pause_hl", 0, 2, 80, 60, PauseGame);
            scoreLabel = UiFactory.Label(battleHud, "0", 83, 0, 400, 73, 44, OriginalInk);
            scoreLabel.name = "Aircraft score"; scoreLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            sparePlane = UiFactory.Rect(battleHud, "Aircraft spare plane", 522, 5, 68, 66).gameObject.AddComponent<Image>();
            sparePlane.sprite = Resources.Load<Sprite>("AircraftFire/Sprites/hero_fly_1");
            sparePlane.preserveAspect = true; sparePlane.raycastTarget = false;
            livesLabel = UiFactory.Label(battleHud, "", 590, 0, 45, 66, 32, OriginalInk, TextAnchor.MiddleRight);
            bombButton = ArtworkButton(battleHud, "Aircraft bomb", "bomb", null, 2, 1000, 84, 76, UseBomb);
            bombLabel = UiFactory.Label(battleHud, "", 91, 1000, 130, 76, 38, OriginalInk);
        }

        private static Button ArtworkButton(Transform parent, string name, string normal, string pressed,
            float x, float y, float width, float height, Action action, bool sliced = false)
        {
            var image = UiFactory.Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("AircraftFire/Sprites/" + normal);
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            var button = UiFactory.ConfigureButton(image, action);
            if (pressed != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                var state = button.spriteState;
                state.pressedSprite = Resources.Load<Sprite>("AircraftFire/Sprites/" + pressed);
                button.spriteState = state;
            }
            return button;
        }
        private static Button OriginalButton(Transform parent, string text, float x, float y, float width, float height, Action action, int fontSize = 35)
        {
            var button = ArtworkButton(parent, text, "button_normal", "button_highlight", x, y, width, height, action, true);
            UiFactory.Label(button.transform, text, 10, 0, width - 20, height, fontSize, OriginalInk, TextAnchor.MiddleCenter);
            return button;
        }

        internal void LayoutNow()
        {
            lastSize = root.rect.size; lastSafe = Screen.safeArea;
            if (lastSize.x <= 0 || lastSize.y <= 0) return;
            var scale = lastSize.x / 640;
            var height = WorldHeight;
            hud.sizeDelta = new Vector2(640, height); hud.localScale = Vector3.one * scale;
            var top = Screen.height > 0 ? (Screen.height - Screen.safeArea.yMax) * height / Screen.height : 0;
            var bottom = Screen.height > 0 ? Screen.safeArea.yMin * height / Screen.height : 0;
            var left = Screen.width > 0 ? Screen.safeArea.xMin * 640 / Screen.width : 0;
            var right = Screen.width > 0 ? (Screen.width - Screen.safeArea.xMax) * 640 / Screen.width : 0;
            SetTop(entry.Find("Aircraft home") as RectTransform, top + 8, left + 14);
            SetTop(entry.Find("Aircraft help") as RectTransform, top + 8, 492 - right);
            SetTop(entry.Find("Aircraft settings") as RectTransform, top + 8, 566 - right);
            SetTop(titleArtwork, top + 100);
            SetTop(entryMenu, height / 2 - 30);
            SetTop(pauseButton.GetComponent<RectTransform>(), top + 2, left);
            SetTop(scoreLabel.rectTransform, top, left + 83);
            SetTop(sparePlane.rectTransform, top + 5, 522 - right);
            SetTop(livesLabel.rectTransform, top, 590 - right);
            SetTop(bombButton.GetComponent<RectTransform>(), height - bottom - 76, left + 2);
            SetTop(bombLabel.rectTransform, height - bottom - 76, left + 91);
            SetTop(warningLabel.rectTransform, height - bottom - 38);
            pointer?.Cancel();
            if (game != null && Mathf.Abs(game.Height - height) > .1f) game.ResizeHeight(height);
            scene?.Layout(lastSize.x, lastSize.y);
            if (game == null) scene?.RenderMenu(height);
        }
        private static void SetTop(RectTransform rect, float y, float? x = null)
        {
            if (rect != null) rect.anchoredPosition = new Vector2(x ?? rect.anchoredPosition.x, -y);
        }
        private void Refresh()
        {
            if (game == null) { RefreshWarning(); return; }
            scene.Render(game, terminalPresentation);
            scoreLabel.text = game.Score.ToString();
            var showLives = game.Mode == AircraftMode.Level && game.Lives > 1;
            sparePlane.gameObject.SetActive(showLives); livesLabel.gameObject.SetActive(showLives);
            livesLabel.text = "×" + Math.Max(0, game.Lives - 1);
            bombLabel.text = "×" + game.Bombs;
            bombButton.gameObject.SetActive(game.Bombs > 0); bombLabel.gameObject.SetActive(game.Bombs > 0);
            bombButton.interactable = CanPlay && game.Bombs > 0;
            pauseButton.interactable = !settingsBlocked;
            flashTime = Mathf.Max(0, flashTime - Time.unscaledDeltaTime);
            bombFlash.color = new Color(1, 1, 1, flashTime * 3);
            RefreshWarning();
        }
        private void RefreshWarning()
        {
            if (warningLabel != null) warningLabel.text = hostSaveWarning ?? SaveError ?? "";
        }
    }
}
