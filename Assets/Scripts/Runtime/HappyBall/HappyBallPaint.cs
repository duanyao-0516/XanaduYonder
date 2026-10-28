using System;
using System.Collections.Generic;
using UnityEngine;
using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    // Transient presentation only: pools never write to the game or saved snapshot.
    internal sealed class HappyBallPaint
    {
        private const int MarkCapacity = 64, DropCapacity = 96;
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private readonly Transform stage;
        private readonly Mark[] marks = new Mark[MarkCapacity];
        private readonly Drop[] drops = new Drop[DropCapacity];
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly List<Vector3> vertices = new List<Vector3>(256);
        private readonly List<Vector2> uvs = new List<Vector2>(256);
        private readonly List<int> indices = new List<int>(384);
        private readonly List<Vector3> sourceVertices = new List<Vector3>(256);
        private readonly List<Vector2> sourceUvs = new List<Vector2>(256);
        private readonly List<int> sourceIndices = new List<int>(384);
        private readonly Vertex[] polygon = new Vertex[4];
        private int nextMark, nextDrop, sequence;

        private sealed class Mark
        {
            public Transform Primary, Secondary;
            public Mesh PrimaryMesh, SecondaryMesh;
            public MeshRenderer PrimaryRenderer, SecondaryRenderer;
            public int LayerIndex;
            public bool Active, Detached;
        }
        private sealed class Drop
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public Vector3 Origin, Velocity;
            public float Age, Duration, Size;
            public bool Active;
        }
        private struct Vertex
        {
            public Vector3 Position;
            public Vector2 Uv;
        }

        public int ActiveStainCount
        {
            get { var count = 0; foreach (var mark in marks) if (mark.Active) count++; return count; }
        }
        public int ActiveDropletCount
        {
            get { var count = 0; foreach (var drop in drops) if (drop.Active) count++; return count; }
        }

        public HappyBallPaint(Transform stage, Material paint, Material liquid, Mesh sphere)
        {
            this.stage = stage;
            for (var i = 0; i < marks.Length; i++)
            {
                var a = new Mesh { name = "HappyBall stain " + i + " mesh" };
                var b = new Mesh { name = "HappyBall stain " + i + " clipped mesh" };
                a.MarkDynamic(); b.MarkDynamic();
                var primary = HappyBallSceneView.Piece("HappyBall stain " + i, stage, a, paint);
                var secondary = HappyBallSceneView.Piece("HappyBall stain clipped " + i, stage, b, paint);
                primary.gameObject.SetActive(false); secondary.gameObject.SetActive(false);
                marks[i] = new Mark { Primary = primary, Secondary = secondary, PrimaryMesh = a, SecondaryMesh = b,
                    PrimaryRenderer = primary.GetComponent<MeshRenderer>(), SecondaryRenderer = secondary.GetComponent<MeshRenderer>() };
            }
            for (var i = 0; i < drops.Length; i++)
            {
                var root = HappyBallSceneView.Piece("HappyBall liquid droplet " + i, stage, sphere, liquid);
                root.gameObject.SetActive(false);
                drops[i] = new Drop { Root = root, Renderer = root.GetComponent<MeshRenderer>() };
            }
        }

        public void AddImpact(HappyBallLayer layer, Transform layerRoot, double towerAngle, float surfaceY,
            float ballY, bool heavy, bool death)
        {
            var variation = ++sequence;
            if (layerRoot != null)
            {
                var mark = marks[nextMark]; nextMark = (nextMark + 1) % marks.Length;
                Release(mark);
                mark.LayerIndex = layer.Index;
                var size = (death ? .95f : heavy ? .91f : .78f) * (1 + .06f * Mathf.Sin(variation * 2.7f));
                BuildSurface(layer, -towerAngle, surfaceY - (float)layer.Y, size, mark.PrimaryMesh);
                if (mark.PrimaryMesh.vertexCount > 0)
                {
                    mark.Active = true;
                    mark.Primary.SetParent(layerRoot, false);
                    mark.Primary.localPosition = Vector3.zero;
                    mark.Primary.localRotation = Quaternion.identity;
                    mark.Primary.localScale = Vector3.one;
                    mark.Primary.gameObject.SetActive(true);
                    properties.Clear(); properties.SetFloat(SeedId, variation * 1.618f); properties.SetFloat(OpacityId, 1);
                    mark.PrimaryRenderer.SetPropertyBlock(properties); mark.SecondaryRenderer.SetPropertyBlock(properties);
                }
            }
            var count = death ? 24 : heavy ? 20 : 13;
            var origin = new Vector3(0, Mathf.Max(surfaceY + .035f, ballY - (float)HappyBallRules.BallRadius * .55f),
                -(float)HappyBallRules.OrbitRadius);
            for (var i = 0; i < count; i++)
            {
                var drop = drops[nextDrop]; nextDrop = (nextDrop + 1) % drops.Length;
                var angle = i * 2.399963f + variation * 1.17f;
                var speed = (death ? 2.25f : 1.6f) * (.55f + .45f * Hash(i + variation * 31));
                drop.Origin = origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .06f;
                drop.Velocity = new Vector3(Mathf.Cos(angle) * speed, 1.5f + Hash(i + variation * 17) * 2.3f,
                    Mathf.Sin(angle) * speed * .75f);
                drop.Size = .035f + Hash(i + variation * 7) * .045f;
                drop.Duration = .36f + Hash(i + variation * 13) * .21f;
                drop.Age = 0; drop.Active = true;
                ApplyDrop(drop);
            }
        }

        // Tessellate only solid floor/wall arcs, so the shader can never paint across a gap.
        private void BuildSurface(HappyBallLayer layer, double centerAngle, float height, float size, Mesh mesh)
        {
            vertices.Clear(); uvs.Clear(); indices.Clear();
            var center = HappyBallSceneGeometry.Direction(centerAngle) * (float)HappyBallRules.OrbitRadius;
            var tangent = new Vector3((float)Math.Cos(centerAngle), 0, (float)Math.Sin(centerAngle));
            var radial = HappyBallSceneGeometry.Direction(centerAngle);
            if (height > .03f)
            {
                foreach (var wall in layer.Walls)
                    if (Mathf.Abs((float)wall.Height - height) < .015f)
                        AddArc(wall.Angle - wall.WidthAngle * .5, wall.WidthAngle, centerAngle, height, size, center, tangent, radial);
            }
            else
            {
                foreach (var sector in layer.Sectors)
                    AddArc(sector.StartAngle, sector.SweepAngle, centerAngle, 0, size, center, tangent, radial);
            }
            Upload(mesh);
        }

        private void AddArc(double start, double sweep, double angle, float height, float size,
            Vector3 center, Vector3 tangent, Vector3 radial)
        {
            var limit = size / (float)HappyBallRules.OrbitRadius;
            // Bring both bounds close to the impact angle before intersecting wrapping sectors.
            start += Math.Floor((angle - limit - start) / (Math.PI * 2)) * Math.PI * 2;
            for (var wrap = 0; wrap < 2; wrap++)
            {
                var a = Math.Max(start, angle - limit);
                var end = Math.Min(start + sweep, angle + limit);
                if (end > a)
                {
                    var count = Math.Max(1, (int)Math.Ceiling((end - a) / .035));
                    var inner = Mathf.Max((float)HappyBallRules.InnerRadius + .002f, (float)HappyBallRules.OrbitRadius - size * .52f);
                    var outer = Mathf.Min((float)HappyBallRules.OuterRadius - .002f, (float)HappyBallRules.OrbitRadius + size * .52f);
                    for (var i = 0; i < count; i++)
                    {
                        var first = vertices.Count;
                        AddVertex(a + (end - a) * i / count, inner, height, size, center, tangent, radial);
                        AddVertex(a + (end - a) * (i + 1) / count, inner, height, size, center, tangent, radial);
                        AddVertex(a + (end - a) * (i + 1) / count, outer, height, size, center, tangent, radial);
                        AddVertex(a + (end - a) * i / count, outer, height, size, center, tangent, radial);
                        indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                        indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
                    }
                }
                start += Math.PI * 2;
            }
        }

        private void AddVertex(double angle, float radius, float height, float size, Vector3 center, Vector3 tangent, Vector3 radial)
        {
            var point = HappyBallSceneGeometry.Direction(angle) * radius;
            var delta = point - center;
            vertices.Add(point + Vector3.up * (height + .009f));
            uvs.Add(new Vector2(.5f + Vector3.Dot(delta, tangent) / size, .5f + Vector3.Dot(delta, radial) / size));
        }

        public void TransferToSplit(int layerIndex, double towerAngle, Transform left, Transform right)
        {
            var plane = new Vector3((float)Math.Cos(towerAngle), 0, -(float)Math.Sin(towerAngle));
            foreach (var mark in marks)
            {
                if (!mark.Active || mark.Detached || mark.LayerIndex != layerIndex) continue;
                sourceVertices.Clear(); sourceUvs.Clear(); sourceIndices.Clear();
                mark.PrimaryMesh.GetVertices(sourceVertices); mark.PrimaryMesh.GetUVs(0, sourceUvs);
                mark.PrimaryMesh.GetTriangles(sourceIndices, 0);
                ClipHalf(mark.PrimaryMesh, plane, -1);
                ClipHalf(mark.SecondaryMesh, plane, 1);
                mark.Primary.SetParent(left, false); mark.Secondary.SetParent(right, false);
                mark.Primary.localPosition = mark.Secondary.localPosition = Vector3.zero;
                mark.Primary.localRotation = mark.Secondary.localRotation = Quaternion.identity;
                mark.Primary.localScale = mark.Secondary.localScale = Vector3.one;
                mark.Primary.gameObject.SetActive(mark.PrimaryMesh.vertexCount > 0);
                mark.Secondary.gameObject.SetActive(mark.SecondaryMesh.vertexCount > 0);
                mark.Detached = true;
            }
        }

        private void ClipHalf(Mesh mesh, Vector3 plane, int side)
        {
            vertices.Clear(); uvs.Clear(); indices.Clear();
            for (var triangle = 0; triangle < sourceIndices.Count; triangle += 3)
            {
                var count = 0;
                for (var edge = 0; edge < 3; edge++)
                {
                    var ia = sourceIndices[triangle + edge];
                    var ib = sourceIndices[triangle + (edge + 1) % 3];
                    var a = new Vertex { Position = sourceVertices[ia], Uv = sourceUvs[ia] };
                    var b = new Vertex { Position = sourceVertices[ib], Uv = sourceUvs[ib] };
                    var da = Vector3.Dot(a.Position, plane) * side;
                    var db = Vector3.Dot(b.Position, plane) * side;
                    if (da >= 0) polygon[count++] = a;
                    if ((da < 0) != (db < 0))
                    {
                        var t = da / (da - db);
                        polygon[count++] = new Vertex { Position = Vector3.Lerp(a.Position, b.Position, t), Uv = Vector2.Lerp(a.Uv, b.Uv, t) };
                    }
                }
                var first = vertices.Count;
                for (var i = 0; i < count; i++) { vertices.Add(polygon[i].Position); uvs.Add(polygon[i].Uv); }
                for (var i = 1; i + 1 < count; i++) { indices.Add(first); indices.Add(first + i); indices.Add(first + i + 1); }
            }
            Upload(mesh);
        }

        private void Upload(Mesh mesh)
        {
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
        }

        public void Tick(float dt)
        {
            foreach (var drop in drops)
            {
                if (!drop.Active) continue;
                drop.Age += dt;
                if (drop.Age >= drop.Duration) { drop.Active = false; drop.Root.gameObject.SetActive(false); }
                else ApplyDrop(drop);
            }
        }

        private void ApplyDrop(Drop drop)
        {
            var t = drop.Age;
            var velocity = drop.Velocity + Vector3.down * (11 * t);
            drop.Root.gameObject.SetActive(true);
            drop.Root.localPosition = drop.Origin + drop.Velocity * t + Vector3.down * (5.5f * t * t);
            drop.Root.localRotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
            drop.Root.localScale = new Vector3(.86f, 1.1f + .08f * velocity.magnitude, .86f) * drop.Size;
            properties.Clear(); properties.SetFloat(OpacityId, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, t / drop.Duration)));
            drop.Renderer.SetPropertyBlock(properties);
        }

        public void ClearLayer(int index)
        {
            foreach (var mark in marks) if (mark.Active && !mark.Detached && mark.LayerIndex == index) Release(mark);
        }
        public void ClearDetached(Transform half)
        {
            foreach (var mark in marks)
                if (mark.Active && mark.Detached && (mark.Primary.parent == half || mark.Secondary.parent == half)) Release(mark);
        }
        public void FadeDetached(Transform half, float opacity)
        {
            foreach (var mark in marks)
            {
                if (!mark.Active || !mark.Detached) continue;
                if (mark.Primary.parent == half) Fade(mark.PrimaryRenderer, opacity);
                if (mark.Secondary.parent == half) Fade(mark.SecondaryRenderer, opacity);
            }
        }
        private void Fade(MeshRenderer renderer, float opacity)
        {
            renderer.GetPropertyBlock(properties); properties.SetFloat(OpacityId, opacity); renderer.SetPropertyBlock(properties);
        }
        public void Rebase(float shift)
        {
            foreach (var drop in drops)
                if (drop.Active) { drop.Origin += Vector3.up * shift; drop.Root.localPosition += Vector3.up * shift; }
        }
        public void Clear()
        {
            foreach (var mark in marks) Release(mark);
            foreach (var drop in drops) { drop.Active = false; drop.Root.gameObject.SetActive(false); }
            nextMark = nextDrop = sequence = 0;
        }
        private void Release(Mark mark)
        {
            mark.Active = mark.Detached = false;
            mark.Primary.gameObject.SetActive(false); mark.Secondary.gameObject.SetActive(false);
            mark.Primary.SetParent(stage, false); mark.Secondary.SetParent(stage, false);
        }
        public void Dispose()
        {
            foreach (var mark in marks) { UnityEngine.Object.Destroy(mark.PrimaryMesh); UnityEngine.Object.Destroy(mark.SecondaryMesh); }
        }
        private static float Hash(int value) { return Mathf.Repeat(Mathf.Sin(value * 127.1f + 311.7f) * 43758.5453f, 1); }
    }
}
