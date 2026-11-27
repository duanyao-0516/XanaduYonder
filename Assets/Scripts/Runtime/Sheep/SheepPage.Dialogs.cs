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
            var panel = openSheet("选择第二关难度", 526, ShowModes);
            var description = UiFactory.Label(panel, DifficultyDescription(selected), 32, 200, panel.rect.width - 64, 52, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            DifficultyChoices(panel, 122, selected, value => { selected = value; description.text = DifficultyDescription(value); });
            bool remember = false;
            DialogUi.SwitchRow(panel, "下次直接使用此难度", 32, 266, panel.rect.width - 64, 66, false, value => remember = value);
            DialogUi.ActionButton(panel, "开始挑战", 32, 350, panel.rect.width - 64, 64, DialogSheetLayout.AfterClose(panel, () => {
                closeModal(); Suspend(); session.StartChallenge(selected, remember, NewSeed()); Save(); Refresh();
                setStatus(warning ?? "第 2 关 · " + DifficultyName(selected) + " · 挑战开始");
            }), true);
            DialogUi.ActionButton(panel, "返回模式选择", 32, 434, panel.rect.width - 64, 52, DialogSheetLayout.AfterClose(panel, ShowModes));
        }

        private void OpenSettings() { if (AcceptsInput) OpenSettings(false); }
        private void OpenSettings(bool fromModes)
        {
            var draft = data.Preferences.Copy();
            Action finish = () => { closeModal(); if (fromModes || game == null) ShowModes(); else CheckEndState(); };
            var panel = openSheet("旸了个旸 · 设置", 620, finish);
            var sections = new DialogSheetSections(panel);
            sections.Heading("第二关难度");
            float choicesTop = sections.Reserve(88);
            var description = sections.Body(DifficultyDescription(draft.DefaultDifficulty));
            var choices = DifficultyChoices(panel, choicesTop, draft.DefaultDifficulty, value =>
            {
                draft.DefaultDifficulty = value;
                description.text = DifficultyDescription(value);
            }, sections.Inset, 88);
            var skip = sections.Switch("直接使用默认难度", draft.SkipDifficultyPrompt, value => draft.SkipDifficultyPrompt = value);
            sections.Heading("新局道具次数");
            sections.Body("每种道具可用 0–99 次，0 表示禁用。无尽换波不补次数。自定义次数的对局不计入标准无尽纪录。");
            var remove = Stepper(panel, sections, "移出", draft.RemoveLimit, value => draft.RemoveLimit = value);
            var undo = Stepper(panel, sections, "撤回", draft.UndoLimit, value => draft.UndoLimit = value);
            var shuffle = Stepper(panel, sections, "洗牌", draft.ShuffleLimit, value => draft.ShuffleLimit = value);
            commonSettings(panel, sections);
            sections.Body("声音与触感在所有游戏中立即生效。");
            sections.Heading("玩法设置");
            sections.Body("保存后，难度与道具次数从下一局生效。");
            sections.Action("恢复玩法默认值", () =>
            {
                draft = new SheepPreferences();
                PaintDifficulties(choices, draft.DefaultDifficulty);
                description.text = DifficultyDescription(draft.DefaultDifficulty);
                skip.GetComponent<DialogSwitch>().SetValueWithoutNotify(draft.SkipDifficultyPrompt);
                remove.Set(draft.RemoveLimit);
                undo.Set(draft.UndoLimit);
                shuffle.Set(draft.ShuffleLimit);
            });
            float top = sections.Reserve(88);
            float half = (sections.Width - 12) / 2;
            DialogUi.ActionButton(panel, "取消修改", sections.Inset, top, half, 88, DialogSheetLayout.AfterClose(panel, finish));
            DialogUi.ActionButton(panel, "保存玩法设置", sections.Inset + half + 12, top, half, 88, DialogSheetLayout.AfterClose(panel, () =>
            {
                session.SetPreferences(draft); Save(); finish();
            }), true);
            sections.Finish();
        }

        private static Button[] DifficultyChoices(RectTransform panel, float top, SheepDifficulty selected, Action<SheepDifficulty> select, float inset = 32, float height = 58)
        {
            var buttons = new Button[4];
            float cell = (panel.rect.width - inset * 2 - 36) / 4;
            for (var i = 0; i < buttons.Length; i++)
            {
                var difficulty = (SheepDifficulty)i;
                buttons[i] = DialogUi.ActionButton(panel, DifficultyName(difficulty), inset + i * (cell + 12), top, cell, height,
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

        private static QuotaStepper Stepper(RectTransform panel, DialogSheetSections sections, string name, int initial, Action<int> changed)
        {
            float top = sections.Reserve(88);
            float right = sections.Inset + sections.Width;
            UiFactory.Label(panel, name, sections.Inset, top, sections.Width - 272, 88, 24, UiFactory.Ink);
            var stepper = new QuotaStepper { Changed = changed };
            stepper.Less = DialogUi.ActionButton(panel, "−", right - 272, top, 88, 88, () => stepper.Set(stepper.Value - 1));
            stepper.Label = UiFactory.Label(panel, "", right - 180, top, 88, 88, 24, UiFactory.Ink, TextAnchor.MiddleCenter);
            stepper.More = DialogUi.ActionButton(panel, "+", right - 88, top, 88, 88, () => stepper.Set(stepper.Value + 1));
            stepper.Set(initial);
            return stepper;
        }
    }
}
