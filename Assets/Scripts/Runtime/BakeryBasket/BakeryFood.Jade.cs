using System;
using UnityEngine;

namespace XanaduYonder.Runtime.BakeryBasket
{
    internal static partial class BakeryFood
    {
        internal static readonly string[] JadeNames =
        {
            "Hill seal", "Moon arc", "Tower finial", "Round bangle", "Split stone", "Brass tablet",
            "Fluted prism", "Square pendant", "Spool bead", "Bamboo sprig", "Shell cameo", "Lotus medallion",
            "Willow leaf", "Square bead", "Teardrop"
        };

        private readonly struct StoneFinish
        {
            internal readonly Color Deep, Light;
            internal readonly float Gloss, Veins, Transmission, Metallic;

            internal StoneFinish(string deep, string light, float gloss, float veins, float transmission, float metallic = 0f)
            {
                Deep = UiFactory.Hex(deep);
                Light = UiFactory.Hex(light);
                Gloss = gloss;
                Veins = veins;
                Transmission = transmission;
                Metallic = metallic;
            }
        }

        private readonly struct JadeSample
        {
            internal readonly Color Paint;
            internal readonly Vector2 Optics;

            internal JadeSample(Color paint, float transmission, float metallic = 0f)
            {
                Paint = paint;
                Optics = new Vector2(transmission, metallic);
            }

            internal JadeSample BlendWith(Color paint, float transmission, float metallic, float amount)
            {
                return new JadeSample(Color.Lerp(Paint, paint, amount),
                    Mathf.Lerp(Optics.x, transmission, amount), Mathf.Lerp(Optics.y, metallic, amount));
            }
        }

        private static readonly StoneFinish[] JadeFinishes =
        {
            new StoneFinish("1A2225", "454C48", .14f, .07f, 0f), // dark seal stone
            new StoneFinish("B8AB88", "FFF1D8", .69f, .13f, .47f), // milk jade crescent
            new StoneFinish("406F61", "88AD95", .45f, .17f, .49f), // celadon finial
            new StoneFinish("006D3B", "48D979", .90f, .19f, .97f), // polished green bangle
            new StoneFinish("483D3E", "AB886C", .10f, .27f, 0f), // weathered stone
            new StoneFinish("925222", "F8CA6C", .79f, .025f, 0f, 1f), // brass
            new StoneFinish("384956", "859AA7", .23f, .09f, 0f), // slate prism
            new StoneFinish("7AA88A", "ECF4CE", .73f, .11f, .79f), // square jade setting
            new StoneFinish("0B3D51", "3594AF", .68f, .12f, .72f), // blue-green spool
            new StoneFinish("526C28", "C0CE5E", .51f, .13f, .71f), // bamboo-green carving
            new StoneFinish("B36B60", "FFD5B6", .38f, .13f, .29f), // peach cameo
            new StoneFinish("A3B4A0", "F2F5E3", .62f, .16f, .58f), // pale lotus bowl
            new StoneFinish("247D71", "79CFBF", .51f, .09f, .66f), // teal leaf
            new StoneFinish("283C55", "637E97", .30f, .12f, .13f), // blue-grey bead
            new StoneFinish("993F21", "EAB05C", .74f, .15f, .85f) // amber drop
        };
        private static readonly Color JadeBrass = UiFactory.Hex("F6CD79");
        private static readonly Color JadeIvory = UiFactory.Hex("F2EBD8");
        private static readonly Color JadeCut = UiFactory.Hex("79CDB4");

        internal static Shape CreateJade(int kind)
        {
            switch (kind)
            {
                case 0:
                    return JadeProfile(kind, Profile(.01f,-.24f, .45f,-.24f, .50f,-.19f, .50f,-.05f, .45f,.015f,
                        .36f,.04f, .29f,.17f, .16f,.27f, .01f,.32f), depth: .67f, rectangular: true);
                case 1: return JadeSprig(kind, true);
                case 2:
                    return JadeProfile(kind, Profile(.01f,-.49f, .34f,-.49f, .41f,-.43f, .40f,-.34f, .28f,-.30f,
                        .32f,-.18f, .24f,-.13f, .27f,-.03f, .185f,.02f, .21f,.125f, .125f,.18f, .086f,.37f, .01f,.49f),
                        lobes: 4, relief: .11f);
                case 3: return JadeLoop(kind, false);
                case 4: return JadeStone();
                case 5:
                    return JadeProfile(kind, Profile(.01f,-.079f, .39f,-.079f, .47f,-.059f, .50f,-.038f, .50f,.038f,
                        .47f,.063f, .39f,.077f, .01f,.077f), depth: .71f);
                case 6:
                    return JadeProfile(kind, Profile(.01f,-.14f, .42f,-.14f, .49f,-.078f, .50f,.075f, .43f,.14f,
                        .29f,.17f, .01f,.17f), depth: .47f, rectangular: true, lobes: 6, relief: .062f);
                case 7: return JadeLoop(kind, true);
                case 8:
                    return JadeProfile(kind, Profile(.01f,-.23f, .31f,-.23f, .44f,-.185f, .47f,-.12f, .34f,-.075f,
                        .34f,.074f, .46f,.116f, .45f,.185f, .34f,.224f, .01f,.224f), depth: .89f);
                case 9: return JadeSprig(kind, false);
                case 10:
                    return JadeProfile(kind, Profile(.01f,-.23f, .34f,-.23f, .46f,-.16f, .49f,-.045f, .41f,.13f,
                        .28f,.26f, .01f,.31f), depth: .78f, lobes: 10, relief: .07f);
                case 11:
                    return JadeProfile(kind, Profile(.01f,-.26f, .28f,-.26f, .33f,-.19f, .39f,-.079f, .49f,.15f,
                        .44f,.20f, .35f,.22f, .21f,.23f, .01f,.235f), lobes: 8, relief: .10f);
                case 12: return JadeLeaf();
                case 13:
                    return JadeProfile(kind, Profile(.01f,-.35f, .42f,-.35f, .49f,-.27f, .50f,-.075f, .49f,.25f,
                        .41f,.34f, .01f,.34f), depth: .93f, rectangular: true);
                case 14:
                    return JadeProfile(kind, Profile(.01f,-.43f, .25f,-.38f, .42f,-.27f, .47f,-.11f,
                        .38f,.12f, .24f,.31f, .09f,.43f, .01f,.46f), lobes: 6, relief: .08f);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static Shape JadeProfile(int kind, Vector3[] profile, float depth = 1, bool rectangular = false, int lobes = 0, float relief = 0)
        {
            profile[0].x = 0;
            profile[profile.Length - 1].x = 0;
            Vector3 Point(float u, float v)
            {
                var p = rectangular ? RoundedRectangle(profile, u, v, depth) : Turn(profile, u, v, 1, depth);
                if (lobes > 0) p = Flute(p, u, v, lobes, relief);
                return p;
            }
            JadeSample Paint(float u, float v, Vector3 p)
            {
                JadeSample paint = JadePaint(p, kind);
                switch (kind)
                {
                    case 0:
                        return paint.BlendWith(Finish(JadeBrass, .48f), 0f, 1f, .78f * Band(p.y, -.211f, .018f));
                    case 2:
                        return paint.BlendWith(Finish(JadeIvory, .40f), .18f, 0f, .73f * Band(p.y, -.35f, .031f));
                    case 5:
                        float rim = Band(new Vector2(p.x, p.z / depth).magnitude, .42f, .033f);
                        return paint.BlendWith(Finish(JadeBrass, .90f), 0f, 1f, rim * .56f);
                    case 8:
                        return paint.BlendWith(Finish(JadeIvory, .42f), .21f, 0f, .85f * Band(p.y, 0, .108f));
                    case 11:
                        return paint.BlendWith(Finish(JadeIvory, .51f), .31f, 0f,
                            Mathf.SmoothStep(0, .88f, Mathf.InverseLerp(.07f, .19f, p.y)));
                    default: return paint;
                }
            }
            return Assemble(new[] { JadeSurface(40, 24, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape JadeLoop(int kind, bool square)
        {
            Vector3 Point(float u, float v)
            {
                float angle = u * Tau;
                float tube = v * Tau;
                float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                float corner = square ? Mathf.Pow(Mathf.Pow(x, 6) + Mathf.Pow(z, 6), -1f / 6) : 1;
                float radius = (.37f + .108f * Mathf.Cos(tube)) * corner;
                return new Vector3(x * radius, Mathf.Sin(tube) * (square ? .062f : .129f), z * radius);
            }
            JadeSample Paint(float u, float v, Vector3 p)
            {
                JadeSample jade = JadePaint(p, kind);
                float inside = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.20f, -.60f, Mathf.Cos(v * Tau)));
                return square ? jade.BlendWith(Finish(JadeBrass, .86f), 0f, 1f, inside) : jade;
            }
            return Assemble(new[] { JadeSurface(48, 18, Point, Paint, true, true) }, Sections(8, Point));
        }

        private static Shape JadeSprig(int kind, bool arc)
        {
            Vector3 Center(float t)
            {
                if (!arc) return new Vector3((t - .5f) * 1.55f, 0, .07f * Mathf.Sin(t * Mathf.PI));
                float a = (t - .5f) * 3.6f;
                return new Vector3(.52f * Mathf.Sin(a), 0, .47f * (Mathf.Cos(a) - .5f));
            }
            float Radius(float t)
            {
                float end = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), arc ? .70f : .22f);
                float ribs = arc ? 1 : .92f + .08f * Mathf.Cos(t * Tau * 5);
                return .016f + (arc ? .17f : .19f) * end * ribs;
            }
            Vector3 Point(float u, float v) => Tube(Center, Radius, u, v, arc ? .86f : .94f);
            JadeSample Paint(float u, float v, Vector3 p)
            {
                JadeSample jade = JadePaint(p, kind);
                if (arc) return jade.BlendWith(Finish(JadeBrass, .72f), 0f, 1f, Band(u, .11f, .047f) * .79f);
                float joints = Mathf.Clamp01((1 - Mathf.Cos(u * Tau * 5)) * .5f);
                return jade.BlendWith(Finish(JadeIvory, .44f), .25f, 0f, Mathf.Pow(joints, 12) * .56f);
            }
            return Assemble(new[] { JadeSurface(48, 16, Point, Paint, false, true) }, Sections(5, Point));
        }

        private static Shape JadeStone()
        {
            var profile = Profile(0,-.27f, .64f,-.25f, .94f,-.16f, 1f,.065f, .87f,.20f, .49f,.27f, 0,.29f);
            var outline = new[]
            {
                new Vector2(.29f, -.35f), new Vector2(.29f, .36f), new Vector2(-.12f, .45f),
                new Vector2(-.42f, .27f), new Vector2(-.54f, -.06f), new Vector2(-.34f, -.34f),
                new Vector2(-.06f, -.44f)
            };
            Vector3 Point(float u, float v)
            {
                float edge = u * outline.Length;
                int first = Mathf.Min((int)edge, outline.Length - 1);
                var point = Vector2.Lerp(outline[first], outline[(first + 1) % outline.Length], edge - first);
                var row = Turn(profile, 0, v);
                return new Vector3(point.x * row.x + row.y * .19f, row.y, point.y * row.x);
            }
            JadeSample Paint(float u, float v, Vector3 p)
            {
                JadeSample rind = JadePaint(p, 4);
                if (u > 1f / outline.Length || v < .19f || v > .79f) return rind;
                float streak = .22f + .20f * Mathf.Sin(p.z * 22 - p.y * 12);
                return new JadeSample(Finish(Color.Lerp(JadeCut, JadeIvory, streak), .77f), .89f);
            }
            return Assemble(new[] { JadeSurface(56, 20, Point, Paint, true) }, new[] { Surface(14, 8, Point, null, true) });
        }

        private static Shape JadeLeaf()
        {
            Vector3 Center(float t) => new Vector3(t - .5f, .05f * Mathf.Sin(t * Mathf.PI), t * t * .18f);
            float Radius(float t) => .008f + .20f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .94f);
            Vector3 Point(float u, float v) => Tube(Center, Radius, u, v, .76f);
            JadeSample Paint(float u, float v, Vector3 p)
            {
                float vein = Band(Mathf.Cos(v * Tau), 0, .28f);
                return JadePaint(p, 12).BlendWith(Finish(JadeIvory, .45f), .27f, 0f, vein * .61f);
            }
            return Assemble(new[] { JadeSurface(36, 22, Point, Paint, false, true) },
                new[] { Surface(12, 10, Point, null, false, true) });
        }

        private static Mesh JadeSurface(int columns, int rows, Func<float, float, Vector3> point,
            Func<float, float, Vector3, JadeSample> paint, bool wrapU, bool wrapV = false)
        {
            int width = columns + 1;
            var optics = new Vector2[width * (rows + 1)];
            Color Paint(float u, float v, Vector3 p)
            {
                JadeSample sample = paint(u, v, p);
                int index = Mathf.RoundToInt(v * rows) * width + Mathf.RoundToInt(u * columns);
                // COLOR retains the visible palette and gloss. TEXCOORD1 adds transmission and metal.
                optics[index] = sample.Optics;
                return sample.Paint;
            }
            var mesh = Surface(columns, rows, point, Paint, wrapU, wrapV);
            if (wrapU && !wrapV)
            {
                // A collapsed profile ring has no circumferential derivative at either pole.
                Vector3[] normals = mesh.normals;
                for (int column = 0; column < width; column++)
                {
                    int top = rows * width + column;
                    if (normals[column].sqrMagnitude < .5f) normals[column] = Vector3.down;
                    if (normals[top].sqrMagnitude < .5f) normals[top] = Vector3.up;
                }
                mesh.normals = normals;
            }
            mesh.uv2 = optics;
            return mesh;
        }

        private static JadeSample JadePaint(Vector3 p, int kind)
        {
            var finish = JadeFinishes[kind];
            float swirl = 9 * p.x + 14 * p.y + 10 * p.z + 1.3f * Mathf.Sin(6 * p.z - 3 * p.x);
            float cloud = (.66f * Mathf.Sin(swirl) + .34f * Mathf.Sin(23 * p.x - 11 * p.y - 17 * p.z)) * finish.Veins;
            float tint = Mathf.Clamp01(.49f + p.y * .22f + cloud);
            return new JadeSample(Finish(Color.Lerp(finish.Deep, finish.Light, tint), finish.Gloss),
                finish.Transmission, finish.Metallic);
        }

        private static float Band(float value, float center, float width) =>
            1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(width * .35f, width, Mathf.Abs(value - center)));
    }
}
