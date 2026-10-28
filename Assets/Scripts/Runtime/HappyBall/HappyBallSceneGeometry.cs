using System;
using System.Collections.Generic;
using UnityEngine;
using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    // Original geometry, without Unity primitives, physics components or imported assets.
    internal static class HappyBallSceneGeometry
    {
        internal static readonly Color PlateColor = new Color32(51, 53, 52, 255);
        internal static readonly Color DangerColor = new Color32(255, 83, 54, 255);
        internal static readonly Color WallColor = new Color32(36, 39, 38, 255);
        internal static readonly Color SmashColor = new Color32(135, 246, 27, 255);

        internal sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>(4096);
            private readonly List<Vector3> normals = new List<Vector3>(4096);
            private readonly List<Color> colors = new List<Color>(4096);
            private readonly List<int> indices = new List<int>(6144);
            private readonly List<Vector2> shadeUvs = new List<Vector2>(4096);

            public void Layer(Mesh mesh, HappyBallLayer layer, int half = 0, double towerAngle = 0, bool heavy = false)
            {
                Clear();
                foreach (var sector in layer.Sectors)
                    ClippedArc(sector.StartAngle, sector.SweepAngle, -(float)HappyBallRules.PlateThickness, 0,
                        heavy ? SmashColor : sector.Dangerous ? DangerColor : PlateColor, half, towerAngle);
                foreach (var wall in layer.Walls)
                {
                    // The same plane cuts plate, wall, and their attached paint together.
                    ClippedArc(wall.Angle - wall.WidthAngle * .5, wall.WidthAngle, 0, (float)wall.Height,
                        heavy ? SmashColor : WallColor, half, towerAngle);
                }
                Upload(mesh);
            }

            // A local, soft contact band on the column gives each plate a readable height.
            // It is owned by the intact floor and disappears with it; no global light is needed.
            public void ColumnShade(Mesh mesh, HappyBallLayer layer)
            {
                Clear();
                const int bands = 5;
                var radius = (float)HappyBallRules.InnerRadius * 1.001f;
                foreach (var sector in layer.Sectors)
                {
                    var count = Math.Max(1, (int)Math.Ceiling(sector.SweepAngle / (Math.PI / 48)));
                    for (var i = 0; i < count; i++)
                    for (var band = 0; band < bands; band++)
                    {
                        var a = sector.StartAngle + sector.SweepAngle * i / count;
                        var b = sector.StartAngle + sector.SweepAngle * (i + 1) / count;
                        var top = -(float)HappyBallRules.PlateThickness - band * .11f;
                        var bottom = top - .11f;
                        var first = vertices.Count;
                        var alpha = .65f * (1 - (float)band / bands);
                        Quad(Point(a, radius, top), Point(b, radius, top), Point(b, radius, bottom), Point(a, radius, bottom),
                            Direction((a + b) * .5), new Color(1, 1, 1, alpha));
                        colors[first + 2] = colors[first + 3] = new Color(1, 1, 1, .65f * (1 - (float)(band + 1) / bands));
                    }
                }
                Upload(mesh);
                shadeUvs.Clear();
                for (var i = 0; i < vertices.Count; i++) shadeUvs.Add(new Vector2(.5f, .5f));
                mesh.SetUVs(0, shadeUvs);
            }

            private void ClippedArc(double start, double sweep, float bottom, float top, Color color, int half, double angle)
            {
                if (half == 0) { Arc(start, sweep, bottom, top, color); return; }
                // sin(localAngle + towerAngle) gives world X. Cut at both camera-space poles.
                var end = start + sweep;
                var cursor = start;
                while (cursor < end - 0.0000001)
                {
                    var boundary = (Math.Floor((cursor + angle) / Math.PI + 0.0000001) + 1) * Math.PI - angle;
                    var finish = Math.Min(end, boundary);
                    var side = Math.Sin((cursor + finish) * .5 + angle) < 0 ? -1 : 1;
                    if (side == half) Arc(cursor, finish - cursor, bottom, top, color);
                    cursor = finish;
                }
            }

            private void Arc(double start, double sweep, float bottom, float top, Color color)
            {
                var inner = (float)HappyBallRules.InnerRadius;
                var outer = (float)HappyBallRules.OuterRadius;
                var count = Math.Max(1, (int)Math.Ceiling(sweep / (Math.PI / 48)));
                for (var i = 0; i < count; i++)
                {
                    var a = start + sweep * i / count;
                    var b = start + sweep * (i + 1) / count;
                    var mid = Direction((a + b) * .5);
                    Quad(Point(a, inner, top), Point(b, inner, top), Point(b, outer, top), Point(a, outer, top), Vector3.up, color);
                    Quad(Point(a, inner, bottom), Point(a, outer, bottom), Point(b, outer, bottom), Point(b, inner, bottom), Vector3.down, color);
                    Quad(Point(a, outer, top), Point(b, outer, top), Point(b, outer, bottom), Point(a, outer, bottom), mid, color);
                    Quad(Point(a, inner, top), Point(a, inner, bottom), Point(b, inner, bottom), Point(b, inner, top), -mid, color);
                }
                var startNormal = new Vector3(-(float)Math.Cos(start), 0, -(float)Math.Sin(start));
                var end = start + sweep;
                var endNormal = new Vector3((float)Math.Cos(end), 0, (float)Math.Sin(end));
                Quad(Point(start, inner, top), Point(start, outer, top), Point(start, outer, bottom), Point(start, inner, bottom), startNormal, color);
                Quad(Point(end, inner, top), Point(end, inner, bottom), Point(end, outer, bottom), Point(end, outer, top), endNormal, color);
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
            {
                var first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (var i = 0; i < 4; i++) { normals.Add(normal); colors.Add(color); }
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0)
                {
                    indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                    indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
                }
                else
                {
                    indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
                    indices.Add(first); indices.Add(first + 3); indices.Add(first + 2);
                }
            }

            private void Clear() { vertices.Clear(); normals.Clear(); colors.Clear(); indices.Clear(); }
            private void Upload(Mesh mesh)
            {
                mesh.Clear();
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateBounds();
            }
        }

        internal static Vector3 Direction(double angle) { return new Vector3((float)Math.Sin(angle), 0, -(float)Math.Cos(angle)); }
        private static Vector3 Point(double angle, float radius, float y) { return Direction(angle) * radius + Vector3.up * y; }

        public static Mesh Cylinder()
        {
            const int sides = 96;
            var v = new Vector3[(sides + 1) * 2];
            var n = new Vector3[v.Length];
            var t = new int[sides * 6];
            for (var i = 0; i <= sides; i++)
            {
                var direction = Direction(i * Math.PI * 2 / sides);
                v[i * 2] = direction * .5f + Vector3.down * .5f;
                v[i * 2 + 1] = direction * .5f + Vector3.up * .5f;
                n[i * 2] = n[i * 2 + 1] = direction;
                if (i == sides) continue;
                var vertex = i * 2; var index = i * 6;
                t[index] = vertex; t[index + 1] = vertex + 1; t[index + 2] = vertex + 3;
                t[index + 3] = vertex; t[index + 4] = vertex + 3; t[index + 5] = vertex + 2;
            }
            return Make("HappyBall ivory pillar", v, n, t);
        }

        public static Mesh Sphere()
        {
            const int sides = 32, rings = 20;
            var v = new Vector3[(rings + 1) * (sides + 1)];
            var n = new Vector3[v.Length];
            var t = new int[rings * sides * 6];
            var index = 0;
            for (var y = 0; y <= rings; y++)
            for (var x = 0; x <= sides; x++)
            {
                var latitude = y * Mathf.PI / rings;
                var longitude = x * Mathf.PI * 2 / sides;
                var vertex = y * (sides + 1) + x;
                n[vertex] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                v[vertex] = n[vertex] * .5f;
                if (x == sides || y == rings) continue;
                t[index++] = vertex; t[index++] = vertex + 1; t[index++] = vertex + sides + 2;
                t[index++] = vertex; t[index++] = vertex + sides + 2; t[index++] = vertex + sides + 1;
            }
            return Make("HappyBall sphere", v, n, t);
        }

        public static Mesh Disc()
        {
            const int sides = 64;
            var v = new Vector3[sides + 2];
            var n = new Vector3[v.Length];
            var uv = new Vector2[v.Length];
            var t = new int[sides * 3];
            n[0] = Vector3.up; uv[0] = new Vector2(.5f, .5f);
            for (var i = 0; i <= sides; i++)
            {
                v[i + 1] = Direction(i * Math.PI * 2 / sides) * .5f;
                n[i + 1] = Vector3.up;
                uv[i + 1] = new Vector2(v[i + 1].x + .5f, v[i + 1].z + .5f);
                if (i == sides) continue;
                t[i * 3] = 0; t[i * 3 + 1] = i + 2; t[i * 3 + 2] = i + 1;
            }
            var mesh = Make("HappyBall soft disc", v, n, t); mesh.uv = uv; return mesh;
        }

        private static Mesh Make(string name, Vector3[] vertices, Vector3[] normals, int[] triangles)
        {
            var colors = new Color[vertices.Length];
            for (var i = 0; i < colors.Length; i++) colors[i] = Color.white;
            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, colors = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
