using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Dino;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Dino;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Exercises the production renderer and original atlas, without changing device or save state.
    [InitializeOnLoad]
    public static class DinoGroundValidation
    {
        private const string Pending = "Xanadu.DinoGroundValidation.Pending";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int assertions;

        static DinoGroundValidation() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Dino ground validation runner").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }

        private static IEnumerator AfterFrame() { yield return null; Execute(); }

        private static void Execute()
        {
            assertions = 0; Report.Clear(); int exit = 0;
            try
            {
                using (var fixture = new Fixture())
                {
                    CheckAtlasAndFootOverlap(fixture);
                    CheckVariantsAndScroll(fixture);
                    CheckModelAndRebuild(fixture);
                    CheckCollisionStop(fixture);
                }
                Report.AppendLine("LIMITS: production UGUI geometry and source atlas pixels; no Android input or screenshot rasterization.");
                Report.AppendLine("DINO_GROUND_CHECKS_PASS: " + assertions + " assertions");
            }
            catch (Exception error) { exit = 1; Report.AppendLine("DINO_GROUND_CHECKS_FAIL: " + error); Debug.LogException(error); }
            finally
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/logs"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "dino-ground-checks.txt"), Report.ToString());
                Debug.Log(Report.ToString()); EditorApplication.Exit(exit);
            }
        }

        private static void CheckAtlasAndFootOverlap(Fixture fixture)
        {
            var track = fixture.Track;
            track.Render(fixture.Game, 0);
            var atlas = Get<Texture2D>(track, "atlas");
            Require(atlas.width == 2446 && atlas.height == 194, "Original 2x atlas loads");
            int flatRaisedPixels = 0, bumpyRaisedPixels = 0;
            for (int y = 104; y < 112; y++)
            for (int x = 0; x < 1200; x++)
            {
                if (Dark(atlas.GetPixel(2 + x, atlas.height - 1 - y))) flatRaisedPixels++;
                if (Dark(atlas.GetPixel(1202 + x, atlas.height - 1 - y))) bumpyRaisedPixels++;
            }
            Require(flatRaisedPixels == 0 && bumpyRaisedPixels > 100, "Second strip actually contains the original raised hills");
            for (int y = 104; y < 118; y++)
            {
                Require(atlas.GetPixel(2, atlas.height - 1 - y) == atlas.GetPixel(1201, atlas.height - 1 - y), "Flat strip horizon endpoints match");
                Require(atlas.GetPixel(1202, atlas.height - 1 - y) == atlas.GetPixel(2401, atlas.height - 1 - y), "Bumpy strip horizon endpoints match");
            }
            var tile = Ground(track)[0]; var player = Get<RawImage>(track, "player");
            float scale = Get<float>(track, "scale");
            float horizonBottom = tile.rectTransform.anchoredPosition.y + tile.rectTransform.sizeDelta.y * 14 / 24;
            float horizonTop = tile.rectTransform.anchoredPosition.y + tile.rectTransform.sizeDelta.y * 16 / 24;
            float footBase = player.rectTransform.anchoredPosition.y;
            Near((horizonBottom - footBase) / scale, 8 * DinoRules.SpriteScale, "Horizon enters the lower legs at the original scaled height");
            Near((horizonTop - footBase) / scale, 9 * DinoRules.SpriteScale, "Horizon band stays within the lower legs");
            // Confirm those rows intersect opaque foot pixels, not merely the sprite's clear bounds.
            float overlap = ((horizonBottom + horizonTop) * .5f - footBase) / player.rectTransform.sizeDelta.y;
            int sourceRow = Mathf.FloorToInt((player.uvRect.y + player.uvRect.height * overlap) * atlas.height);
            int legPixels = 0;
            for (int x = 0; x < 88; x++) if (Dark(atlas.GetPixel(1678 + x, sourceRow))) legPixels++;
            Require(legPixels > 0, "The main horizon intersects actual opaque leg pixels");
            Report.AppendLine("PASS original hills, seamless source edges and horizon / opaque-foot overlap");
        }

        private static void CheckVariantsAndScroll(Fixture fixture)
        {
            var track = fixture.Track; float scale = Get<float>(track, "scale");
            var tiles = Ground(track);
            GroundAt(track, 0);
            CheckUv(tiles[0], 2); CheckUv(tiles[1], 1202);
            Near(tiles[0].rectTransform.sizeDelta.x / scale, 810, "Ground pixels share character scale");
            GroundAt(track, 100); var uv = tiles[0].uvRect; float before = tiles[0].rectTransform.anchoredPosition.x;
            GroundAt(track, 137);
            Near(tiles[0].rectTransform.anchoredPosition.x - before, -37 * scale, "Ground scroll uses actual traveled distance");
            Require(tiles[0].uvRect == uv, "Within one world tile its terrain never rerolls");
            GroundAt(track, 809.875); var nextUv = tiles[1].uvRect; float nextX = tiles[1].rectTransform.anchoredPosition.x;
            GroundAt(track, 810.125);
            Require(tiles[0].uvRect == nextUv, "Visible tile retains its type when pooled slot changes");
            Near(tiles[0].rectTransform.anchoredPosition.x - nextX, -.25 * scale, "Crossing the pool boundary is spatially continuous");
            var stableUv = tiles[0].uvRect; var stablePosition = tiles[0].rectTransform.anchoredPosition;
            for (int i = 0; i < 20; i++) GroundAt(track, 810.125);
            Require(tiles[0].uvRect == stableUv && tiles[0].rectTransform.anchoredPosition == stablePosition, "Repeated frozen frames retain the exact ground");
            GroundAt(track, 2430); CheckUv(tiles[0], 2);
            Report.AppendLine("PASS both source strips, distance scroll, tile-boundary continuity and repeated-frame stability");
        }

        private static void CheckModelAndRebuild(Fixture fixture)
        {
            var game = fixture.Game; game.Jump();
            for (int i = 0; i < 181; i++) game.Step();
            Require(!game.IsGameOver && game.Obstacles.Count > 0, "Real simulation supplies a ground obstacle");
            long tick = game.Tick, score = game.Score; double distance = game.Distance, y = game.PlayerY;
            var obstacle = game.Obstacles[0];
            fixture.Track.Render(game, 0);
            var player = Get<RawImage>(fixture.Track, "player");
            float scale = Get<float>(fixture.Track, "scale");
            Near(player.rectTransform.anchoredPosition.x / scale, game.Rules.PlayerX, "Player keeps its model x");
            Near(player.rectTransform.anchoredPosition.y / scale, 30 + game.PlayerY, "Player baseline is unchanged");
            var obstacleImage = Get<RawImage[]>(fixture.Track, "obstacles")[0];
            Near(obstacleImage.rectTransform.anchoredPosition.x / scale, obstacle.X, "Obstacle keeps its model x");
            Near(obstacleImage.rectTransform.anchoredPosition.y / scale, 30 + obstacle.Y, "Obstacle baseline is unchanged");
            var oldGround = Ground(fixture.Track); int count = Get<int>(fixture.Track, "groundCount");
            var positions = new Vector2[count]; var uvs = new Rect[count];
            for (int i = 0; i < count; i++) { positions[i] = oldGround[i].rectTransform.anchoredPosition; uvs[i] = oldGround[i].uvRect; }
            var presentation = fixture.Track.Capture(game);
            var rebuilt = fixture.CreateTrack(608, 344);
            rebuilt.Restore(game, presentation); rebuilt.Render(game, 0);
            var newGround = Ground(rebuilt);
            for (int i = 0; i < count; i++)
                Require(newGround[i].uvRect == uvs[i] && newGround[i].rectTransform.anchoredPosition == positions[i], "Resized tree reconstructs the same paused world ground");
            for (int i = 0; i < 30; i++) rebuilt.Render(game, 0);
            Require(game.Tick == tick && game.Score == score && game.Distance == distance && game.PlayerY == y &&
                game.Obstacles[0].X == obstacle.X && game.Obstacles[0].Y == obstacle.Y, "Rendering, terrain variation and rebuilding never change model state");
            Report.AppendLine("PASS real player / obstacle baselines, unchanged model state and paused-tree reconstruction");
        }

        private static void CheckCollisionStop(Fixture fixture)
        {
            var game = fixture.Game;
            for (int i = 0; i < 1500 && !game.IsGameOver; i++) game.Step();
            Require(game.IsGameOver && game.PresentationDistance > game.Distance,
                "Real collision stops partway through a motion frame while score distance retains completed ticks");
            fixture.Track.Render(game, 0);
            float scale = Get<float>(fixture.Track, "scale");
            double expected = -(game.PresentationDistance % (600 * DinoRules.SpriteScale)) * scale;
            Near(Ground(fixture.Track)[0].rectTransform.anchoredPosition.x, expected,
                "Ground freezes at the same fractional collision position as player and obstacles");
            var position = Ground(fixture.Track)[0].rectTransform.anchoredPosition;
            for (int i = 0; i < 30; i++) { game.Step(); fixture.Track.Render(game, .016f); }
            Require(Ground(fixture.Track)[0].rectTransform.anchoredPosition == position,
                "Terminal collision position remains frozen even when given render time");
            Report.AppendLine("PASS real collision contact scroll and terminal ground freeze");
        }

        private static bool Dark(Color pixel) { return pixel.a > .9f && pixel.r < .6f; }
        private static List<RawImage> Ground(DinoTrackView track) { return Get<List<RawImage>>(track, "ground"); }
        private static void GroundAt(DinoTrackView track, double distance) { typeof(DinoTrackView).GetMethod("RenderGround", Private).Invoke(track, new object[] { distance }); }
        private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
        private static void CheckUv(RawImage tile, int sourceX)
        {
            Near(tile.uvRect.x * 2446, sourceX, "Expected ground source x");
            Near(tile.uvRect.width * 2446, 1200, "Complete ground source width");
            Near(tile.uvRect.height * 194, 24, "Complete ground source height");
        }
        private static void Near(double actual, double expected, string message) { Require(Math.Abs(actual - expected) < .002, message + ": " + actual + " != " + expected); }
        private static void Require(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject owner = new GameObject("Dino ground fixture", typeof(RectTransform));
            internal readonly DinoGame Game = new DinoGame(new DinoRules(608 / 1.2), 37);
            internal readonly DinoTrackView Track;
            internal Fixture() { Track = CreateTrack(608, 320); }
            internal DinoTrackView CreateTrack(float width, float height)
            {
                var card = UiFactory.Panel(owner.transform, "Dino ground card", 0, 0, width, height, Color.white);
                card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var track = card.gameObject.AddComponent<DinoTrackView>();
                track.Initialize(card.rectTransform); track.SetLayout(width, height, Game.Rules.ViewportWidth);
                return track;
            }
            public void Dispose() { Object.DestroyImmediate(owner); }
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
