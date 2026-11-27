using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // DialogTypography owns the reading column. Lay out paragraphs vertically using
    // Text.preferredHeight after their available width or font size changes.
    internal sealed class DialogParagraphLayout : VerticalLayoutGroup
    {
        public override void SetLayoutHorizontal() { }
    }
}
