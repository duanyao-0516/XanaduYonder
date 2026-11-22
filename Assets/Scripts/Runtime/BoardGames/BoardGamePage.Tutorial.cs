using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed partial class BoardGamePage
    {
        private void DrawTutorial()
        {
            var lesson = lessons[lessonIndex];
            Label(lesson.Title, Margin, 108, ContentWidth, 56, 34, UiFactory.Ink, TextAnchor.MiddleLeft);
            var instruction = UiFactory.Label(view, (lessonIndex + 1) + " / " + lessons.Length + " · " + lesson.Instruction,
                Margin, 163, ContentWidth, 90, 24, UiFactory.Muted, TextAnchor.UpperLeft);
            float instructionHeight = Mathf.Max(76, Mathf.Ceil(instruction.preferredHeight) + 7);
            instruction.rectTransform.sizeDelta = new Vector2(ContentWidth, instructionHeight);
            float footerTop = Height - 173;
            string status = notice ?? (lessonDone ? lesson.Outcome ?? "已完成本节" : MovePreviewHint() ?? "");
            var hint = UiFactory.Label(view, status, Margin, footerTop - 83, ContentWidth, 77, 23, UiFactory.Ink, TextAnchor.UpperLeft);
            float hintHeight = Mathf.Max(60, Mathf.Ceil(hint.preferredHeight) + 6);
            hint.rectTransform.anchoredPosition = new Vector2(Margin, -(footerTop - hintHeight - 6));
            hint.rectTransform.sizeDelta = new Vector2(ContentWidth, hintHeight);
            int viewer = lesson.Kind == BoardLessonKind.Handoff && lessonDone ? 1 :
                lesson.ShowReplyView && lessonDone ? lessonPosition.CurrentSide : lesson.Position.CurrentSide;
            DrawBoard(lessonPosition, 163 + instructionHeight + 8, footerTop - hintHeight - 13, viewer, game == BoardGameId.Army ? viewer : 0,
                CanSelect, false, lessonLastMove.From >= 0 ? (BoardMove?)lessonLastMove : null, lesson.RevealArmy);
            float half = (ContentWidth - 10) / 2;
            GameButton(view, "退出教程", Margin, footerTop, half, 73, ExitTutorial);
            GameButton(view, "跳过本节", Margin + half + 10, footerTop, half, 73, NextLesson);
            if (lessonDone)
            {
                BottomPair("上一步", PriorLesson, lessonIndex + 1 == lessons.Length ? "完成教程" : "下一节", NextLesson, lessonIndex > 0);
                return;
            }
            switch (lesson.Kind)
            {
                case BoardLessonKind.Formation:
                case BoardLessonKind.FormationRestriction:
                    BottomPair("上一步", PriorLesson, "试布阵", PracticeFormation, lessonIndex > 0);
                    break;
                case BoardLessonKind.Handoff:
                    BottomPair("上一步", PriorLesson, "演示遮挡", () => { lessonDone = true; handoff = true; Render(); }, lessonIndex > 0);
                    break;
                case BoardLessonKind.Inspect:
                    BottomPair("上一步", PriorLesson, "查看结果", () => { lessonDone = true; Render(); }, lessonIndex > 0);
                    break;
                case BoardLessonKind.ClaimDraw:
                    BottomPair("上一步", PriorLesson, "申请和棋", () => Commit(BoardMove.ClaimDraw), lessonIndex > 0);
                    break;
                case BoardLessonKind.Pass:
                    BottomPair("上一步", PriorLesson, "跳过本手", () => Commit(BoardMove.Pass), lessonIndex > 0);
                    break;
                default:
                    BottomPair("上一步", PriorLesson, "重新练习", StartLesson, lessonIndex > 0);
                    break;
            }
        }

        private void PriorLesson()
        {
            if (lessonIndex > 0) SelectLesson(lessonIndex - 1);
        }

        private void SelectLesson(int index)
        {
            lessonIndex = index;
            StartLesson();
        }

        private void DrawLessonDirectory(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "教程目录");
            const float margin = 26;
            const float top = 106;
            const float rowHeight = 72;
            float listWidth = width - 2 * margin;
            float listHeight = height - top - 106;
            var viewport = UiFactory.Rect(sheet, "Tutorial lessons", margin, top, listWidth, listHeight);
            viewport.gameObject.AddComponent<RectMask2D>();
            var dragSurface = viewport.gameObject.AddComponent<Image>();
            dragSurface.color = Color.clear;
            dragSurface.raycastTarget = true;
            float contentHeight = Mathf.Max(listHeight, rowHeight * lessons.Length);
            var content = UiFactory.Rect(viewport, "Tutorial lessons content", 0, 0, listWidth, contentHeight);
            for (int i = 0; i < lessons.Length; i++)
            {
                int index = i;
                GameButton(content, (i + 1) + ". " + lessons[i].Title, 0, i * rowHeight,
                    listWidth, rowHeight - 7, () => SelectLesson(index), i == lessonIndex);
            }
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 44;
            scroll.verticalNormalizedPosition = contentHeight > listHeight
                ? 1 - Mathf.Clamp01(lessonIndex * rowHeight / (contentHeight - listHeight)) : 1;
            GameButton(sheet, "返回练习", margin, height - 88, listWidth, 66, ClosePanel, true);
        }
    }
}
