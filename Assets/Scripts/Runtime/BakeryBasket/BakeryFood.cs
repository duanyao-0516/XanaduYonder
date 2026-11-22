using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XanaduYonder.Runtime.BakeryBasket
{
    // The same profiles generate the visible pastries and their lower-resolution collision pieces.
    internal static partial class BakeryFood
    {
        internal sealed class Shape
        {
            public Mesh Surface;
            public Mesh[] ContactParts;
        }

        internal static readonly string[] Names =
        {
            "Milk bun", "Croissant", "Vanilla cupcake", "Strawberry doughnut", "Berry tart", "Bread_Slice",
            "ChocolateBar", "Jam biscuit", "Pistachio macaron", "Chocolate éclair", "Cream puff", "Lemon tartlet",
            "Shortbread", "Fruit jelly", "Meringue"
        };
        internal static readonly float[] Lengths =
            { 1.70f, 1.57f, 1.22f, 1.23f, 1.34f, .94f, .81f, .86f, .77f, 1.63f, 1.12f, .93f, .64f, .48f, .57f };
        internal const int Toast = 5;
        internal const int Doughnut = 3;

        private const float Tau = Mathf.PI * 2;
        private static readonly Color Golden = UiFactory.Hex("E8A54E");
        private static readonly Color Toasted = UiFactory.Hex("AB511F");
        private static readonly Color Cream = UiFactory.Hex("FFF0C6");
        private static readonly Color Strawberry = UiFactory.Hex("F58B9F");
        private static readonly Color Jam = UiFactory.Hex("B82B49");

        internal static bool IsImported(int kind) => kind == Toast || kind == 6;

        internal static Shape Create(int kind)
        {
            switch (kind)
            {
                case 0: return Bun();
                case 1: return Croissant();
                case 2: return Cupcake();
                case 3: return DoughnutShape();
                case 4: return Tart();
                case 7: return Biscuit();
                case 8: return Macaron();
                case 9: return Eclair();
                case 10: return CreamPuff();
                case 11: return LemonTartlet();
                case 12: return Shortbread();
                case 13: return FruitJelly();
                case 14: return Meringue();
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static Shape Bun()
        {
            Vector3[] profile = Profile(.01f,-.27f, .37f,-.27f, .47f,-.22f, .50f,-.10f, .49f,.06f,
                .43f,.20f, .32f,.29f, .16f,.32f, .01f,.32f);
            Vector3 Point(float u, float v)
            {
                Vector3 p = Turn(profile, u, v, 1.19f, .91f);
                float top = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .25f, p.y));
                p.y -= Scores(p) * .035f * top;
                return p;
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float gold = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.20f, .25f, p.y));
                Color skin = Color.Lerp(UiFactory.Hex("DA9B5A"), UiFactory.Hex("A94F23"), gold);
                float cut = Scores(p) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.13f, .25f, p.y));
                return Finish(Color.Lerp(skin, UiFactory.Hex("F8D197"), cut * .91f), .41f);
            }
            return Assemble(new[] { Surface(42, 24, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });

            float Scores(Vector3 p)
            {
                float across = p.x + p.z * .23f;
                float detail = Mathf.Abs(across - Mathf.Round(across / .24f) * .24f);
                return Mathf.Clamp01(1 - detail / .037f) * Mathf.Clamp01((.36f - Mathf.Abs(p.z)) * 12);
            }
        }

        private static Shape Croissant()
        {
            Vector3 Center(float t)
            {
                float angle = (t - .5f) * 3.82f;
                return new Vector3(Mathf.Sin(angle) * .55f, .055f * Mathf.Sin(t * Mathf.PI), (Mathf.Cos(angle) - .5f) * .48f);
            }
            float Radius(float t) => .022f + .225f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .68f) *
                (1 + .115f * Mathf.Cos(t * Tau * 7));
            Vector3 Point(float u, float v) => Tube(Center, Radius, u, v, .9f);
            Color Paint(float u, float v, Vector3 p)
            {
                float seam = Mathf.Pow(.5f + .5f * Mathf.Sin(u * Tau * 7 + .7f), 7);
                Color skin = Color.Lerp(UiFactory.Hex("FFCD75"), UiFactory.Hex("D78A36"),
                    Mathf.SmoothStep(.12f, .71f, Mathf.InverseLerp(-.12f, .30f, p.y)));
                return Finish(Color.Lerp(skin, UiFactory.Hex("FBE1A4"), seam * .72f), .43f);
            }
            return Assemble(new[] { Surface(48, 16, Point, Paint, false, true) }, Sections(5, Point));
        }

        private static Shape Cupcake()
        {
            Vector3[] liner = Profile(.01f,-.36f, .25f,-.36f, .28f,-.33f, .35f,.04f, .35f,.08f, .01f,.08f);
            Vector3 Liner(float u, float v) => Flute(Turn(liner, u, v), u, v, 22, .058f);
            Color LinerPaint(float u, float v, Vector3 p) => Finish(Color.Lerp(UiFactory.Hex("644063"),
                UiFactory.Hex("BA8BB2"), .38f + .28f * Mathf.Cos(u * Tau * 22)), .12f);
            Vector3[] icing = Profile(.01f,.045f, .31f,.045f, .37f,.105f, .34f,.175f, .26f,.20f,
                .30f,.24f, .285f,.30f, .185f,.325f, .22f,.36f, .185f,.422f, .11f,.438f, .105f,.495f, .01f,.535f);
            Vector3 Icing(float u, float v)
            {
                var p = Turn(icing, u, v);
                float swirl = .011f * Mathf.Sin(u * Tau * 3 - v * 11);
                p.y += swirl * Mathf.Sin(v * Mathf.PI);
                return Flute(p, u + v * .29f, v, 7, .04f);
            }
            Color IcingPaint(float u, float v, Vector3 p) => Finish(Color.Lerp(UiFactory.Hex("DEB98C"), UiFactory.Hex("FFF8E9"), Mathf.InverseLerp(.02f, .39f, p.y)), .41f);
            var cherryCenter = new Vector3(.026f,.56f,-.015f);
            var cherrySize = new Vector3(.112f,.114f,.11f);
            var cherry = Berry(cherryCenter, cherrySize, false);
            return Assemble(new[] { Surface(48, 16, Liner, LinerPaint, true), Surface(36, 34, Icing, IcingPaint, true), cherry },
                new[] { Surface(12, 6, Liner, null, true), Surface(12, 8, Icing, null, true), Berry(cherryCenter, cherrySize, false, 9, 5) });
        }

        private static Shape DoughnutShape()
        {
            Vector3 Point(float u, float v)
            {
                float a = u * Tau;
                float b = v * Tau;
                float r = .166f + .006f * Mathf.Sin(a * 5) + .003f * Mathf.Cos(a * 11);
                float drip = .115f + .075f * Mathf.Sin(a * 7) + .09f * Mathf.Cos(a * 11);
                float glaze = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(drip - .04f, drip + .035f, Mathf.Sin(b)));
                r += .012f * glaze;
                float ring = .381f + .015f * Mathf.Sin(a * 3);
                float d = ring + r * Mathf.Cos(b);
                return new Vector3(Mathf.Cos(a) * d, Mathf.Sin(b) * r * .93f, Mathf.Sin(a) * d);
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float sine = Mathf.Sin(v * Tau);
                float drip = .115f + .075f * Mathf.Sin(u * Tau * 7) + .09f * Mathf.Cos(u * Tau * 11);
                float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(drip - .025f, drip + .035f, sine));
                Color dough = Color.Lerp(Golden, UiFactory.Hex("F6C574"), Mathf.Pow(Mathf.Abs(Mathf.Cos(v * Tau)), 9));
                var icing = Color.Lerp(UiFactory.Hex("DE526E"), Strawberry, Mathf.InverseLerp(.1f, .94f, sine));
                return Finish(Color.Lerp(dough, icing, edge), Mathf.Lerp(.15f, .87f, edge));
            }
            return Assemble(new[] { Surface(48, 24, Point, Paint, true, true) }, Sections(8, Point));
        }

        private static Shape Tart()
        {
            Vector3[] shell = Profile(.01f,-.23f, .36f,-.23f, .42f,-.18f, .48f,-.02f,
                .475f,.048f, .42f,.068f, .376f,.025f, .01f,.025f);
            Vector3 Shell(float u, float v) => Flute(Turn(shell, u, v), u, v, 20, .05f);
            Color ShellPaint(float u, float v, Vector3 p) => Finish(Color.Lerp(Toasted, Golden, .4f + .34f * Mathf.Cos(u * Tau * 20)), .14f);
            Vector3[] filling = Profile(.01f,.018f, .405f,.018f, .409f,.037f, .345f,.081f, .19f,.105f, .01f,.115f);
            Vector3 Filling(float u, float v) => Turn(filling, u, v);
            var rendered = new List<Mesh> { Surface(48, 18, Shell, ShellPaint, true),
                Surface(26, 8, Filling, (_, _, _) => Finish(Cream, .35f), true) };
            var contact = new List<Mesh> { Surface(12, 8, Shell, null, true) };
            AddBerry(new Vector3(.07f,.16f,.21f), new Vector3(.15f,.174f,.145f));
            AddBerry(new Vector3(-.22f,.14f,-.028f), new Vector3(.139f,.149f,.122f));
            AddBerry(new Vector3(.17f,.17f,-.15f), new Vector3(.153f,.194f,.138f));
            return Assemble(rendered.ToArray(), contact.ToArray());

            void AddBerry(Vector3 center, Vector3 size)
            {
                rendered.Add(Berry(center, size, true));
                contact.Add(Berry(center, size, true, 9, 5));
            }
        }

        private static Shape Biscuit()
        {
            Vector3[] cookie = Profile(.01f,-.16f, .42f,-.16f, .48f,-.12f, .50f,-.04f,
                .48f,.049f, .42f,.082f, .32f,.09f, .26f,.055f, .01f,.053f);
            Vector3 Point(float u, float v) => Flute(Turn(cookie, u, v), u, v, 13, .029f);
            Color Paint(float u, float v, Vector3 p)
            {
                float r = new Vector2(p.x, p.z).magnitude;
                float inset = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.24f, .30f, r))) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.01f, .052f, p.y));
                var baked = Color.Lerp(UiFactory.Hex("D88A35"), UiFactory.Hex("FFD38B"), Mathf.InverseLerp(-.15f,.07f,p.y));
                return Finish(Color.Lerp(baked, Jam, inset), Mathf.Lerp(.10f, .96f, inset));
            }
            return Assemble(new[] { Surface(36, 20, Point, Paint, true) }, new[] { Surface(12, 7, Point, null, true) });
        }

        private static Shape Macaron()
        {
            Vector3[] cookie = Profile(.01f,-.24f, .25f,-.24f, .41f,-.208f, .48f,-.146f, .47f,-.07f,
                .412f,-.053f, .412f,.044f, .477f,.058f, .48f,.13f, .39f,.203f, .21f,.226f, .01f,.23f);
            Vector3 Point(float u, float v)
            {
                var p = Turn(cookie,u,v,1,.94f);
                float ruffle = Mathf.Clamp01(1 - Mathf.Abs(Mathf.Abs(p.y) - .078f) * 26);
                return Flute(p, u, v, 31, .014f * ruffle);
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float top = Mathf.Abs(p.y);
                float filling = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.044f,.060f,top));
                var shell = Color.Lerp(UiFactory.Hex("788B3C"), UiFactory.Hex("B7CB76"), Mathf.InverseLerp(.052f,.23f,top));
                return Finish(Color.Lerp(shell, UiFactory.Hex("FFF4D5"), filling), .25f);
            }
            return Assemble(new[] { Surface(44, 31, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape Eclair()
        {
            Vector3 Center(float t) => new Vector3((t - .5f) * 1.54f, 0, 0);
            float Radius(float t) => .009f + .25f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .32f);
            Vector3 Point(float u, float v) => Tube(Center, Radius, u, v, .75f);
            Color Paint(float u, float v, Vector3 p)
            {
                float icing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.19f, .33f, Mathf.Sin(v * Tau) + .12f * Mathf.Sin(u * Tau * 7)));
                float stripe = Mathf.Pow(.5f + .5f * Mathf.Cos(u * Tau * 5 + p.z * 14), 13);
                Color chocolate = Color.Lerp(UiFactory.Hex("4F271D"), UiFactory.Hex("FFDAA9"), stripe * .87f);
                return Finish(Color.Lerp(Golden, chocolate, icing), Mathf.Lerp(.20f, .80f, icing));
            }
            return Assemble(new[] { Surface(44, 22, Point, Paint, false, true) },
                new[] { Surface(10, 12, Point, null, false, true) });
        }

        private static Shape CreamPuff()
        {
            Vector3[] profile = Profile(.01f,-.30f, .30f,-.30f, .44f,-.23f, .49f,-.05f, .44f,.02f,
                .48f,.10f, .46f,.23f, .32f,.36f, .15f,.43f, .01f,.44f);
            Vector3 Point(float u, float v) => Flute(Turn(profile, u, v), u + v * .10f, v, 11, .086f);
            Color Paint(float u, float v, Vector3 p)
            {
                float filling = Mathf.Clamp01(1 - Mathf.Abs(p.y + .004f) * 28);
                float dusting = Mathf.InverseLerp(.03f, .32f, p.y) * (.67f + .30f * Mathf.Cos((u * 11 + v) * Tau));
                Color crust = Color.Lerp(UiFactory.Hex("B76932"), Golden, .35f + dusting * .55f);
                return Finish(Color.Lerp(crust, UiFactory.Hex("FFF4DF"), Mathf.Max(filling, dusting)), .21f);
            }
            return Assemble(new[] { Surface(44, 24, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape LemonTartlet()
        {
            Vector3[] profile = Profile(.01f,-.23f, .31f,-.23f, .39f,-.18f, .49f,.035f, .48f,.085f,
                .39f,.092f, .35f,.065f, .27f,.095f, .13f,.104f, .01f,.107f);
            Vector3 Point(float u, float v) => Flute(Turn(profile, u, v), u, v, 16, .062f * (1 - Mathf.InverseLerp(.50f, .66f, v)));
            Color Paint(float u, float v, Vector3 p)
            {
                float filling = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.56f, .69f, v));
                Color crust = Color.Lerp(Toasted, Golden, Mathf.InverseLerp(-.23f, .12f, p.y));
                var curd = Color.Lerp(UiFactory.Hex("E3A52E"), UiFactory.Hex("FFF17A"), Mathf.InverseLerp(.61f, .98f, v));
                return Finish(Color.Lerp(crust, curd, filling), Mathf.Lerp(.16f, .78f, filling));
            }
            return Assemble(new[] { Surface(48, 23, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape Shortbread()
        {
            Vector3[] profile = Profile(.01f,-.19f, .38f,-.19f, .47f,-.14f, .50f,-.055f,
                .50f,.09f, .45f,.16f, .31f,.18f, .01f,.18f);
            Vector3 Point(float u, float v)
            {
                var p = RoundedRectangle(profile, u, v, .62f);
                p.y -= Prick(p) * Mathf.InverseLerp(.11f, .18f, p.y) * .035f;
                return p;
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float crumb = Mathf.InverseLerp(-.19f, .16f, p.y) * (1 - .66f * Prick(p));
                return Finish(Color.Lerp(UiFactory.Hex("CB9A63"), UiFactory.Hex("FBE2AF"), crumb), .13f);
            }
            return Assemble(new[] { Surface(40, 22, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });

            float Prick(Vector3 p)
            {
                if (Mathf.Abs(p.x) > .29f) return 0;
                float x = p.x - Mathf.Round(p.x / .20f) * .20f;
                float z = Mathf.Abs(p.z) - .11f;
                return Mathf.Clamp01(1 - Mathf.Sqrt(x * x + z * z) * 36);
            }
        }

        private static Shape FruitJelly()
        {
            Vector3[] profile = Profile(.01f,-.36f, .26f,-.36f, .43f,-.27f, .48f,-.11f,
                .47f,.17f, .40f,.35f, .25f,.40f, .01f,.40f);
            Vector3 Point(float u, float v) => RoundedRectangle(profile, u, v, .96f);
            Color Paint(float u, float v, Vector3 p)
            {
                float top = Mathf.InverseLerp(-.30f, .40f, p.y);
                return Finish(Color.Lerp(UiFactory.Hex("951B45"), UiFactory.Hex("EB5476"), top), .92f);
            }
            return Assemble(new[] { Surface(28, 18, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape Meringue()
        {
            Vector3[] profile = Profile(.01f,-.32f, .30f,-.32f, .43f,-.26f, .48f,-.16f,
                .42f,-.035f, .34f,.10f, .25f,.22f, .14f,.31f, .06f,.41f, .01f,.47f);
            Vector3 Point(float u, float v)
            {
                var p = Flute(Turn(profile, u, v), u + v * .21f, v, 8, .14f);
                p.x += Mathf.InverseLerp(.13f, .47f, p.y) * .11f;
                return p;
            }
            Color Paint(float u, float v, Vector3 p) => Finish(Color.Lerp(UiFactory.Hex("F1C3CD"), UiFactory.Hex("FFF4EF"),
                Mathf.InverseLerp(-.25f, .30f, p.y)), .22f);
            return Assemble(new[] { Surface(40, 23, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });
        }

        private static Vector3 RoundedRectangle(Vector3[] profile, float u, float v, float depth)
        {
            var p = Turn(profile, u, v);
            float angle = u * Tau;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float scale = 1 / Mathf.Pow(c * c * c * c + s * s * s * s, .25f);
            return new Vector3(p.x * scale, p.y, p.z * scale * depth);
        }

        private static Mesh Berry(Vector3 center, Vector3 size, bool strawberry, int columns = 21, int rows = 14)
        {
            Vector3[] profile = strawberry ? Profile(.01f,-1.0f,.42f,-.73f,.76f,-.28f, .98f,.32f,.76f,.86f,.10f,1f) :
                Profile(.01f,-.95f,.65f,-.75f,.98f,-.21f,.97f,.39f,.58f,.88f,.01f,.96f);
            Vector3 Point(float u, float v) => center + Vector3.Scale(Turn(profile,u,v), size);
            Color shade = UiFactory.Hex(strawberry ? "960D2D" : "690D2E");
            Color shine = UiFactory.Hex(strawberry ? "F83D4D" : "C73558");
            Color Paint(float u, float v, Vector3 _) => Finish(Color.Lerp(shade, shine, Mathf.Sin(v * Mathf.PI)), .79f);
            return Surface(columns, rows, Point, Paint, true);
        }

        private static Shape Assemble(Mesh[] rendered, Mesh[] contact)
        {
            if (rendered.Length == 1) return new Shape { Surface = rendered[0], ContactParts = contact };
            var parts = new CombineInstance[rendered.Length];
            for (int i = 0; i < parts.Length; i++) parts[i].mesh = rendered[i];
            var mesh = new Mesh();
            mesh.CombineMeshes(parts, true, false);
            foreach (Mesh part in rendered) Object.Destroy(part);
            return new Shape { Surface = mesh, ContactParts = contact };
        }

        private static Mesh[] Sections(int count, Func<float, float, Vector3> point)
        {
            var parts = new Mesh[count];
            for (int part = 0; part < count; part++)
            {
                float start = (float)part / count;
                float end = (float)(part + 1) / count;
                parts[part] = Surface(4, 10, (u, v) => point(Mathf.Lerp(start,end,u), v), null, false, true);
            }
            return parts;
        }

        private static Color Finish(Color color, float gloss) { color.a = gloss; return color; }

        private static Vector3[] Profile(params float[] values)
        {
            var points = new Vector3[values.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector3(values[2*i], values[2*i+1]);
            return points;
        }

        private static Vector3 Turn(Vector3[] profile, float u, float v, float width = 1, float depth = 1)
        {
            float row = v * (profile.Length - 1);
            int index = Mathf.Min((int)row, profile.Length - 2);
            var p = Vector3.Lerp(profile[index], profile[index+1], row - index);
            float a = u * Tau;
            return new Vector3(width * p.x * Mathf.Cos(a), p.y, depth * p.x * Mathf.Sin(a));
        }

        private static Vector3 Flute(Vector3 p, float u, float v, int count, float amount)
        {
            float scale = 1 + amount * Mathf.Cos(u * Tau * count) * Mathf.Sin(v * Mathf.PI);
            return new Vector3(p.x * scale, p.y, p.z * scale);
        }

        private static Vector3 Tube(Func<float,Vector3> center, Func<float,float> radius, float u, float v, float height)
        {
            Vector3 forward = (center(Mathf.Min(1,u+.001f)) - center(Mathf.Max(0,u-.001f))).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 up = Vector3.Cross(forward, side);
            float angle = v * Tau;
            return center(u) + radius(u) * (side * Mathf.Cos(angle) + up * (Mathf.Sin(angle) * height));
        }

        private static Mesh Surface(int columns, int rows, Func<float,float,Vector3> point,
            Func<float,float,Vector3,Color> paint, bool wrapU = false, bool wrapV = false)
        {
            int width = columns + 1;
            var vertices = new Vector3[width * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    int i = y * width + x;
                    float u = (float)x / columns;
                    float v = (float)y / rows;
                    Vector3 p = point(u, v);
                    vertices[i] = p;
                    uv[i] = new Vector2(u,v);
                    Vector3 du = point(Wrap(u+.002f,wrapU),v) - point(Wrap(u-.002f,wrapU),v);
                    Vector3 dv = point(u,Wrap(v+.002f,wrapV)) - point(u,Wrap(v-.002f,wrapV));
                    normals[i] = Vector3.Cross(dv.normalized,du.normalized).normalized;
                    colors[i] = paint == null ? Color.white : paint(u,v,p);
                    if (y == rows || x == columns) continue;
                    int t = (y*columns+x)*6;
                    triangles[t] = i;
                    triangles[t+1] = i+width;
                    triangles[t+2] = i+1;
                    triangles[t+3] = i+1;
                    triangles[t+4] = i+width;
                    triangles[t+5] = i+width+1;
                }
            return new Mesh { vertices=vertices, triangles=triangles, normals=normals, colors32=colors, uv=uv };
        }

        private static float Wrap(float value, bool wrap) => wrap ? Mathf.Repeat(value,1) : Mathf.Clamp01(value);
    }
}
