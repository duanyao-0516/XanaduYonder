using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XanaduYonder.Core.BakeryBasket;
using Object = UnityEngine.Object;

namespace XanaduYonder.Runtime.BakeryBasket
{
    internal sealed class BakeryWorld : IDisposable
    {
        internal const int ViewWidth = 656;
        internal const int ViewHeight = 880;
        internal const int ReserveTop = 706;
        internal const int ReserveHeight = 58;
        internal const int ReserveWidth = 76;
        internal const int ReservePitch = 85;
        internal const int ReserveLeft = (ViewWidth - ReserveWidth - (BakeryRound.ReserveSlotCount - 1) * ReservePitch) / 2;
        internal const int TrayTop = 794;
        internal const int SlotWidth = 80;
        internal const int SlotHeight = 72;
        internal const int SlotPitch = 88;
        internal const int TrayLeft = 24;
        internal const float PickBottom = 1 - (ReserveTop - 7f) / ViewHeight;
        private const float PreviousViewHeight = 784;
        internal const float Step = 1f / 90;
        private const int StageLayer = 29;
        private const int DisplayLayer = 28;
        private const int ShadowResolution = 768;
        private const float StraightViewDistance = 26f;
        private const float CellDepth = 2f;
        private const float EntranceSpread = .18f;
        private const float EntranceFlight = .45f;
        private static readonly int ShadowsOn = Shader.PropertyToID("_BakeryShadowsOn");
        private const float SmallBottom = .48f;
        private const float OuterLimit = 3.75f;
        private static readonly Vector3 Origin = new Vector3(0, -300, 0);
        private static readonly Vector3 KeyDirection = new Vector3(-.54f, .77f, -.34f).normalized;
        private const float ContactOffset = .0045f;
        private const int MinimumOpeningSteps = 210;
        private const int MaximumOpeningSteps = 318;
        private const int MaximumTaperedOpeningSteps = 450;
        private const int QuietOpeningSteps = 15;
        private const float OpeningRestSpeed = .11f;
        private const float OpeningRestSpin = .80f;
        private const float TossDuration = .54f;
        private const float MaximumTossSpeed = 4.15f;
        private const int DropColumns = 12;
        private const float GoldenAngle = 2.399963f;
        private static readonly Quaternion TrayRotation = Quaternion.Euler(10, -30, 0);
        private readonly Scene scene;
        private readonly PhysicsScene physics;
        private readonly GameObject stage;
        private readonly Camera camera;
        private readonly Camera shadowCamera;
        private readonly RawImage image;
        private readonly RenderTexture texture;
        private readonly RenderTexture shadowMap;
        private readonly Transform basket;
        private readonly Rigidbody pan;
        private readonly float tossHeight;
        private readonly BakeryMesh basketMesh;
        private readonly Material outlineMaterial;
        private readonly Shader surfaceShader;
        private readonly Shader shadowShader;
        private readonly BakeryModelCache modelCache;
        private readonly bool ownsModelCache;
        private readonly List<Object> owned = new List<Object>();
        private readonly List<Material> litMaterials = new List<Material>();
        private readonly Dictionary<Color, Material> modelMaterials = new Dictionary<Color, Material>();
        private readonly Bounds[] pickBounds = new Bounds[BakeryRound.PieceCount];
        private readonly List<MeshRenderer> selectedOutlines = new List<MeshRenderer>();
        private readonly MaterialPropertyBlock selection = new MaterialPropertyBlock();
        private int selected = -1;
        private bool disposed;
        private bool pickBoundsCurrent;
        private bool pileNeedsWake;
        private bool shadowDirty = true;
        private int shakeIndex;
        private float tossElapsed = -1;
        private float entranceElapsed = -1;
        private int entranceFrame;

        internal sealed class Piece
        {
            public int Kind;
            public float Length;
            public Rigidbody Rigidbody;
            public Transform Root;
            public Transform Visual;
            public Vector3 Centered, EntranceOffset, EntranceUp;
            public float ModelScale, StartDelay;
            public MeshFilter[] Surfaces;
            public MeshRenderer[] Renderers;
            public bool InBasket = true;
            public bool InFlight;
            public bool IsSmall => Length <= .88f;
        }

        internal Piece[] Pieces { get; } = new Piece[BakeryRound.PieceCount];
        internal RenderTexture Texture => texture;
        internal int Selected => selected;
        internal float FloorRadius { get; }
        internal float Depth { get; }
        internal int Recoveries { get; private set; }
        internal bool IsEntering => entranceElapsed >= 0;

        internal BakeryWorld(RawImage target, BakeryRound round, int seed, bool tapered, BakeryTheme theme,
            bool bake = false, bool animateOpening = false, BakeryModelCache models = null)
        {
            image = target;
            ownsModelCache = models == null;
            modelCache = models ?? new BakeryModelCache(BakeryThemeCatalog.Get(theme));
            surfaceShader = Resources.Load<Shader>("BakeryBasket/BakerySurface");
            outlineMaterial = Own(new Material(Resources.Load<Shader>("BakeryBasket/BakeryOutline")));
            scene = SceneManager.CreateScene("Bakery basket " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            stage = new GameObject("Bakery 3D stage") { layer = StageLayer };
            SceneManager.MoveGameObjectToScene(stage, scene);
            stage.transform.position = Origin;
            shadowShader = Resources.Load<Shader>("BakeryBasket/BakeryShadowDepth");
            shadowMap = Own(new RenderTexture(ShadowResolution, ShadowResolution, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "Bakery shadow map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            });
            shadowCamera = Child(stage.transform, "Bakery key shadow camera").gameObject.AddComponent<Camera>();
            shadowCamera.transform.SetLocalPositionAndRotation(Vector3.up * 2.8f + KeyDirection * 11,
                Quaternion.LookRotation(-KeyDirection));
            shadowCamera.enabled = false;
            shadowCamera.orthographic = true;
            shadowCamera.orthographicSize = 5.4f;
            shadowCamera.aspect = 1;
            shadowCamera.nearClipPlane = .1f;
            shadowCamera.farClipPlane = 20;
            shadowCamera.cullingMask = 1 << StageLayer;
            shadowCamera.useOcclusionCulling = false;
            shadowCamera.clearFlags = CameraClearFlags.SolidColor;
            shadowCamera.backgroundColor = Color.white;
            shadowCamera.allowHDR = false;
            shadowCamera.allowMSAA = false;
            shadowCamera.targetTexture = shadowMap;
            basket = Child(stage.transform, "Bakery basket");
            pan = basket.gameObject.AddComponent<Rigidbody>();
            pan.isKinematic = true;
            pan.useGravity = false;
            Depth = tapered ? BakeryBasketGeometry.TaperedDepth : BakeryBasketGeometry.StraightDepth;
            tossHeight = tapered ? .35f : .30f;
            Mesh geometry = Own(BakeryBasketGeometry.Create(tapered));
            basketMesh = new BakeryMesh(geometry);
            FloorRadius = tapered ? BakeryBasketGeometry.TaperedBottom : BakeryBasketGeometry.StraightBottom;
            basket.gameObject.AddComponent<MeshFilter>().sharedMesh = geometry;
            basket.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                MakePaint(UiFactory.Hex("63422F"), .06f, .15f), MakePaint(UiFactory.Hex("A87B54"), 0, .22f),
                MakePaint(UiFactory.Hex("483124"), 0, .13f), MakePaint(UiFactory.Hex("69482E"), 0, .12f)
            };
            var contact = Own(new PhysicsMaterial("Bakery contact") { staticFriction = .44f, dynamicFriction = .33f, bounciness = .075f });
            var bowlCollider = basket.gameObject.AddComponent<MeshCollider>();
            bowlCollider.sharedMesh = geometry;
            bowlCollider.sharedMaterial = contact;
            bowlCollider.contactOffset = ContactOffset;
            var guard = Child(basket, "Toss guard").gameObject.AddComponent<MeshCollider>();
            // Keep the drop zone covered when a tall piece slides off another near the rim.
            guard.sharedMesh = Own(BakeryBasketGeometry.RetainingWall(Depth, !tapered && theme == BakeryTheme.Pastry ? 2.25f : 4.25f));
            guard.sharedMaterial = contact;
            guard.contactOffset = ContactOffset;

            var cameraRoot = Child(stage.transform, "Bakery camera");
            Quaternion view = Quaternion.Euler(tapered ? 69 : 74, 0, 0);
            float distance = tapered ? 15 : StraightViewDistance;
            float originalSize = tapered ? 4.75f : 4.64f;
            float size = originalSize * ViewHeight / PreviousViewHeight;
            float verticalOffset = theme == BakeryTheme.Desk ? -.09f : -.38f;
            if (tapered) verticalOffset += .26f;
            verticalOffset -= size - originalSize;
            var focus = new Vector3(0, 1.14f, 0) + view * Vector3.up * verticalOffset;
            cameraRoot.SetLocalPositionAndRotation(focus + view * Vector3.back * distance, view);
            camera = cameraRoot.gameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = tapered;
            camera.orthographicSize = size;
            camera.fieldOfView = 2 * Mathf.Atan(size / distance) * Mathf.Rad2Deg;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 44;
            camera.cullingMask = (1 << StageLayer) | (1 << DisplayLayer);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            texture = Own(new RenderTexture(ViewWidth, ViewHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "Bakery basket view", antiAliasing = 2, filterMode = FilterMode.Bilinear
            });
            camera.targetTexture = texture;
            image.texture = texture;
            var opening = bake ? null : BakeryOpeningPoses.Get(seed, theme, tapered);
            Spawn(round, seed, contact, theme, tapered, opening);
            if (opening == null) SettleOpening(tapered ? MaximumTaperedOpeningSteps : MaximumOpeningSteps);
            if (animateOpening) BeginEntrance();
            else Render();
        }

        private void BeginEntrance()
        {
            entranceElapsed = 0;
            entranceFrame = -1;
            foreach (var material in litMaterials) material.SetFloat(ShadowsOn, 0);
            var origin = Origin + Vector3.up * .55f;
            foreach (var piece in Pieces)
            {
                piece.StartDelay = EntranceSpread * Mathf.InverseLerp(.15f, Depth + 1.9f, piece.Root.position.y - Origin.y);
                piece.EntranceOffset = piece.Root.InverseTransformVector(origin - piece.Root.position);
                piece.EntranceUp = piece.Root.InverseTransformVector(Vector3.up);
                piece.Visual.gameObject.SetActive(false);
            }
            AdvanceEntrance(0, true);
        }

        internal void AdvanceEntrance(float delta, bool render)
        {
            entranceElapsed += delta;
            if (entranceElapsed >= EntranceSpread + EntranceFlight)
            {
                FinishEntrance(render);
                return;
            }
            int frame = (int)(entranceElapsed * 30);
            if (!render || frame == entranceFrame) return;
            entranceFrame = frame;
            foreach (var piece in Pieces)
            {
                float time = (entranceElapsed - piece.StartDelay) / EntranceFlight;
                if (time < 0) continue;
                float progress = Mathf.Clamp01(time);
                float spread = Mathf.SmoothStep(0, 1, progress);
                piece.Visual.localPosition = piece.Centered + piece.EntranceOffset * (1 - spread) +
                    piece.EntranceUp * (Mathf.Sin(progress * Mathf.PI) * .92f);
                piece.Visual.localScale = Vector3.one * (piece.ModelScale * Mathf.Lerp(.24f, 1, spread));
                if (!piece.Visual.gameObject.activeSelf) piece.Visual.gameObject.SetActive(true);
            }
            camera.Render();
        }

        internal void FinishEntrance(bool render = true)
        {
            if (!IsEntering) return;
            entranceElapsed = -1;
            foreach (var piece in Pieces)
            {
                piece.Visual.localPosition = piece.Centered;
                piece.Visual.localScale = Vector3.one * piece.ModelScale;
                piece.Visual.gameObject.SetActive(true);
            }
            foreach (var material in litMaterials) material.SetFloat(ShadowsOn, 1);
            pickBoundsCurrent = false;
            shadowDirty = true;
            if (render) Render();
        }

        private void SettleOpening(int maximumSteps)
        {
            int quiet = 0;
            for (int step = 1; step <= maximumSteps; step++)
            {
                Simulate();
                if (step < MinimumOpeningSteps) continue;
                quiet = IsOpeningQuiet() ? quiet + 1 : 0;
                if (quiet < QuietOpeningSteps) continue;
                foreach (var piece in Pieces) piece.Rigidbody.Sleep();
                return;
            }
        }

        private bool IsOpeningQuiet()
        {
            foreach (var piece in Pieces)
            {
                var body = piece.Rigidbody;
                if (body.linearVelocity.sqrMagnitude > OpeningRestSpeed * OpeningRestSpeed ||
                    body.angularVelocity.sqrMagnitude > OpeningRestSpin * OpeningRestSpin) return false;
            }
            return true;
        }

        private void Spawn(BakeryRound round, int seed, PhysicsMaterial contact, BakeryTheme theme, bool tapered, Pose[] opening)
        {
            bool pastry = theme == BakeryTheme.Pastry;
            var models = BakeryThemeCatalog.Get(theme);
            var names = models.Names;
            var lengths = models.Lengths;
            var rng = new System.Random(seed);
            var shapes = modelCache.Shapes;
            var hulls = modelCache.Hulls;
            var glaze = pastry ? MakePaint(Color.white, .11f, 0, true) : null;
            if (pastry) glaze.SetFloat("_Sprinkles", 1);
            Material painted;
            if (theme == BakeryTheme.Jade)
            {
                painted = MakeLitMaterial(Resources.Load<Shader>("BakeryBasket/BakeryJade"));
            }
            else
            {
                painted = MakePaint(Color.white, pastry ? .11f : 0, 0, true);
                if (!pastry) painted.SetFloat("_Grain", 0);
                if (theme == BakeryTheme.Produce) painted.SetFloat("_FillLight", .055f);
            }
            // Use the same drop order in both baskets. Smaller pieces tend to arrive earlier,
            // with enough variation to leave all sizes available throughout the pile.
            var order = new int[Pieces.Length];
            var dropOrder = new float[Pieces.Length];
            for (int id = 0; id < order.Length; id++)
            {
                order[id] = id;
                dropOrder[id] = lengths[round.KindOf(id)] * .29f + Range(rng, 0, 1);
            }
            Array.Sort(dropOrder, order);

            for (int index = 0; index < order.Length; index++)
            {
                int id = order[index];
                int kind = round.KindOf(id);
                bool imported = shapes[kind] == null;
                Transform root = Child(stage.transform, names[kind] + " " + id);
                Transform pivot = Child(root, "Centered model");
                if (imported)
                {
                    var model = Object.Instantiate(modelCache.Prefabs[kind], pivot, false);
                    foreach (var node in model.GetComponentsInChildren<Transform>()) node.gameObject.layer = StageLayer;
                }
                else
                {
                    var shape = Child(pivot, "Piece mesh");
                    shape.gameObject.AddComponent<MeshFilter>().sharedMesh = shapes[kind].Surface;
                    shape.gameObject.AddComponent<MeshRenderer>().sharedMaterial = pastry && kind == BakeryFood.Doughnut ? glaze : painted;
                }
                var surfaces = pivot.GetComponentsInChildren<MeshFilter>();
                var renderers = new MeshRenderer[surfaces.Length];
                Bounds bounds = modelCache.Bounds[kind] ??= ModelBounds(surfaces, root);
                Vector3 size = bounds.size;
                float length = lengths[kind];
                float factor = length / Mathf.Max(size.x, size.y, size.z);
                pivot.localScale = Vector3.one * factor;
                pivot.localPosition = -bounds.center * factor;
                for (int i = 0; i < surfaces.Length; i++)
                {
                    var filter = surfaces[i];
                    if (!modelCache.Picks.ContainsKey(filter.sharedMesh))
                        modelCache.Picks.Add(filter.sharedMesh, new BakeryMesh(filter.sharedMesh));
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (imported)
                    {
                        var materials = renderer.sharedMaterials;
                        for (int m = 0; m < materials.Length; m++) materials[m] = ImportedPaint(kind, materials[m]);
                        renderer.sharedMaterials = materials;
                    }
                    renderers[i] = renderer;
                }

                Vector3 localSize = size * factor;
                bool thinImport = imported && Mathf.Min(localSize.x, Mathf.Min(localSize.y, localSize.z)) <= .24f;
                var body = root.gameObject.AddComponent<Rigidbody>();
                body.mass = Mathf.Clamp(localSize.x * localSize.y * localSize.z * .52f, .07f, .9f);
                body.linearDamping = .20f;
                body.angularDamping = thinImport ? 1.12f : .28f;
                body.maxDepenetrationVelocity = thinImport ? 1.65f : 3.5f;
                if (thinImport) body.sleepThreshold = .014f;
                body.solverIterations = thinImport && tapered ? 16 : 8;
                body.solverVelocityIterations = thinImport && tapered ? 6 : 3;
                if (hulls[kind] == null)
                    hulls[kind] = shapes[kind] == null
                        ? new[] { modelCache.Own(MakeHullSource(surfaces, root)) }
                        : BakeContacts(shapes[kind].ContactParts, bounds.center, factor);
                foreach (var mesh in hulls[kind])
                {
                    var hull = root.gameObject.AddComponent<MeshCollider>();
                    hull.convex = true;
                    hull.sharedMesh = mesh;
                    hull.sharedMaterial = contact;
                    hull.contactOffset = thinImport ? .0025f : ContactOffset;
                }

                Pieces[id] = new Piece
                {
                    Kind = kind, Length = length, Root = root, Visual = pivot, Centered = pivot.localPosition,
                    ModelScale = factor, Rigidbody = body, Surfaces = surfaces, Renderers = renderers
                };
                if (opening != null)
                {
                    var pose = opening[id];
                    root.SetPositionAndRotation(Origin + pose.position, pose.rotation);
                    body.position = root.position;
                    body.rotation = pose.rotation;
                    body.Sleep();
                    continue;
                }

                int layer = index / DropColumns;
                int column = index % DropColumns;
                float angle = column * GoldenAngle + layer * .61f + Range(rng, -.13f, .13f);
                float radius = Mathf.Sqrt((column + .65f) / DropColumns) * 2.47f + Range(rng, -.10f, .10f);
                float height = Depth + 1.25f + layer * .12f + column * .055f;
                body.position = Origin + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                body.rotation = Quaternion.Euler(Range(rng, -30, 30), Range(rng, 0, 360), Range(rng, -33, 33));
                if (column == DropColumns - 1)
                    for (int step = 0; step < 13; step++) physics.Simulate(Step);
            }
        }

        private Mesh[] BakeContacts(Mesh[] pieces, Vector3 center, float factor)
        {
            var result = new Mesh[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                Vector3[] vertices = piece.vertices;
                for (int j = 0; j < vertices.Length; j++) vertices[j] = (vertices[j] - center) * (factor * .988f);
                result[i] = modelCache.Own(new Mesh { name = "Fitted piece contact", vertices = vertices, triangles = piece.triangles });
            }
            return result;
        }

        private static Bounds ModelBounds(MeshFilter[] surfaces, Transform root)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (var surface in surfaces)
            {
                var matrix = root.worldToLocalMatrix * surface.transform.localToWorldMatrix;
                foreach (Vector3 vertex in surface.sharedMesh.vertices)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(vertex);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        private static Mesh MakeHullSource(MeshFilter[] surfaces, Transform root)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var surface in surfaces)
            {
                Matrix4x4 matrix = root.worldToLocalMatrix * surface.transform.localToWorldMatrix;
                int start = vertices.Count;
                foreach (Vector3 point in surface.sharedMesh.vertices) vertices.Add(matrix.MultiplyPoint3x4(point) * .988f);
                foreach (int index in surface.sharedMesh.triangles) triangles.Add(start + index);
            }
            var mesh = new Mesh { name = "Bakery convex hull source" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        internal void Simulate()
        {
            pickBoundsCurrent = false;
            if (pileNeedsWake)
            {
                pileNeedsWake = false;
                WakePile();
            }
            bool tossing = tossElapsed >= 0;
            if (tossing)
            {
                MovePan();
                shadowDirty = true;
            }
            // The final step before sleep can still change the resting pose.
            if (!shadowDirty)
            {
                foreach (var piece in Pieces)
                {
                    if (!piece.InBasket || piece.Rigidbody.IsSleeping()) continue;
                    shadowDirty = true;
                    break;
                }
            }
            physics.Simulate(Step);
            foreach (var piece in Pieces)
            {
                if (!piece.InBasket) continue;
                if (tossing && piece.Rigidbody.linearVelocity.y > MaximumTossSpeed)
                {
                    var speed = piece.Rigidbody.linearVelocity;
                    speed.y = MaximumTossSpeed;
                    piece.Rigidbody.linearVelocity = speed;
                }
                Vector3 local = piece.Rigidbody.position - Origin;
                if (local.y >= -.45f && (local.x * local.x + local.z * local.z < OuterLimit * OuterLimit || local.y > 1.5f)) continue;
#if UNITY_EDITOR
                Debug.Log($"BAKERY_RECOVERY {piece.Root.name}: {local}, speed={piece.Rigidbody.linearVelocity}");
#endif
                piece.Rigidbody.position = Origin + new Vector3(-local.x * .23f, 1.72f, -local.z * .23f);
                piece.Rigidbody.linearVelocity = Vector3.zero;
                piece.Rigidbody.angularVelocity = Vector3.zero;
                Recoveries++;
                shadowDirty = true;
            }
        }

        internal void Shake()
        {
            if (tossElapsed >= 0) return;
            shakeIndex++;
            tossElapsed = 0;
            pileNeedsWake = false;
            WakePile();
        }

        private void WakePile()
        {
            foreach (var piece in Pieces)
                if (piece.InBasket && piece.Rigidbody.IsSleeping()) piece.Rigidbody.WakeUp();
        }

        private void MovePan()
        {
            float t = tossElapsed + Step;
            float height;
            if (t < .068f) height = Mathf.SmoothStep(0, -.048f, t / .068f);
            else if (t < .225f) height = Mathf.SmoothStep(-.048f, tossHeight, (t - .068f) / .157f);
            else if (t < .382f) height = Mathf.SmoothStep(tossHeight, -.026f, (t - .225f) / .157f);
            else height = Mathf.SmoothStep(-.026f, 0, (t - .382f) / (TossDuration - .382f));

            // The pan drives the contacts. A small tilt breaks up a flat launch without pushing the pile sideways.
            float pitch = 2.4f * Mathf.Sin(2 * Mathf.PI * Mathf.InverseLerp(.051f, .385f, t));
            float angle = GoldenAngle * shakeIndex;
            Vector3 axis = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            pan.MovePosition(Origin + Vector3.up * height);
            pan.MoveRotation(Quaternion.AngleAxis(pitch, axis));
            tossElapsed = t < TossDuration ? t : -1;
        }

        internal int Pick(Vector2 viewport)
        {
            if (viewport.x < 0 || viewport.x > 1 || viewport.y < PickBottom || viewport.y > 1) return -1;
            RefreshPickBounds();
            Ray ray = camera.ViewportPointToRay(viewport);
            float distance = camera.farClipPlane;
            basketMesh.Intersect(ray, basket, ref distance);
            int front = -1;
            for (int id = 0; id < Pieces.Length; id++)
            {
                var piece = Pieces[id];
                if ((!piece.InBasket && !piece.InFlight) || !pickBounds[id].IntersectRay(ray, out float entry) || entry >= distance) continue;
                foreach (var surface in piece.Surfaces)
                    if (modelCache.Picks[surface.sharedMesh].Intersect(ray, surface.transform, ref distance)) front = piece.InBasket ? id : -1;
            }
            return front;
        }

        private void RefreshPickBounds()
        {
            if (pickBoundsCurrent) return;
            for (int id = 0; id < Pieces.Length; id++)
            {
                var piece = Pieces[id];
                if (!piece.InBasket && !piece.InFlight) continue;
                Bounds bounds = piece.Renderers[0].bounds;
                foreach (var renderer in piece.Renderers) bounds.Encapsulate(renderer.bounds);
                pickBounds[id] = bounds;
            }
            pickBoundsCurrent = true;
        }

        internal List<int> BottomSmall()
        {
            var ids = new List<int>();
            for (int id = 0; id < Pieces.Length; id++)
            {
                var piece = Pieces[id];
                if (piece.InBasket && piece.IsSmall && piece.Root.localPosition.y <= SmallBottom) ids.Add(id);
            }
            return ids;
        }

        internal int VisibleCount(List<int> ids)
        {
            int count = 0;
            foreach (int id in ids) if (TryFindVisiblePoint(id, out _)) count++;
            return count;
        }

        internal bool TryFindVisiblePoint(int id, out Vector2 point)
        {
            point = default;
            var piece = Pieces[id];
            if (!piece.InBasket) return false;
            RefreshPickBounds();
            var bounds = pickBounds[id];
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                var offset = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                Vector3 where = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, offset));
                min = Vector2.Min(min, where);
                max = Vector2.Max(max, where);
            }
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                {
                    var sample = new Vector2(Mathf.Lerp(min.x, max.x, (x + .5f) / 5), Mathf.Lerp(min.y, max.y, (y + .5f) / 5));
                    if (Pick(sample) != id) continue;
                    point = sample;
                    return true;
                }
            return false;
        }

        internal void Mark(int id)
        {
            if (selected == id) return;
            if (selected >= 0)
                foreach (var renderer in Pieces[selected].Renderers) renderer.SetPropertyBlock(null);
            foreach (var renderer in selectedOutlines)
            {
                renderer.enabled = false;
                Object.Destroy(renderer.gameObject);
            }
            selectedOutlines.Clear();
            selected = id;
            if (id < 0) return;
            var piece = Pieces[id];
            selection.SetFloat("_Selected", 1);
            foreach (var renderer in piece.Renderers) renderer.SetPropertyBlock(selection);
            foreach (var surface in piece.Surfaces)
            {
                if (!modelCache.Outlines.TryGetValue(surface.sharedMesh, out var mesh))
                {
                    mesh = modelCache.Own(BakeryMesh.Outline(surface.sharedMesh));
                    modelCache.Outlines.Add(surface.sharedMesh, mesh);
                }
                var outline = Child(surface.transform, "Selection silhouette");
                outline.gameObject.layer = DisplayLayer;
                outline.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = outline.gameObject.AddComponent<MeshRenderer>();
                var paints = new Material[mesh.subMeshCount];
                for (int i = 0; i < paints.Length; i++) paints[i] = outlineMaterial;
                renderer.sharedMaterials = paints;
                selectedOutlines.Add(renderer);
            }
        }

        internal void RemoveFromBasket(int id)
        {
            var piece = Pieces[id];
            piece.InBasket = false;
            piece.InFlight = true;
            piece.Rigidbody.isKinematic = true;
            piece.Rigidbody.detectCollisions = false;
            pileNeedsWake = true;
            shadowDirty = true;
        }

        internal void MoveToTray(int id, int slot, Vector3 from, Quaternion fromRotation, float fromScale, float t)
        {
            var piece = Pieces[id];
            piece.InFlight = t < 1;
            pickBoundsCurrent = false;
            if (piece.Renderers[0].gameObject.layer == StageLayer)
            {
                shadowDirty = true;
                if (t >= .5f)
                    foreach (var renderer in piece.Renderers) renderer.gameObject.layer = DisplayLayer;
            }
            float eased = t * t * (3 - 2 * t);
            var start = camera.WorldToViewportPoint(from);
            var end = camera.WorldToViewportPoint(SlotPosition(slot));
            var path = Vector3.Lerp(start, end, eased);
            path.y += Mathf.Sin(t * Mathf.PI) * .36f / (camera.orthographicSize * 2);
            piece.Root.SetPositionAndRotation(camera.ViewportToWorldPoint(path), Quaternion.Slerp(fromRotation, TrayRotation, eased));
            float projected = Mathf.Lerp(fromScale / ScaleAt(start.z), .690f / piece.Length, eased);
            piece.Root.localScale = Vector3.one * (projected * ScaleAt(path.z));
        }

        internal void PlaceTray(int id, int slot)
        {
            var piece = Pieces[id];
            piece.Root.SetPositionAndRotation(SlotPosition(slot), TrayRotation);
            piece.Root.localScale = Vector3.one * TrayScale(piece);
        }

        internal void PlaceReserve(int id, int slot)
        {
            var piece = Pieces[id];
            piece.Root.SetPositionAndRotation(CellPosition(
                ReserveLeft + slot * ReservePitch + ReserveWidth * .5f, ReserveTop + ReserveHeight * .5f), TrayRotation);
            piece.Root.localScale = Vector3.one * (.49f / piece.Length * ScaleAt(CellDepth));
        }

        internal void ShrinkMatch(int id, float t)
        {
            var piece = Pieces[id];
            piece.Root.localScale = Vector3.one * TrayScale(piece) * Mathf.Lerp(1.10f, .04f, t);
            if (t >= 1) piece.Root.gameObject.SetActive(false);
        }

        private Vector3 SlotPosition(int slot) =>
            CellPosition(TrayLeft + slot * SlotPitch + SlotWidth * .5f, TrayTop + SlotHeight * .5f);

        private Vector3 CellPosition(float x, float y) =>
            camera.ViewportToWorldPoint(new Vector3(x / ViewWidth, 1 - y / ViewHeight, CellDepth));
        private float ScaleAt(float depth) => camera.orthographic ? 1 : depth / StraightViewDistance;
        private float TrayScale(Piece piece) => .690f / piece.Length * ScaleAt(CellDepth);
        internal void RenderWhenChanged(bool animated)
        {
            if (animated || shadowDirty || !shadowMap.IsCreated() || !texture.IsCreated()) Render();
        }

        internal void Render()
        {
            if (shadowDirty || !shadowMap.IsCreated())
            {
                shadowCamera.RenderWithShader(shadowShader, string.Empty);
                shadowDirty = false;
            }
            camera.Render();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (stage != null) stage.SetActive(false);
            if (camera != null) camera.targetTexture = null;
            if (shadowCamera != null) shadowCamera.targetTexture = null;
            if (image != null && image.texture == texture) image.texture = null;
            if (texture != null) texture.Release();
            if (shadowMap != null) shadowMap.Release();
            foreach (Object item in owned) if (item != null) Object.Destroy(item);
            if (ownsModelCache) modelCache.Dispose();
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name) { layer = StageLayer };
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private Material MakePaint(Color tint, float cavity, float gloss, bool vertexPaint = false)
        {
            var material = MakeLitMaterial(surfaceShader);
            material.color = tint;
            material.SetFloat("_Cavity", cavity);
            material.SetFloat("_Gloss", gloss);
            material.SetFloat("_VertexPaint", vertexPaint ? 1 : 0);
            material.SetFloat("_Grain", vertexPaint ? .21f : 0);
            return material;
        }

        private Material MakeLitMaterial(Shader shader)
        {
            var material = Own(new Material(shader));
            material.SetTexture("_BakeryShadowMap", shadowMap);
            material.SetMatrix("_BakeryLightProjection", shadowCamera.projectionMatrix * shadowCamera.worldToCameraMatrix);
            material.SetVector("_BakeryKey", KeyDirection);
            material.SetFloat(ShadowsOn, 1);
            litMaterials.Add(material);
            return material;
        }

        private Material ImportedPaint(int kind, Material source)
        {
            Color original = source.HasProperty("_Color") ? source.color.gamma : Color.white;
            Color tint = kind == BakeryFood.Toast
                ? (original.grayscale > .42f ? UiFactory.Hex("FFE6AF") : UiFactory.Hex("CA7535"))
                : Color.Lerp(UiFactory.Hex("542B25"), UiFactory.Hex("A86240"), Mathf.InverseLerp(.28f, .75f, original.grayscale));
            if (!modelMaterials.TryGetValue(tint, out var material))
            {
                material = MakePaint(tint, .11f, kind == BakeryFood.Toast ? .10f : .42f);
                modelMaterials.Add(tint, material);
            }
            return material;
        }

        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
