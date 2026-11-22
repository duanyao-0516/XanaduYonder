using System;
using System.Collections.Generic;
using UnityEngine;

namespace XanaduYonder.Runtime.BakeryBasket
{
    internal static partial class BakeryFood
    {
        internal static readonly string[] ProduceNames =
        {
            "Watermelon slice", "Banana bunch", "Pineapple", "Apple", "Eggplant", "Sweet corn",
            "Mushroom", "Pomegranate", "Grape bunch", "Carrot", "Pear", "Orange wedge",
            "Bell pepper", "Cherries", "Garlic"
        };

        private static readonly Color ProduceLeafDark = UiFactory.Hex("397644");
        private static readonly Color ProduceLeafLight = UiFactory.Hex("A0C565");
        private static readonly Color ProduceStem = UiFactory.Hex("805B31");

        internal static Shape CreateProduce(int kind)
        {
            switch (kind)
            {
                case 0: return ProduceSlice(true);
                case 1: return ProduceBananas();
                case 2: return ProducePineapple();
                case 3: return ProduceApple();
                case 4: return ProduceEggplant();
                case 5: return ProduceCorn();
                case 6: return ProduceMushroom();
                case 7: return ProducePomegranate();
                case 8: return ProduceGrapes();
                case 9: return ProduceCarrot();
                case 10: return ProducePear();
                case 11: return ProduceSlice(false);
                case 12: return ProducePepper();
                case 13: return ProduceCherries();
                case 14: return ProduceGarlic();
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static Shape ProduceSlice(bool watermelon)
        {
            float spread = watermelon ? 1.48f : 1.19f;
            float depth = watermelon ? 1.01f : .92f;
            float halfThickness = watermelon ? .25f : .265f;
            Color rind = UiFactory.Hex(watermelon ? "2F7944" : "E4771C");
            Color rindLight = UiFactory.Hex(watermelon ? "78B44A" : "FFA938");
            Color pith = UiFactory.Hex(watermelon ? "EDF1C5" : "FFF2C6");
            Color flesh = UiFactory.Hex(watermelon ? "E9435A" : "F27C15");
            Color fleshLight = UiFactory.Hex(watermelon ? "FF9EAA" : "FFC963");
            Color seed = UiFactory.Hex("3A2529");
            Vector3[] edge = Profile(.002f,-.72f, .85f,-1, .95f,-.95f, 1,-.78f,
                1,.78f, .95f,.95f, .85f,1, .002f,.72f, .002f,-.72f);

            Vector3 Point(float u, float v)
            {
                float angle = Mathf.Lerp(spread, -spread, u);
                var p = Turn(edge, 0, v);
                return new Vector3(Mathf.Sin(angle) * p.x, p.y * halfThickness, Mathf.Cos(angle) * p.x * depth);
            }

            Color Paint(float u, float v, Vector3 p)
            {
                float radius = new Vector2(p.x, p.z / depth).magnitude;
                float edgeWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.925f, .98f, radius));
                float pithWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.83f, .92f, radius));
                float variation = .5f + .26f * Mathf.Sin(radius * 41 + u * 22) + .20f * Mathf.Sin(u * 71 - radius * 29);
                Color pulp = Color.Lerp(fleshLight, flesh, .32f + .55f * radius + .12f * variation);
                if (watermelon)
                {
                    float row = radius < .53f ? 0 : 1;
                    float step = u * 11 + row * .48f;
                    float cell = Mathf.Round(step);
                    float across = (step - cell) * Mathf.Lerp(.45f, 1, radius);
                    float along = (radius - (.43f + row * .24f + .035f * Mathf.Sin(cell * 2.2f))) * 3.7f;
                    float distance = new Vector2(across, along).magnitude;
                    float seedWeight = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .26f, distance));
                    pulp = Color.Lerp(pulp, seed, seedWeight * Mathf.InverseLerp(0, .09f, Mathf.Min(u, 1 - u)));
                }
                else
                {
                    float segment = .5f + .5f * Mathf.Cos(u * Tau * 7);
                    float membrane = Mathf.Max(Mathf.Pow(segment, 16), 1 - Mathf.InverseLerp(.035f, .16f, radius));
                    pulp = Color.Lerp(pulp, pith, .82f * membrane);
                }
                float stripe = .5f + .5f * Mathf.Sin(u * Tau * 12 + 1.2f * Mathf.Sin(p.y * 21));
                Color skin = Color.Lerp(rind, rindLight, watermelon ? Mathf.Pow(stripe, 2.4f) : .45f + variation * .4f);
                return Finish(Color.Lerp(Color.Lerp(pulp, pith, pithWeight), skin, edgeWeight), Mathf.Lerp(.52f, .28f, edgeWeight));
            }

            Mesh End(bool right)
            {
                float angle = right ? spread : -spread;
                Vector3 Side(float u, float v)
                {
                    float radius = Mathf.Lerp(.002f, 1, right ? u : 1 - u);
                    float height = radius <= .85f ? Mathf.Lerp(.72f, 1, Mathf.InverseLerp(.002f, .85f, radius)) :
                        Mathf.Lerp(1, .95f, Mathf.InverseLerp(.85f, .95f, radius)) - .17f * Mathf.InverseLerp(.95f, 1, radius);
                    return new Vector3(Mathf.Sin(angle) * radius, (2 * v - 1) * halfThickness * height,
                        Mathf.Cos(angle) * radius * depth);
                }
                return Surface(20, 5, Side, (_, v, p) => Paint(right ? 0 : 1, v, p));
            }

            return Assemble(new[] { Surface(84, 56, Point, Paint, false, true), End(false), End(true) },
                new[] { Surface(12, 8, Point, null, false, true) });
        }

        private static Shape ProduceBananas()
        {
            Color gold = UiFactory.Hex("F5C13B");
            Color yellow = UiFactory.Hex("FFEB7B");
            Color green = UiFactory.Hex("A9B95C");
            Color tips = UiFactory.Hex("7B6440");

            Vector3 Center(float t) => new Vector3((t - .5f) * 1.54f, .04f * Mathf.Sin(t * Mathf.PI), .50f * Mathf.Sin(t * Mathf.PI));
            float Radius(float t) => .021f + .122f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .42f);
            var rendered = new Mesh[4];
            for (int finger = 0; finger < 3; finger++)
            {
                float offset = finger - 1;
                Vector3 FingerCenter(float t)
                {
                    float spread = Mathf.SmoothStep(.11f, 1, t);
                    return Center(t) + new Vector3(-.047f * offset * offset * t, offset * .128f * spread,
                        offset * .206f * spread + .037f * offset * t);
                }
                float FingerRadius(float t) => Radius(t) * (1 - .065f * offset);
                Vector3 Point(float u, float v)
                {
                    Vector3 round = Tube(FingerCenter, FingerRadius, u, v, .88f);
                    return round + (round - FingerCenter(u)) * (.048f * Mathf.Cos(v * Tau * 5));
                }
                Color Paint(float u, float v, Vector3 p)
                {
                    float rib = .52f + .17f * Mathf.Cos(v * Tau) + .12f * Mathf.Cos(v * Tau * 5);
                    Color peel = Color.Lerp(gold, yellow, rib + .09f * offset);
                    float end = Mathf.Min(u, 1 - u);
                    peel = Color.Lerp(green, peel, Mathf.InverseLerp(.025f, .21f, end));
                    return Finish(Color.Lerp(tips, peel, Mathf.InverseLerp(0, .045f, end)), .33f);
                }
                rendered[finger] = Surface(32, 20, Point, Paint, false, true);
            }
            var branchStart = Center(0) - new Vector3(.08f, -.035f, .035f);
            rendered[3] = ProduceStalk(branchStart, Center(.065f), .064f, green);

            Vector3 Contact(float u, float v)
            {
                float spread = Mathf.SmoothStep(.11f, 1, u);
                float radius = Radius(u) + .24f * spread;
                return Tube(Center, _ => radius, u, v, (Radius(u) + .135f * spread) / radius);
            }
            return Assemble(rendered, Sections(5, Contact));
        }

        private static Shape ProducePineapple()
        {
            Color amber = UiFactory.Hex("B78238");
            Color gold = UiFactory.Hex("E9AC51");
            Color olive = UiFactory.Hex("889347");
            Color eye = UiFactory.Hex("765A35");
            Vector3[] profile = Profile(.002f,-.53f, .26f,-.53f, .36f,-.44f, .41f,-.25f, .415f,-.05f,
                .38f,.15f, .30f,.29f, .20f,.335f, .002f,.34f);
            float Scales(float u, float y)
            {
                float diagonal = u * 10;
                float height = y * 5.6f;
                return Mathf.Abs(Mathf.Sin(Mathf.PI * (diagonal + height)) * Mathf.Sin(Mathf.PI * (diagonal - height)));
            }
            Vector3 Point(float u, float v)
            {
                var p = Turn(profile, u, v, 1, .92f);
                float scale = .94f + .15f * Scales(u, p.y);
                return new Vector3(p.x * scale, p.y, p.z * scale);
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float scales = Scales(u, p.y);
                Color peel = Color.Lerp(amber, gold, .28f + .68f * scales);
                float shoulder = .72f * Mathf.InverseLerp(-.11f, .30f, p.y);
                peel = Color.Lerp(peel, olive, shoulder);
                float center = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.80f, .99f, scales));
                return Finish(Color.Lerp(peel, eye, .65f * center), .26f);
            }
            var mesh = new List<Mesh> { Surface(60, 34, Point, Paint, true) };
            for (int layer = 0; layer < 2; layer++)
                for (int i = 0; i < 5; i++)
                {
                    float angle = (i + layer * .48f) * Tau / 5;
                    var ray = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    mesh.Add(ProduceLeaf(Vector3.up * .30f + ray * .041f,
                        Vector3.up * (.69f + layer * .16f + .036f * Mathf.Sin(i * 3)) + ray * (.26f - .13f * layer),
                        .091f - .014f * layer, .031f, layer % 2 == 0));
                }
            Mesh baseContact = Surface(12, 8, (u, v) => Turn(profile, u, v, 1.03f, .96f), null, true);
            Mesh crownContact = ProduceOval(new Vector3(0, .48f, 0), new Vector3(.17f, .20f, .16f), Color.white, Color.white, 0, 9, 5);
            return Assemble(mesh.ToArray(), new[] { baseContact, crownContact });
        }

        private static Shape ProduceApple()
        {
            Vector3[] profile = Profile(.006f,-.38f, .20f,-.427f, .35f,-.375f, .44f,-.25f, .499f,-.05f,
                .51f,.18f, .46f,.33f, .32f,.415f, .18f,.406f, .006f,.315f);
            Color scarlet = UiFactory.Hex("A81926");
            Color red = UiFactory.Hex("EE332C");
            Color blush = UiFactory.Hex("F9884C");
            Vector3 Point(float u, float v) => Flute(Turn(profile, u, v, 1, .93f), u, v, 5, .063f);
            Color Paint(float u, float v, Vector3 p)
            {
                float falloff = .48f + .25f * Mathf.Sin(u * Tau + .35f) + .18f * Mathf.InverseLerp(-.25f, .38f, p.y);
                float streak = Mathf.Pow(.5f + .5f * Mathf.Cos(u * Tau * 14 + .9f * Mathf.Sin(v * 10)), 7);
                return Finish(Color.Lerp(Color.Lerp(scarlet, red, falloff), blush, streak * .19f), .79f);
            }
            var stem = ProduceStalk(new Vector3(0,.325f,0), new Vector3(.05f,.57f,-.016f), .032f, ProduceStem);
            var leaf = ProduceLeaf(new Vector3(.026f,.48f,0), new Vector3(.31f,.525f,.135f), .089f, .047f, true);
            return Assemble(new[] { Surface(48, 25, Point, Paint, true), stem, leaf },
                new[] { Surface(12, 9, Point, null, true) });
        }

        private static Shape ProduceEggplant()
        {
            Vector3[] profile = Profile(.002f,-.67f, .19f,-.64f, .32f,-.54f, .373f,-.39f, .352f,-.20f,
                .29f,.02f, .23f,.23f, .19f,.40f, .135f,.52f, .002f,.56f);
            Color plum = UiFactory.Hex("221431");
            Color purple = UiFactory.Hex("763F84");
            Color calyx = UiFactory.Hex("87A755");
            Vector3 Point(float u, float v)
            {
                var p = Turn(profile, u, v, 1, .87f);
                p.x += .18f * Mathf.Pow(Mathf.InverseLerp(-.37f, .52f, p.y), 1.65f);
                return p;
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float sheen = .23f + .48f * Mathf.Pow(.5f + .5f * Mathf.Cos(u * Tau - .75f), 1.7f);
                float sepals = .365f + .095f * Mathf.Cos(u * Tau * 6 + .4f);
                float cap = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(sepals - .026f, sepals + .025f, p.y));
                return Finish(Color.Lerp(Color.Lerp(plum, purple, sheen), calyx, cap), Mathf.Lerp(.89f, .29f, cap));
            }
            var stem = ProduceStalk(new Vector3(.18f,.51f,0), new Vector3(.28f,.722f,-.078f), .051f, calyx);
            return Assemble(new[] { Surface(48, 28, Point, Paint, true), stem },
                new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape ProduceCorn()
        {
            Color shade = UiFactory.Hex("D7A64C");
            Color kernel = UiFactory.Hex("FCE0A0");
            Color tip = UiFactory.Hex("FFF1C6");
            Vector3[] profile = Profile(.002f,-.57f, .16f,-.55f, .245f,-.44f, .256f,-.12f, .24f,.18f,
                .211f,.40f, .143f,.55f, .002f,.59f);
            float Kernels(float u, float y)
            {
                float along = .5f + .5f * Mathf.Cos((y * 9.5f + .045f * Mathf.Sin(u * Tau * 5)) * Tau);
                float across = .5f + .5f * Mathf.Cos(u * Tau * 11);
                return Mathf.Pow(along * across, .41f);
            }
            Vector3 Point(float u, float v)
            {
                var p = Turn(profile, u, v, 1, .9f);
                float swell = .94f + .19f * Kernels(u, p.y);
                return new Vector3(p.x * swell, p.y, p.z * swell);
            }
            Color Paint(float u, float v, Vector3 p)
            {
                var color = Color.Lerp(shade, kernel, .16f + .84f * Kernels(u, p.y));
                return Finish(Color.Lerp(color, tip, Mathf.InverseLerp(.32f, .58f, p.y) * .57f), .57f);
            }
            var husk1 = ProduceLeaf(new Vector3(.035f,-.57f,.015f), new Vector3(.257f,.25f,.115f), .132f, .10f, true);
            var husk2 = ProduceLeaf(new Vector3(-.017f,-.57f,-.02f), new Vector3(-.263f,.072f,-.126f), .135f, .109f, false);
            return Assemble(new[] { Surface(55, 54, Point, Paint, true), husk1, husk2 },
                new[] { Surface(12, 8, (u, v) => Turn(profile, u, v, 1.05f, .96f), null, true) });
        }

        private static Shape ProduceMushroom()
        {
            Vector3[] top = Profile(.002f,.009f, .24f,.016f, .366f,-.041f, .47f,-.055f, .51f,.025f,
                .48f,.145f, .36f,.245f, .20f,.296f, .002f,.321f);
            Vector3[] stalk = Profile(.002f,-.387f, .13f,-.38f, .18f,-.297f, .145f,-.15f, .119f,-.016f, .002f,.05f);
            Color chestnut = UiFactory.Hex("AC7955");
            Color beige = UiFactory.Hex("F0D1A9");
            Color gills = UiFactory.Hex("A57F66");
            Color cream = UiFactory.Hex("FAE5CD");
            Vector3 Cap(float u, float v) => Flute(Turn(top, u, v, 1, .91f), u + .07f, v, 9, .042f);
            Color CapPaint(float u, float v, Vector3 p)
            {
                float fibers = .5f + .5f * Mathf.Cos(u * Tau * 16);
                Color underside = Color.Lerp(gills, cream, .16f + .59f * fibers);
                float dapple = .53f + .08f * Mathf.Sin(u * Tau * 7 + v * 10) + .26f * Mathf.InverseLerp(.07f, .31f, p.y);
                return Finish(Color.Lerp(underside, Color.Lerp(chestnut, beige, dapple), Mathf.InverseLerp(.37f, .51f, v)), .25f);
            }
            Vector3 Stalk(float u, float v) => Turn(stalk, u, v, 1, .85f) + new Vector3(.032f - .070f * v, 0, .017f);
            Color StalkPaint(float u, float v, Vector3 p) => Finish(Color.Lerp(beige, cream,
                .38f + .4f * v + .16f * Mathf.Sin(u * Tau * 7)), .16f);
            return Assemble(new[] { Surface(64, 23, Cap, CapPaint, true), Surface(28, 14, Stalk, StalkPaint, true) },
                new[] { Surface(12, 8, Cap, null, true), Surface(10, 6, Stalk, null, true) });
        }

        private static Shape ProducePomegranate()
        {
            Vector3[] profile = Profile(.002f,-.41f, .25f,-.41f, .39f,-.33f, .455f,-.19f, .462f,.01f,
                .43f,.20f, .31f,.338f, .125f,.39f, .002f,.38f);
            Color berry = UiFactory.Hex("7E2944");
            Color pink = UiFactory.Hex("C64C66");
            Color gold = UiFactory.Hex("DA956C");
            Color crown = UiFactory.Hex("8A4B57");
            Vector3 Point(float u, float v) => Flute(Turn(profile, u, v, 1, .92f), u + v * .06f, v, 6, .078f);
            Color Paint(float u, float v, Vector3 p)
            {
                float blush = Mathf.Pow(.5f + .5f * Mathf.Cos(u * Tau - 2), 4) * .54f;
                float mottle = .57f + .14f * Mathf.Sin(u * Tau * 9 - v * 12) + .08f * Mathf.Cos(v * 26);
                return Finish(Color.Lerp(Color.Lerp(berry, pink, mottle), gold, blush), .37f);
            }
            Vector3[] cup = Profile(.002f,.34f, .071f,.34f, .09f,.405f, .09f,.476f, .074f,.473f, .046f,.421f, .002f,.414f);
            Vector3 Crown(float u, float v)
            {
                var p = Turn(cup, u, v);
                float tips = Mathf.Pow(.5f + .5f * Mathf.Cos(u * Tau * 6), 2.3f);
                p.y += .104f * tips * Mathf.Clamp01(1 - Mathf.Abs(v - .55f) * 3.2f);
                return p;
            }
            Color CrownPaint(float u, float v, Vector3 p) => Finish(Color.Lerp(crown, gold, .45f * Mathf.InverseLerp(.42f, .56f, p.y)), .19f);
            return Assemble(new[] { Surface(42, 22, Point, Paint, true), Surface(36, 16, Crown, CrownPaint, true) },
                new[] { Surface(12, 8, Point, null, true), Surface(10, 6, Crown, null, true) });
        }

        private static Shape ProduceGrapes()
        {
            Color shade = UiFactory.Hex("4B3E87");
            Color blush = UiFactory.Hex("9F87CF");
            var mesh = new List<Mesh>();
            for (int layer = 0; layer < 3; layer++)
            {
                int count = layer == 0 ? 2 : 5;
                float spread = .108f + .062f * layer;
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + layer * .40f) * Tau / count;
                    var center = new Vector3(Mathf.Cos(angle) * spread, -.28f + layer * .212f + .047f * Mathf.Sin(i * 3 + layer),
                        Mathf.Sin(angle) * spread);
                    float radius = .138f + .014f * Mathf.Cos(i * 3 + layer);
                    Color skin = Color.Lerp(shade, blush, .18f + .26f * (.5f + .5f * Mathf.Sin(i * 2.2f + layer * 3)));
                    mesh.Add(ProduceOval(center, new Vector3(radius, radius * 1.12f, radius), skin, blush, .47f));
                }
            }
            mesh.Add(ProduceOval(new Vector3(.012f,.167f,.013f), new Vector3(.154f,.166f,.148f), shade, blush, .47f));
            mesh.Add(ProduceStalk(new Vector3(0,.15f,0), new Vector3(.05f,.40f,-.046f), .036f, ProduceStem));
            mesh.Add(ProduceLeaf(new Vector3(.02f,.33f,0), new Vector3(.257f,.46f,-.074f), .09f, .037f, true));
            Vector3[] cluster = Profile(.002f,-.40f, .18f,-.35f, .245f,-.21f, .302f,.006f, .342f,.17f, .28f,.28f, .002f,.32f);
            return Assemble(mesh.ToArray(), new[] { Surface(12, 8, (u, v) => Turn(cluster, u, v, 1, .95f), null, true) });
        }

        private static Shape ProduceCarrot()
        {
            Vector3[] profile = Profile(.002f,-.72f, .053f,-.61f, .103f,-.39f, .161f,-.14f, .224f,.12f,
                .264f,.32f, .26f,.48f, .221f,.55f, .14f,.576f, .002f,.579f);
            Color shade = UiFactory.Hex("DC6028");
            Color orange = UiFactory.Hex("FFA044");
            Color groove = UiFactory.Hex("BA6733");
            float Rings(float u, float v)
            {
                float band = Mathf.Pow(.5f + .5f * Mathf.Cos(v * Tau * 8 + .8f * Mathf.Sin(u * Tau * 3)), 15);
                return band * Mathf.Pow(.5f + .5f * Mathf.Cos(u * Tau * 3 - v * Tau * 5), 2);
            }
            Vector3 Point(float u, float v)
            {
                var p = Flute(Turn(profile, u, v, 1, .91f), u, v, 5, .052f);
                float scale = 1 - .062f * Rings(u, v);
                return new Vector3(p.x * scale + .119f * (1 - v) * (1 - v), p.y, p.z * scale);
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float tint = .49f + .31f * Mathf.Sin(u * Tau + .4f) + .16f * v;
                return Finish(Color.Lerp(Color.Lerp(shade, orange, tint), groove, Rings(u, v) * .55f), .24f);
            }
            var mesh = new List<Mesh> { Surface(39, 35, Point, Paint, true) };
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Tau / 5;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                mesh.Add(ProduceLeaf(Vector3.up * .555f + direction * .034f,
                    Vector3.up * (.78f + .06f * Mathf.Cos(i * 2.3f)) + direction * (.155f + .04f * Mathf.Sin(i * 2)),
                    .041f, .035f, i % 2 == 0));
            }
            return Assemble(mesh.ToArray(), new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape ProducePear()
        {
            Vector3[] profile = Profile(.002f,-.46f, .20f,-.457f, .34f,-.39f, .395f,-.23f, .378f,-.07f, .30f,.085f,
                .211f,.24f, .15f,.39f, .106f,.482f, .002f,.505f);
            Color olive = UiFactory.Hex("769936");
            Color lime = UiFactory.Hex("C7D36B");
            Color blush = UiFactory.Hex("E5AE67");
            Vector3 Point(float u, float v)
            {
                var p = Flute(Turn(profile, u, v, 1, .92f), u + .17f, v, 5, .034f);
                p.x -= .11f * v * v;
                return p;
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float warmth = Mathf.Pow(.5f + .5f * Mathf.Cos((u - .17f) * Tau), 4) * .58f;
                float fleck = Mathf.Pow(.5f + .5f * Mathf.Sin(u * Tau * 17 + v * 33), 7) *
                    Mathf.Pow(.5f + .5f * Mathf.Cos(v * Tau * 13), 5);
                Color skin = Color.Lerp(olive, lime, .54f + .19f * v - .25f * fleck);
                return Finish(Color.Lerp(skin, blush, warmth), .36f);
            }
            var stem = ProduceStalk(new Vector3(-.105f,.49f,0), new Vector3(-.184f,.665f,.032f), .028f, ProduceStem);
            var leaf = ProduceLeaf(new Vector3(-.149f,.61f,.018f), new Vector3(.133f,.653f,-.095f), .065f, .037f, false);
            return Assemble(new[] { Surface(48, 28, Point, Paint, true), stem, leaf },
                new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape ProducePepper()
        {
            Vector3[] profile = Profile(.002f,-.343f, .19f,-.39f, .31f,-.355f, .365f,-.228f, .394f,-.043f,
                .414f,.155f, .342f,.291f, .20f,.323f, .002f,.251f);
            Color green = UiFactory.Hex("18733D");
            Color lightGreen = UiFactory.Hex("65AD49");
            Vector3 Point(float u, float v) => Flute(Turn(profile, u, v, 1, .88f), u + .037f * v, v, 4, .177f);
            Color Paint(float u, float v, Vector3 p)
            {
                float lobes = .5f + .5f * Mathf.Cos((u + .037f * v) * Tau * 4);
                return Finish(Color.Lerp(green, lightGreen, .29f + .39f * lobes + .15f * Mathf.Sin(u * Tau)), .84f);
            }
            var stem = ProduceStalk(new Vector3(0,.252f,0), new Vector3(.101f,.504f,-.073f), .051f, ProduceLeafDark);
            return Assemble(new[] { Surface(40, 24, Point, Paint, true), stem },
                new[] { Surface(12, 8, Point, null, true) });
        }

        private static Shape ProduceCherries()
        {
            Color burgundy = UiFactory.Hex("650D2B");
            Color scarlet = UiFactory.Hex("BD2454");
            var left = new Vector3(-.194f,-.115f,.019f);
            var right = new Vector3(.197f,-.07f,-.025f);
            var leftSize = new Vector3(.226f,.223f,.207f);
            var rightSize = new Vector3(.215f,.218f,.215f);
            var fork = new Vector3(.024f,.385f,-.04f);
            var mesh = new[]
            {
                ProduceOval(left, leftSize, burgundy, scarlet, .76f),
                ProduceOval(right, rightSize, scarlet, UiFactory.Hex("FE8876"), .74f),
                ProduceStalk(left + Vector3.up * .195f, fork, .022f, ProduceLeafDark),
                ProduceStalk(right + Vector3.up * .193f, fork, .020f, ProduceLeafLight),
                ProduceLeaf(fork, new Vector3(.20f,.413f,.053f), .051f, .022f, true)
            };
            return Assemble(mesh, new[]
            {
                ProduceOval(left, leftSize, Color.white, Color.white, 0, 10, 6),
                ProduceOval(right, rightSize, Color.white, Color.white, 0, 10, 6)
            });
        }

        private static Shape ProduceGarlic()
        {
            Vector3[] profile = Profile(.002f,-.355f, .21f,-.351f, .36f,-.292f, .425f,-.17f, .411f,-.035f, .336f,.09f,
                .224f,.191f, .13f,.31f, .081f,.397f, .002f,.402f);
            Color violet = UiFactory.Hex("D1ACB7");
            Color pearl = UiFactory.Hex("FCEBDD");
            Color cork = UiFactory.Hex("C79E72");
            Vector3 Point(float u, float v) => Flute(Turn(profile, u, v, 1, .95f), u + .04f * v, v, 7, .136f);
            Color Paint(float u, float v, Vector3 p)
            {
                float clove = .5f + .5f * Mathf.Cos((u + .04f * v) * Tau * 7);
                float fibers = .5f + .5f * Mathf.Cos(u * Tau * 19 + 1.4f * Mathf.Sin(v * 5));
                Color skin = Color.Lerp(violet, pearl, .33f + .48f * clove + .14f * fibers);
                float baseColor = 1 - Mathf.InverseLerp(-.35f, -.302f, p.y);
                float tipColor = Mathf.InverseLerp(.34f, .40f, p.y);
                return Finish(Color.Lerp(skin, cork, .83f * Mathf.Max(baseColor, tipColor)), .23f);
            }
            return Assemble(new[] { Surface(49, 24, Point, Paint, true) }, new[] { Surface(12, 8, Point, null, true) });
        }

        private static Mesh ProduceOval(Vector3 center, Vector3 size, Color baseColor, Color highlight,
            float gloss, int columns = 16, int rows = 10)
        {
            Vector3 Point(float u, float v)
            {
                float angle = u * Tau;
                float latitude = (v - .5f) * (Mathf.PI - .008f);
                float radius = Mathf.Cos(latitude);
                return center + Vector3.Scale(size, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(latitude), Mathf.Sin(angle) * radius));
            }
            Color Paint(float u, float v, Vector3 p) => Finish(Color.Lerp(baseColor, highlight,
                .18f + .58f * Mathf.Pow(.5f + .5f * Mathf.Cos(u * Tau - .45f), 2) * Mathf.Sin(v * Mathf.PI)), gloss);
            return Surface(columns, rows, Point, Paint, true);
        }

        private static Mesh ProduceStalk(Vector3 start, Vector3 end, float width, Color color)
        {
            float length = (end - start).magnitude;
            var rotation = Quaternion.FromToRotation(Vector3.up, end - start);
            Vector3[] profile = Profile(.002f,0, width,0, width * .79f,length * .38f,
                width * .73f,length * .81f, width * .8f,length, .002f,length);
            Vector3 Point(float u, float v)
            {
                var p = Turn(profile, u, v);
                p.x += width * .57f * Mathf.Sin(v * Mathf.PI);
                return start + rotation * p;
            }
            Color Paint(float u, float v, Vector3 p) => Finish(Color.Lerp(color, ProduceLeafLight,
                .20f + .14f * Mathf.Cos(u * Tau * 3) + .15f * v), .20f);
            return Surface(12, 8, Point, Paint, true);
        }

        private static Mesh ProduceLeaf(Vector3 start, Vector3 end, float width, float bow, bool light)
        {
            var axis = end - start;
            Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;
            Vector3 normal = Vector3.Cross(axis.normalized, side);
            Vector3 Point(float u, float v)
            {
                float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), .82f);
                float cross = v * Tau;
                float span = Mathf.Cos(cross);
                var bend = normal * (bow * taper * (.83f + .17f * Mathf.Cos(u * Mathf.PI)));
                var edge = side * (span * (.001f + width * taper));
                var fold = normal * (taper * width * (.11f * Mathf.Sin(cross) - .25f * Mathf.Abs(span)));
                return start + axis * u + bend + edge + fold;
            }
            Color Paint(float u, float v, Vector3 p)
            {
                float span = Mathf.Abs(Mathf.Cos(v * Tau));
                float vein = Mathf.Pow(.5f + .5f * Mathf.Cos((u * 7 - span * .69f) * Tau), 12) * (1 - span);
                float midrib = 1 - Mathf.InverseLerp(.07f, .27f, span);
                float tone = (light ? .49f : .28f) + .19f * Mathf.Sin(v * Tau) + .14f * vein + .24f * midrib;
                return Finish(Color.Lerp(ProduceLeafDark, ProduceLeafLight, tone), .29f);
            }
            return Surface(14, 12, Point, Paint, false, true);
        }
    }
}
