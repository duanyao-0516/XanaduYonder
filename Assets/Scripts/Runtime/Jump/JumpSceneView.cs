using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Jump;

namespace XanaduYonder.Runtime.Jump
{
    /// <summary>A self-contained 3D stage. Game rules and committed progress belong to the core model.</summary>
    internal sealed class JumpSceneView : MonoBehaviour
    {
        private const int SceneLayer = 31;
        private const float PlatformTop = 1f;
        private const float PawnHeight = 1.35f;
        private const float FallenFootHeight = -.38f;
        private static readonly Vector3 ViewDirection = new Vector3(-8, 10, -8).normalized;
        private static readonly Quaternion ViewRotation = Quaternion.LookRotation(-ViewDirection, Vector3.up);
        private static readonly Color Background = UiFactory.Hex("E7EAE7");
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Transform[] particles = new Transform[12];
        private readonly Material[] platformColors = new Material[6];
        private readonly Material[] topColors = new Material[6];
        private RawImage target;
        private GameObject stage;
        private Camera sceneCamera;
        private RenderTexture texture;
        private Mesh boxMesh, cylinderMesh, taperedMesh, sphereMesh, planeMesh;
        private Material dark, cream, shadowMaterial, pawnShadowMaterial, particleMaterial;
        private Platform current, next, previous;
        private Transform pawn, pawnPose, pawnShadow;
        private JumpSnapshot rendered;
        private Vector3 cameraFocus;
        private float cameraSize;
        private int animationVersion;
        private bool visible;
        private bool animating;
        private float chargeAmount;
        private float landingCompression;

        private sealed class Platform
        {
            public Transform Root;
            public Transform Shadow;
            public MeshRenderer Body;
            public MeshRenderer Top;
            public Vector3 Position;
            public float HalfSize;
            public int Step;
        }

        private struct Framing
        {
            public Vector3 Focus;
            public float Size;
        }

        public void Initialize(RawImage image)
        {
            if (stage != null) throw new InvalidOperationException("Jump scene is already initialized.");
            target = image != null ? image : throw new ArgumentNullException(nameof(image));
            var surfaceShader = Resources.Load<Shader>("Jump/JumpSurface");
            var shadowShader = Resources.Load<Shader>("Jump/JumpShadow");
            if (surfaceShader == null || shadowShader == null)
                throw new InvalidOperationException("Bundled jump scene shaders are missing.");
            // This root is intentionally outside the UI hierarchy: Canvas scale must not affect world geometry.
            stage = new GameObject("Jump 3D stage") { layer = SceneLayer };
            stage.transform.position = new Vector3(0, -1000, 0);
            boxMesh = Own(JumpSceneGeometry.Box());
            cylinderMesh = Own(JumpSceneGeometry.Cylinder());
            taperedMesh = Own(JumpSceneGeometry.Cylinder(.3f));
            sphereMesh = Own(JumpSceneGeometry.Sphere());
            planeMesh = Own(JumpSceneGeometry.Plane());
            dark = Material(surfaceShader, UiFactory.Hex("373143"));
            cream = Material(surfaceShader, UiFactory.Hex("FCF7E9"));
            particleMaterial = Material(surfaceShader, UiFactory.Hex("EAC46F"));
            shadowMaterial = Material(shadowShader, new Color(.16f, .22f, .22f, .19f));
            shadowMaterial.SetFloat("_Round", 0);
            pawnShadowMaterial = Material(shadowShader, new Color(.13f, .15f, .19f, .27f));
            pawnShadowMaterial.SetFloat("_Round", 1);
            var palette = new[] { "D8B774", "96BE91", "A6B8CC", "B6A1C5", "D59979", "8CBDB4" };
            for (var i = 0; i < palette.Length; i++)
            {
                var color = UiFactory.Hex(palette[i]);
                platformColors[i] = Material(surfaceShader, color);
                topColors[i] = Material(surfaceShader, Color.Lerp(color, Color.white, .19f));
            }
            // The floor is deliberately untextured, like the airy original game's studio backdrop.
            var floor = Piece("Floor", stage.transform, planeMesh, Material(surfaceShader, Background / .949f), Vector3.zero, new Vector3(200, 1, 200));
            floor.localPosition = new Vector3(0, -.025f, 0);
            current = CreatePlatform("Current platform");
            next = CreatePlatform("Next platform");
            previous = CreatePlatform("Previous platform");
            SetPlatformActive(previous, false);
            CreatePawn();
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i] = Piece("Jump spark " + i, stage.transform, boxMesh, particleMaterial, Vector3.zero, Vector3.one * .07f);
                particles[i].gameObject.SetActive(false);
            }
            var cameraObject = new GameObject("Jump orthographic camera") { layer = SceneLayer };
            cameraObject.transform.SetParent(stage.transform, false);
            sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.orthographic = true;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = Background;
            sceneCamera.cullingMask = 1 << SceneLayer;
            sceneCamera.nearClipPlane = .1f;
            sceneCamera.farClipPlane = 80;
            sceneCamera.allowHDR = false;
            sceneCamera.allowMSAA = true;
            sceneCamera.useOcclusionCulling = false;
            sceneCamera.enabled = false;
            EnsureTexture();
            SetVisible(false);
        }

        public void Render(JumpSnapshot snapshot)
        {
            if (stage == null || snapshot == null) return;
            animationVersion++;
            animating = false;
            chargeAmount = 0;
            landingCompression = 0;
            HideParticles();
            // A restore/restart can arrive with a different origin. Discard only visual history.
            if (rendered == null || rendered.Steps != snapshot.Steps || snapshot.Steps == 0)
                SetPlatformActive(previous, false);
            Configure(current, Vector3.zero, (float)snapshot.CurrentHalfSize, snapshot.Steps);
            Configure(next, Point(snapshot.NextX, snapshot.NextZ), (float)snapshot.NextHalfSize, NextStep(snapshot.Steps));
            rendered = snapshot;
            SetPawn(Point(snapshot.PlayerX, snapshot.PlayerZ), PlatformTop, Quaternion.identity, Vector3.one);
            if (snapshot.IsGameOver)
                SetPawn(Point(snapshot.PlayerX, snapshot.PlayerZ), FallenFootHeight, Quaternion.AngleAxis(88, Vector3.forward), Vector3.one);
            ApplyFrame(Frame(snapshot, Vector3.zero));
        }

        public void SetCharge(float normalized)
        {
            chargeAmount = Mathf.Clamp01(normalized);
            if (rendered == null || animating || rendered.IsGameOver) return;
            ApplyRestingPose();
        }

        private void ApplyRestingPose()
        {
            var charge = chargeAmount;
            var squash = 1 - charge * .09f - landingCompression * .12f;
            ScalePlatform(current, squash);
            SetPawn(Point(rendered.PlayerX, rendered.PlayerZ), PlatformTop * squash, Quaternion.identity,
                new Vector3(1 + .19f * charge + .17f * landingCompression,
                    1 - .36f * charge - .23f * landingCompression, 1 + .19f * charge + .17f * landingCompression));
        }

        public IEnumerator Animate(JumpSnapshot before, JumpResult result, JumpSnapshot after)
        {
            if (stage == null || before == null || after == null) yield break;
            var token = ++animationVersion;
            animating = true;
            chargeAmount = 0;
            landingCompression = 0;
            ScalePlatform(next, 1);
            var start = Point(result.StartX, result.StartZ);
            var end = Point(result.EndX, result.EndZ);
            var direction = end - start;
            if (direction.sqrMagnitude < .001f) direction = Point(before.NextX, before.NextZ) - start;
            direction.Normalize();
            var flipAxis = Vector3.Cross(Vector3.up, direction);
            var arc = ArcHeight(Vector3.Distance(start, end));
            var flight = Mathf.Lerp(.42f, .62f, Mathf.Clamp01(Vector3.Distance(start, end) / 8));
            var initialFrame = new Framing { Focus = cameraFocus, Size = cameraSize };
            var flightFrame = Frame(before, Vector3.zero, end, arc);
            var elapsed = 0f;
            while (elapsed < flight)
            {
                if (token != animationVersion) yield break;
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / flight);
                var position = Vector3.Lerp(start, end, t);
                var height = PlatformTop + 4 * arc * t * (1 - t);
                var launchStretch = .12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 4));
                ScalePlatform(current, Mathf.Lerp(.91f, 1, Mathf.Clamp01(t * 6)));
                SetPawn(position, height, Quaternion.AngleAxis(360 * Smooth(t), flipAxis), new Vector3(1 - launchStretch * .4f, 1 + launchStretch, 1 - launchStretch * .4f));
                FlightParticles(start, end, arc, t);
                ApplyFrame(Lerp(initialFrame, flightFrame, Smooth(Mathf.Clamp01(t * 3))));
                yield return null;
            }
            if (token != animationVersion) yield break;
            HideParticles();
            ScalePlatform(current, 1);
            SetPawn(end, PlatformTop, Quaternion.identity, Vector3.one);
            if (result.Outcome == JumpOutcome.Missed)
            {
                elapsed = 0;
                const float fallDuration = .42f;
                while (elapsed < fallDuration)
                {
                    if (token != animationVersion) yield break;
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / fallDuration);
                    SetPawn(end, Mathf.Lerp(PlatformTop, FallenFootHeight, t * t), Quaternion.AngleAxis(88 * Smooth(t), flipAxis), Vector3.one);
                    yield return null;
                }
                rendered = after;
                animating = false;
                yield break;
            }

            var landed = result.Outcome == JumpOutcome.Landed || result.Outcome == JumpOutcome.Perfect;
            if (landed)
            {
                var offset = Point(result.TargetX, result.TargetZ);
                var recycled = previous;
                previous = current;
                current = next;
                next = recycled;
                Configure(next, Point(after.NextX, after.NextZ), (float)after.NextHalfSize, NextStep(after.Steps));
                MovePlatform(previous, previous.Position - offset);
                MovePlatform(current, Vector3.zero);
                // Publish the landed coordinate system before accepting the next charge.
                ApplyFrame(new Framing { Focus = cameraFocus - offset, Size = cameraSize });
            }
            rendered = after;
            animating = false;
            ApplyRestingPose();
            // The page's flight completes here. Camera/bounce no longer hold its input gate.
            StartCoroutine(Settle(token, landed, result.Outcome == JumpOutcome.Perfect));
        }

        private IEnumerator Settle(int token, bool newPlatform, bool perfect)
        {
            var from = new Framing { Focus = cameraFocus, Size = cameraSize };
            var to = Frame(rendered, Vector3.zero);
            var elapsed = 0f;
            const float cameraDuration = .24f;
            const float bounceDuration = .14f;
            while (elapsed < cameraDuration)
            {
                if (token != animationVersion) yield break;
                elapsed += Time.unscaledDeltaTime;
                var t = Smooth(Mathf.Clamp01(elapsed / cameraDuration));
                ApplyFrame(Lerp(from, to, t));
                if (newPlatform) ScalePlatform(next, Mathf.Lerp(.03f, 1, t));
                var bounce = Mathf.Clamp01(elapsed / bounceDuration);
                landingCompression = Mathf.Sin(bounce * Mathf.PI);
                ApplyRestingPose();
                LandingParticles(Point(rendered.PlayerX, rendered.PlayerZ), bounce, perfect);
                yield return null;
            }
            if (token != animationVersion) yield break;
            landingCompression = 0;
            ApplyRestingPose();
            HideParticles();
        }

        public void Suspend(JumpSnapshot committed)
        {
            Render(committed);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (stage == null) return;
            stage.SetActive(value && isActiveAndEnabled);
            sceneCamera.enabled = value && isActiveAndEnabled;
            if (value) EnsureTexture();
        }

        private void LateUpdate()
        {
            if (!visible || stage == null) return;
            if (EnsureTexture() && rendered != null && !animating) ApplyFrame(Frame(rendered, Vector3.zero));
        }

        private bool EnsureTexture()
        {
            var rect = target.rectTransform.rect;
            var canvas = target.canvas;
            var scale = canvas != null ? canvas.scaleFactor : 1;
            var width = Mathf.Max(128, rect.width * scale);
            var height = Mathf.Max(128, rect.height * scale);
            var resolutionScale = Mathf.Min(1, 1024 / Mathf.Max(width, height));
            var pixelWidth = Mathf.Max(128, Mathf.RoundToInt(width * resolutionScale));
            var pixelHeight = Mathf.Max(128, Mathf.RoundToInt(height * resolutionScale));
            if (texture != null && texture.width == pixelWidth && texture.height == pixelHeight)
            {
                // Mobile graphics contexts can lose a render target without changing its dimensions.
                if (!texture.IsCreated()) texture.Create();
                return false;
            }
            ReleaseTexture();
            texture = new RenderTexture(pixelWidth, pixelHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "Jump scene render texture",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            target.texture = texture;
            target.color = Color.white;
            sceneCamera.targetTexture = texture;
            sceneCamera.aspect = (float)pixelWidth / pixelHeight;
            return true;
        }

        private Platform CreatePlatform(string name)
        {
            var root = Node(name, stage.transform);
            var body = Piece("Shaded sides", root, boxMesh, platformColors[0], new Vector3(0, .45f, 0), new Vector3(1, .9f, 1));
            var top = Piece("Top rim", root, boxMesh, topColors[0], new Vector3(0, .95f, 0), new Vector3(1.015f, .1f, 1.015f));
            Piece("Landing ring", root, cylinderMesh, cream, new Vector3(0, 1.006f, 0), new Vector3(.34f, .012f, .34f));
            Piece("Target center", root, cylinderMesh, platformColors[0], new Vector3(0, 1.014f, 0), new Vector3(.21f, .01f, .21f));
            Piece("Target dot", root, cylinderMesh, cream, new Vector3(0, 1.022f, 0), new Vector3(.045f, .008f, .045f));
            // Short side bands read as a finished toy block rather than a plain debug cube.
            Piece("Front band", root, boxMesh, cream, new Vector3(0, .64f, -.502f), new Vector3(.42f, .035f, .005f));
            Piece("Right band", root, boxMesh, cream, new Vector3(-.502f, .64f, 0), new Vector3(.005f, .035f, .42f));
            var shadow = Piece(name + " shadow", stage.transform, planeMesh, shadowMaterial, Vector3.zero, Vector3.one);
            return new Platform { Root = root, Body = body.GetComponent<MeshRenderer>(), Top = top.GetComponent<MeshRenderer>(), Shadow = shadow };
        }

        private void CreatePawn()
        {
            pawn = Node("Pawn foot", stage.transform);
            pawnPose = Node("Pawn flip pivot", pawn);
            pawnPose.localPosition = Vector3.up * .65f;
            Piece("Tapered body", pawnPose, taperedMesh, dark, new Vector3(0, -.245f, 0), new Vector3(.49f, .81f, .49f));
            Piece("Collar", pawnPose, cylinderMesh, cream, new Vector3(0, .164f, 0), new Vector3(.205f, .045f, .205f));
            Piece("Neck", pawnPose, cylinderMesh, dark, new Vector3(0, .225f, 0), new Vector3(.19f, .11f, .19f));
            Piece("Round head", pawnPose, sphereMesh, dark, new Vector3(0, .45f, 0), new Vector3(.49f, .49f, .49f));
            pawnShadow = Piece("Pawn shadow", stage.transform, planeMesh, pawnShadowMaterial, Vector3.zero, Vector3.one);
        }

        private void Configure(Platform platform, Vector3 position, float halfSize, int step)
        {
            platform.HalfSize = halfSize;
            platform.Step = step;
            var color = step % platformColors.Length;
            platform.Body.sharedMaterial = platformColors[color];
            platform.Top.sharedMaterial = topColors[color];
            platform.Root.Find("Target center").GetComponent<MeshRenderer>().sharedMaterial = platformColors[color];
            MovePlatform(platform, position);
            ScalePlatform(platform, 1);
            SetPlatformActive(platform, true);
        }

        private static void SetPlatformActive(Platform platform, bool active)
        {
            platform.Root.gameObject.SetActive(active);
            platform.Shadow.gameObject.SetActive(active);
        }

        private static void MovePlatform(Platform platform, Vector3 position)
        {
            platform.Position = position;
            platform.Root.localPosition = position;
            platform.Shadow.localPosition = position + new Vector3(.24f, .008f, -.18f);
        }

        private static void ScalePlatform(Platform platform, float height)
        {
            var width = platform.HalfSize * 2;
            var bulge = 1 + (1 - height) * .13f;
            platform.Root.localScale = new Vector3(width * bulge, height, width * bulge);
            platform.Shadow.localScale = new Vector3(width * 1.36f, 1, width * 1.36f);
        }

        private void SetPawn(Vector3 position, float footHeight, Quaternion rotation, Vector3 scale)
        {
            pawn.localPosition = position + Vector3.up * footHeight;
            pawn.localScale = scale;
            pawnPose.localRotation = rotation;
            var floor = SurfaceAt(position);
            var altitude = Mathf.Max(0, footHeight - floor);
            pawnShadow.localPosition = position + new Vector3(.06f + altitude * .12f, floor + .03f, -.04f);
            var diameter = .67f + altitude * .12f;
            pawnShadow.localScale = new Vector3(diameter, 1, diameter);
        }

        private float SurfaceAt(Vector3 position)
        {
            if (OnPlatform(current, position)) return current.Root.localScale.y;
            if (OnPlatform(next, position)) return next.Root.localScale.y;
            if (OnPlatform(previous, position)) return previous.Root.localScale.y;
            return 0;
        }

        private static bool OnPlatform(Platform platform, Vector3 position)
        {
            return platform.Root.gameObject.activeSelf && Mathf.Abs(position.x - platform.Position.x) <= platform.HalfSize
                && Mathf.Abs(position.z - platform.Position.z) <= platform.HalfSize;
        }

        private Framing Frame(JumpSnapshot snapshot, Vector3 offset, Vector3? jumpEnd = null, float arc = 0)
        {
            var focus = offset + Point(snapshot.NextX * .5, snapshot.NextZ * .5);
            var inverse = Quaternion.Inverse(ViewRotation);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            IncludePlatform(offset, (float)snapshot.CurrentHalfSize, focus, inverse, ref min, ref max);
            IncludePlatform(offset + Point(snapshot.NextX, snapshot.NextZ), (float)snapshot.NextHalfSize, focus, inverse, ref min, ref max);
            var start = offset + Point(snapshot.PlayerX, snapshot.PlayerZ);
            var end = jumpEnd ?? offset + Point(snapshot.NextX, snapshot.NextZ);
            var apex = arc > 0 ? arc : ArcHeight(Vector3.Distance(start, end));
            for (var i = 0; i <= 8; i++)
            {
                var t = i / 8f;
                var point = Vector3.Lerp(start, end, t) + Vector3.up * (PlatformTop + PawnHeight + 4 * apex * t * (1 - t));
                Include(point + Vector3.right * .35f, focus, inverse, ref min, ref max);
                Include(point - Vector3.right * .35f, focus, inverse, ref min, ref max);
            }
            var center = (min + max) * .5f;
            var half = (max - min) * .5f;
            var aspect = sceneCamera.aspect > 0 ? sceneCamera.aspect : 1;
            return new Framing
            {
                Focus = focus + ViewRotation * new Vector3(center.x, center.y, 0),
                Size = Mathf.Max(half.y + .38f, (half.x + .38f) / aspect)
            };
        }

        private static void IncludePlatform(Vector3 position, float halfSize, Vector3 focus, Quaternion inverse, ref Vector2 min, ref Vector2 max)
        {
            for (var x = -1; x <= 1; x += 2)
            for (var z = -1; z <= 1; z += 2)
            for (var y = 0; y <= 1; y++)
                Include(position + new Vector3(x * (halfSize + .22f), y * PlatformTop, z * (halfSize + .22f)), focus, inverse, ref min, ref max);
        }

        private static void Include(Vector3 point, Vector3 focus, Quaternion inverse, ref Vector2 min, ref Vector2 max)
        {
            var projected = inverse * (point - focus);
            min = Vector2.Min(min, new Vector2(projected.x, projected.y));
            max = Vector2.Max(max, new Vector2(projected.x, projected.y));
        }

        private void ApplyFrame(Framing frame)
        {
            cameraFocus = frame.Focus;
            cameraSize = frame.Size;
            sceneCamera.transform.localPosition = frame.Focus + ViewDirection * 30;
            sceneCamera.transform.localRotation = ViewRotation;
            sceneCamera.orthographicSize = frame.Size;
        }

        private void FlightParticles(Vector3 start, Vector3 end, float arc, float progress)
        {
            for (var i = 0; i < particles.Length; i++)
            {
                var t = progress - .025f * (i + 1);
                var active = t > 0 && progress < .9f;
                particles[i].gameObject.SetActive(active);
                if (!active) continue;
                particles[i].localPosition = Vector3.Lerp(start, end, t) + Vector3.up * (PlatformTop + .45f + 4 * arc * t * (1 - t));
                particles[i].localScale = Vector3.one * Mathf.Lerp(.085f, .025f, i / (float)particles.Length);
                particles[i].localRotation = Quaternion.Euler(i * 31, t * 360, i * 43);
            }
        }

        private void LandingParticles(Vector3 center, float progress, bool perfect)
        {
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i].gameObject.SetActive(perfect || i < 6);
                var angle = i * Mathf.PI * 2 / particles.Length;
                var radius = Mathf.Lerp(.12f, perfect ? 1 : .7f, progress);
                particles[i].localPosition = center + new Vector3(Mathf.Cos(angle) * radius, PlatformTop + .08f + .45f * Mathf.Sin(progress * Mathf.PI), Mathf.Sin(angle) * radius);
                particles[i].localScale = Vector3.one * .1f * (1 - progress);
                particles[i].localRotation = Quaternion.Euler(progress * 180, i * 30, progress * 90);
            }
        }

        private void HideParticles()
        {
            foreach (var particle in particles) if (particle != null) particle.gameObject.SetActive(false);
        }

        private static Vector3 Point(double x, double z) { return new Vector3((float)x, 0, (float)z); }
        private static int NextStep(int step) { return step == int.MaxValue ? step : step + 1; }
        private static float ArcHeight(float distance) { return Mathf.Clamp(1.25f + distance * .09f, 1.4f, 2.05f); }
        private static float Smooth(float value) { return value * value * (3 - 2 * value); }
        private static Framing Lerp(Framing a, Framing b, float t) { return new Framing { Focus = Vector3.Lerp(a.Focus, b.Focus, t), Size = Mathf.Lerp(a.Size, b.Size, t) }; }

        private Mesh Own(Mesh mesh) { meshes.Add(mesh); return mesh; }
        private Material Material(Shader shader, Color color)
        {
            var material = new Material(shader) { name = "Jump " + color, color = color };
            materials.Add(material);
            return material;
        }

        private static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name) { layer = SceneLayer };
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        private static Transform Piece(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var piece = Node(name, parent);
            piece.localPosition = position;
            piece.localScale = scale;
            piece.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return piece;
        }

        private void OnEnable() { if (stage != null) SetVisible(visible); }
        private void OnDisable()
        {
            animationVersion++;
            if (stage != null) stage.SetActive(false);
            if (sceneCamera != null) sceneCamera.enabled = false;
        }

        private void ReleaseTexture()
        {
            if (sceneCamera != null) sceneCamera.targetTexture = null;
            if (texture == null) return;
            if (target != null && target.texture == texture) target.texture = null;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void OnDestroy()
        {
            animationVersion++;
            ReleaseTexture();
            if (stage != null) Destroy(stage);
            foreach (var material in materials) if (material != null) Destroy(material);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
