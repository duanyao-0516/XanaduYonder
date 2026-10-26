using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace XanaduYonder.EditorTools
{
    // Applied for every build so local Player Settings cannot silently restore Unity branding.
    internal static class AppBranding
    {
        private const string LogoPath = "Assets/Branding/XY_logo.png";
        private const string BackgroundPath = "Assets/Branding/IconBackground.asset";

        public static void Configure()
        {
            var importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("The app logo is missing: " + LogoPath);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled || importer.maxTextureSize != 1024 || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo == null || logo.width != logo.height)
                throw new InvalidOperationException("The app logo must import as a square texture.");

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { logo }, IconKind.Any);
#if UNITY_ANDROID
            var background = GetIconBackground();
            ConfigureIcons(AndroidPlatformIconKind.Adaptive, background, logo);
            // These fallbacks are retained by the pinned Editor for launcher compatibility.
#pragma warning disable CS0618
            ConfigureIcons(AndroidPlatformIconKind.Legacy, logo);
            ConfigureIcons(AndroidPlatformIconKind.Round, logo);
#pragma warning restore CS0618
#endif
            // BrandSplash renders the complete original image at an explicit size at runtime.
            // Disable Unity's native splash so its automatic logo sizing cannot enlarge it again.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.white;
            PlayerSettings.SplashScreen.overlayOpacity = 0;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.backgroundPortrait = null;
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.logos = Array.Empty<PlayerSettings.SplashScreenLogo>();

            var configured = PlayerSettings.SplashScreen.logos;
            if (PlayerSettings.SplashScreen.show || PlayerSettings.SplashScreen.showUnityLogo || configured.Length != 0 ||
                PlayerSettings.SplashScreen.overlayOpacity != 0 || PlayerSettings.SplashScreen.backgroundColor != Color.white)
                throw new InvalidOperationException("The native splash screen was not disabled for the runtime brand splash.");
            var defaults = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (defaults.Length == 0 || defaults[0] != logo)
                throw new InvalidOperationException("The default application icon did not apply.");
            Debug.Log("XanaduYonder branding verified: original XY_logo app icons; native splash disabled for BrandSplash.");
        }

#if UNITY_ANDROID
        private static Texture2D GetIconBackground()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (texture == null)
            {
                // A plain platform-icon background; the supplied logo image itself stays unchanged.
                texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "Icon white background", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply(false, false);
                AssetDatabase.CreateAsset(texture, BackgroundPath);
            }
            if (texture.width != 1 || texture.height != 1 || texture.GetPixel(0, 0) != Color.white)
                throw new InvalidOperationException("The adaptive icon background must be a solid white pixel.");
            return texture;
        }

        private static void ConfigureIcons(PlatformIconKind kind, params Texture2D[] textures)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (icons.Length == 0) throw new InvalidOperationException("No Android icon slots available for " + kind);
            foreach (var icon in icons)
            {
                if (icon.maxLayerCount != textures.Length)
                    throw new InvalidOperationException("Unexpected Android icon layer count for " + kind);
                // Android adaptive icons are background layer 0, foreground layer 1.
                icon.SetTextures(textures);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            foreach (var icon in PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind))
            {
                if (icon.layerCount != textures.Length) throw new InvalidOperationException("Android icon layers were not saved.");
                for (var layer = 0; layer < textures.Length; layer++)
                    if (icon.GetTexture(layer) != textures[layer]) throw new InvalidOperationException("Android icon texture was not saved.");
            }
        }
#endif
    }

#if UNITY_ANDROID
    // Android 12+ shows its own starting window before Unity can render the runtime splash.
    // Unity's stock theme points that window at a transparent color; use the actual launcher icon.
    public sealed class AppLaunchTheme : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var resources = Path.Combine(path, "src/main/res");
            var launcher = Path.Combine(Path.GetDirectoryName(path), "launcher");
            if (!Directory.Exists(launcher))
                throw new DirectoryNotFoundException("The generated Android launcher module is missing: " + launcher);
            PreserveAdaptiveIconMargins(launcher);
            var foundAndroid12 = false;
            foreach (var relative in new[] { "values/styles.xml", "values-v21/styles.xml", "values-v31/styles.xml" })
            {
                var file = Path.Combine(resources, relative);
                if (!File.Exists(file)) continue;
                var document = XDocument.Load(file);
                var styles = document.Root.Elements("style").Where(style =>
                    (string)style.Attribute("name") == "BaseUnityTheme" ||
                    (string)style.Attribute("name") == "BaseUnityGameActivityTheme" ||
                    // The Activity uses this child theme, whose explicit background overrides its base.
                    (string)style.Attribute("name") == "UnityThemeSelector").ToArray();
                foreach (var style in styles)
                {
                    SetItem(style, "android:windowBackground", "@android:color/white");
                }
                document.Save(file);
                if (relative != "values-v31/styles.xml" || styles.Length == 0) continue;
                // app_icon belongs to the launcher module. Overlay these styles there rather than
                // making unityLibrary depend on a resource provided only by its consuming app.
                var overlay = new XDocument(new XElement("resources", styles.Select(style => new XElement(style))));
                foreach (var style in overlay.Root.Elements("style"))
                {
                    SetItem(style, "android:windowSplashScreenAnimatedIcon", "@mipmap/app_icon");
                    SetItem(style, "android:windowSplashScreenBackground", "@android:color/white");
                    SetItem(style, "android:windowSplashScreenIconBackgroundColor", "@android:color/white");
                    SetItem(style, "android:windowSplashScreenAnimationDuration", "0");
                }
                var destination = Path.Combine(launcher, "src/main/res/values-v31/xanadu_launch_theme.xml");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                overlay.Save(destination);
                foundAndroid12 = true;
            }
            if (!foundAndroid12)
                throw new InvalidOperationException("The generated Android 12 launch theme was not found; app branding is incomplete.");
            Debug.Log("XanaduYonder Android launch theme verified: app_icon on white before the custom splash.");
        }

        private static void PreserveAdaptiveIconMargins(string launcher)
        {
            XNamespace android = "http://schemas.android.com/apk/res/android";
            const string image = "@mipmap/ic_launcher_foreground";
            const string insetFraction = "16.666667%";
            foreach (var name in new[] { "app_icon.xml", "app_icon_round.xml" })
            {
                var file = Path.Combine(launcher, "src/main/res/mipmap-anydpi-v26", name);
                if (!File.Exists(file)) throw new FileNotFoundException("The generated adaptive icon is missing: " + file);
                var document = XDocument.Load(file);
                var foreground = document.Root?.Element("foreground");
                var directImage = (string)foreground?.Attribute(android + "drawable");
                var insetImage = (string)foreground?.Element("inset")?.Attribute(android + "drawable");
                if (document.Root?.Name != "adaptive-icon" || foreground == null ||
                    (directImage != image && insetImage != image))
                    throw new InvalidOperationException("Unexpected adaptive icon foreground in " + file);

                // Android displays the central 72 dp of each 108 dp layer. Insetting the whole
                // unchanged image by 1/6 per edge compensates for that 1.5x magnification.
                // Percentages also preserve the same logo/white-margin ratio at any display size.
                foreground.Attribute(android + "drawable")?.Remove();
                foreground.ReplaceNodes(new XElement("inset", new XAttribute(android + "drawable", image),
                    new XAttribute(android + "insetLeft", insetFraction),
                    new XAttribute(android + "insetTop", insetFraction),
                    new XAttribute(android + "insetRight", insetFraction),
                    new XAttribute(android + "insetBottom", insetFraction)));
                document.Save(file);
            }
            Debug.Log("XanaduYonder adaptive icons verified: original image margins preserved with a 1/6 inset per edge.");
        }

        private static void SetItem(XElement style, string name, string value)
        {
            var item = style.Elements("item").FirstOrDefault(element => (string)element.Attribute("name") == name);
            if (item == null) { item = new XElement("item", new XAttribute("name", name)); style.Add(item); }
            item.Value = value;
        }
    }
#endif
}
