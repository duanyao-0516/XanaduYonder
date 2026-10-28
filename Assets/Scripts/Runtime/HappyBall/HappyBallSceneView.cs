using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    /// <summary>Owns a bounded, isolated presentation of the authoritative HappyBall snapshot.</summary>
    internal sealed class HappyBallSceneView : MonoBehaviour
    {
        internal const int SceneLayer = 30;
        private static readonly Color Background = new Color32(246, 244, 236, 255);
        private static readonly Vector3 CameraDirection = new Vector3(0, .6156615f, -.7880108f);
        private static readonly Quaternion CameraRotation = Quaternion.LookRotation(-CameraDirection, Vector3.up);
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BallShadowId = Shader.PropertyToID("_BallShadow");
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Material> materials = new List<Material>();
        private readonly LayerVisual[] layers = new LayerVisual[HappyBallRules.MaximumLayers];
        private readonly int[] splitHistory = new int[64];
        private readonly int[] scoreHistory = new int[64];
        private readonly Transform[] trail = new Transform[8];
        private readonly MeshRenderer[] trailRenderers = new MeshRenderer[8];
        private MaterialPropertyBlock properties;
        private MaterialPropertyBlock columnProperties;
        private readonly HappyBallSceneGeometry.Builder geometry = new HappyBallSceneGeometry.Builder();
        private RawImage target;
        private GameObject stage;
        private Transform tower, pillar, ball, ballShadow, armedRing;
        private MeshRenderer shadowRenderer, pillarRenderer, ringRenderer;
        private Camera sceneCamera;
        private RenderTexture texture;
        private HappyBallEffects effects;
        private HappyBallPaint paint;
        private HappyBallSnapshot rendered;
        private float followY, cameraY, bounceTime, blockedTime, effectTime;
        private Text scorePopup, armedLabel;
        private float scorePopupTime, armedLabelTime;
        private int nextHistory, nextScoreHistory, lastArmedLayer = -1, lastDeathLayer = -1;
        private bool visible;

        private sealed class LayerVisual
        {
            public Transform Root;
            public Mesh Mesh;
            public Mesh ShadeMesh;
            public HappyBallLayer Data;
            public bool Seen;
        }

        public int ActiveSplitCount { get { return effects == null ? 0 : effects.ActiveSplitCount; } }
        public int ActiveFragmentCount { get { return ActiveSplitCount * 2; } }
        public int ActiveStainCount { get { return paint == null ? 0 : paint.ActiveStainCount; } }
        public int ActiveDropletCount { get { return paint == null ? 0 : paint.ActiveDropletCount; } }
        public Transform SceneRoot { get { return stage == null ? null : stage.transform; } }
        public Camera SceneCamera { get { return sceneCamera; } }

        public void Initialize(RawImage image)
        {
            if (stage != null) throw new InvalidOperationException("HappyBall scene is already initialized.");
            target = image != null ? image : throw new ArgumentNullException(nameof(image));
            var surface = Resources.Load<Shader>("HappyBall/HappyBallSurface");
            var soft = Resources.Load<Shader>("HappyBall/HappyBallSoft");
            var stain = Resources.Load<Shader>("HappyBall/HappyBallPaint");
            var columnShader = Resources.Load<Shader>("HappyBall/HappyBallColumn");
            if (surface == null || soft == null || stain == null || columnShader == null)
                throw new InvalidOperationException("HappyBall scene shaders are missing.");
            // Native Unity resources cannot be created in a MonoBehaviour field initializer.
            properties = new MaterialPropertyBlock();
            columnProperties = new MaterialPropertyBlock();

            // Keep world transforms independent of the Canvas and the other games' 3D stages.
            stage = new GameObject("HappyBall 3D stage") { layer = SceneLayer };
            stage.transform.position = new Vector3(0, -4000, 0);
            tower = Node("HappyBall rotating tower", stage.transform);
            var plateMaterial = OwnMaterial(surface, Color.white);
            plateMaterial.SetFloat("_ContactShade", .18f);
            plateMaterial.SetFloat("_EdgeLift", .025f);
            var fragmentMaterial = OwnMaterial(surface, Color.white);
            fragmentMaterial.SetFloat("_ContactShade", .16f);
            fragmentMaterial.SetFloat("_EdgeLift", .025f);
            MakeTransparent(fragmentMaterial);
            var ivory = OwnMaterial(columnShader, new Color32(255, 253, 239, 255));
            ivory.SetFloat("_Shine", .045f);
            ivory.SetFloat("_Ambient", .76f);
            ivory.SetFloat("_Emission", .10f);
            var green = OwnMaterial(surface, new Color32(143, 248, 26, 255));
            green.SetFloat("_Shine", .5f);
            green.SetFloat("_Ambient", .67f);
            green.SetFloat("_Emission", .12f);
            var trailing = OwnMaterial(surface, new Color32(149, 242, 64, 255));
            trailing.SetFloat("_Emission", .24f);
            trailing.SetFloat("_Shine", 0);
            MakeTransparent(trailing);
            var liquid = OwnMaterial(surface, new Color32(142, 244, 35, 255));
            liquid.SetFloat("_Ambient", .8f); liquid.SetFloat("_Emission", .16f); liquid.SetFloat("_Shine", .1f);
            MakeTransparent(liquid);
            var stainMaterial = OwnMaterial(stain, new Color32(131, 242, 22, 255));
            var shadowMaterial = OwnMaterial(soft, new Color(.11f, .13f, .10f, .34f));
            var columnShadeMaterial = OwnMaterial(soft, new Color(.13f, .15f, .12f, .34f));
            var ringMaterial = OwnMaterial(soft, new Color(.50f, .89f, .12f, .7f));
            ringMaterial.SetFloat("_Ring", 1);
            var cylinder = OwnMesh(HappyBallSceneGeometry.Cylinder());
            var sphere = OwnMesh(HappyBallSceneGeometry.Sphere());
            var disc = OwnMesh(HappyBallSceneGeometry.Disc());

            pillar = Piece("HappyBall ivory column", stage.transform, cylinder, ivory);
            pillarRenderer = pillar.GetComponent<MeshRenderer>();
            pillar.localScale = new Vector3((float)HappyBallRules.InnerRadius * 1.995f, 60, (float)HappyBallRules.InnerRadius * 1.995f);
            for (var i = 0; i < layers.Length; i++)
            {
                var mesh = OwnMesh(new Mesh { name = "HappyBall pooled layer " + i });
                mesh.MarkDynamic();
                var root = Piece("HappyBall layer slot " + i, tower, mesh, plateMaterial);
                var shadeMesh = OwnMesh(new Mesh { name = "HappyBall column shade " + i });
                shadeMesh.MarkDynamic();
                Piece("HappyBall floor column shade " + i, root, shadeMesh, columnShadeMaterial);
                root.gameObject.SetActive(false);
                layers[i] = new LayerVisual { Root = root, Mesh = mesh, ShadeMesh = shadeMesh };
            }
            ball = Piece("HappyBall green ball", stage.transform, sphere, green);
            ballShadow = Piece("HappyBall ball contact shadow", stage.transform, disc, shadowMaterial);
            shadowRenderer = ballShadow.GetComponent<MeshRenderer>();
            armedRing = Piece("HappyBall armed ring", stage.transform, disc, ringMaterial);
            ringRenderer = armedRing.GetComponent<MeshRenderer>();
            for (var i = 0; i < trail.Length; i++)
            {
                trail[i] = Piece("HappyBall falling trail " + i, stage.transform, sphere, trailing);
                trailRenderers[i] = trail[i].GetComponent<MeshRenderer>();
                trail[i].gameObject.SetActive(false);
            }
            paint = new HappyBallPaint(stage.transform, stainMaterial, liquid, sphere);
            effects = new HappyBallEffects(stage.transform, fragmentMaterial, geometry, paint);
            var cameraObject = Node("HappyBall perspective camera", stage.transform);
            sceneCamera = cameraObject.gameObject.AddComponent<Camera>();
            sceneCamera.orthographic = false;
            sceneCamera.fieldOfView = 42;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = Background;
            sceneCamera.cullingMask = 1 << SceneLayer;
            sceneCamera.nearClipPlane = .1f;
            sceneCamera.farClipPlane = 80;
            sceneCamera.allowHDR = false;
            sceneCamera.allowMSAA = true;
            sceneCamera.useOcclusionCulling = false;
            sceneCamera.enabled = false;
            scorePopup = CreateLabel("HappyBall score popup", 30);
            armedLabel = CreateLabel("HappyBall armed label", 23);
            scorePopup.transform.SetSiblingIndex(0);
            armedLabel.transform.SetSiblingIndex(1);
            ClearHistory();
            EnsureTexture();
            SetVisible(false);
        }

        public void Reset(HappyBallSnapshot snapshot)
        {
            if (stage == null || snapshot == null) return;
            effects.Clear();
            paint.Clear();
            ClearHistory();
            // A restored Spent layer is already broken; its later pass must not replay
            // the removal that happened before saving. The snapshot window is <=32 IDs.
            foreach (var layer in snapshot.Layers)
            {
                if (layer.State != HappyBallLayerState.Active) RememberSplit(layer.Index);
                if (layer.Index < snapshot.PassedLayers) RememberScore(layer.Index);
            }
            ClearLabels();
            lastArmedLayer = snapshot.Power == HappyBallPower.Armed ? snapshot.PassedLayers - 1 : -1;
            lastDeathLayer = snapshot.GameOver ? snapshot.PassedLayers : -1;
            foreach (var visual in layers)
            {
                visual.Data = null;
                visual.Root.gameObject.SetActive(false);
            }
            bounceTime = blockedTime = effectTime = 0;
            followY = cameraY = (float)snapshot.BallY - (float)HappyBallRules.BallRadius;
            followY = cameraY = ResetFollowHeight(snapshot);
            rendered = snapshot;
            // Restore shows committed geometry only; it never replays historical removals.
            Render(snapshot);
        }

        public void Render(HappyBallSnapshot snapshot, HappyBallStepResult result = null)
        {
            if (stage == null || snapshot == null) return;
            if (rendered == null) { Reset(snapshot); return; }
            if (snapshot.OriginLayer != rendered.OriginLayer)
                Rebase((float)((snapshot.OriginLayer - rendered.OriginLayer) * HappyBallRules.LayerSpacing));
            tower.localRotation = Quaternion.Euler(0, -(float)(snapshot.TowerAngle * Mathf.Rad2Deg), 0);
            foreach (var visual in layers) visual.Seen = false;
            // Update existing roots before the event pass, but keep absent old geometry until afterwards.
            foreach (var layer in snapshot.Layers)
            {
                var visual = Find(layer.Index);
                if (visual == null) continue;
                visual.Data = layer;
                visual.Seen = true;
                visual.Root.localPosition = Vector3.up * (float)layer.Y;
                visual.Root.gameObject.SetActive(layer.State == HappyBallLayerState.Active && !AlreadySplit(layer.Index));
            }
            if (result != null && result.Events != null)
            {
                foreach (var item in result.Events)
                {
                    switch (item.Kind)
                    {
                        case HappyBallEventKind.LayerBroken:
                            if (!AlreadySplit(item.LayerIndex)) Impact(snapshot, item, true, false);
                            Split(snapshot, item, true);
                            ShowScore(item);
                            break;
                        case HappyBallEventKind.LayerPassed:
                            Split(snapshot, item, false);
                            ShowScore(item);
                            break;
                        case HappyBallEventKind.Bounce:
                            Impact(snapshot, item, false, false);
                            break;
                        case HappyBallEventKind.Died:
                            if (lastDeathLayer != item.LayerIndex)
                            {
                                lastDeathLayer = item.LayerIndex;
                                Impact(snapshot, item, false, true);
                            }
                            break;
                        case HappyBallEventKind.Blocked: blockedTime = .12f; break;
                        case HappyBallEventKind.Armed:
                            if (lastArmedLayer == item.LayerIndex) break;
                            lastArmedLayer = item.LayerIndex;
                            armedLabel.text = "重击";
                            armedLabelTime = .85f;
                            break;
                    }
                }
            }
            foreach (var visual in layers)
            {
                if (visual.Seen) continue;
                if (visual.Data != null) paint.ClearLayer(visual.Data.Index);
                visual.Data = null;
                visual.Root.gameObject.SetActive(false);
            }
            // Acquire new layers after old events have consumed their geometry. This remains
            // bounded even when a complete 32-layer snapshot advances its window in one frame.
            foreach (var layer in snapshot.Layers)
            {
                if (layer.State != HappyBallLayerState.Active || Find(layer.Index) != null) continue;
                var visual = Acquire(layer);
                if (visual == null) continue;
                visual.Seen = true;
                visual.Root.localPosition = Vector3.up * (float)layer.Y;
                visual.Root.gameObject.SetActive(!AlreadySplit(layer.Index));
            }
            rendered = snapshot;
            followY = Mathf.Min(followY, (float)snapshot.BallY - (float)HappyBallRules.BallRadius);
            // Bound camera lag after a long, legitimate descent without following the upward bounce.
            cameraY = Mathf.Min(cameraY, followY + (float)HappyBallRules.LayerSpacing * .4f);
            ApplyPose();
            ApplyCamera();
        }

        public void Tick(float dt)
        {
            if (stage == null || rendered == null || !visible || dt <= 0) return;
            dt = Mathf.Min(dt, .25f);
            effectTime += dt;
            bounceTime = Mathf.Max(0, bounceTime - dt);
            blockedTime = Mathf.Max(0, blockedTime - dt);
            scorePopupTime = Mathf.Max(0, scorePopupTime - dt);
            armedLabelTime = Mathf.Max(0, armedLabelTime - dt);
            cameraY = Mathf.Lerp(cameraY, followY, 1 - Mathf.Exp(-dt * 12));
            effects.Tick(dt);
            paint.Tick(dt);
            ApplyPose();
            ApplyCamera();
        }

        private void Split(HappyBallSnapshot snapshot, HappyBallEvent item, bool heavy)
        {
            var index = item.LayerIndex;
            if (AlreadySplit(index)) return;
            var visual = Find(index);
            var data = visual == null ? FindData(snapshot, index) : visual.Data;
            if (data == null) return;
            var y = visual == null ? (float)data.Y : visual.Root.localPosition.y;
            var angle = item.HasContact ? item.ContactTowerAngle : snapshot.TowerAngle;
            effects.Begin(data, y, angle, Quaternion.Euler(0, -(float)(angle * Mathf.Rad2Deg), 0), heavy);
            RememberSplit(index);
            if (visual != null) visual.Root.gameObject.SetActive(false);
        }

        private void Impact(HappyBallSnapshot snapshot, HappyBallEvent item, bool heavy, bool death)
        {
            var visual = Find(item.LayerIndex);
            var layer = visual == null ? FindData(snapshot, item.LayerIndex) : visual.Data;
            if (layer == null) return;
            var angle = item.HasContact ? item.ContactTowerAngle : snapshot.TowerAngle;
            var ballY = item.HasContact
                ? (float)(item.ContactBallY + (snapshot.OriginLayer - item.ContactOriginLayer) * HappyBallRules.LayerSpacing)
                : (float)snapshot.BallY;
            var surfaceY = (float)layer.Y;
            // Contact payload owns timing/rotation. Only the floor's own wall geometry can
            // raise its stain surface; presentation never guesses from the rebound velocity.
            foreach (var wall in layer.Walls)
            {
                var distance = Math.Abs(Math.Atan2(Math.Sin(-angle - wall.Angle), Math.Cos(-angle - wall.Angle)));
                var reach = wall.WidthAngle * .5 + Math.Asin(HappyBallRules.BallRadius / HappyBallRules.OrbitRadius);
                var top = (float)(layer.Y + wall.Height);
                if (distance <= reach && ballY >= top - .005f && ballY - top <= HappyBallRules.BallRadius + .01)
                    surfaceY = Mathf.Max(surfaceY, top);
            }
            paint.AddImpact(layer, visual == null ? null : visual.Root, angle, surfaceY, ballY, heavy, death);
            bounceTime = .18f;
            // Follow an actual new landing height, never the ball's whole upward flight.
            // This allows tall wall-top bounces to retain headroom without a jump in cameraY.
            if (!death && surfaceY > followY) followY = surfaceY;
        }

        private static float ResetFollowHeight(HappyBallSnapshot snapshot)
        {
            var layer = FindData(snapshot, snapshot.PassedLayers);
            var bottom = (float)(snapshot.BallY - HappyBallRules.BallRadius);
            if (layer == null) return bottom;
            var support = (float)layer.Y;
            var trajectoryStart = snapshot.BallY + snapshot.BallVelocity * snapshot.BallVelocity / (2 * HappyBallRules.Gravity)
                - HappyBallRules.BallRadius - HappyBallRules.BounceSpeed * HappyBallRules.BounceSpeed / (2 * HappyBallRules.Gravity);
            foreach (var wall in layer.Walls)
            {
                var top = (float)(layer.Y + wall.Height);
                var angle = Math.Abs(Math.Atan2(Math.Sin(-snapshot.TowerAngle - wall.Angle),
                    Math.Cos(-snapshot.TowerAngle - wall.Angle)));
                var reach = wall.WidthAngle * .5 + Math.Asin(HappyBallRules.BallRadius / HappyBallRules.OrbitRadius);
                // The wall may have rotated away during the upward flight. A real wall
                // whose height matches the current ballistic energy remains a framing
                // candidate; rounded rim contacts can launch up to one radius below top+r.
                var trajectoryMatches = trajectoryStart <= top + .01 && trajectoryStart >= top - HappyBallRules.BallRadius - .01;
                if ((angle <= reach || trajectoryMatches) && snapshot.BallY >= top) support = Mathf.Max(support, top);
            }
            // A restored snapshot has no historical Bounce event. The currently supporting
            // wall or a matching launch height provides a bounded camera baseline only.
            return Mathf.Min(bottom, support);
        }

        private LayerVisual Acquire(HappyBallLayer layer)
        {
            foreach (var visual in layers)
            {
                if (visual.Data != null) continue;
                visual.Data = layer;
                geometry.Layer(visual.Mesh, layer);
                geometry.ColumnShade(visual.ShadeMesh, layer);
                visual.Root.name = "HappyBall intact layer " + layer.Index;
                return visual;
            }
            return null;
        }

        private LayerVisual Find(int index)
        {
            foreach (var visual in layers) if (visual.Data != null && visual.Data.Index == index) return visual;
            return null;
        }

        private static HappyBallLayer FindData(HappyBallSnapshot snapshot, int index)
        {
            foreach (var layer in snapshot.Layers) if (layer.Index == index) return layer;
            return null;
        }

        private bool AlreadySplit(int index)
        {
            foreach (var seen in splitHistory) if (seen == index) return true;
            return false;
        }

        private void ClearHistory()
        {
            for (var i = 0; i < splitHistory.Length; i++) { splitHistory[i] = -1; scoreHistory[i] = -1; }
            nextHistory = nextScoreHistory = 0;
        }

        private void RememberSplit(int index)
        {
            splitHistory[nextHistory] = index;
            nextHistory = (nextHistory + 1) % splitHistory.Length;
        }

        private void RememberScore(int index)
        {
            scoreHistory[nextScoreHistory] = index;
            nextScoreHistory = (nextScoreHistory + 1) % scoreHistory.Length;
        }

        private void ShowScore(HappyBallEvent item)
        {
            foreach (var index in scoreHistory) if (index == item.LayerIndex) return;
            RememberScore(item.LayerIndex);
            if (item.ScoreDelta <= 0) return;
            scorePopup.text = "+" + item.ScoreDelta.ToString(CultureInfo.InvariantCulture);
            scorePopupTime = .68f;
        }

        private Text CreateLabel(string name, int size)
        {
            var label = UiFactory.Label(target.transform, "", 0, 0, 150, 44, size,
                UiFactory.Hex("6BC719"), TextAnchor.MiddleLeft, FontStyle.Bold);
            label.name = name;
            label.gameObject.layer = target.gameObject.layer;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.zero;
            label.rectTransform.pivot = new Vector2(0, .5f);
            label.gameObject.SetActive(false);
            return label;
        }

        private void ApplyLabels()
        {
            var point = sceneCamera.WorldToViewportPoint(ball.position);
            var rect = target.rectTransform.rect;
            var edge = sceneCamera.WorldToViewportPoint(ball.position + sceneCamera.transform.right * (float)HappyBallRules.BallRadius);
            var radiusPixels = Mathf.Abs(edge.x - point.x) * rect.width;
            var position = new Vector2(point.x * rect.width + radiusPixels + 9, point.y * rect.height);
            PositionLabel(scorePopup, position + Vector2.up * (24 + 24 * (1 - scorePopupTime / .68f)), scorePopupTime);
            PositionLabel(armedLabel, position + Vector2.up * (66 + 15 * (1 - armedLabelTime / .85f)), armedLabelTime);
        }

        private static void PositionLabel(Text label, Vector2 position, float time)
        {
            label.gameObject.SetActive(time > 0);
            if (time <= 0) return;
            label.rectTransform.anchoredPosition = position;
            var color = label.color; color.a = Mathf.Clamp01(time / .2f); label.color = color;
        }

        private void ClearLabels()
        {
            scorePopupTime = armedLabelTime = 0;
            if (scorePopup != null) scorePopup.gameObject.SetActive(false);
            if (armedLabel != null) armedLabel.gameObject.SetActive(false);
        }

        private void Rebase(float shift)
        {
            followY += shift;
            cameraY += shift;
            foreach (var visual in layers) if (visual.Data != null) visual.Root.localPosition += Vector3.up * shift;
            effects.Rebase(shift);
            paint.Rebase(shift);
        }

        private void ApplyPose()
        {
            var radius = (float)HappyBallRules.BallRadius;
            var position = new Vector3(0, (float)rendered.BallY, -(float)HappyBallRules.OrbitRadius);
            ball.localPosition = position;
            // Impact is flattened on the event frame, then stretches as upward speed peaks.
            // At the apex the velocity term is zero and the silhouette returns to a sphere.
            var squash = bounceTime > 0 ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .055f, .18f - bounceTime)) : 0;
            if (rendered.GameOver) squash = Mathf.Max(squash, .65f);
            var block = Mathf.Sin(Mathf.PI * Mathf.Clamp01(blockedTime / .12f));
            var speed = Mathf.Clamp01(Mathf.Abs((float)rendered.BallVelocity) / (float)HappyBallRules.BounceSpeed);
            var stretch = rendered.GameOver ? 0 : speed * .31f * (1 - squash);
            var height = 1 - squash * .38f + stretch;
            var width = 1 + squash * .24f - stretch * .24f;
            ball.localScale = new Vector3(width + block * .05f, height, width) * (radius * 2);
            if (squash > 0) ball.localPosition += Vector3.up * (radius * (height - 1));
            var moving = !rendered.GameOver && Math.Abs(rendered.BallVelocity) > 1.25;
            var armed = rendered.Power == HappyBallPower.Armed;
            var length = (armed ? 1.45f : 1.02f) * speed;
            var count = armed ? trail.Length : 6;
            var behind = rendered.BallVelocity >= 0 ? Vector3.down : Vector3.up;
            for (var i = 0; i < trail.Length; i++)
            {
                trail[i].gameObject.SetActive(moving && i < count);
                if (!moving || i >= count) continue;
                var fraction = (i + 1f) / count;
                trail[i].localPosition = position + behind * (radius * .65f + fraction * length);
                trail[i].localScale = Vector3.one * Mathf.Lerp(radius * 1.35f, radius * .25f, fraction);
                properties.SetFloat(OpacityId, speed * Mathf.Lerp(.68f, .08f, fraction));
                trailRenderers[i].SetPropertyBlock(properties);
            }
            armedRing.gameObject.SetActive(armed && !rendered.GameOver);
            armedRing.localPosition = position;
            armedRing.localScale = Vector3.one * (radius * (4.2f + .4f * Mathf.Sin(effectTime * 7)));
            armedRing.localRotation = Quaternion.Euler(72, 0, 0);
            properties.SetFloat(OpacityId, .65f + .2f * Mathf.Sin(effectTime * 7));
            ringRenderer.SetPropertyBlock(properties);
            ApplyShadow(position, radius);
        }

        private void ApplyShadow(Vector3 position, float radius)
        {
            HappyBallLayer support = null;
            var localAngle = -rendered.TowerAngle;
            foreach (var layer in rendered.Layers)
            {
                if (layer.State != HappyBallLayerState.Active || layer.Y > position.y || position.y - layer.Y > 2.5) continue;
                var solid = false;
                foreach (var sector in layer.Sectors)
                {
                    var offset = (localAngle - sector.StartAngle) % (Math.PI * 2);
                    if (offset < 0) offset += Math.PI * 2;
                    if (offset <= sector.SweepAngle) { solid = true; break; }
                }
                if (solid && (support == null || layer.Y > support.Y)) support = layer;
            }
            ballShadow.gameObject.SetActive(support != null);
            if (support == null) return;
            var altitude = position.y - (float)support.Y;
            ballShadow.localPosition = new Vector3(0, (float)support.Y + .006f, position.z);
            var size = radius * 3 + altitude * .12f;
            ballShadow.localScale = new Vector3(size, 1, size);
            properties.SetFloat(OpacityId, Mathf.Clamp01(1 - altitude / 2.7f));
            shadowRenderer.SetPropertyBlock(properties);
        }

        private void ApplyCamera()
        {
            var aspect = Mathf.Max(.2f, sceneCamera.aspect);
            var distance = Mathf.Max(11.6f, 7.33f / aspect);
            var focus = new Vector3(0, cameraY - 1.9f, 0);
            sceneCamera.transform.localPosition = focus + CameraDirection * distance;
            sceneCamera.transform.localRotation = CameraRotation;
            pillar.localPosition = new Vector3(0, focus.y, 0);
            var localCenterY = ((float)rendered.BallY - .19f - pillar.localPosition.y) / pillar.localScale.y;
            var inverseHalfHeight = pillar.localScale.y / ((float)HappyBallRules.BallRadius * 1.85f);
            columnProperties.SetVector(BallShadowId, new Vector4(localCenterY, inverseHalfHeight, -11 * Mathf.Deg2Rad, .34f * .72f));
            pillarRenderer.SetPropertyBlock(columnProperties);
            ApplyLabels();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (stage == null) return;
            stage.SetActive(value && isActiveAndEnabled);
            sceneCamera.enabled = value && isActiveAndEnabled;
            if (value) { EnsureTexture(); if (rendered != null) ApplyCamera(); }
            else { effects.Clear(); paint.Clear(); ClearLabels(); }
        }

        private void LateUpdate()
        {
            if (!visible || stage == null) return;
            if (EnsureTexture() && rendered != null) ApplyCamera();
        }

        private bool EnsureTexture()
        {
            var rect = target.rectTransform.rect;
            var scale = target.canvas == null ? 1 : target.canvas.scaleFactor;
            var width = Mathf.Max(128, rect.width * scale);
            var height = Mathf.Max(128, rect.height * scale);
            // Keep native portrait detail up to a 2048px long edge, with at least 540px width.
            var factor = Mathf.Min(1, 2048 / Mathf.Max(width, height));
            if (width * factor < 540) factor = Mathf.Min(540 / width, 2048 / height);
            var pixelWidth = Mathf.Max(128, Mathf.RoundToInt(width * factor));
            var pixelHeight = Mathf.Max(128, Mathf.RoundToInt(height * factor));
            if (texture != null && texture.width == pixelWidth && texture.height == pixelHeight)
            {
                if (!texture.IsCreated()) texture.Create();
                return false;
            }
            ReleaseTexture();
            texture = new RenderTexture(pixelWidth, pixelHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "HappyBall portrait scene", antiAliasing = 2,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            target.texture = texture;
            target.color = Color.white;
            sceneCamera.targetTexture = texture;
            sceneCamera.aspect = (float)pixelWidth / pixelHeight;
            return true;
        }

        private Mesh OwnMesh(Mesh mesh) { meshes.Add(mesh); return mesh; }
        private Material OwnMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { name = "HappyBall " + shader.name, color = color };
            materials.Add(material);
            return material;
        }

        private static void MakeTransparent(Material material)
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
        }

        internal static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name) { layer = SceneLayer };
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        internal static Transform Piece(string name, Transform parent, Mesh mesh, Material material)
        {
            var piece = Node(name, parent);
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
            if (stage != null) stage.SetActive(false);
            if (sceneCamera != null) sceneCamera.enabled = false;
        }

        private void ReleaseTexture()
        {
            if (texture == null) return;
            if (sceneCamera != null) sceneCamera.targetTexture = null;
            if (target != null && target.texture == texture) target.texture = null;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void OnDestroy()
        {
            if (sceneCamera != null) sceneCamera.enabled = false;
            ReleaseTexture();
            if (effects != null) effects.Dispose();
            if (paint != null) paint.Dispose();
            if (stage != null) Destroy(stage);
            foreach (var material in materials) if (material != null) Destroy(material);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (scorePopup != null) Destroy(scorePopup.gameObject);
            if (armedLabel != null) Destroy(armedLabel.gameObject);
            properties = null;
            columnProperties = null;
        }
    }
}
