using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XanaduYonder.Runtime.BakeryBasket
{
    // A ray uses the same triangles as the camera, independent of the broad physics hulls.
    internal sealed class BakeryMesh
    {
        private const float ParallelToleranceSquared = .000000000001f;
        private const int LeafSize = 12;
        private readonly Triangle[] triangles;
        private readonly Node[] nodes;

        private readonly struct Triangle
        {
            public readonly Vector3 A, Edge1, Edge2;
            public readonly Bounds Bounds;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a;
                Edge1 = b - a;
                Edge2 = c - a;
                var bounds = new Bounds(a, Vector3.zero);
                bounds.Encapsulate(b);
                bounds.Encapsulate(c);
                Bounds = bounds;
            }
        }

        private struct Node
        {
            public Bounds Bounds;
            public int Start, Count, Left, Right;
        }

        public BakeryMesh(Mesh mesh)
        {
            var positions = mesh.vertices;
            var indices = mesh.triangles;
            triangles = new Triangle[indices.Length / 3];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = new Triangle(positions[indices[i*3]], positions[indices[i*3+1]], positions[indices[i*3+2]]);
            var tree = new List<Node>();
            BuildNode(tree, 0, triangles.Length);
            nodes = tree.ToArray();
        }

        private int BuildNode(List<Node> tree, int first, int count)
        {
            var node = new Node { Start = first, Count = count, Bounds = triangles[first].Bounds };
            for (int i = first + 1; i < first + count; i++) node.Bounds.Encapsulate(triangles[i].Bounds);
            int index = tree.Count;
            tree.Add(node);
            if (count <= LeafSize) return index;
            Vector3 size = node.Bounds.size;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            float midpoint = node.Bounds.center[axis];
            int left = first;
            int right = first + count - 1;
            while (left <= right)
            {
                if (triangles[left].Bounds.center[axis] < midpoint) { left++; continue; }
                (triangles[left], triangles[right]) = (triangles[right], triangles[left]);
                right--;
            }
            int split = left - first;
            // Keep the tree bounded for meshes with many coincident centroids.
            if (split < count / 8 || count - split < count / 8) split = count / 2;
            node.Count = 0;
            node.Left = BuildNode(tree, first, split);
            node.Right = BuildNode(tree, first + split, count - split);
            tree[index] = node;
            return index;
        }

        public bool Intersect(Ray ray, Transform surface, ref float distance)
        {
            Matrix4x4 inverse = surface.worldToLocalMatrix;
            Vector3 direction = inverse.MultiplyVector(ray.direction);
            var boundsRay = new Ray(inverse.MultiplyPoint3x4(ray.origin), direction);
            return IntersectNode(0, boundsRay, direction, ref distance);
        }

        private bool IntersectNode(int index, Ray ray, Vector3 direction, ref float distance)
        {
            Node node = nodes[index];
            if (!node.Bounds.IntersectRay(ray)) return false;
            if (node.Count == 0)
            {
                bool left = IntersectNode(node.Left, ray, direction, ref distance);
                return IntersectNode(node.Right, ray, direction, ref distance) || left;
            }
            bool hit = false;
            for (int i = node.Start; i < node.Start + node.Count; i++)
            {
                Triangle triangle = triangles[i];
                Vector3 perpendicular = Vector3.Cross(direction, triangle.Edge2);
                float determinant = Vector3.Dot(triangle.Edge1, perpendicular);
                // Baked FBX units vary, so the parallel test uses the angle rather than triangle area.
                if (determinant <= 0 || determinant * determinant <=
                    ParallelToleranceSquared * triangle.Edge1.sqrMagnitude * perpendicular.sqrMagnitude) continue;
                Vector3 offset = ray.origin - triangle.A;
                float u = Vector3.Dot(offset, perpendicular) / determinant;
                if (u < 0 || u > 1) continue;
                Vector3 cross = Vector3.Cross(offset, triangle.Edge1);
                float v = Vector3.Dot(direction, cross) / determinant;
                if (v < 0 || u + v > 1) continue;
                float depth = Vector3.Dot(triangle.Edge2, cross) / determinant;
                if (depth <= 0 || depth >= distance) continue;
                distance = depth;
                hit = true;
            }
            return hit;
        }

        public static Mesh Outline(Mesh source)
        {
            var shell = Object.Instantiate(source);
            var points = shell.vertices;
            var normals = shell.normals;
            // Imported material seams may duplicate vertices. Weld the normals for the outline only.
            var smoothing = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < points.Length; i++)
            {
                smoothing.TryGetValue(points[i], out var normal);
                smoothing[points[i]] = normal + normals[i];
            }
            for (int i = 0; i < points.Length; i++) normals[i] = smoothing[points[i]].normalized;
            shell.normals = normals;
            return shell;
        }
    }

    internal static class BakeryBasketGeometry
    {
        internal const float MouthRadius = 3.17f;
        internal const float StraightBottom = MouthRadius;
        internal const float TaperedBottom = .84f;
        internal const float StraightDepth = 2.2f;
        internal const float TaperedDepth = 2.9f;
        private const int Sides = 36;

        public static Mesh Create(bool tapered)
        {
            float bottom = tapered ? TaperedBottom : StraightBottom;
            float depth = tapered ? TaperedDepth : StraightDepth;
            var points = new List<Vector3>();
            var triangles = new List<int>[4] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var mesh = new Mesh { name = tapered ? "Tapered basket" : "Straight basket" };
            Ring(bottom, 0, MouthRadius, depth, true, 0);
            Ring(MouthRadius, depth, MouthRadius + .07f, depth + .055f, true, 1);
            Ring(MouthRadius + .07f, depth + .055f, MouthRadius + .15f, depth, true, 1);
            Ring(bottom + .13f, -.12f, MouthRadius + .15f, depth, false, 2);
            int floor = points.Count;
            points.Add(Vector3.zero);
            for (int i = 0; i < Sides; i++) points.Add(Point(i, bottom, 0));
            for (int i = 0; i < Sides; i++)
                triangles[3].AddRange(new[] { floor, floor + 1 + i, floor + 1 + (i + 1) % Sides });
            mesh.SetVertices(points);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            void Ring(float lowRadius, float lowY, float highRadius, float highY, bool reverse, int material)
            {
                int index = points.Count;
                for (int i = 0; i < Sides; i++)
                {
                    points.Add(Point(i, lowRadius, lowY));
                    points.Add(Point(i, highRadius, highY));
                }
                for (int i = 0; i < Sides; i++)
                {
                    int a = index + 2 * i;
                    int b = index + 2 * ((i + 1) % Sides);
                    if (reverse) triangles[material].AddRange(new[] { a, b + 1, b, a, a + 1, b + 1 });
                    else triangles[material].AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                }
            }
        }

        public static Mesh RetainingWall(float depth, float height)
        {
            var points = new Vector3[Sides * 2];
            var triangles = new int[Sides * 6];
            for (int i = 0; i < Sides; i++)
            {
                int a = 2 * i;
                int b = 2 * ((i + 1) % Sides);
                points[a] = Point(i, MouthRadius, depth - .01f);
                points[a + 1] = Point(i, MouthRadius + .23f, depth + height);
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = b + 1;
                triangles[t + 2] = b;
                triangles[t + 3] = a;
                triangles[t + 4] = a + 1;
                triangles[t + 5] = b + 1;
            }
            return new Mesh { name = "Invisible toss guard", vertices = points, triangles = triangles };
        }

        private static Vector3 Point(int index, float radius, float y)
        {
            float angle = index * Mathf.PI * 2 / Sides;
            return new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
        }
    }
}
