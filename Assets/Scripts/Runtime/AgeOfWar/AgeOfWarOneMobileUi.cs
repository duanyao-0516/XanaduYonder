using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>Offline touch controls over the original 650 by 450 scene coordinates.</summary>
    internal static class AgeOfWarOneMobileUi
    {
        private const string FontKey = "font/one/962";

        internal static void Apply(AgeOfWarSceneView scene, AgeOfWarAssets assets)
        {
            if (scene == null || assets == null || assets.Edition != 1) return;
            for (int i = 0; i < scene.Definition.layers.Length; ++i)
                if (IsPromotionLayer(scene.Definition.layers[i])) scene.SuppressLayer(i);
            foreach (var hit in scene.Definition.hotspots ?? new AgeOfWarHotspot[0])
                if (IsExternalHotspot(hit) || hit.id == 1021) scene.SuppressHotspot(hit.id);

            switch (scene.Definition.key)
            {
                case "menu": Menu(scene); break;
                case "paused": Pause(scene); break;
                case "victory":
                case "defeat": EndScreen(scene); break;
            }
        }

        // Character IDs survive the manifest's color-transform instance variants.
        internal static bool IsPromotionLayer(AgeOfWarSceneLayer layer)
        {
            int id = CharacterId(layer);
            return id == 990 || id == 992 || id == 1021 || id == 1022 || id == 1023;
        }

        internal static bool IsExternalHotspot(AgeOfWarHotspot hit)
        {
            return hit != null && (hit.id == 992 || hit.id == 1022 || hit.id == 1023);
        }

        private static int CharacterId(AgeOfWarSceneLayer layer)
        {
            if (layer == null || string.IsNullOrEmpty(layer.key)) return -1;
            int id;
            return int.TryParse(layer.key.Substring(layer.key.LastIndexOf('/') + 1), out id) ? id : -1;
        }

        private static void Menu(AgeOfWarSceneView scene)
        {
            for (int i = 0; i < scene.Definition.layers.Length; ++i)
            {
                int id = CharacterId(scene.Definition.layers[i]);
                // Sprite1017 combines the animated title and all DefineText1012 menu rows.
                // Keep the title artwork; the replacement controls own the lower region.
                if (id == 1017) KeepUpperArtwork(scene, i, 180);
                if (id >= 1019 && id <= 1023) scene.SuppressLayer(i);
            }
            for (int id = 1019; id <= 1023; ++id) scene.SuppressHotspot(id);
            AddMenuButton(scene, 1019, "mobile-play", "Play", 197.25f);
            AddMenuButton(scene, 1020, "mobile-instructions", "Instructions", 234.25f);
            AddMenuButton(scene, 1021, "mobile-exit", "Exit", 272.25f);
        }

        private static void AddMenuButton(AgeOfWarSceneView scene, int originalId, string action, string label, float fallbackY)
        {
            var rect = new Rect(0, fallbackY, 650, 39.5f);
            foreach (var hit in scene.Definition.hotspots ?? new AgeOfWarHotspot[0])
                if (hit.id == originalId) { rect = new Rect(hit.x, hit.y, hit.w, hit.h); break; }
            // DefineText1012: embedded Bookman Old Style bold, textHeight=600 twips.
            scene.AddTextButton(action, label, FontKey, 30, rect, Color.white, true);
        }

        private static void Pause(AgeOfWarSceneView scene)
        {
            float textTop = -24.4f;
            for (int i = 0; i < scene.Definition.layers.Length; ++i)
            {
                int id = CharacterId(scene.Definition.layers[i]);
                if (id != 963 && id != 964) continue;
                if (id == 964) textTop = scene.Definition.layers[i].y;
                scene.SuppressLayer(i);
            }
            // DefineText963/964 use font962 at 400 twips; the scene root is the stage centre.
            scene.AddTextButton("", "PAUSED", FontKey, 20,
                new Rect(-260, textTop - 42, 520, 36), Color.white, true);
            scene.AddTextButton("mobile-resume", "Press Screen to resume", FontKey, 20,
                new Rect(-260, textTop, 520, 36), Color.white, true);
            scene.AddTextButton("mobile-exit", "Exit", FontKey, 20,
                new Rect(-110, textTop + 42, 220, 36), Color.white, true);
        }

        private static void EndScreen(AgeOfWarSceneView scene)
        {
            for (int i = 0; i < scene.Definition.layers.Length; ++i)
            {
                int id = CharacterId(scene.Definition.layers[i]);
                // The last record of text1036..1039 is the promotion at source baseline184.
                // The preceding Play again record ends at stage324.6; preserve it and the title.
                if (id >= 1036 && id <= 1039) KeepUpperArtwork(scene, i, 329);
            }
        }

        private static void KeepUpperArtwork(AgeOfWarSceneView scene, int index, float height)
        {
            var image = scene.Images[index].Root;
            int sibling = image.GetSiblingIndex();
            var mask = AgeOfWarDrawing.Rect(scene.Root, "Offline original artwork", 0, 0, 650, height);
            mask.SetSiblingIndex(sibling);
            mask.gameObject.AddComponent<RectMask2D>();
            image.SetParent(mask, false);
        }
    }
}
