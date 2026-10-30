using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Tetris;
using XanaduYonder.Runtime.Minesweeper;
using XanaduYonder.Runtime.Tetris;

namespace XanaduYonder.Runtime
{
    // Static lobby illustrations only: never construct a game session or own its shared assets.
    internal static class LobbyPreviews
    {
        internal static RectTransform Frame(Transform parent, string name)
        {
            return UiFactory.Rect(parent, name + " preview", 426, 60, 150, 104);
        }

        internal static void Numbers2048(Transform parent)
        {
            var root = Frame(parent, "2048");
            var values = new[] { "2", "4", "8", "16" };
            var shades = new[] { "F5F1D9", "E4E9B8", "A8C89C", "79AB81" };
            for (int i = 0; i < values.Length; i++)
            {
                var tile = Tile(root, "2048 tile " + values[i], 25 + i % 2 * 53, 2 + i / 2 * 53,
                    47, 47, UiFactory.Hex(shades[i]));
                UiFactory.Label(tile.transform, values[i], 0, 0, 47, 47, 25,
                    i == 3 ? Color.white : UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        internal static void Sudoku(Transform parent)
        {
            var root = Frame(parent, "Sudoku");
            for (int i = 0; i < 9; i++)
            {
                var tile = UiFactory.Panel(root, "Sudoku preview " + i,
                    23 + i % 3 * 36, i / 3 * 36, 32, 32, UiFactory.Paper, false);
                UiFactory.Label(tile.transform, i == 1 || i == 6 ? "" : (i + 1).ToString(),
                    0, 0, 32, 32, 21, UiFactory.Accent, TextAnchor.MiddleCenter);
            }
        }

        internal static void Minesweeper(Transform parent)
        {
            var root = Frame(parent, "Minesweeper");
            for (int i = 0; i < 9; i++)
            {
                var tile = UiFactory.Panel(root, "Minesweeper preview " + i,
                    23 + i % 3 * 36, i / 3 * 36, 32, 32,
                    i == 0 || i == 3 ? UiFactory.Paper : UiFactory.Hex("BBCBB6"), false);
                if (i == 0 || i == 3)
                    UiFactory.Label(tile.transform, i == 0 ? "1" : "2", 0, 0, 32, 32, 21,
                        UiFactory.Accent, TextAnchor.MiddleCenter);
                if (i != 4) continue;
                var flag = UiFactory.Rect(tile.transform, "Flag", 4, 4, 24, 24).gameObject.AddComponent<MinesweeperIconGraphic>();
                flag.Kind = MinesweeperIconKind.Flag;
                flag.color = UiFactory.Hex("B34842");
                flag.ContrastFlagPole = true;
                flag.raycastTarget = false;
            }
        }

        internal static void Zuma(Transform parent)
        {
            // This lobby-owned resource is independent of textures unloaded by ZumaAssets.Dispose.
            Artwork(parent, "Zuma", "Lobby/ZumaLogo");
        }

        internal static void Dino(Transform parent)
        {
            var root = Frame(parent, "Dino");
            var image = UiFactory.Rect(root, "Dino standing frame", 31, 5, 88, 94).gameObject.AddComponent<RawImage>();
            var atlas = Resources.Load<Texture2D>("Dino/Sprites");
            image.texture = atlas;
            // Same standing frame and top-origin atlas coordinates as DinoTrackView.RenderPlayer.
            image.uvRect = new Rect(1678f / atlas.width, (atlas.height - 2 - 94f) / atlas.height,
                88f / atlas.width, 94f / atlas.height);
            image.raycastTarget = false;
        }

        internal static void HappyBall(Transform parent)
        {
            Artwork(parent, "HappyBall", "Lobby/HappyBallLogo");
        }

        internal static void SlidingPuzzle(Transform parent)
        {
            var root = Frame(parent, "SlidingPuzzle");
            UiFactory.Panel(root, "Sliding board", 23, 0, 104, 104, UiFactory.Hex("BBCBB6"), false);
            for (int i = 0; i < 8; i++)
            {
                var tile = Tile(root, "Sliding tile " + (i + 1), 26 + i % 3 * 34, 3 + i / 3 * 34,
                    30, 30, UiFactory.Hex("F5F1D9"));
                UiFactory.Label(tile.transform, (i + 1).ToString(), 0, 0, 30, 30, 20,
                    UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        internal static void Tetris(Transform parent)
        {
            var graphic = Frame(parent, "Tetris").gameObject.AddComponent<TetrisGraphic>();
            graphic.Kind = TetrisGraphicKind.Preview;
            graphic.SetPreview(TetrisPiece.T);
            graphic.raycastTarget = false;
        }

        private static Image Tile(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var tile = UiFactory.Panel(parent, name, x, y, width, height, color);
            tile.pixelsPerUnitMultiplier = 4; // Keep four-unit corners on small tiles instead of pill-shaped cells.
            return tile;
        }

        private static void Artwork(Transform parent, string name, string resource)
        {
            var root = Frame(parent, name);
            var image = UiFactory.Rect(root, name + " artwork", 0, 0, 96, 96).gameObject.AddComponent<RawImage>();
            UiFactory.Center(image.rectTransform);
            image.texture = Resources.Load<Texture2D>(resource);
            image.raycastTarget = false;
        }
    }
}
