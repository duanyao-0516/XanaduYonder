using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Radii in the 720-unit host canvas. Smaller game layouts pass their local scale.
    internal static class DialogCorners
    {
        public const float Sheet = 40;
        public const float Surface = 32;
        public const float Row = 16;
        public const float Action = 16;
        private const int SourceRadius = 48;
        private static Sprite rounded, topRounded;

        public static float Pill(float height) => height * .5f;

        public static Image Panel(Transform parent, string name, float x, float y, float width,
            float height, Color color, float localRadius)
        {
            var image = UiFactory.Panel(parent, name, x, y, width, height, color, false);
            Apply(image, localRadius);
            return image;
        }

        public static void Apply(Image image, float localRadius)
        {
            if (rounded == null) rounded = UiFactory.CreateRoundedSprite(SourceRadius, false);
            ApplySprite(image, rounded, localRadius);
        }

        // A sheet meets the physical bottom edge, including the area behind the system gesture bar.
        public static void ApplyTop(Image image, float localRadius)
        {
            if (topRounded == null) topRounded = UiFactory.CreateRoundedSprite(SourceRadius, true);
            ApplySprite(image, topRounded, localRadius);
        }

        private static void ApplySprite(Image image, Sprite sprite, float localRadius)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = SourceRadius / (image.pixelsPerUnit * localRadius);
        }
    }
}
