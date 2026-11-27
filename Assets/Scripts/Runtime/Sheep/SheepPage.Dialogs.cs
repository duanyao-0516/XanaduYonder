using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    internal sealed partial class SheepPage
    {
        private static string DifficultyName(SheepDifficulty difficulty)
        {
            switch (difficulty) { case SheepDifficulty.Easy: return "简单"; case SheepDifficulty.Hard: return "困难";
                case SheepDifficulty.Extreme: return "超难"; default: return "正常"; }
        }

        private void ShowModes()
        {
            Suspend(); Save();
            var panel = openModal("旸了个旸 · 选择模式", 660, returnLobby);
            bool challenge = data.Challenge != null && new SheepGame(data.Challenge).Phase != SheepPhase.Won;
            bool endless = data.Endless != null;
            DialogUi.Disclosure(panel, "第二关挑战", 32, 108, 532, 70, () =>
            {
                if (challenge) ShowSavedMode(SheepMode.Challenge); else ChooseChallenge();
            });
            UiFactory.Label(panel, challenge ? "有进行中的挑战" : "四档难度 · 清空全部牌面", 42, 179, 512, 34, 21, UiFactory.Muted);
            DialogUi.Disclosure(panel, "无尽模式", 32, 225, 532, 70, () =>
            {
                if (endless) ShowSavedMode(SheepMode.Endless); else Begin(SheepMode.Endless, SheepDifficulty.Normal);
            });
            UiFactory.Label(panel, (endless ? "可继续上次对局 · " : "") + "标准最高 " + data.EndlessBest,
                42, 296, 512, 34, 21, UiFactory.Muted);
            DialogUi.ActionButton(panel, data.Tutorial == null ? "第一关热身" : "继续热身", 32, 352, 532, 64, () =>
            {
                if (data.Tutorial == null) Begin(SheepMode.Tutorial, SheepDifficulty.Normal); else Resume(SheepMode.Tutorial);
            });
            DialogUi.Disclosure(panel, "玩法设置", 32, 438, 532, 64, () => OpenSettings(true));
            DialogUi.ActionButton(panel, "返回大厅", 32, 550, 532, 64, () => { closeModal(); returnLobby(); });
        }

        private void ShowSavedMode(SheepMode mode)
        {
            bool challenge = mode == SheepMode.Challenge;
            var panel = openModal(challenge ? "第二关挑战" : "无尽模式", 474, ShowModes);
            UiFactory.Label(panel, "已有进行中的对局。", 32, 108, 532, 48, 24, UiFactory.Muted);
            DialogUi.ActionButton(panel, challenge ? "继续第二关" : "继续无尽", 32, 174, 532, 68, () => Resume(mode), true);
            DialogUi.ActionButton(panel, "另开新局", 32, 258, 532, 64, () =>
                showDialog("开始新的一局？", "将替换这个模式的当前进度，\n其他模式的对局会保留。", "开始新局",
                    () => { if (challenge) ChooseChallenge(); else Begin(mode, SheepDifficulty.Normal); }, "取消", () => ShowSavedMode(mode)));
            DialogUi.ActionButton(panel, "返回模式选择", 32, 368, 532, 64, ShowModes);
        }

        private void ChooseChallenge()
        {
            if (data.Preferences.SkipDifficultyPrompt) { Begin(SheepMode.Challenge, data.Preferences.DefaultDifficulty); return; }
            var selected = data.Preferences.DefaultDifficulty;
            var panel = openModal("选择第二关难度", 526, ShowModes);
            var description = UiFactory.Label(panel, DifficultyDescription(selected), 32, 200, 532, 52, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            DifficultyChoices(panel, 122, selected, value => { selected = value; description.text = DifficultyDescription(value); });
            bool remember = false;
            DialogUi.SwitchRow(panel, "下次直接使用此难度", 32, 266, 532, 66, false, value => remember = value);
            DialogUi.ActionButton(panel, "开始挑战", 32, 350, 532, 64, () => {
                closeModal(); Suspend(); session.StartChallenge(selected, remember, NewSeed()); Save(); Refresh();
                setStatus(warning ?? "第 2 关 · " + DifficultyName(selected) + " · 挑战开始");
            }, true);
            DialogUi.ActionButton(panel, "返回模式选择", 32, 434, 532, 52, ShowModes);
        }

        private void OpenSettings() { if (AcceptsInput) OpenSettings(false); }
        private void OpenSettings(bool fromModes, SheepPreferences initial = null)
        {
            var draft = (initial ?? data.Preferences).Copy();
            Action finish = () => { closeModal(); if (fromModes || game == null) ShowModes(); else CheckEndState(); };
            var panel = openModal("旸了个旸 · 设置", 624, finish);
            Action back = () => OpenSettings(fromModes, draft);
            DialogUi.Disclosure(panel, "第二关难度 · " + DifficultyName(draft.DefaultDifficulty), 32, 106, 532, 72,
                () => EditDifficulty(draft, back));
            DialogUi.Disclosure(panel, "新局道具次数", 32, 190, 532, 72, () => EditQuotas(draft, back));
            DialogUi.Disclosure(panel, "声音与触感", 32, 274, 532, 72, () => { closeModal(); commonSettings(back); });
            DialogUi.ActionButton(panel, "恢复默认", 32, 364, 532, 62, () => OpenSettings(fromModes, new SheepPreferences()));
            UiFactory.Label(panel, "难度与道具设置从下一局生效。", 32, 444, 532, 46, 22, UiFactory.Muted);
            DialogUi.ActionButton(panel, "取消", 32, 516, 172, 70, finish);
            DialogUi.ActionButton(panel, "保存设置", 220, 516, 344, 70, () => { session.SetPreferences(draft); Save(); finish(); }, true);
        }

        private void EditDifficulty(SheepPreferences draft, Action back)
        {
            var panel = openModal("第二关难度", 508, back);
            var description = UiFactory.Label(panel, DifficultyDescription(draft.DefaultDifficulty), 32, 208, 532, 52, 22, UiFactory.Muted);
            DifficultyChoices(panel, 124, draft.DefaultDifficulty, value =>
            {
                draft.DefaultDifficulty = value;
                description.text = DifficultyDescription(value);
            });
            DialogUi.SwitchRow(panel, "直接使用默认难度", 32, 284, 532, 68, draft.SkipDifficultyPrompt,
                value => draft.SkipDifficultyPrompt = value);
            DialogUi.ActionButton(panel, "返回设置", 32, 402, 532, 68, back, true);
        }

        private void EditQuotas(SheepPreferences draft, Action back)
        {
            var panel = openModal("新局道具次数", 602, back);
            UiFactory.Label(panel, "每种道具可用 0–99 次，0 表示禁用。", 32, 110, 532, 48, 22, UiFactory.Muted);
            Stepper(panel, "移出", 184, draft.RemoveLimit, value => draft.RemoveLimit = value);
            Stepper(panel, "撤回", 260, draft.UndoLimit, value => draft.UndoLimit = value);
            Stepper(panel, "洗牌", 336, draft.ShuffleLimit, value => draft.ShuffleLimit = value);
            UiFactory.Label(panel, "无尽换波不补次数。自定义次数的对局不计入标准无尽纪录。", 32, 408, 532, 72, 21, UiFactory.Muted);
            DialogUi.ActionButton(panel, "返回设置", 32, 502, 532, 66, back, true);
        }

        private static Button[] DifficultyChoices(RectTransform panel, float top, SheepDifficulty selected, Action<SheepDifficulty> select)
        {
            var buttons = new Button[4];
            for (var i = 0; i < buttons.Length; i++)
            {
                var difficulty = (SheepDifficulty)i;
                buttons[i] = DialogUi.ActionButton(panel, DifficultyName(difficulty), 32 + i * 136, top, 124, 58,
                    () => { select(difficulty); PaintDifficulties(buttons, difficulty); });
            }
            PaintDifficulties(buttons, selected); return buttons;
        }

        private static string DifficultyDescription(SheepDifficulty difficulty)
        {
            switch (difficulty)
            {
                case SheepDifficulty.Easy: return "连续消除更顺手，适合轻松游玩。";
                case SheepDifficulty.Hard: return "对子跨层交错，需要规划取牌顺序。";
                case SheepDifficulty.Extreme: return "容错很低，需反复尝试并善用道具。";
                default: return "留意叠层，给后续匹配预留卡槽。";
            }
        }

        private static void PaintDifficulties(Button[] buttons, SheepDifficulty selected)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].GetComponent<Image>().color = i == (int)selected ? UiFactory.Accent : UiFactory.Hex("E7EDE4");
                buttons[i].GetComponentInChildren<Text>().color = i == (int)selected ? Color.white : UiFactory.Ink;
            }
        }

        private sealed class QuotaStepper
        {
            public int Value { get; private set; }
            public Text Label; public Button Less, More; public Action<int> Changed;
            public void Set(int value) { Value = Mathf.Clamp(value, 0, 99); Label.text = Value.ToString(); Less.interactable = Value > 0; More.interactable = Value < 99; Changed(Value); }
        }

        private static void Stepper(RectTransform panel, string name, float top, int initial, Action<int> changed)
        {
            UiFactory.Label(panel, name, 32, top, 240, 54, 25, UiFactory.Ink);
            var stepper = new QuotaStepper { Changed = changed };
            stepper.Less = DialogUi.ActionButton(panel, "−", 316, top, 68, 54, () => stepper.Set(stepper.Value - 1));
            stepper.Label = UiFactory.Label(panel, "", 396, top, 88, 54, 28, UiFactory.Ink, TextAnchor.MiddleCenter);
            stepper.More = DialogUi.ActionButton(panel, "+", 496, top, 68, 54, () => stepper.Set(stepper.Value + 1));
            stepper.Set(initial);
        }
    }
}
