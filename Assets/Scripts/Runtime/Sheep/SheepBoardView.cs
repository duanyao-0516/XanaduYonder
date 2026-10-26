using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    // Presentation only. The core owns coverage, selection, matching and tool state.
    internal sealed class SheepBoardView
    {
        private sealed class Tile
        {
            public RectTransform Rect;
            public RawImage Background, Icon;
            public Button Button;
        }

        private readonly RectTransform stage, tray, reserve, overlay;
        private readonly Action<int> pick;
        private readonly Texture2D[] icons = new Texture2D[16];
        private readonly Texture2D background;
        private readonly List<Tile> tiles = new List<Tile>();
        private readonly List<GameObject> ghosts = new List<GameObject>();
        private readonly Text[] stackCounts = new Text[2];
        private readonly Text reserveHint, trayCount;
        private SheepPosition[] positions;
        private int level = -1, layout = -1;
        private const float TileWidth = 72, TileHeight = 81;
        private const float TrayTilesTop = 46;

        public SheepBoardView(RectTransform root, RectTransform boardArea, RectTransform trayArea,
            RectTransform reserveArea, Action<int> onPick)
        {
            stage = boardArea; tray = trayArea; reserve = reserveArea; pick = onPick;
            background = Resources.Load<Texture2D>("Sheep/Tiles/block_bg");
            for (var i = 0; i < icons.Length; i++)
            {
                icons[i] = Resources.Load<Texture2D>("Sheep/Tiles/block_" + (i + 1));
                if (icons[i] == null) throw new InvalidOperationException("Missing tile art: " + (i + 1));
            }
            if (background == null) throw new InvalidOperationException("Missing tile background.");
            for (var i = 0; i < 7; i++)
                UiFactory.Panel(tray, "Tray slot " + (i + 1), 16 + i * 84, TrayTilesTop, TileWidth, TileHeight,
                    UiFactory.Hex("D1DCC0"));
            trayCount = UiFactory.Label(tray, "0 / 7", 466, 10, 134, 28, 18, UiFactory.Accent, TextAnchor.MiddleRight);
            UiFactory.Label(tray, "集齐三个，自动消除", 16, 10, 400, 28, 20, UiFactory.Accent);
            reserveHint = UiFactory.Label(reserve, "移出区 · 暂存的牌可随时点回", 0, 0, 608, 28, 19,
                UiFactory.Muted, TextAnchor.MiddleCenter);
            for (var i = 0; i < 6; i++)
                UiFactory.Panel(reserve, "Reserve slot " + i, 64 + i * 80, 32, 64, 72, UiFactory.Hex("E6EAD9"));
            for (var i = 0; i < 2; i++)
                stackCounts[i] = UiFactory.Label(stage, "", 0, 0, 84, 26, 17, UiFactory.Ink, TextAnchor.MiddleCenter);
            overlay = UiFactory.Rect(root, "Tile animation overlay", 0, 0, 720, 1320);
            UiFactory.Stretch(overlay);
        }

        private Tile CreateTile(Transform parent, int id)
        {
            var rect = UiFactory.Rect(parent, "Tile " + id, 0, 0, TileWidth, TileHeight);
            var bg = rect.gameObject.AddComponent<RawImage>();
            bg.texture = background;
            bg.raycastTarget = id >= 0;
            var icon = UiFactory.Rect(rect, "Pattern", 0, 0, TileWidth, TileHeight).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            UiFactory.Stretch(icon.rectTransform);
            Button button = null;
            if (id >= 0)
            {
                button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var colors = button.colors;
                colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.disabledColor = Color.white;
                colors.pressedColor = new Color(.85f, .92f, .82f);
                button.colors = colors;
                var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
                button.onClick.AddListener(() => pick(id));
            }
            return new Tile { Rect = rect, Background = bg, Icon = icon, Button = button };
        }

        public void Render(SheepGame game)
        {
            var snapshot = game.GetSnapshot();
            if (snapshot.Level != level || snapshot.LayoutId != layout)
            {
                level = snapshot.Level; layout = snapshot.LayoutId;
                positions = SheepBoardLayout.GetPositions(level, layout);
            }
            while (tiles.Count < positions.Length) tiles.Add(CreateTile(stage, tiles.Count));
            for (var i = positions.Length; i < tiles.Count; i++) tiles[i].Rect.gameObject.SetActive(false);
            var topLayer = new[] { -1, -1, -1 };
            var stackCount = new int[3];
            foreach (var pos in positions)
                if (pos.BlindStack > 0 && snapshot.Locations[pos.Id] == SheepLocation.Board)
                {
                    stackCount[pos.BlindStack]++;
                    topLayer[pos.BlindStack] = Math.Max(topLayer[pos.BlindStack], pos.Layer);
                }
            // Sibling order follows the same layer ordering as core coverage.
            var ordered = (SheepPosition[])positions.Clone();
            Array.Sort(ordered, (a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Id.CompareTo(b.Id));
            foreach (var pos in ordered)
            {
                var id = pos.Id;
                var tile = tiles[id];
                var location = snapshot.Locations[id];
                tile.Rect.gameObject.SetActive(location != SheepLocation.Removed);
                tile.Rect.localScale = Vector3.one;
                if (location == SheepLocation.Removed) continue;
                var canPick = game.CanPick(id);
                tile.Icon.texture = icons[snapshot.Kinds[id]];
                tile.Icon.enabled = true;
                tile.Button.interactable = canPick;
                tile.Background.color = tile.Icon.color = Color.white;
                if (location == SheepLocation.Board)
                {
                    Place(tile, stage, 40 + pos.X * 36, 18 + pos.Y * 36, TileWidth, TileHeight);
                    if (pos.BlindStack > 0)
                    {
                        tile.Icon.enabled = pos.Layer == topLayer[pos.BlindStack];
                        // Fan out blank backs like a deck; the selectable face stays at its core position.
                        var depth = Math.Min(11, topLayer[pos.BlindStack] - pos.Layer);
                        var side = pos.BlindStack == 1 ? -1 : 1;
                        tile.Rect.anchoredPosition += new Vector2(side * depth * 2.7f, -depth * .35f);
                    }
                    if (!canPick)
                    {
                        tile.Background.color = UiFactory.Hex("A0AB8D");
                        tile.Icon.color = UiFactory.Hex("8C9784");
                    }
                }
                else if (location == SheepLocation.Tray)
                    Place(tile, tray, 16 + Array.IndexOf(snapshot.Tray, id) * 84, TrayTilesTop, TileWidth, TileHeight);
                else
                    Place(tile, reserve, 64 + Array.IndexOf(snapshot.Reserve, id) * 80, 32, 64, 72);
            }
            for (var stack = 1; stack <= 2; stack++)
            {
                var label = stackCounts[stack - 1];
                label.gameObject.SetActive(stackCount[stack] > 0);
                if (stackCount[stack] == 0) continue;
                var pos = Array.Find(positions, p => p.BlindStack == stack);
                label.rectTransform.anchoredPosition = new Vector2(34 + pos.X * 36, -(110 + pos.Y * 36));
                label.text = "余 " + stackCount[stack] + " 张";
                label.transform.SetAsLastSibling();
            }
            trayCount.text = snapshot.Tray.Length + " / 7";
            trayCount.color = snapshot.Tray.Length >= 6 ? UiFactory.Hex("B55A3F") : UiFactory.Accent;
            reserveHint.text = snapshot.Reserve.Length == 0 ? "移出区 · 暂存的牌可随时点回" : "移出区 · 点击放回卡槽";
        }

        private static void Place(Tile tile, Transform parent, float x, float y, float width, float height)
        {
            tile.Rect.SetParent(parent, false);
            tile.Rect.SetAsLastSibling();
            tile.Rect.anchoredPosition = new Vector2(x, -y);
            tile.Rect.sizeDelta = new Vector2(width, height);
        }

        public IEnumerator AnimatePick(SheepGame game, SheepSnapshot before, SheepMove move)
        {
            var source = tiles[move.PickedId].Rect.position;
            var matched = move.MatchedIds.Length > 0;
            var oldMatchOrigins = new List<Vector3>();
            foreach (var id in move.MatchedIds)
                if (id != move.PickedId) oldMatchOrigins.Add(tiles[id].Rect.position);
            Render(game);
            var after = game.GetSnapshot();
            var picked = tiles[move.PickedId];
            var index = Array.IndexOf(after.Tray, move.PickedId);
            if (matched)
            {
                index = 0;
                for (var i = 0; i < before.Tray.Length; i++)
                    if (before.Kinds[before.Tray[i]] == before.Kinds[move.PickedId]) { index = Math.Min(i + 2, 6); break; }
            }
            var target = tray.TransformPoint(new Vector3(16 + index * 84, -TrayTilesTop, 0));
            var ghost = Ghost(before.Kinds[move.PickedId], source);
            picked.Rect.gameObject.SetActive(false);
            var companions = new List<Tile>();
            if (matched)
                foreach (var origin in oldMatchOrigins) companions.Add(Ghost(before.Kinds[move.PickedId], origin));
            const float travel = .16f;
            for (var time = 0f; time < travel; time += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(time / travel); t = 1 - (1 - t) * (1 - t);
                ghost.Rect.position = Vector3.Lerp(source, target, t);
                yield return null;
            }
            ghost.Rect.position = target;
            if (matched)
            {
                companions.Add(ghost);
                const float pop = .12f;
                for (var time = 0f; time < pop; time += Time.unscaledDeltaTime)
                {
                    var t = Mathf.Clamp01(time / pop);
                    foreach (var item in companions)
                    {
                        item.Rect.localScale = Vector3.one * (1 + Mathf.Sin(t * Mathf.PI) * .15f);
                        item.Background.color = item.Icon.color = new Color(1, 1, 1, 1 - t);
                    }
                    yield return null;
                }
            }
            FinishAnimations(game);
        }

        private Tile Ghost(int kind, Vector3 position)
        {
            var tile = CreateTile(overlay, -1);
            tile.Icon.texture = icons[kind];
            tile.Rect.position = position;
            ghosts.Add(tile.Rect.gameObject);
            return tile;
        }

        public void FinishAnimations(SheepGame game)
        {
            foreach (var ghost in ghosts)
                if (ghost != null) { ghost.SetActive(false); UnityEngine.Object.Destroy(ghost); }
            ghosts.Clear();
            Render(game);
        }
    }
}
