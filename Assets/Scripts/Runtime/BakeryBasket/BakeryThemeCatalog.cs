using System;
using System.Collections.Generic;

namespace XanaduYonder.Runtime.BakeryBasket
{
    // Values 0 and 1 were saved by the first release of the theme picker.
    internal enum BakeryTheme { Pastry = 0, Jade = 1, Produce = 2, Desk = 3 }

    internal sealed class BakeryThemeCatalog
    {
        private static readonly float[] ProduceLengths =
            { 1.57f, 1.48f, 1.94f, 1.03f, 1.25f, 1.28f, .73f, .91f, 1.44f, 1.32f, 1.20f, .92f, .78f, .49f, .60f };

        private static readonly BakeryThemeCatalog[] Entries =
        {
            new BakeryThemeCatalog(BakeryTheme.Pastry, "烘焙", BakeryFood.Names, BakeryFood.Create),
            new BakeryThemeCatalog(BakeryTheme.Jade, "青玉", BakeryFood.JadeNames, BakeryFood.CreateJade),
            new BakeryThemeCatalog(BakeryTheme.Produce, "果蔬", BakeryFood.ProduceNames, BakeryFood.CreateProduce, ProduceLengths),
            new BakeryThemeCatalog(BakeryTheme.Desk, "书桌", BakeryFood.DeskNames, BakeryFood.CreateDesk)
        };

        internal static IReadOnlyList<BakeryThemeCatalog> All { get; } = Array.AsReadOnly(Entries);

        internal static BakeryTheme FromSavedValue(int value) =>
            (uint)value < (uint)Entries.Length ? Entries[value].Theme : BakeryTheme.Pastry;

        internal static BakeryThemeCatalog Get(BakeryTheme theme) => Entries[(int)theme];

        internal readonly BakeryTheme Theme;
        internal readonly string Title;
        internal readonly string[] Names;
        internal readonly IReadOnlyList<float> Lengths;
        internal readonly Func<int, BakeryFood.Shape> CreateShape;
        private BakeryThemeCatalog(BakeryTheme theme, string title, string[] names, Func<int, BakeryFood.Shape> createShape,
            float[] lengths = null)
        {
            Theme = theme;
            Title = title;
            Names = names;
            CreateShape = createShape;
            Lengths = Array.AsReadOnly(lengths ?? BakeryFood.Lengths);
        }
    }
}
