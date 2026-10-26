using System.Collections.Generic;
using UnityEngine;

namespace XanaduYonder.Runtime.Jump
{
    // Small original meshes; generating them directly avoids primitive colliders and a Physics dependency.
    internal static class JumpSceneGeometry
    {
        public static Mesh Box()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            Face(vertices, normals, triangles, Vector3.up, Vector3.right, Vector3.back);
            Face(vertices, normals, triangles, Vector3.down, Vector3.right, Vector3.forward);
            Face(vertices, normals, triangles, Vector3.forward, Vector3.right, Vector3.up);
            Face(vertices, normals, triangles, Vector3.back, Vector3.left, Vector3.up);
            Face(vertices, normals, triangles, Vector3.right, Vector3.back, Vector3.up);
            Face(vertices, normals, triangles, Vector3.left, Vector3.forward, Vector3.up);
            return Make("Jump box", vertices, normals, triangles);
        }

        private static void Face(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 normal, Vector3 right, Vector3 up)
        {
            var first = v.Count;
            v.Add((normal - right - up) * .5f);
            v.Add((normal + right - up) * .5f);
            v.Add((normal + right + up) * .5f);
            v.Add((normal - right + up) * .5f);
            for (var i = 0; i < 4; i++) n.Add(normal);
            t.Add(first); t.Add(first + 1); t.Add(first + 2);
            t.Add(first); t.Add(first + 2); t.Add(first + 3);
        }

        public static Mesh Cylinder(float topRadius = .5f)
        {
            const int segments = 32;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices.Add(direction * .5f + Vector3.down * .5f);
                vertices.Add(direction * topRadius + Vector3.up * .5f);
                var normal = (direction + Vector3.up * (.5f - topRadius)).normalized;
                normals.Add(normal); normals.Add(normal);
                if (i == segments) continue;
                var first = i * 2;
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 3);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }
            for (var cap = 0; cap < 2; cap++)
            {
                var normal = cap == 0 ? Vector3.down : Vector3.up;
                var radius = cap == 0 ? .5f : topRadius;
                var first = vertices.Count;
                vertices.Add(normal * .5f); normals.Add(normal);
                for (var i = 0; i <= segments; i++)
                {
                    var angle = i * Mathf.PI * 2 / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, normal.y * .5f, Mathf.Sin(angle) * radius));
                    normals.Add(normal);
                    if (i == segments) continue;
                    triangles.Add(first);
                    triangles.Add(first + i + (cap == 0 ? 1 : 2));
                    triangles.Add(first + i + (cap == 0 ? 2 : 1));
                }
            }
            return Make("Jump cylinder", vertices, normals, triangles);
        }

        public static Mesh Sphere()
        {
            const int sides = 24, rings = 16;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var y = 0; y <= rings; y++)
            {
                var latitude = y * Mathf.PI / rings;
                for (var x = 0; x <= sides; x++)
                {
                    var longitude = x * Mathf.PI * 2 / sides;
                    var normal = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    vertices.Add(normal * .5f); normals.Add(normal);
                    if (y == rings || x == sides) continue;
                    var first = y * (sides + 1) + x;
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + sides + 2);
                    triangles.Add(first); triangles.Add(first + sides + 2); triangles.Add(first + sides + 1);
                }
            }
            return Make("Jump sphere", vertices, normals, triangles);
        }

        public static Mesh Plane()
        {
            var mesh = new Mesh { name = "Jump shadow plane" };
            mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Make(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
