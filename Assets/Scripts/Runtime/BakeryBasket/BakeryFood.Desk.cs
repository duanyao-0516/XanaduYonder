using System;
using System.Collections.Generic;
using UnityEngine;

namespace XanaduYonder.Runtime.BakeryBasket
{
    internal static partial class BakeryFood
    {
        internal static readonly string[] DeskNames =
        {
            "Clothbound journal", "Desk bell", "Pencil sharpener", "Washi tape", "Rubber stamp",
            "Sleeved eraser", "Binder clip", "Ink bottle", "Enamel die", "Wooden ruler",
            "Magnifier", "Stapler", "Drawing tack", "Brass paperclip", "Wax seal"
        };

        internal static Shape CreateDesk(int kind)
        {
            switch (kind)
            {
                case 0: return DeskJournal();
                case 1: return DeskBell();
                case 2: return DeskSharpener();
                case 3: return DeskTape();
                case 4: return DeskStamp();
                case 5: return DeskEraser();
                case 6: return DeskBinderClip();
                case 7: return DeskInkBottle();
                case 8: return DeskDie();
                case 9: return DeskRuler();
                case 10: return DeskMagnifier();
                case 11: return DeskStapler();
                case 12: return DeskTack();
                case 13: return DeskPaperclip();
                case 14: return DeskWaxSeal();
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static Shape DeskJournal()
        {
            var prop = new DeskProp();
            var paper = UiFactory.Hex("E4CAA0");
            var cloth = UiFactory.Hex("C45137");
            var weave = UiFactory.Hex("E47751");
            prop.Block(Vector3.zero, new Vector3(1.60f, .41f, 1.14f), .038f, "E4CAA0", .06f, true,
                (_, _, p) => Finish(Color.Lerp(paper, Color.white,
                    .24f + .13f * Mathf.Sin(p.y * 184 + p.x * 2 + p.z * 3)), .06f));
            for (int side = -1; side <= 1; side += 2)
                prop.Block(new Vector3(0, side * .215f, 0), new Vector3(1.70f, .075f, 1.23f),
                    .023f, "C45137", .14f, true, (_, _, p) => Finish(Color.Lerp(cloth, weave,
                        .32f + .09f * Mathf.Cos(p.z * 100) * Mathf.Cos(p.x * 94)), .14f));
            prop.Block(new Vector3(-.738f, 0, 0), new Vector3(.226f, .49f, 1.20f), .043f, "8E2D3D", .14f);
            prop.Block(new Vector3(.49f, 0, 0), new Vector3(.084f, .519f, 1.23f), .027f, "592F45", .19f);
            prop.Block(new Vector3(.02f, .256f, 0), new Vector3(.52f, .017f, .41f), .008f, "F4D8AA", .12f);
            prop.Block(new Vector3(.02f, .266f, .064f), new Vector3(.34f, .012f, .040f), .005f, "AD613B", .10f);
            prop.Block(new Vector3(-.024f, .266f, -.026f), new Vector3(.25f, .012f, .034f), .005f, "AD613B", .10f);
            return prop.Build();
        }

        private static Shape DeskBell()
        {
            var prop = new DeskProp();
            prop.Lathe(Vector3.zero, Profile(.002f,-.35f, .49f,-.35f, .55f,-.32f, .57f,-.27f,
                .553f,-.20f, .47f,-.17f, .002f,-.17f), "485560", .23f, true);
            var shade = UiFactory.Hex("7798A6");
            var glint = UiFactory.Hex("EAF1E9");
            prop.Lathe(Vector3.zero, Profile(.002f,-.215f, .47f,-.215f, .498f,-.17f, .490f,-.07f,
                .464f,.035f, .404f,.125f, .318f,.195f, .205f,.243f, .099f,.254f, .002f,.254f),
                "C8D7DA", .90f, true, paint: (u, v, p) => Finish(Color.Lerp(shade, glint,
                    .38f + .25f * Mathf.Cos((u - .14f) * Tau * 2) + .32f * Mathf.InverseLerp(-.18f, .26f, p.y)), .90f));
            prop.Lathe(Vector3.zero, Profile(.002f,.242f, .086f,.242f, .080f,.298f, .125f,.302f,
                .145f,.327f, .133f,.367f, .002f,.375f), "D36953", .72f, true);
            return prop.Build();
        }

        private static Shape DeskSharpener()
        {
            var prop = new DeskProp();
            var forward = Quaternion.Euler(90, 0, 0);
            var steel = UiFactory.Hex("B8CBCA");
            var bore = UiFactory.Hex("222B35");
            prop.Block(new Vector3(0, -.407f, 0), new Vector3(1.05f, .17f, .89f), .055f, "7F541E", .34f, true);
            prop.Block(new Vector3(0, .003f, 0), new Vector3(.98f, .76f, .78f), .10f, "EAC343", .60f, true);
            prop.Block(new Vector3(0, -.162f, .374f), new Vector3(.73f, .301f, .099f), .033f, "F9E6A5", .39f);
            prop.Block(new Vector3(0, -.18f, .424f), new Vector3(.35f, .051f, .070f), .016f, "574236", .40f);
            prop.Lathe(new Vector3(0, .167f, .394f), Profile(.002f,-.031f, .202f,-.031f, .205f,.008f,
                .177f,.031f, .107f,.031f, .101f,.002f, .002f,.002f), "B8CBCA", .88f, rotation: forward,
                paint: (_, _, p) => Finish(p.x * p.x + (p.y - .167f) * (p.y - .167f) < .011f ? bore : steel, .76f));
            prop.Block(new Vector3(.0f, .090f, -.435f), new Vector3(.13f, .29f, .132f), .032f, "47707A", .71f, true);
            prop.Lathe(new Vector3(0, .224f, -.49f), Profile(.002f,-.09f, .074f,-.09f, .091f,-.059f,
                .091f,.056f, .073f,.081f, .002f,.081f), "D9A054", .41f, true, forward, 10, .065f);
            return prop.Build();
        }

        private static Shape DeskTape()
        {
            var prop = new DeskProp();
            var pink = UiFactory.Hex("C787AC");
            var flower = UiFactory.Hex("F4CDDF");
            var edge = UiFactory.Hex("F3E1CB");
            var core = UiFactory.Hex("AB8666");
            prop.Ring(Vector3.zero, .51f, .265f, .185f, "C787AC", .27f, 8, paint: (u, _, p) =>
            {
                float radius = new Vector2(p.x, p.z).magnitude;
                if (radius < .30f) return Finish(core, .12f);
                if (Mathf.Abs(p.y) > .173f) return Finish(edge, .16f);
                float blossoms = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.54f, .92f,
                    Mathf.Cos(u * Tau * 8) * Mathf.Cos(p.y * 13 + u * Tau * 4)));
                return Finish(Color.Lerp(pink, flower, blossoms), .27f);
            });
            return prop.Build();
        }

        private static Shape DeskStamp()
        {
            var prop = new DeskProp();
            prop.Block(new Vector3(0, -.294f, 0), new Vector3(.98f, .13f, .83f), .031f, "A94C52", .25f, true);
            var ochre = UiFactory.Hex("DD9C54");
            var walnut = UiFactory.Hex("9B582D");
            Color Wood(float u, float v, Vector3 p) => Finish(Color.Lerp(walnut, ochre,
                .62f + .14f * Mathf.Sin(p.y * 68 + p.x * 7 + p.z * 11) + .12f * Mathf.Sin(p.z * 54 + p.x * 4)), .31f);
            prop.Block(new Vector3(0, -.177f, 0), new Vector3(1.04f, .169f, .87f), .054f, "DD9C54", .31f, true, Wood);
            prop.Lathe(Vector3.zero, Profile(.002f,-.115f, .24f,-.115f, .226f,-.064f, .152f,.012f,
                .145f,.104f, .188f,.147f, .305f,.184f, .337f,.259f, .302f,.341f,
                .195f,.392f, .002f,.404f), "DD9C54", .31f, true, paint: Wood);
            prop.Block(new Vector3(0, -.362f, 0), new Vector3(.60f, .029f, .082f), .013f, "672937", .25f);
            prop.Block(new Vector3(0, -.362f, 0), new Vector3(.082f, .029f, .46f), .013f, "672937", .25f);
            return prop.Build();
        }

        private static Shape DeskEraser()
        {
            var prop = new DeskProp();
            prop.Block(Vector3.zero, new Vector3(1.04f, .291f, .65f), .061f, "E9AC9F", .07f, true);
            prop.Block(new Vector3(-.082f, 0, 0), new Vector3(.57f, .313f, .67f), .049f, "3A536E", .22f);
            prop.Block(new Vector3(.187f, 0, 0), new Vector3(.074f, .318f, .674f), .056f, "F0C674", .18f);
            prop.Block(new Vector3(-.095f, .158f, 0), new Vector3(.29f, .018f, .047f), .008f, "F3D7AD", .09f);
            prop.Block(new Vector3(-.095f, -.158f, 0), new Vector3(.29f, .018f, .047f), .008f, "F3D7AD", .09f);
            return prop.Build();
        }

        private static Shape DeskBinderClip()
        {
            var prop = new DeskProp();
            for (int side = -1; side <= 1; side += 2)
            {
                prop.Block(new Vector3(0, -.07f, side * .145f), new Vector3(.79f, .43f, .076f),
                    .035f, "3E4764", .52f, true, rotation: Quaternion.Euler(-side * 33, 0, 0));
                prop.Block(new Vector3(0, -.248f, side * .25f), new Vector3(.79f, .112f, .105f),
                    .044f, "556880", .62f, true);
                // The tilt keeps the two wire handles visually separated when the clip is laid flat.
                prop.Wire(t =>
                {
                    float angle = t * Tau;
                    float radius = Mathf.Sin(angle);
                    return new Vector3(Mathf.Cos(angle) * (.197f + radius * .025f),
                        -.02f + radius * .25f, side * (.30f + radius * .207f));
                }, .033f, "D6C79F", .92f, 5, true);
            }
            return prop.Build();
        }

        private static Shape DeskInkBottle()
        {
            var prop = new DeskProp();
            var glass = UiFactory.Hex("294A72");
            var rim = UiFactory.Hex("6997AF");
            prop.Block(new Vector3(0, -.121f, 0), new Vector3(.91f, .66f, .82f), .115f,
                "294A72", .89f, true, (_, _, p) => Finish(Color.Lerp(glass, rim,
                    .12f + .48f * Mathf.InverseLerp(.25f, .46f, Mathf.Abs(p.x)) +
                    .26f * Mathf.InverseLerp(.06f, .22f, p.y)), .89f));
            prop.Lathe(Vector3.zero, Profile(.002f,.125f, .213f,.125f, .219f,.233f, .207f,.279f,
                .24f,.302f, .246f,.434f, .214f,.467f, .002f,.467f), "303446", .38f, true, flutes: 16, relief: .07f);
            prop.Lathe(Vector3.zero, Profile(.002f,.267f, .23f,.267f, .239f,.281f, .239f,.305f,
                .232f,.313f, .002f,.313f), "C59464", .73f);
            prop.Block(new Vector3(0, -.105f, .405f), new Vector3(.51f, .30f, .030f),
                .014f, "F0D5A7", .15f);
            prop.Block(new Vector3(0, -.119f, .424f), new Vector3(.34f, .064f, .015f),
                .006f, "815646", .16f);
            prop.Block(new Vector3(-.055f, -.196f, .423f), new Vector3(.23f, .035f, .014f),
                .006f, "C39569", .15f);
            return prop.Build();
        }

        private static Shape DeskDie()
        {
            var prop = new DeskProp();
            prop.Block(Vector3.zero, Vector3.one * .83f, .083f, "725BB2", .66f, true);
            Face(1, Vector3.up, Vector3.right);
            Face(6, Vector3.down, Vector3.right);
            Face(2, Vector3.forward, Vector3.right);
            Face(5, Vector3.back, Vector3.left);
            Face(3, Vector3.right, Vector3.back);
            Face(4, Vector3.left, Vector3.forward);
            return prop.Build();

            void Face(int count, Vector3 normal, Vector3 across)
            {
                var down = Vector3.Cross(across, normal);
                Quaternion angle = Quaternion.FromToRotation(Vector3.up, normal);
                if (count % 2 == 1) Pip(Vector3.zero);
                if (count >= 2)
                {
                    Pip((across + down) * .165f);
                    Pip(-(across + down) * .165f);
                }
                if (count >= 4)
                {
                    Pip((across - down) * .165f);
                    Pip(-(across - down) * .165f);
                }
                if (count == 6)
                {
                    Pip(across * .165f);
                    Pip(-across * .165f);
                }
                void Pip(Vector3 offset) => prop.Lathe(normal * .414f + offset,
                    Profile(.002f,-.017f, .046f,-.017f, .057f,-.006f, .053f,.003f, .002f,.010f),
                    "FAE3AB", .53f, rotation: angle, columns: 16);
            }
        }

        private static Shape DeskRuler()
        {
            var prop = new DeskProp();
            var grain = UiFactory.Hex("AF773C");
            var maple = UiFactory.Hex("EAC17A");
            prop.Block(Vector3.zero, new Vector3(1.64f, .128f, .43f), .035f, "EAC17A", .30f, true,
                (_, _, p) => Finish(Color.Lerp(grain, maple,
                    .71f + .19f * Mathf.Sin(p.z * 93 + p.x * 2 + Mathf.Sin(p.x * 6))), .30f));
            for (int mark = 0; mark <= 26; mark++)
            {
                float x = .049f * mark - .637f;
                float length = mark % 5 == 0 ? .132f : mark % 5 == 2 ? .091f : .061f;
                prop.Mark(x, .065f, .149f - length * .5f, .013f, length, "663A29");
            }
            prop.Mark(0, .065f, -.125f, 1.26f, .018f, "9A6237");
            return prop.Build();
        }

        private static Shape DeskMagnifier()
        {
            var prop = new DeskProp();
            var center = new Vector3(-.29f, 0, 0);
            prop.Ring(center, .386f, .291f, .083f, "A76D44", .86f);
            var water = UiFactory.Hex("719C9E");
            var reflection = UiFactory.Hex("C9EBDE");
            prop.Lathe(center, Profile(.002f,-.039f, .244f,-.035f, .308f,-.016f, .315f,.008f,
                .291f,.024f, .215f,.034f, .002f,.046f), "719C9E", .96f,
                paint: (_, _, p) => Finish(Color.Lerp(water, reflection,
                    .30f + .58f * Mathf.Exp(-Mathf.Pow((p.x + .29f + p.z * .55f + .08f) * 17, 2))), .96f));
            prop.Lathe(new Vector3(.269f, 0, 0), Profile(.002f,-.21f, .094f,-.21f, .102f,-.176f,
                .098f,-.119f, .087f,-.088f, .119f,.169f, .129f,.292f, .101f,.347f,
                .002f,.363f), "355663", .61f, true, Quaternion.Euler(0, 0, -90));
            prop.Lathe(new Vector3(.148f, 0, 0), Profile(.002f,-.032f, .107f,-.032f, .11f,-.016f,
                .11f,.016f, .102f,.032f, .002f,.032f), "C49158", .80f,
                rotation: Quaternion.Euler(0, 0, -90));
            return prop.Build();
        }

        private static Shape DeskStapler()
        {
            var prop = new DeskProp();
            prop.Block(new Vector3(0, -.127f, 0), new Vector3(1.06f, .149f, .438f), .047f, "536576", .42f, true);
            prop.Block(new Vector3(-.354f, -.008f, 0), new Vector3(.30f, .201f, .410f), .058f, "496777", .44f, true);
            var tilt = Quaternion.Euler(0, 0, 9);
            prop.Block(new Vector3(-.021f, .107f, 0), new Vector3(1.01f, .203f, .449f),
                .069f, "248E76", .64f, true, rotation: tilt);
            prop.Block(new Vector3(.103f, .017f, 0), new Vector3(.72f, .058f, .285f),
                .024f, "9DAEB5", .81f, rotation: tilt);
            prop.Block(new Vector3(.335f, -.047f, 0), new Vector3(.159f, .038f, .24f), .015f, "CBD0C9", .84f);
            return prop.Build();
        }

        private static Shape DeskTack()
        {
            var prop = new DeskProp();
            prop.Lathe(Vector3.zero, Profile(.002f,-.118f, .28f,-.118f, .307f,-.081f, .293f,-.03f,
                .177f,.026f, .121f,.094f, .127f,.176f, .192f,.209f, .193f,.276f, .153f,.312f,
                .002f,.312f), "69B29E", .63f, true);
            prop.Lathe(Vector3.zero, Profile(.002f,-.349f, .020f,-.299f, .032f,-.240f, .041f,-.116f,
                .002f,-.116f), "A0B4B7", .87f, true);
            return prop.Build();
        }

        private static Shape DeskPaperclip()
        {
            var prop = new DeskProp();
            Vector2[] path =
            {
                new Vector2(.203f,-.143f), new Vector2(-.158f,-.143f), new Vector2(-.27f,-.110f),
                new Vector2(-.303f,.009f), new Vector2(-.26f,.130f), new Vector2(-.149f,.158f),
                new Vector2(.218f,.158f), new Vector2(.305f,.116f), new Vector2(.325f,.010f),
                new Vector2(.270f,-.057f), new Vector2(.173f,-.071f), new Vector2(-.139f,-.071f),
                new Vector2(-.192f,-.042f), new Vector2(-.198f,.013f), new Vector2(-.161f,.066f),
                new Vector2(-.076f,.078f), new Vector2(.158f,.078f)
            };
            prop.Wire(Center, .028f, "CCA767", .93f, 10);
            return prop.Build();

            Vector3 Center(float u)
            {
                float row = u * (path.Length - 1);
                int i = Mathf.Min((int)row, path.Length - 2);
                float t = row - i;
                Vector2 a = path[Mathf.Max(0, i - 1)];
                Vector2 b = path[i];
                Vector2 c = path[i + 1];
                Vector2 d = path[Mathf.Min(path.Length - 1, i + 2)];
                Vector2 p = .5f * ((2 * b) + (-a + c) * t +
                    (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
                return new Vector3(p.x, 0, p.y);
            }
        }

        private static Shape DeskWaxSeal()
        {
            var prop = new DeskProp();
            var wax = UiFactory.Hex("A83F62");
            var crest = UiFactory.Hex("D17A8A");
            Vector3[] profile = Profile(.002f,-.12f, .28f,-.12f, .43f,-.084f, .49f,-.043f,
                .48f,.018f, .426f,.097f, .354f,.122f, .296f,.069f, .17f,.072f, .002f,.077f);
            Vector3 Point(float u, float v) => Flute(Turn(profile, u, v), u, v, 7, .07f);
            prop.SurfacePart(Point, (u, _, p) => Finish(Color.Lerp(wax, crest,
                .26f + .28f * Mathf.InverseLerp(-.08f, .12f, p.y) + .09f * Mathf.Sin(u * Tau * 7)), .61f),
                42, (profile.Length - 1) * 2, true, true);
            for (int ray = 0; ray < 5; ray++)
            {
                float a = ray * Tau / 5;
                prop.Block(new Vector3(Mathf.Cos(a) * .079f, .082f, Mathf.Sin(a) * .079f),
                    new Vector3(.17f, .031f, .064f), .014f, "EAA799", .58f,
                    rotation: Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
            }
            return prop.Build();
        }

        private sealed class DeskProp
        {
            private readonly List<Mesh> rendered = new List<Mesh>();
            private readonly List<Mesh> contacts = new List<Mesh>();

            public void Block(Vector3 center, Vector3 size, float bevel, string hex, float gloss,
                bool contact = false, Func<float, float, Vector3, Color> paint = null, Quaternion? rotation = null)
            {
                // A shared bevel keeps the cover, sleeve, and label edges readable at basket scale.
                float halfHeight = size.y * .5f;
                float inset = 1 - Mathf.Min(.45f, bevel * 2 / Mathf.Min(size.x, size.z));
                var profile = Profile(.002f,-halfHeight, inset,-halfHeight, 1,-halfHeight + bevel,
                    1,halfHeight - bevel, inset,halfHeight, .002f,halfHeight);
                var orientation = rotation ?? Quaternion.identity;
                Vector3 Point(float u, float v)
                {
                    var p = RoundedRectangle(profile, u, v, size.z / size.x);
                    return center + orientation * new Vector3(p.x * size.x * .5f, p.y, p.z * size.x * .5f);
                }
                SurfacePart(Point, paint ?? Paint(hex, gloss), 24, (profile.Length - 1) * 3, true, contact);
            }

            public void Lathe(Vector3 center, Vector3[] profile, string hex, float gloss, bool contact = false,
                Quaternion? rotation = null, int flutes = 0, float relief = 0,
                Func<float, float, Vector3, Color> paint = null, int columns = 36)
            {
                var orientation = rotation ?? Quaternion.identity;
                Vector3 Point(float u, float v)
                {
                    var p = Turn(profile, u, v);
                    if (flutes > 0) p = Flute(p, u, v, flutes, relief);
                    return center + orientation * p;
                }
                SurfacePart(Point, paint ?? Paint(hex, gloss), Mathf.Max(columns, flutes * 3),
                    (profile.Length - 1) * 2, true, contact);
            }

            public void Ring(Vector3 center, float outer, float inner, float halfHeight, string hex, float gloss,
                int segments = 0, Func<float, float, Vector3, Color> paint = null)
            {
                float bevel = Mathf.Min(.025f, halfHeight * .29f);
                var profile = Profile(inner,-halfHeight + bevel, inner + bevel,-halfHeight, outer - bevel,-halfHeight,
                    outer,-halfHeight + bevel, outer,halfHeight - bevel, outer - bevel,halfHeight,
                    inner + bevel,halfHeight, inner,halfHeight - bevel, inner,-halfHeight + bevel);
                Vector3 Point(float u, float v) => center + Turn(profile, u, v);
                rendered.Add(Surface(48, 24, Point, paint ?? Paint(hex, gloss), true, true));
                // The tape roll stays open. The magnifier's frame and glass act as one solid piece.
                if (segments > 0) contacts.AddRange(Sections(segments, Point));
                else contacts.Add(Surface(12, 8, Point, null, true, true));
            }

            public void Wire(Func<float, Vector3> center, float radius, string hex, float gloss,
                int segments, bool loop = false)
            {
                float Radius(float u) => loop ? radius : radius * Mathf.Lerp(.44f, 1, Mathf.Min(u, 1 - u) * 75);
                Vector3 Point(float u, float v) => Tube(center, Radius, u, v, 1);
                rendered.Add(Surface(segments * 8, 10, Point, Paint(hex, gloss), loop, true));
                contacts.AddRange(Sections(segments, Point));
            }

            public void Mark(float x, float y, float z, float width, float depth, string hex)
            {
                rendered.Add(Surface(1, 1,
                    (u, v) => new Vector3(x + (u - .5f) * width, y, z + (v - .5f) * depth), Paint(hex, .20f)));
            }

            public void SurfacePart(Func<float, float, Vector3> point, Func<float, float, Vector3, Color> paint,
                int columns, int rows, bool wrapU, bool contact)
            {
                rendered.Add(Surface(columns, rows, point, paint, wrapU));
                if (contact) contacts.Add(Surface(12, 8, point, null, wrapU));
            }

            public Shape Build() => Assemble(rendered.ToArray(), contacts.ToArray());

            private static Func<float, float, Vector3, Color> Paint(string hex, float gloss)
            {
                Color color = Finish(UiFactory.Hex(hex), gloss);
                return (_, _, _) => color;
            }
        }

    }
}
