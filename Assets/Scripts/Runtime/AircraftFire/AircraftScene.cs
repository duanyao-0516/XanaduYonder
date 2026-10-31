using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AircraftFire;

namespace XanaduYonder.Runtime.AircraftFire
{
    // Imported sprite frames remain owned by Resources. Images are pooled; a long
    // classic run does not create one GameObject for every projectile or enemy.
    internal sealed class AircraftScene
    {
        private readonly RectTransform world;
        private readonly List<Image> backgrounds = new List<Image>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Pool enemies, bullets, pickups, explosions;
        private readonly Image hero, heroExplosion;
        internal event Action<bool> EnemyExploded;

        internal AircraftScene(RectTransform surface)
        {
            world = UiFactory.Rect(surface, "Aircraft logical world", 0, 0, 640, 1136);
            world.anchorMin = world.anchorMax = world.pivot = Vector2.zero;
            var backgroundLayer = Layer("Scrolling background");
            for (var i = 0; i < 5; i++) backgrounds.Add(NewImage(backgroundLayer, "Background " + i));
            pickups = new Pool(Layer("Supply drops"));
            enemies = new Pool(Layer("Enemy aircraft"));
            bullets = new Pool(Layer("Projectiles"));
            hero = NewImage(Layer("Player"), "Hero aircraft");
            explosions = new Pool(Layer("Explosions"));
            heroExplosion = NewImage(world, "Hero explosion");
        }

        internal void Layout(float width, float height)
        {
            var scale = width / AircraftGame.Width;
            world.sizeDelta = new Vector2(AircraftGame.Width, height / scale);
            world.localScale = Vector3.one * scale;
        }

        internal void Render(AircraftGame game, float terminalAge = 0)
        {
            var scroll = (float)(game.ElapsedTime * 65 % 1136);
            RenderBackground(game.Height, scroll);
            enemies.Begin();
            foreach (var enemy in game.Enemies)
            {
                var view = enemies.Get(enemy.Id, out _);
                var prefix = EnemyPrefix(enemy.Kind);
                var fly = enemy.Kind == AircraftEnemyKind.Boss || enemy.Kind == AircraftEnemyKind.Large
                    ? 1 + (int)(enemy.Age * 8) % 2 : 1;
                var name = prefix + "_fly_" + fly;
                if (view.Hp >= 0 && view.Hp > enemy.Hp) view.FlashUntil = (float)game.ElapsedTime + .1f;
                view.Hp = enemy.Hp;
                if (view.FlashUntil > game.ElapsedTime && enemy.Kind != AircraftEnemyKind.Small) name = prefix + "_hit_1";
                Paint(view.Image, name, enemy.X, enemy.Y, enemy.Width, enemy.Height);
            }
            enemies.End();
            bullets.Begin();
            foreach (var bullet in game.Bullets)
            {
                var view = bullets.Get(bullet.Id, out _);
                Paint(view.Image, bullet.Friendly ? (bullet.Doubled ? "bullet2" : "bullet1") : "enemy_bullet", bullet.X, bullet.Y, bullet.Width, bullet.Height);
                view.Image.color = Color.white;
            }
            bullets.End();
            pickups.Begin();
            foreach (var pickup in game.Pickups)
            {
                var view = pickups.Get(pickup.Id, out _);
                Paint(view.Image, pickup.Kind == AircraftPickupKind.Bomb ? "enemy4_fly_1" : "enemy5_fly_1",
                    pickup.X, pickup.Y, pickup.Width, pickup.Height);
                var swing = pickup.Age < .4f ? pickup.Age * 37.5f : 15 - Mathf.PingPong((pickup.Age - .4f) * 37.5f, 30);
                view.Image.rectTransform.localRotation = Quaternion.Euler(0, 0, swing);
            }
            pickups.End();
            explosions.Begin();
            foreach (var explosion in game.Explosions)
            {
                var age = explosion.Age + terminalAge;
                if (age >= explosion.Duration) continue;
                var view = explosions.Get(explosion.Id, out var fresh);
                var large = explosion.Kind == AircraftEnemyKind.Large || explosion.Kind == AircraftEnemyKind.Boss;
                var count = large ? 7 : 4;
                var frame = Mathf.Clamp((int)(age / explosion.Duration * count) + 1, 1, count);
                Paint(view.Image, EnemyPrefix(explosion.Kind) + "_blowup_" + frame,
                    explosion.X, explosion.Y, explosion.Width, explosion.Height);
                if (fresh) EnemyExploded?.Invoke(large);
            }
            explosions.End();
            var alive = game.Lives > 0 && game.Phase != AircraftPhase.GameOver;
            hero.gameObject.SetActive(alive);
            Paint(hero, "hero_fly_" + (1 + (int)(game.ElapsedTime * 10) % 2),
                game.HeroX, game.HeroY, AircraftGame.HeroWidth, AircraftGame.HeroHeight);
            hero.color = game.HeroInvulnerability > 0 && (int)(game.ElapsedTime * 12) % 2 == 0
                ? new Color(1, 1, 1, .25f) : Color.white;
            var deathAge = game.HeroDeathAge + terminalAge;
            var death = game.HeroDeathAge >= 0 && deathAge < .4f;
            heroExplosion.gameObject.SetActive(death);
            if (death) Paint(heroExplosion, "hero_blowup_" + Mathf.Clamp((int)(deathAge / .1f) + 1, 1, 4),
                game.HeroDeathX, game.HeroDeathY, AircraftGame.HeroWidth, AircraftGame.HeroHeight);
        }

        internal void RenderMenu(float height)
        {
            RenderBackground(height, 0);
            hero.gameObject.SetActive(false); heroExplosion.gameObject.SetActive(false);
        }
        private void RenderBackground(float height, float scroll)
        {
            for (var i = 0; i < backgrounds.Count; i++)
            {
                var y = i * 1136 - scroll;
                backgrounds[i].gameObject.SetActive(y < height);
                Paint(backgrounds[i], "background_2", 320, y + 568, 640, 1136);
            }
        }
        internal void Reset() { enemies.Clear(); bullets.Clear(); pickups.Clear(); explosions.Clear(); }
        private static string EnemyPrefix(AircraftEnemyKind kind) => kind == AircraftEnemyKind.Small ? "enemy1" : kind == AircraftEnemyKind.Medium ? "enemy3" : "enemy2";
        private RectTransform Layer(string name)
        {
            var layer = UiFactory.Rect(world, name, 0, 0, 640, 1136);
            UiFactory.Stretch(layer); return layer;
        }
        private void Paint(Image image, string name, float x, float y, float width, float height)
        {
            if (!sprites.TryGetValue(name, out var sprite))
            {
                sprite = Resources.Load<Sprite>("AircraftFire/Sprites/" + name);
                if (sprite == null) throw new InvalidOperationException("Missing Aircraft Fire sprite: " + name);
                sprites.Add(name, sprite);
            }
            image.sprite = sprite;
            image.rectTransform.anchoredPosition = new Vector2(x, y);
            image.rectTransform.sizeDelta = new Vector2(width, height);
        }
        private static Image NewImage(Transform parent, string name)
        {
            var image = UiFactory.Rect(parent, name, 0, 0, 1, 1).gameObject.AddComponent<Image>();
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            image.raycastTarget = false;
            return image;
        }
        private sealed class View
        {
            internal Image Image;
            internal int Hp = -1;
            internal float FlashUntil;
            internal bool Seen;
        }
        private sealed class Pool
        {
            private readonly Transform parent;
            private readonly Dictionary<int, View> active = new Dictionary<int, View>();
            private readonly Stack<View> free = new Stack<View>();
            private readonly List<int> dead = new List<int>();
            internal Pool(Transform parent) { this.parent = parent; }
            internal void Begin() { foreach (var item in active.Values) item.Seen = false; }
            internal View Get(int id, out bool fresh)
            {
                fresh = !active.TryGetValue(id, out var view);
                if (fresh)
                {
                    view = free.Count == 0 ? new View { Image = NewImage(parent, "Pooled sprite") } : free.Pop();
                    view.Image.gameObject.SetActive(true); view.Image.color = Color.white;
                    view.Hp = -1; view.FlashUntil = 0;
                    active.Add(id, view);
                }
                view.Seen = true; return view;
            }
            internal void End()
            {
                dead.Clear();
                foreach (var item in active) if (!item.Value.Seen) dead.Add(item.Key);
                foreach (var id in dead)
                {
                    var view = active[id]; active.Remove(id);
                    view.Image.gameObject.SetActive(false); free.Push(view);
                }
            }
            internal void Clear() { Begin(); End(); }
        }
    }
}
