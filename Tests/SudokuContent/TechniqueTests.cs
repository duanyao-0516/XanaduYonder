using System;
using System.Reflection;
using XanaduYonder.Core.Sudoku;

internal static class TechniqueTests
{
    internal static void Run()
    {
        int[] single = Full(); single[0] = 2;
        var naked = (SudokuHint)Invoke("NakedSingle", new int[81], single, 0);
        Program.Check(naked != null, "Naked single positive fixture"); ProofChecker.Apply((int[])single.Clone(), naked.Steps[0]);
        single[0] = 6; Program.Check(Invoke("NakedSingle", new int[81], single, 0) == null, "Naked single rejects two candidates");
        single = Full(); for (int i = 1; i < 9; i++) single[i] &= ~2;
        var hidden = (SudokuHint)Invoke("HiddenSingle", single, 0, (Action)(() => { }));
        Program.Check(hidden != null, "Hidden single positive fixture"); ProofChecker.Apply((int[])single.Clone(), hidden.Steps[0]);
        single[1] |= 2; Program.Check(Invoke("HiddenSingle", single, 0, (Action)(() => { })) == null, "Hidden single rejects another position");
        for (int size = 2; size <= 3; size++) for (int kind = 0; kind < 2; kind++)
        {
            bool isHidden = kind == 1; int digits = size == 2 ? 6 : 14;
            var c = Full();
            if (isHidden)
            {
                for (int i = size; i < 9; i++) c[i] &= ~digits;
                for (int i = 0; i < size; i++) c[i] = digits | (1 << (i + 4));
            }
            else for (int i = 0; i < size; i++) c[i] = digits;
            string name = (isHidden ? "隐藏" : "裸") + (size == 2 ? "数对" : "三数组");
            Verify("Subsets", c, name, size, isHidden);
            if (isHidden) c[size] |= digits; else c[0] |= 32;
            Program.Check(Invoke("Subsets", c, size, isHidden, (Action)(() => { })) == null, name + " rejects broken subset");
        }
        var locked = Full(); foreach (int cell in new[] { 2, 9, 10, 11, 18, 19, 20 }) locked[cell] &= ~32;
        Verify("Locked", locked, "区块排除"); locked[9] |= 32;
        Program.Check(Invoke("Locked", locked, (Action)(() => { })) == null, "Locked candidate rejects non-intersection position");
        for (int orientation = 0; orientation < 2; orientation++)
        {
            var wing = Full();
            for (int line = 0; line <= 3; line += 3) for (int p = 0; p < 9; p++) if (p != 1 && p != 7) wing[orientation == 0 ? line * 9 + p : p * 9 + line] &= ~32;
            Verify("XWing", wing, "X-Wing"); wing[orientation == 0 ? 0 : 0] |= 32;
            Program.Check(Invoke("XWing", wing, (Action)(() => { })) == null, "X-Wing rejects a third corner candidate");
        }
    }
    private static void Verify(string method, int[] candidates, string name, params object[] extra)
    {
        var before = (int[])candidates.Clone(); var working = (int[])candidates.Clone();
        var args = new object[extra.Length + 2]; args[0] = working; for (int i = 0; i < extra.Length; i++) args[i + 1] = extra[i]; args[args.Length - 1] = (Action)(() => { });
        object elimination = Invoke(method, args);
        Program.Check(elimination != null, name + " positive fixture produces an elimination");
        var proof = (SudokuReasoningStep)elimination.GetType().GetField("Proof", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(elimination);
        Program.Check(proof.Technique == name, "Correct technique label");
        ProofChecker.Apply(before, proof);
        Program.Check(string.Join(",", before) == string.Join(",", working), name + " proof accounts for every changed candidate");
        var corrupt = proof.Copy(); corrupt.RemovedMasks[0] = 1022;
        bool rejected = false; try { ProofChecker.Apply((int[])candidates.Clone(), corrupt); } catch (Exception) { rejected = true; }
        Program.Check(rejected, name + " independent checker rejects invalid candidate removal");
    }
    private static object Invoke(string name, params object[] args) { return typeof(SudokuHintEngine).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
    private static int[] Full() { var c = new int[81]; for (int i = 0; i < 81; i++) c[i] = 1022; return c; }
}
