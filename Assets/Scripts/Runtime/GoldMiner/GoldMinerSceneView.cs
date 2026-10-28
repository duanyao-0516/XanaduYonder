using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed class GoldMinerSceneView
    {
        private readonly RectTransform root;
        private readonly GoldMinerArtwork art;
        private readonly GoldMinerLayout layout;
        private readonly Dictionary<int, Image> entities = new Dictionary<int, Image>();
        private readonly Image[] hooks;
        private readonly GoldMinerHempRope[] ropes;
        private readonly GoldMinerWinchView[] winches;
        private readonly float[] celebrations;
        private readonly List<Effect> effects = new List<Effect>();
        private readonly GoldMinerMode mode;
        private float time;

        private sealed class Effect
        {
            public RectTransform Rect;
            public Graphic Graphic;
            public float Age;
            public float Duration;
            public Vector2 Start;
            public bool Floating;
        }

        public GoldMinerSceneView(RectTransform parent, GoldMinerArtwork artwork, GoldMinerLayout metrics, GoldMinerMode gameMode)
        {
            root = parent;
            art = artwork;
            layout = metrics;
            mode = gameMode;
            int players = GoldMinerRules.PlayerCount(mode);
            winches = new GoldMinerWinchView[players];
            hooks = new Image[players];
            ropes = new GoldMinerHempRope[players];
            celebrations = new float[players];
        }

        public void Build(GoldMinerRunSnapshot run)
        {
            foreach (var entity in run.Current.Entities)
            {
                var image = Picture(root, "Ore " + entity.Id, SpriteFor(entity, 0), 0, 0, 40, 40);
                entities.Add(entity.Id, image);
            }
            // Animals pass in front of static objects, without changing the collision ordering.
            foreach (var entity in run.Current.Entities)
            {
                if (!GoldMinerRules.IsAnimal(entity.Kind)) continue;
                entities[entity.Id].transform.SetAsLastSibling();
            }
            for (int player = 0; player < winches.Length; player++)
            {
                var origin = layout.World(GoldMinerRules.OriginX(mode, player), 0);
                var rope = UiFactory.Rect(root, "Rope P" + (player + 1), origin.x, origin.y, 4.4f, 20).gameObject.AddComponent<GoldMinerHempRope>();
                rope.raycastTarget = false;
                rope.rectTransform.pivot = new Vector2(.5f, 1);
                ropes[player] = rope;
                hooks[player] = Picture(root, "Hook P" + (player + 1),
                    art.Get("characters/hook-open-closed", "open"), 0, 0, 48, 48);
                hooks[player].rectTransform.pivot = new Vector2(.5f, .85f);
            }
            for (int player = 0; player < winches.Length; player++)
            {
                var origin = layout.World(GoldMinerRules.OriginX(mode, player), 0);
                winches[player] = new GoldMinerWinchView(root, art, origin, player, layout.Cooperative,
                    origin.y - layout.Hud.yMax - 8, layout.Scale);
            }
            Render(run, 0);
        }

        public void Render(GoldMinerRunSnapshot run, float delta)
        {
            time += delta;
            var state = run.Current;
            foreach (var entity in state.Entities)
            {
                var image = entities[entity.Id];
                bool visible = entity.Status == GoldMinerEntityStatus.Available;
                image.gameObject.SetActive(visible);
                if (!visible) continue;
                var point = layout.World(entity.X, entity.Y);
                float diameter = (float)GoldMinerRules.Radius(entity.Kind) * 2 * layout.Scale;
                bool animal = GoldMinerRules.IsAnimal(entity.Kind);
                if (animal) diameter *= 1.45f;
                image.sprite = SpriteFor(entity, time);
                var visibleBounds = entity.Kind == GoldMinerKind.DiamondMole
                    ? new Rect(119f / 1254, 137f / 1254, 1069f / 1254, 924f / 1254)
                    : animal ? new Rect(0, 0, 1, 1) : GoldMinerArtwork.VisibleBoundsNormalized(ObjectPath(entity.Kind));
                float size = diameter / Mathf.Max(visibleBounds.width, visibleBounds.height);
                image.rectTransform.sizeDelta = new Vector2(size, animal && entity.Kind != GoldMinerKind.DiamondMole ? size * .8f : size);
                image.rectTransform.pivot = new Vector2(.5f, .5f);
                image.rectTransform.anchoredPosition = new Vector2(point.x, -point.y);
                image.rectTransform.localScale = new Vector3(animal && entity.MoveDirection < 0 ? -1 : 1, 1, 1);
                image.rectTransform.localRotation = Quaternion.identity;
                if (entity.Kind == GoldMinerKind.DiamondMole)
                {
                    // The diamond and both paws are painted into this one sprite.
                    // A small whole-body gait keeps the carried gem physically connected.
                    float step = Mathf.Sin(time * 16 + entity.Id);
                    image.rectTransform.anchoredPosition += Vector2.up * Mathf.Abs(step) * diameter * .035f;
                    image.rectTransform.localEulerAngles = new Vector3(0, 0, step * 3);
                }
                var visibleCenter = Vector2.Scale(visibleBounds.center - Vector2.one * .5f, image.rectTransform.rect.size);
                visibleCenter.x *= image.rectTransform.localScale.x;
                image.rectTransform.anchoredPosition -= (Vector2)(image.rectTransform.localRotation * (Vector3)visibleCenter);
            }
            for (int player = 0; player < state.Hooks.Length; player++)
            {
                var hook = state.Hooks[player];
                var origin = layout.World(GoldMinerRules.OriginX(mode, player), 0);
                var point = layout.World(GoldMinerRules.OriginX(mode, player) + Math.Sin(hook.AngleRadians) * hook.Length,
                    Math.Cos(hook.AngleRadians) * hook.Length);
                var rope = ropes[player].rectTransform;
                rope.anchoredPosition = new Vector2(origin.x, -origin.y);
                rope.sizeDelta = new Vector2(4.4f, (float)hook.Length * layout.Scale);
                ropes[player].SetTravel((float)hook.Length * layout.Scale);
                rope.localEulerAngles = new Vector3(0, 0, (float)(hook.AngleRadians * 180 / Math.PI));
                GoldMinerEntity held = null;
                if (hook.HeldEntityId.HasValue)
                    foreach (var entity in state.Entities)
                        if (entity.Id == hook.HeldEntityId.Value && entity.Status == GoldMinerEntityStatus.Held)
                        { held = entity; break; }
                var image = hooks[player];
                var rect = image.rectTransform;
                float hookHeight = Mathf.Clamp(layout.Scale * .60f, 36, 64);
                if (held == null)
                {
                    image.sprite = art.Get("characters/hook-open-closed", "open");
                    rect.pivot = GoldMinerCaughtArt.OpenRing;
                    rect.sizeDelta = new Vector2(hookHeight * image.sprite.rect.width / image.sprite.rect.height, hookHeight);
                }
                else
                {
                    var caught = GoldMinerCaughtArt.For(held.Kind);
                    image.sprite = art.Get(caught.Resource);
                    rect.pivot = caught.Ring;
                    // All nine complete sprites have the exact same displayed eye diameter
                    // as the empty hook. Import resolution and transparent margins do not matter.
                    float width = hookHeight * GoldMinerCaughtArt.OpenRingDiameterPerHeight / caught.RingDiameter;
                    rect.sizeDelta = new Vector2(width, width * image.sprite.rect.height / image.sprite.rect.width);
                }
                rect.anchoredPosition = new Vector2(point.x, -point.y);
                rect.localEulerAngles = rope.localEulerAngles;
                celebrations[player] = Mathf.Max(0, celebrations[player] - delta);
                double retractionSpeed = held == null ? GoldMinerRules.EmptyRetractSpeed
                    : GoldMinerRules.LoadedRetractSpeed / Math.Max(GoldMinerRules.Weight(held.Kind), .5)
                        * (state.ActiveBuffs.Strength ? 1.5 : 1);
                winches[player].Render(hook, state.Phase, celebrations[player] > 0, delta, retractionSpeed);
            }
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                effect.Age += delta;
                if (effect.Age >= effect.Duration)
                {
                    UnityEngine.Object.Destroy(effect.Rect.gameObject);
                    effects.RemoveAt(i);
                    continue;
                }
                float progress = effect.Age / effect.Duration;
                var color = effect.Graphic.color;
                color.a = 1 - progress;
                effect.Graphic.color = color;
                if (effect.Floating) effect.Rect.anchoredPosition = effect.Start + Vector2.up * 32 * progress;
                else effect.Rect.localScale = Vector3.one * Mathf.Lerp(.6f, 1.2f, progress);
            }
        }

        public void Collected(int player, long amount)
        {
            celebrations[player] = .45f;
            var origin = layout.World(GoldMinerRules.OriginX(mode, player), 0);
            var label = UiFactory.Label(root, "+" + amount, origin.x - 70, origin.y + 24, 140, 52, 32,
                UiFactory.Hex("FFF4BA"), TextAnchor.MiddleCenter, FontStyle.Bold);
            effects.Add(new Effect { Rect = label.rectTransform, Graphic = label, Duration = .45f,
                Start = label.rectTransform.anchoredPosition, Floating = true });
            AddEffect("fx_collect_sparkle", origin, .24f);
        }

        public void Explode(GoldMinerRunSnapshot beforeCommand, int player)
        {
            // Input synchronization may catch a new load before the next Render.
            // Derive the burst from the committed command's pre-state, never a cached visual.
            var hook = beforeCommand.Current.Hooks[player];
            var point = layout.World(GoldMinerRules.OriginX(mode, player) + Math.Sin(hook.AngleRadians) * hook.Length,
                Math.Cos(hook.AngleRadians) * hook.Length);
            if (hook.HeldEntityId.HasValue)
                foreach (var entity in beforeCommand.Current.Entities)
                {
                    if (entity.Id != hook.HeldEntityId.Value) continue;
                    var caught = GoldMinerCaughtArt.For(entity.Kind);
                    float width = Mathf.Clamp(layout.Scale * .60f, 36, 64) * GoldMinerCaughtArt.OpenRingDiameterPerHeight / caught.RingDiameter;
                    Vector2 offset = (caught.LoadCenter - caught.Ring) * width;
                    offset = Quaternion.Euler(0, 0, (float)(hook.AngleRadians * 180 / Math.PI)) * offset;
                    point += new Vector2(offset.x, -offset.y);
                    break;
                }
            AddEffect("fx_explosion", point, .28f);
        }

        public void Launched(int player)
        {
            celebrations[player] = 0;
            AddEffect("fx_dust_puff", layout.World(GoldMinerRules.OriginX(mode, player), 0), .22f);
        }

        private void AddEffect(string key, Vector2 point, float duration)
        {
            var image = Picture(root, key, art.Get("effects/" + key), point.x, point.y, 90, 90);
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            effects.Add(new Effect { Rect = image.rectTransform, Graphic = image, Duration = duration });
        }

        private Sprite SpriteFor(GoldMinerEntity entity, float elapsed)
        {
            switch (entity.Kind)
            {
                case GoldMinerKind.GoldSmall: return art.Get("objects/ore_gold_small");
                case GoldMinerKind.GoldMedium: return art.Get("objects/ore_gold_medium");
                case GoldMinerKind.GoldLarge: return art.Get("objects/ore_gold_large");
                case GoldMinerKind.RockSmall: return art.Get("objects/ore_rock_small");
                case GoldMinerKind.RockLarge: return art.Get("objects/ore_rock_large");
                case GoldMinerKind.Diamond: return art.Get("objects/ore_diamond");
                case GoldMinerKind.MysteryBag: return art.Get("objects/ore_mystery_bag");
                case GoldMinerKind.DiamondMole: return art.Get("characters/mole-carrying-diamond-run");
                default: return art.Get("characters/mole-run-4frames", "run_0" + (1 + (int)(elapsed * 8) % 4));
            }
        }

        private static string ObjectPath(GoldMinerKind kind)
        {
            switch (kind)
            {
                case GoldMinerKind.GoldSmall: return "objects/ore_gold_small";
                case GoldMinerKind.GoldMedium: return "objects/ore_gold_medium";
                case GoldMinerKind.GoldLarge: return "objects/ore_gold_large";
                case GoldMinerKind.RockSmall: return "objects/ore_rock_small";
                case GoldMinerKind.RockLarge: return "objects/ore_rock_large";
                case GoldMinerKind.Diamond: return "objects/ore_diamond";
                default: return "objects/ore_mystery_bag";
            }
        }

        internal static Image Picture(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, bool preserve = true)
        {
            var image = UiFactory.Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserve;
            image.raycastTarget = false;
            return image;
        }
    }
}
