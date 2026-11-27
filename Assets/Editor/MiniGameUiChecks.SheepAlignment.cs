using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.Sheep;

namespace XanaduYonder.EditorTools
{
    public static partial class MiniGameUiChecks
    {
        private static void CheckSheepMenuColumn(RectTransform panel, string warmup)
        {
            var title = SheepText(panel, "旸了个旸 · 选择模式");
            foreach (string name in new[] { "第二关挑战", "无尽模式", warmup, "玩法设置" })
            {
                var row = FindCaption(panel, name);
                var labels = row.GetComponentsInChildren<Text>();
                int count = name == "第二关挑战" || name == "无尽模式" ? 2 : 1;
                Require(labels.Length == count, name + " puts both the title and any description in the same touch row");
                foreach (var label in labels) CheckSheepLeft(panel, title, label);
                if (labels.Length == 2)
                {
                    var main = labels[0].rectTransform;
                    var note = labels[1].rectTransform;
                    Require(main.TransformPoint(main.rect.min).y >= note.TransformPoint(note.rect.max).y - 1,
                        name + " gives its description its own line below its label");
                }
            }
            Require(FindCaption(panel, "返回大厅").GetComponentInChildren<Text>().alignment == TextAnchor.MiddleCenter,
                "The single footer action remains centered in its button");
        }

        private static void CheckSheepSettingsColumn(RectTransform panel)
        {
            var title = SheepText(panel, "旸了个旸 · 设置");
            foreach (string caption in new[] { "第二关难度", "留意叠层", "新局道具次数", "每种道具", "移出", "撤回", "洗牌", "声音与触感", "玩法设置" })
                CheckSheepLeft(panel, title, SheepText(panel, caption));
        }

        private static void CheckSheepChallengeColumn(RectTransform panel)
        {
            var title = SheepText(panel, "选择第二关难度");
            CheckSheepLeft(panel, title, SheepText(panel, "留意叠层"));
            var normal = FindCaption(panel, "正常").GetComponentInChildren<Text>();
            Require(normal.alignment == TextAnchor.MiddleCenter, "Each peer choice stays centered in its equal-width cell");
        }

        private static void CheckSheepReadingColumn(RectTransform panel, string title, string body)
        {
            CheckSheepLeft(panel, SheepText(panel, title), SheepText(panel, body));
        }

        private static void CheckSheepRulesLayout(RectTransform panel)
        {
            var body = SheepText(panel, "只取未被盖住的牌");
            CheckSheepLeft(panel, SheepText(panel, "旸了个旸 · 玩法"), body);
            var viewport = (RectTransform)body.transform.parent;
            var button = (RectTransform)FindCaption(panel, "知道了").transform;
            float gap = panel.InverseTransformPoint(viewport.TransformPoint(viewport.rect.min)).y -
                        panel.InverseTransformPoint(button.TransformPoint(button.rect.max)).y;
            Require(gap >= 16 && gap <= 28, "Sheep rules place the close action immediately below the reading area");
            float textHeight = Mathf.Ceil(body.preferredHeight);
            Require(viewport.rect.height <= textHeight + 1, "Sheep rules shrink the card to fit its text");
            Require(viewport.rect.height >= textHeight - 1 || viewport.GetComponent<ScrollRect>()?.enabled == true,
                "Sheep rules scroll when the full text cannot fit above the close action");
        }

        private static void CheckSheepResultColumn(SheepPage page)
        {
            var title = Get<Text>(page, "resultTitle");
            CheckSheepLeft((RectTransform)title.transform.parent, title, Get<Text>(page, "resultMessage"));
        }

        private static Text SheepText(RectTransform panel, string start) =>
            panel.GetComponentsInChildren<Text>().First(label => label.text.StartsWith(start, StringComparison.Ordinal));

        private static void CheckSheepLeft(RectTransform panel, Text heading, Text label)
        {
            Canvas.ForceUpdateCanvases();
            bool Left(Text item) => item.alignment == TextAnchor.MiddleLeft || item.alignment == TextAnchor.UpperLeft ||
                                    item.alignment == TextAnchor.LowerLeft;
            float LeadingEdge(Text item)
            {
                var rect = item.rectTransform;
                return panel.InverseTransformPoint(rect.TransformPoint(rect.rect.min)).x;
            }
            Require(Left(heading) && Left(label) && Mathf.Abs(LeadingEdge(heading) - LeadingEdge(label)) <= 1.5f,
                "Sheep dialog labels must share a left edge: " + heading.text + " / " + label.text);
        }
    }
}
