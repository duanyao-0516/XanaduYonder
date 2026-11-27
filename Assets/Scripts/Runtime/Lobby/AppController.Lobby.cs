using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.BoardGames;
using XanaduYonder.Runtime.FlappyBird;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private void BuildLobby()
        {
            var root = UiFactory.Rect(frame, "Lobby", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            lobbyPage = root.gameObject;
            var entries = new List<LobbyEntry>
            {
                LobbyGame("sudoku", "Sudoku", "数独", "06", "逻辑策略", "九宫推理", "九宫之间，找到唯一。", "", "", EnterSudoku, LobbyPreviews.Sudoku),
                LobbyGame("minesweeper", "Minesweeper", "扫雷", "07", "逻辑策略", "经典扫雷", "数字之间，步步为营。", "首击安全 · 三档挑战", "首击安全 · 三档挑战", EnterMinesweeper, LobbyPreviews.Minesweeper),
                LobbyGame("sliding-puzzle", "SlidingPuzzle", "数字华容道", "14", "逻辑策略", "数字归位", "留一格空白，理顺每一步。", "", "", EnterSlidingPuzzle, LobbyPreviews.SlidingPuzzle),
                LobbyGame("sokoban", "Sokoban", "推箱子", "12", "逻辑策略", "经典推箱", "把每一只箱子，推回它的位置。", "", "", EnterSokoban, AddSokobanPreview),
                LobbyGame("gomoku", "Gomoku", "五子棋", "10", "逻辑策略", "黑白对弈", "一子一步，先连成五。", "人机 / 本地双人 · 三档难度", "人机 / 双人", EnterGomoku, AddGomokuPreview),
                ClassicBoardEntry(BoardGameId.Army, "ArmyChess", "29", "暗棋对弈", "秘密布阵，沿铁路线夺取军旗。", EnterArmyChess),
                ClassicBoardEntry(BoardGameId.Xiangqi, "Xiangqi", "30", "楚河汉界", "车马炮交锋，守住九宫。", EnterXiangqi),
                ClassicBoardEntry(BoardGameId.Chess, "Chess", "31", "黑白方格", "发展子力，寻找将死。", EnterChess),
                ClassicBoardEntry(BoardGameId.Checkers, "ChineseCheckers", "32", "六角跳棋", "跨过棋珠，先到对角的营地。", EnterChineseCheckers),
                CutTheRopeEntry(),
                LobbyGame("zhaoyun-adou", "ZhaoyunAdou", "赵云与阿斗", "23", "逻辑策略", "合成布阵", "合兵布阵，与阿斗守住战场。", "离线人机 · 兵种合成 · 武器成长", "离线人机 · 合成布阵", EnterZhaoyunAdou, AddZhaoyunAdouPreview),
                LobbyGame("2048", "2048", "2048", "01", "合成消除", "数字益智", "让相同的数字相遇。", "", "", EnterGame, LobbyPreviews.Numbers2048),
                LobbyGame("watermelon", "Watermelon", "合成大西瓜", "04", "合成消除", "碰碰合成", "小小一颗，合成满满的快乐。", "水果 / 表情 / 校徽 · 三种主题", "三种主题", EnterWatermelon, AddWatermelonPreview),
                LobbyGame("bakery-basket", "BakeryBasket", BakeryBasket.BakeryPage.DisplayName, "28", "合成消除", "烘焙篮", "颠一颠，找齐三件。", "直壁 / 收底 · 3D 翻找", "直壁 / 收底", EnterBakeryBasket, AddBakeryBasketPreview),
                LobbyGame("sheep", "Sheep", "旸了个旸", "03", "合成消除", "层层三消", "翻开一层，凑齐三个。", "", "", EnterSheep, AddSheepPreview),
                LobbyGame("link-match", "LinkMatch", "连连看", "17", "合成消除", "轻松连线", "找到相同，连起快乐。", "轻松 / 挑战 · 五档棋盘", "轻松 / 挑战", EnterLinkMatch, AddLinkMatchPreview),
                LobbyGame("zuma", "Zuma", "Zuma", "08", "合成消除", "神庙弹珠", "转动青蛙，让同色弹珠相遇。", "横屏游玩 · 18 张地图 · 4 档难度", "18 张地图", EnterZuma, LobbyPreviews.Zuma),
                LobbyGame("tetris", "Tetris", "俄罗斯方块", "15", "合成消除", "方块慢落", "落下一块，留一点从容。", "", "", EnterTetris, LobbyPreviews.Tetris),
                LobbyGame("jump", "Jump", "跳一跳", "02", "敏捷挑战", "轻巧向前", "按住，松手，再远一点。", "", "", EnterJump, AddJumpPreview),
                LobbyGame("dino", "Dino", "Dino", "09", "敏捷挑战", "像素跑酷", "横屏或竖屏，跑得更远一点。", "", "", EnterDino, LobbyPreviews.Dino),
                LobbyGame("flappy-bird", "FlappyBird", "Flappy Bird", "16", "敏捷挑战", "轻点飞行", "轻点一下，再飞远一点。", "", "", EnterFlappyBird, AddFlappyPreview),
                LobbyGame("snake", "Snake", "贪吃蛇", "26", "敏捷挑战", "经典贪吃蛇", "穿过边界，绕过障碍。", "经典无限 · 障碍闯关", "经典无限 / 12 关挑战", EnterSnake, AddSnakePreview),
                LobbyGame("happy-ball", "HappyBall", "欢乐球球", "11", "敏捷挑战", "轻巧下落", "转动高塔，穿过层层豁口。", "", "", EnterHappyBall, LobbyPreviews.HappyBall),
                LobbyGame("gold-miner", "Gold Miner", "黄金矿工", "13", "敏捷挑战", "一起寻宝", "看准时机，把宝藏带回家。", "单人循环闯关 / 双人 30 关合作", "单人 / 双人", EnterGoldMiner, AddGoldMinerPreview),
                LobbyGame("level-devil", "LevelDevil", "只有一道门", "22", "敏捷挑战", "小心下一步", "越过意想不到的机关，走到门前。", "横屏离线 · 单人 / 双人 · 触屏操作", "单人 / 双人", EnterLevelDevil, AddLevelDevilPreview),
                LobbyGame("bomb-it", "BombIt", "Q版泡泡堂", "27", "敏捷挑战", "经典炸弹人", "炸开道路，躲过火焰，战胜对手。", "初代原版 · 离线人机 / 同屏双人", "人机 / 双人", EnterBombIt, AddBombItPreview),
                LobbyGame("aircraft-fire", "AircraftFire", "飞机大战", "20", "敏捷挑战", "指尖空战", "穿过弹幕，守住这一片天空。", "", "", EnterAircraftFire, AddAircraftFirePreview),
                LobbyGame("idiom", "Idiom", "成语接龙", "05", "文字语言", "一词一世界", "一来一往，认识更多好词。", "同字 / 同音 · 随时收藏生词", "同字 / 同音", EnterIdiom, null),
                BuildFeihualingEntry()
            };
            lobbyView = root.gameObject.AddComponent<LobbyView>();
            lobbyView.Initialize(entries, OpenSettings, LobbyDisplayPreferences.Load(), LobbyDisplayPreferences.Save);
            lobbyScroll = lobbyView.Scroll;
            lobbyStatus = lobbyView.StatusLabel;
            RefreshDinoRecord();
            RefreshGoldMinerRecord();
        }

        private static LobbyEntry LobbyGame(string id, string objectName, string name, string number,
            string category, string tagline, string description, string record, string compactRecord,
            Action enter, Action<Transform> drawLogo)
        {
            return new LobbyEntry
            {
                Id = id, ObjectName = objectName, Name = name, Number = number,
                Category = category, Tagline = tagline, Description = description,
                Record = record, CompactRecord = compactRecord, Enter = enter,
                // BirdRest's 124x112 atlas cell has asymmetric transparent padding (alpha bounds
                // x=5..122, y=23..109 from the top). At 82x74 and 7 degrees, its visible center
                // is (1.39,-6.57) from the image center; keep that artwork optically centered.
                BuildLogo = drawLogo == null ? null : slot => LobbyArtwork.Build(slot, drawLogo,
                    id == "flappy-bird" ? new Vector2(1.39f, -6.57f) : Vector2.zero)
            };
        }

        private static void AddWatermelonPreview(Transform parent)
        {
            var fruit = UiFactory.Rect(parent, "Watermelon artwork", 0, 0, 90, 90).gameObject.AddComponent<RawImage>();
            fruit.texture = Resources.Load<Texture2D>("Watermelon/Fruit/11");
            fruit.raycastTarget = false;
        }

        private static void AddSokobanPreview(Transform parent)
        {
            var crate = UiFactory.Rect(parent, "Sokoban crate preview", 0, 0, 90, 90).gameObject.AddComponent<RawImage>();
            crate.texture = Resources.Load<Texture2D>("Sokoban/Sprites/crate");
            crate.raycastTarget = false;
        }

        private static void AddGoldMinerPreview(Transform parent)
        {
            var miner = UiFactory.Rect(parent, "Gold Miner celebrating miner", 0, 0, 142, 138).gameObject.AddComponent<Image>();
            foreach (var pose in Resources.LoadAll<Sprite>("GoldMiner/characters/miner-p1-states"))
                if (pose.name == "success") { miner.sprite = pose; break; }
            miner.preserveAspect = true;
            miner.raycastTarget = false;
            var treasure = UiFactory.Rect(parent, "Gold Miner prize", 90, 88, 62, 54).gameObject.AddComponent<Image>();
            treasure.sprite = Resources.Load<Sprite>("GoldMiner/objects/ore_gold_medium");
            treasure.preserveAspect = true;
            treasure.raycastTarget = false;
        }

        private void AddFlappyPreview(Transform parent)
        {
            var bird = UiFactory.Rect(parent, "Bird artwork", 0, 0, 82, 74).gameObject.AddComponent<Image>();
            bird.preserveAspect = true;
            if (flappyLobbySprite == null) flappyLobbySprite = FlappyArt.LoadSprite("BirdRest");
            bird.sprite = flappyLobbySprite;
            bird.raycastTarget = false;
            bird.rectTransform.localRotation = Quaternion.Euler(0, 0, 7);
        }

        private static void AddGomokuPreview(Transform parent)
        {
            for (int i = 0; i < 3; i++)
            {
                UiFactory.Panel(parent, "Gomoku preview row " + i, 432, 82 + i * 34, 136, 1.5f, UiFactory.Hex("8C907A"), false).raycastTarget = false;
                UiFactory.Panel(parent, "Gomoku preview column " + i, 466 + i * 34, 62, 1.5f, 108, UiFactory.Hex("8C907A"), false).raycastTarget = false;
            }
            for (int i = 0; i < 5; i++)
            {
                float x = 466 + (i % 3) * 34, y = 82 + (i / 3) * 34;
                UiFactory.CirclePanel(parent, "Gomoku preview stone " + i, x - 13, y - 13, 26,
                    i % 2 == 0 ? UiFactory.Ink : UiFactory.Paper).raycastTarget = false;
            }
        }

        private static void AddLinkMatchPreview(Transform parent)
        {
            var art = UiFactory.Rect(parent, "Link Match fruit preview", 413, 56, 171, 171);
            string[] fruits = { "strawberry", "banana", "strawberry", "grapes" };
            for (int i = 0; i < fruits.Length; i++)
            {
                float x = 26 + i % 2 * 69, y = 15 + i / 2 * 74;
                UiFactory.Panel(art, "Fruit shadow " + i, x, y + 4, 59, 59, UiFactory.Hex("D3DBC3"));
                var tile = UiFactory.Panel(art, "Fruit tile " + i, x, y, 59, 59, UiFactory.Hex("FFFCF0"));
                var fruit = UiFactory.Rect(tile.transform, "Fruit", 3.5f, 3.5f, 52, 52).gameObject.AddComponent<RawImage>();
                fruit.texture = Resources.Load<Texture2D>("LinkMatch/Sprites/" + fruits[i]);
                fruit.raycastTarget = false;
            }
            var route = UiFactory.Hex("739652");
            UiFactory.Panel(art, "Pair route top", 16, 48.5f, 28, 3, route, false);
            UiFactory.Panel(art, "Pair route outside", 14.5f, 50, 3, 74, route, false);
            UiFactory.Panel(art, "Pair route bottom", 16, 122.5f, 28, 3, route, false);
        }

        private static void AddSheepPreview(Transform parent)
        {
            for (var i = 0; i < 3; i++)
            {
                var tile = UiFactory.Rect(parent, "Sheep tile preview " + i, 420 + i * 52, i == 1 ? 66 : 82, 64, 72);
                var backing = tile.gameObject.AddComponent<RawImage>();
                backing.texture = Resources.Load<Texture2D>("Sheep/Tiles/block_bg");
                backing.raycastTarget = false;
                var icon = UiFactory.Rect(tile, "Tile icon", 0, 0, 64, 72).gameObject.AddComponent<RawImage>();
                icon.texture = Resources.Load<Texture2D>("Sheep/Tiles/block_" + (i + 1));
                icon.raycastTarget = false;
            }
        }

        private static void AddJumpPreview(Transform parent)
        {
            var frame = LobbyPreviews.Frame(parent, "Jump");
            var artwork = UiFactory.Rect(frame, "Jump artwork", 0, 0, 140, 167);
            UiFactory.Center(artwork);
            artwork.localScale = Vector3.one * .85f;
            var platform = UiFactory.Rect(artwork, "Jump platform preview", 0, 42, 140, 125).gameObject.AddComponent<RawImage>();
            platform.texture = Resources.Load<Texture2D>("Jump/Preview/platform");
            platform.raycastTarget = false;
            var pawn = UiFactory.Rect(artwork, "Jump pawn preview", 50, 0, 25, 69).gameObject.AddComponent<RawImage>();
            pawn.texture = Resources.Load<Texture2D>("Jump/Preview/pawn");
            pawn.raycastTarget = false;
        }
    }
}
