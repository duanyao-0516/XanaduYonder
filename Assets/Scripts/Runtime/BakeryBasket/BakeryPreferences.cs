using UnityEngine;

namespace XanaduYonder.Runtime.BakeryBasket
{
    internal sealed class BakeryPreferences
    {
        private const string Prefix = "xanadu.bakery.v1.";
        internal bool ShowBottom = true;
        internal bool ShowRemaining = true;
        internal bool RandomOnEntry = true;
        internal BakeryTheme Theme;

        internal static BakeryPreferences Load() => new BakeryPreferences
        {
            ShowBottom = PlayerPrefs.GetInt(Prefix + "showBottom", 1) != 0,
            ShowRemaining = PlayerPrefs.GetInt(Prefix + "showRemaining", 1) != 0,
            RandomOnEntry = PlayerPrefs.GetInt(Prefix + "randomOnEntry", 1) != 0,
            Theme = BakeryThemeCatalog.FromSavedValue(PlayerPrefs.GetInt(Prefix + "theme", 0))
        };

        internal void Save()
        {
            PlayerPrefs.SetInt(Prefix + "showBottom", ShowBottom ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "showRemaining", ShowRemaining ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "randomOnEntry", RandomOnEntry ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "theme", (int)Theme);
            PlayerPrefs.Save();
        }

        internal BakeryTheme ChooseForEntry(System.Random random) => RandomOnEntry
            ? BakeryThemeCatalog.All[random.Next(BakeryThemeCatalog.All.Count)].Theme
            : Theme;
    }
}
