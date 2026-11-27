using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.BakeryBasket;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private BakeryPage bakeryPage;
        private BakeryPreferences bakeryPreferences;

        private static void AddBakeryBasketPreview(Transform parent)
        {
            var sample = UiFactory.Rect(parent, "Bakery preview", 449, 30, 240, 124);
            UiFactory.Panel(sample, "Basket", 20, 48, 200, 70, UiFactory.Hex("D2B28F"));
            UiFactory.CirclePanel(sample, "Bun", 40, 22, 53, UiFactory.Hex("CF9152"));
            UiFactory.CirclePanel(sample, "Doughnut", 109, 8, 64, UiFactory.Hex("A66745"));
            UiFactory.CirclePanel(sample, "Doughnut hole", 130, 29, 22, UiFactory.Hex("F1DBBD"));
            UiFactory.Panel(sample, "Chocolate", 170, 58, 33, 42, UiFactory.Hex("88553D"));
        }

        private void EnterBakeryBasket()
        {
            CloseModal();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            SetActivePage(Page.BakeryBasket);
            if (status != null) status.gameObject.SetActive(false);
            if (bakeryPage == null)
            {
                bakeryPreferences = BakeryPreferences.Load();
                var root = UiFactory.Rect(frame, "Bakery Basket page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                bakeryPage = root.gameObject.AddComponent<BakeryPage>();
                bakeryPage.Initialize(root, ShowLobby,
                    () => activePage == Page.BakeryBasket && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, feedback,
                    bakeryPreferences, ShowBakeryHelp, OpenBakerySettings);
            }
            bakeryPage.Enter();
        }

        private void ShowBakeryHelp()
        {
            bakeryPage.Suspend();
            var panel = CreateModal(BakeryPage.DisplayName + " · 玩法说明", 610, CloseModal);
            UiFactory.Label(panel,
                "轻按物件查看轮廓，松手取进下方消除栏。\n可以连续点取，三件同类物品自动消除。\n\n「移出」把消除栏最前面最多三件放进六格暂存区。点暂存物件可放回消除栏。\n\n清空篮子、消除栏和暂存区即可获胜。\n点「颠一下」能翻动物件。切换篮型会重开。",
                32, 112, 532, 340, 24, UiFactory.Muted, TextAnchor.MiddleLeft);
            DialogUi.ActionButton(panel, "知道了", 32, 503, 532, 68, CloseModal, true);
            dismissModal = CloseModal;
        }

        private void OpenBakerySettings()
        {
            bakeryPage.Suspend();
            var panel = CreateSheet(BakeryPage.DisplayName + " · 设置", 600);
            var sections = new DialogSheetSections(panel);
            sections.Heading("数量显示");
            sections.Switch("篮底小件数量", bakeryPreferences.ShowBottom, value =>
            {
                bakeryPreferences.ShowBottom = value;
                SaveBakeryDisplay();
            });
            sections.Switch("篮中剩余数量", bakeryPreferences.ShowRemaining, value =>
            {
                bakeryPreferences.ShowRemaining = value;
                SaveBakeryDisplay();
            });
            sections.Heading("3D 主题");
            sections.Switch("进入时随机主题", bakeryPreferences.RandomOnEntry, value =>
            {
                bakeryPreferences.RandomOnEntry = value;
                SaveBakeryDisplay();
            });
            sections.Body("切换主题会重开当前篮。");
            var themes = BakeryThemeCatalog.All;
            var buttons = new Button[themes.Count];
            void PaintThemes()
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool selected = themes[i].Theme == bakeryPage.ActiveTheme;
                    buttons[i].GetComponent<Image>().color = selected ? UiFactory.Accent : DialogUi.Secondary;
                    buttons[i].GetComponentInChildren<Text>().color = selected ? Color.white : UiFactory.Ink;
                }
            }
            float column = (sections.Width - 12) / 2;
            float top = 0;
            for (int i = 0; i < themes.Count; i++)
            {
                var option = themes[i];
                if (i % 2 == 0) top = sections.Reserve(88);
                buttons[i] = DialogUi.ActionButton(panel, option.Title, sections.Inset + i % 2 * (column + 12), top, column, 88, () =>
                {
                    bakeryPreferences.Theme = option.Theme;
                    bakeryPreferences.Save();
                    if (bakeryPage.ActiveTheme != option.Theme) bakeryPage.ChangeTheme(option.Theme);
                    PaintThemes();
                });
            }
            PaintThemes();
            AddFeedbackControls(panel, sections);
            sections.Action("完成", dismissModal, true);
            sections.Finish();
        }

        private void SaveBakeryDisplay()
        {
            bakeryPreferences.Save();
            bakeryPage.ApplyDisplay();
        }
    }
}
