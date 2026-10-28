using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.HappyBall;
using XanaduYonder.Runtime;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Diagnostic only: isolate the two production renderers and compare identical
    // shadow math with/without depth testing. Does not infer an Android result from Metal.
    [InitializeOnLoad]
    public static class HappyBallShadowChecks
    {
        private const string Pending = "Xanadu.HappyBallShadowChecks.Pending";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly StringBuilder Report = new StringBuilder();
        private static readonly StringBuilder Csv = new StringBuilder();
        private static string output;

        static HappyBallShadowChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            new GameObject("HappyBall shadow diagnostic runner").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private static IEnumerator Execute()
        {
            yield return null;
            int exit = 0;
            output = Path.GetFullPath("artifacts/validation/happy-ball-shadow");
            Directory.CreateDirectory(output); Report.Clear(); Csv.Clear();
            Csv.AppendLine("sequence,frame,stage_y,ball_y,production_mass,always_mass,retention,production_cx,production_cy,always_cx,always_cy");
            var oldFont = UiFactory.Font;
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Report.AppendLine("GPU " + SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName);
                Report.AppendLine("API " + SystemInfo.graphicsDeviceVersion + " colorSpace=" + QualitySettings.activeColorSpace);
                using (var fixture = new Fixture())
                {
                    fixture.Scan("fixed", 120, i => -4000f, i => 1.2f, false);
                    fixture.Scan("micro-translation", 128, i => -4000f + i / 256f, i => 1.2f, true);
                    fixture.Scan("wide-translation", 65, i => -64f * i, i => 1.2f, true);
                    fixture.Scan("height-origin", 64, i => 0, i => .3f + 2.1f * i / 63, true);
                    fixture.Scan("height-far", 64, i => -4000, i => .3f + 2.1f * i / 63, true);
                }
                Report.AppendLine("HAPPY_BALL_SHADOW_DIAGNOSTIC_COMPLETE (this is not a device-pass assertion)");
            }
            catch (Exception error) { exit = 1; Report.AppendLine("DIAGNOSTIC_ERROR " + error); Debug.LogException(error); }
            finally
            {
                UiFactory.Font = oldFont;
                File.WriteAllText(Path.Combine(output, "report.txt"), Report.ToString());
                File.WriteAllText(Path.Combine(output, "metrics.csv"), Csv.ToString());
                Debug.Log(Report.ToString());
                EditorApplication.Exit(exit);
            }
        }
        private static object Get(object value, string name) { return value.GetType().GetField(name, Members).GetValue(value); }
        private static object Invoke(object value, string method, params object[] arguments)
        { return value.GetType().GetMethod(method, Members).Invoke(value, arguments); }
        private static string Number(double value) { return value.ToString("G12", Invariant); }
        private struct Measure { public double Mass, X, Y; }

        private sealed class Fixture : IDisposable
        {
            private const int RoiWidth = 128, RoiHeight = 256;
            private readonly GameObject root;
            private readonly Component scene;
            private readonly Transform stage, pillar, shadow;
            private readonly MeshRenderer shadowRenderer;
            private readonly Camera camera;
            private readonly RenderTexture texture, originalTarget;
            private readonly Texture2D readback, preview;
            private readonly Material production, always;
            private readonly Shader alwaysShader;
            private readonly bool integrated;
            private readonly MaterialPropertyBlock parameters = new MaterialPropertyBlock();
            private readonly HappyBallSnapshot snapshot;
            private readonly Vector3 cameraPosition, pillarPosition;
            private readonly Rect fixedRoi;

            public Fixture()
            {
                root = new GameObject("HappyBall shadow diagnostic", typeof(RectTransform), typeof(Canvas));
                var image = new GameObject("Shadow fixture image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(root.transform, false); image.rectTransform.sizeDelta = new Vector2(720, 1140);
                var type = typeof(AppController).Assembly.GetType("XanaduYonder.Runtime.HappyBall.HappyBallSceneView");
                scene = root.AddComponent(type); Invoke(scene, "Initialize", image);
                snapshot = new HappyBallGame(719).GetSnapshot(); snapshot.BallY = 1.2; snapshot.BallVelocity = 0;
                Invoke(scene, "Reset", snapshot); Invoke(scene, "SetVisible", true);
                stage = (Transform)type.GetProperty("SceneRoot").GetValue(scene);
                camera = (Camera)type.GetProperty("SceneCamera").GetValue(scene);
                pillar = (Transform)Get(scene, "pillar");
                integrated = pillar.GetComponent<MeshRenderer>().sharedMaterial.HasProperty("_BallShadow");
                shadow = integrated ? pillar : (Transform)Get(scene, "columnBallShadow");
                shadowRenderer = shadow.GetComponent<MeshRenderer>();
                foreach (var renderer in stage.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.enabled = renderer.transform == pillar || renderer.transform == shadow;
                originalTarget = camera.targetTexture;
                texture = new RenderTexture(256, 405, 24, RenderTextureFormat.ARGB32)
                { name = "HappyBall shadow diagnostic RT", antiAliasing = 2 };
                texture.Create(); camera.targetTexture = texture; camera.aspect = 256f / 405; camera.enabled = false;
                cameraPosition = camera.transform.localPosition; pillarPosition = pillar.localPosition;
                readback = new Texture2D(RoiWidth, RoiHeight, TextureFormat.RGBAFloat, false, true);
                preview = new Texture2D(RoiWidth, RoiHeight, TextureFormat.RGBA32, false, true);
                production = shadowRenderer.sharedMaterial;
                var source = integrated
                    ? File.ReadAllText("Assets/Resources/HappyBall/HappyBallColumn.shader")
                        .Replace("XanaduYonder/HappyBall Column", "Hidden/XanaduYonder/HappyBall Column Always Diagnostic")
                        .Replace("ZWrite On", "ZWrite On\n        ZTest Always")
                    : File.ReadAllText("Assets/Resources/HappyBall/HappyBallSoft.shader")
                        .Replace("XanaduYonder/HappyBall Soft", "Hidden/XanaduYonder/HappyBall Shadow Always Diagnostic")
                        .Replace("ZWrite Off", "ZWrite Off\n        ZTest Always");
                alwaysShader = ShaderUtil.CreateShaderAsset(source, true);
                if (ShaderUtil.ShaderHasError(alwaysShader)) throw new InvalidOperationException("Diagnostic shader did not compile.");
                always = new Material(production) { shader = alwaysShader, name = "Diagnostic depth bypass only" };
                stage.position = Vector3.zero; fixedRoi = Roi();
                Report.AppendLine("RT size=" + texture.width + "x" + texture.height + " depth=" + texture.depth +
                    " depthStencil=" + texture.depthStencilFormat + " AA=" + texture.antiAliasing + " ROI=128x256 RGBAFloat");
                Report.AppendLine("Isolated renderers=" + stage.GetComponentsInChildren<MeshRenderer>().Count(r => r.enabled));
                Report.AppendLine("Projection=" + (integrated ? "opaque-column-local-shading" : "separate-transparent-overlay"));
                if (integrated && stage.GetComponentsInChildren<Transform>(true).Any(t => t.name == "HappyBall ball projected column shadow"))
                    throw new InvalidOperationException("Integrated shadow still has a competing overlay object.");
                Report.AppendLine("Pillar scale=" + pillar.localScale + " patch scale=" + shadow.localScale + " camera=" + cameraPosition);
            }

            public void Scan(string name, int frames, Func<int, float> stageY, Func<int, float> ballY, bool capture)
            {
                var values = new List<double>(); var productionMasses = new List<double>();
                var directory = Path.Combine(output, name); if (capture) Directory.CreateDirectory(directory);
                for (var frame = 0; frame < frames; frame++)
                {
                    snapshot.BallY = ballY(frame);
                    Invoke(scene, "Render", snapshot, null);
                    camera.transform.localPosition = cameraPosition; pillar.localPosition = pillarPosition;
                    stage.position = Vector3.up * stageY(frame);
                    var roi = name.StartsWith("height", StringComparison.Ordinal) ? Roi() : fixedRoi;
                    shadowRenderer.GetPropertyBlock(parameters);
                    var shadowParameters = parameters.GetVector("_BallShadow");
                    Mode(0, shadowParameters);
                    var baseline = Capture(roi, null);
                    Mode(1, shadowParameters);
                    var normal = Capture(roi, capture ? Path.Combine(directory, frame.ToString("D3") + "-depth.png") : null);
                    Mode(2, shadowParameters);
                    var bypass = Capture(roi, capture ? Path.Combine(directory, frame.ToString("D3") + "-always.png") : null);
                    var a = Metric(baseline, normal); var b = Metric(baseline, bypass);
                    var retention = b.Mass > .000001 ? a.Mass / b.Mass : double.NaN;
                    values.Add(retention); productionMasses.Add(a.Mass);
                    Csv.AppendLine(name + "," + frame + "," + Number(stageY(frame)) + "," + Number(ballY(frame)) + "," +
                        Number(a.Mass) + "," + Number(b.Mass) + "," + Number(retention) + "," + Number(a.X) + "," + Number(a.Y) + "," + Number(b.X) + "," + Number(b.Y));
                }
                shadowRenderer.sharedMaterial = production;
                var finite = values.Where(v => !double.IsNaN(v)).ToArray();
                if (finite.Length != frames) throw new InvalidOperationException(name + ": shadow reference has no visible mass.");
                var loss = finite.Min() < .98;
                Report.AppendLine(name + " frames=" + frames + " retention[min,max]=" + Number(finite.Min()) + "," + Number(finite.Max()) +
                    " mass[min,max]=" + Number(productionMasses.Min()) + "," + Number(productionMasses.Max()) +
                    (loss ? " DEPTH_LOSS_OBSERVED" : " NO_SIGNIFICANT_DEPTH_LOSS_ON_THIS_GPU"));
            }

            private Rect Roi()
            {
                Vector3 center;
                if (integrated)
                {
                    shadowRenderer.GetPropertyBlock(parameters);
                    var value = parameters.GetVector("_BallShadow");
                    center = pillar.TransformPoint(new Vector3(Mathf.Sin(value.z) * .5f, value.x, -Mathf.Cos(value.z) * .5f));
                }
                else center = shadow.TransformPoint(shadow.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                var point = camera.WorldToScreenPoint(center);
                return new Rect(Mathf.Clamp(Mathf.RoundToInt(point.x) - RoiWidth / 2, 0, texture.width - RoiWidth),
                    Mathf.Clamp(Mathf.RoundToInt(point.y) - RoiHeight / 2, 0, texture.height - RoiHeight), RoiWidth, RoiHeight);
            }
            private void Mode(int mode, Vector4 value)
            {
                shadowRenderer.sharedMaterial = mode == 2 ? always : production;
                shadowRenderer.enabled = integrated || mode != 0;
                if (!integrated) return;
                if (mode == 0) value.w = 0;
                parameters.SetVector("_BallShadow", value); shadowRenderer.SetPropertyBlock(parameters);
            }
            private Color[] Capture(Rect roi, string path)
            {
                camera.Render(); var old = RenderTexture.active;
                RenderTexture.active = texture; readback.ReadPixels(roi, 0, 0, false); readback.Apply(false, false); RenderTexture.active = old;
                var pixels = readback.GetPixels();
                if (path != null)
                {
                    preview.SetPixels(pixels); preview.Apply(false, false); File.WriteAllBytes(path, preview.EncodeToPNG());
                }
                return pixels;
            }
            private static Measure Metric(Color[] baseline, Color[] shadowed)
            {
                double mass = 0, x = 0, y = 0;
                for (var i = 0; i < baseline.Length; i++)
                {
                    // Paired no-shadow baseline removes absolute-world lighting changes.
                    // Float ratios retain small coverage changes rather than thresholding 8-bit pixels.
                    var light = baseline[i].r * .2126 + baseline[i].g * .7152 + baseline[i].b * .0722;
                    var dark = shadowed[i].r * .2126 + shadowed[i].g * .7152 + shadowed[i].b * .0722;
                    var amount = Math.Max(0, light - dark) / Math.Max(.05, light);
                    mass += amount; x += amount * (i % RoiWidth); y += amount * (i / RoiWidth);
                }
                return new Measure { Mass = mass, X = mass > 0 ? x / mass : 0, Y = mass > 0 ? y / mass : 0 };
            }
            public void Dispose()
            {
                shadowRenderer.sharedMaterial = production; camera.targetTexture = originalTarget;
                texture.Release(); Object.Destroy(texture); Object.Destroy(readback); Object.Destroy(preview); Object.Destroy(always); Object.Destroy(alwaysShader);
                Object.Destroy(root);
            }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
