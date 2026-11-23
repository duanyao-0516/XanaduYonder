using System;
using System.Collections.Generic;
using System.Text;

namespace XanaduYonder.Core.Feihualing
{
    public sealed partial class PoetryCatalog
    {
        public IReadOnlyList<int> WorkLineIds(int workIndex)
        {
            var ids = LinesInWork(workIndex);
            return ids == null ? Array.Empty<int>() : (IReadOnlyList<int>)Array.AsReadOnly(ids);
        }

        internal IReadOnlyList<int> ClueLines(int targetLineId, IReadOnlyList<int> allTargets, int lineLimit)
        {
            if (!HasLine(targetLineId, lineLimit))
                return Array.Empty<int>();
            var work = LinesInWork(Lines[targetLineId].WorkIndex);
            var clues = new List<int>(2);
            AddClue(precedingLines[targetLineId], targetLineId, allTargets, clues, lineLimit);
            AddClue(followingLines[targetLineId], targetLineId, allTargets, clues, lineLimit);

            if (clues.Count < 2)
            {
                var targetIndex = Array.IndexOf(work, targetLineId);
                for (var distance = 1; clues.Count < 2 && distance < work.Length; distance++)
                {
                    if (targetIndex >= distance)
                        AddClue(work[targetIndex - distance], targetLineId, allTargets, clues, lineLimit);
                    if (clues.Count < 2 && targetIndex + distance < work.Length)
                        AddClue(work[targetIndex + distance], targetLineId, allTargets, clues, lineLimit);
                }
            }
            return clues.AsReadOnly();
        }

        private void AddClue(int lineId, int targetLineId, IReadOnlyList<int> allTargets, List<int> clues, int lineLimit)
        {
            if (!HasLine(lineId, lineLimit) || Lines[lineId].Group == Lines[targetLineId].Group)
                return;
            var group = Lines[lineId].Group;
            foreach (var clue in clues)
                if (Lines[clue].Group == group)
                    return;

            var text = Text(lineId);
            if (string.Equals(text, MaskClue(text, targetLineId, allTargets, lineLimit), StringComparison.Ordinal))
                clues.Add(lineId);
        }

        internal string MaskClue(string text, int targetLineId, IReadOnlyList<int> allTargets, int lineLimit)
        {
            if (string.IsNullOrEmpty(text) || !HasLine(targetLineId, lineLimit) || !HasValidUtf16(text))
                return text;

            // Compare normalized forms without replacing punctuation in displayed source text.
            var display = text.Normalize(NormalizationForm.FormC);
            var comparison = CanonicalizeMarks(display);
            bool[] mask = null;
            var target = Lines[targetLineId].Group;
            MarkGroup(comparison, target, lineLimit, ref mask);
            foreach (var lineId in allTargets)
                if (Lines[lineId].Group != target)
                    MarkGroup(comparison, Lines[lineId].Group, lineLimit, ref mask);
            if (mask == null)
                return text;

            var output = new StringBuilder(display.Length);
            for (var index = 0; index < display.Length; index++)
            {
                if (!mask[index])
                    output.Append(display[index]);
                else
                {
                    output.Append('□');
                    if (char.IsHighSurrogate(display[index]))
                        index++;
                }
            }
            return output.ToString();
        }

        private void MarkGroup(string text, int group, int lineLimit, ref bool[] mask)
        {
            for (var id = groupHeads[group]; id >= 0; id = nextInGroup[id])
            {
                if (!HasLine(id, lineLimit))
                    continue;
                MarkForm(text, lineKeys[id], ref mask);
                foreach (var alias in Lines[id].Aliases)
                    MarkForm(text, Normalize(alias), ref mask);
            }
        }

        private static void MarkForm(string text, string key, ref bool[] mask)
        {
            for (var start = text.IndexOf(key, StringComparison.Ordinal); start >= 0;
                start = text.IndexOf(key, start + 1, StringComparison.Ordinal))
            {
                if (mask == null)
                    mask = new bool[text.Length];
                for (var index = start; index < start + key.Length; index++)
                    mask[index] = true;
            }
        }
    }
}
