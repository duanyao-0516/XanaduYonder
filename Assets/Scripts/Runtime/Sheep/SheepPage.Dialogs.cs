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
            var panel = openModal("旸了个旸 · 选择模式", 654, () => { closeModal(); returnLobby(); });
            UiFactory.Label(panel, "第二关挑战", 32, 112, 532, 38, 29, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(panel, "四档难度 · 清空全部牌面", 32, 154, 532, 32, 21, UiFactory.Muted);
            var challenge = data.Challenge != null && new SheepGame(data.Challenge).Phase != SheepPhase.Won;
            UiFactory.Button(panel, challenge ? "继续第二关" : "开始第二关", 32, 198, challenge ? 360 : 532, 64,
                () => { if (challenge) Resume(SheepMode.Challenge); else ChooseChallenge(); }, true);
            if (challenge) UiFactory.Button(panel, "新一局", 404, 198, 160, 64, () => ConfirmNew(SheepMode.Challenge));
            UiFactory.Label(panel, "无尽模式", 32, 290, 532, 38, 29, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(panel, "每消除一个计 1 分 · 标准最高 " + data.EndlessBest, 32, 332, 532, 32, 21, UiFactory.Muted);
            var endless = data.Endless != null;
            UiFactory.Button(panel, endless ? "继续无尽" : "开始无尽", 32, 376, endless ? 360 : 532, 64,
                () => { if (endless) Resume(SheepMode.Endless); else Begin(SheepMode.Endless, SheepDifficulty.Normal); }, true);
            if (endless) UiFactory.Button(panel, "新一局", 404, 376, 160, 64, () => ConfirmNew(SheepMode.Endless));
            UiFactory.Button(panel, data.Tutorial == null ? "第一关热身" : "继续热身", 32, 478, 256, 56, () => {
                if (data.Tutorial == null) Begin(SheepMode.Tutorial, SheepDifficulty.Normal); else Resume(SheepMode.Tutorial);
            });
            UiFactory.Button(panel, "玩法设置", 308, 478, 256, 56, () => OpenSettings(true));
            UiFactory.Button(panel, "返回大厅", 32, 558, 532, 56, () => { closeModal(); returnLobby(); });
        }

        private void ConfirmNew(SheepMode mode)
        {
            showDialog("开始新的一局？", "将替换这个模式的当前进度，\n其他模式的对局会保留。", "开始新局",
                () => { if (mode == SheepMode.Challenge) ChooseChallenge(); else Begin(mode, SheepDifficulty.Normal); }, "取消", ShowModes);
        }

        private void ChooseChallenge()
        {
            if (data.Preferences.SkipDifficultyPrompt) { Begin(SheepMode.Challenge, data.Preferences.DefaultDifficulty); return; }
            var selected = data.Preferences.DefaultDifficulty;
            var panel = openModal("选择第二关难度", 526, ShowModes);
            var description = UiFactory.Label(panel, DifficultyDescription(selected), 32, 200, 532, 52, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            DifficultyChoices(panel, 122, selected, value => { selected = value; description.text = DifficultyDescription(value); });
            var remember = Checkbox(panel, "下次直接使用此难度，不再询问", 274, false);
            UiFactory.Button(panel, "开始挑战", 32, 350, 532, 64, () => {
                closeModal(); Suspend(); session.StartChallenge(selected, remember.isOn, NewSeed()); Save(); Refresh();
                setStatus(warning ?? "第 2 关 · " + DifficultyName(selected) + " · 挑战开始");
            }, true);
            UiFactory.Button(panel, "返回模式选择", 32, 434, 532, 52, ShowModes);
        }

        private void OpenSettings() { if (AcceptsInput) OpenSettings(false); }
        private void OpenSettings(bool fromModes, SheepPreferences initial = null)
        {
            var draft = (initial ?? data.Preferences).Copy();
            Action finish = () => { closeModal(); if (fromModes || game == null) ShowModes(); else CheckEndState(); };
            var panel = openModal("旸了个旸 · 设置", 826, finish);
            UiFactory.Label(panel, "第二关默认难度", 32, 106, 532, 36, 24, UiFactory.Ink);
            var choices = DifficultyChoices(panel, 154, draft.DefaultDifficulty, value => draft.DefaultDifficulty = value);
            var skip = Checkbox(panel, "新开第二关直接使用默认难度", 226, draft.SkipDifficultyPrompt);
            UiFactory.Label(panel, "新局道具次数（0–99，0 表示禁用）", 32, 300, 532, 36, 22, UiFactory.Ink);
            var remove = Stepper(panel, "移出", 354, draft.RemoveLimit);
            var undo = Stepper(panel, "撤回", 422, draft.UndoLimit);
            var shuffle = Stepper(panel, "洗牌", 490, draft.ShuffleLimit);
            UiFactory.Label(panel, "修改从下一局生效，无尽换波不补次数。\n自定义次数不计入标准无尽纪录。", 32, 562, 532, 62, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(panel, "恢复默认", 32, 634, 256, 54, () => {
                draft.DefaultDifficulty = SheepDifficulty.Normal; PaintDifficulties(choices, draft.DefaultDifficulty);
                skip.isOn = false; remove.Set(2); undo.Set(3); shuffle.Set(1);
            });
            UiFactory.Button(panel, "声音与触感", 308, 634, 256, 54, () => {
                draft.SkipDifficultyPrompt = skip.isOn; draft.RemoveLimit = remove.Value; draft.UndoLimit = undo.Value; draft.ShuffleLimit = shuffle.Value;
                closeModal(); commonSettings(() => OpenSettings(fromModes, draft));
            });
            UiFactory.Button(panel, "保存设置", 32, 712, 344, 68, () => {
                draft.SkipDifficultyPrompt = skip.isOn; draft.RemoveLimit = remove.Value; draft.UndoLimit = undo.Value; draft.ShuffleLimit = shuffle.Value;
                session.SetPreferences(draft); Save(); finish();
            }, true);
            UiFactory.Button(panel, "取消", 392, 712, 172, 68, finish);
        }

        private static Button[] DifficultyChoices(RectTransform panel, float top, SheepDifficulty selected, Action<SheepDifficulty> select)
        {
            var buttons = new Button[4];
            for (var i = 0; i < buttons.Length; i++)
            {
                var difficulty = (SheepDifficulty)i;
                buttons[i] = UiFactory.Button(panel, DifficultyName(difficulty), 32 + i * 136, top, 124, 58,
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

        private static Toggle Checkbox(RectTransform panel, string label, float top, bool initial)
        {
            var row = UiFactory.Rect(panel, label, 32, top, 532, 52);
            var target = row.gameObject.AddComponent<Image>(); target.color = Color.clear;
            var toggle = row.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = target;
            var box = UiFactory.Panel(row, "Checkbox", 0, 10, 32, 32, UiFactory.Hex("D3DDCE"));
            var mark = UiFactory.Label(box.transform, "✓", 0, -2, 32, 36, 26, UiFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            toggle.graphic = mark; toggle.isOn = initial;
            UiFactory.Label(row, label, 48, 0, 484, 52, 23, UiFactory.Ink);
            return toggle;
        }

        private sealed class QuotaStepper
        {
            public int Value { get; private set; }
            public Text Label; public Button Less, More;
            public void Set(int value) { Value = Mathf.Clamp(value, 0, 99); Label.text = Value.ToString(); Less.interactable = Value > 0; More.interactable = Value < 99; }
        }

        private static QuotaStepper Stepper(RectTransform panel, string name, float top, int initial)
        {
            UiFactory.Label(panel, name, 32, top, 240, 54, 25, UiFactory.Ink);
            var stepper = new QuotaStepper();
            stepper.Less = UiFactory.Button(panel, "−", 316, top, 68, 54, () => stepper.Set(stepper.Value - 1));
            stepper.Label = UiFactory.Label(panel, "", 396, top, 88, 54, 28, UiFactory.Ink, TextAnchor.MiddleCenter);
            stepper.More = UiFactory.Button(panel, "+", 496, top, 68, 54, () => stepper.Set(stepper.Value + 1));
            stepper.Set(initial); return stepper;
        }
    }
}
