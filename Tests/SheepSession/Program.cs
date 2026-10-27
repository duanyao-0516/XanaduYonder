using System;
using XanaduYonder.Core.Sheep;
using XanaduYonder.Runtime.Sheep;
class Program
{
    static int failed, passed;
    static void Main()
    {
        Check("empty session never creates an unsolicited run", () => { var s = new SheepSession(new SheepSaveData()); Assert(s.Game == null); Assert(!s.Resume(SheepMode.Challenge)); });
        Check("preferences freeze at start and modes resume independently", () => {
            var d = new SheepSaveData(); var s = new SheepSession(d); s.Start(SheepMode.Challenge, SheepDifficulty.Hard, 17);
            var id = SheepLevelGenerator.Generate(SheepMode.Challenge, SheepDifficulty.Hard, 17).SolutionIds[0];
            s.Game.Pick(id); s.Capture(); var original = s.Game.GetSnapshot();
            var p = d.Preferences.Copy(); p.RemoveLimit = 9; s.SetPreferences(p); Assert(s.Game.GetSnapshot().RemoveLimit == 2);
            s.Start(SheepMode.Endless, SheepDifficulty.Normal, 18); Assert(s.Game.GetSnapshot().RemoveLimit == 9);
            Assert(s.Resume(SheepMode.Challenge)); Assert(s.Game.GetSnapshot().Tray.Length == original.Tray.Length); Assert(s.Game.GetSnapshot().Difficulty == SheepDifficulty.Hard);
        });
        Check("resuming current mode captures latest move and invalid modes do not mutate", () => {
            var d = new SheepSaveData(); var s = new SheepSession(d); s.Start(SheepMode.Tutorial, SheepDifficulty.Normal, 5);
            s.Game.Pick(SheepLevelGenerator.Generate(1, 5).SolutionIds[0]); Assert(s.Resume(SheepMode.Tutorial)); Assert(s.Game.GetSnapshot().Tray.Length == 1);
            Assert(!s.Resume((SheepMode)99)); Assert(d.ActiveMode == SheepMode.Tutorial);
        });
        Check("remembered difficulty commits on start", () => { var d = new SheepSaveData(); var s = new SheepSession(d); s.StartChallenge(SheepDifficulty.Extreme, true, 19); Assert(d.Preferences.SkipDifficultyPrompt); Assert(d.Preferences.DefaultDifficulty == SheepDifficulty.Extreme); Assert(s.Game.GetSnapshot().Difficulty == SheepDifficulty.Extreme); });
        Check("challenge completion counted once across mode switches and restore", () => {
            var d = new SheepSaveData(); var s = new SheepSession(d); s.Start(SheepMode.Challenge, SheepDifficulty.Easy, 20);
            foreach(var id in SheepLevelGenerator.Generate(SheepMode.Challenge, SheepDifficulty.Easy, 20).SolutionIds) s.Game.Pick(id);
            s.Capture(); s.Capture(); Assert(d.CompletedRuns == 1 && d.WinRecorded);
            s.Start(SheepMode.Endless, SheepDifficulty.Normal, 21); s.Resume(SheepMode.Challenge); new SheepSession(d).Capture(); Assert(d.CompletedRuns == 1);
        });
        Check("custom endless score cannot replace standard record", () => {
            var d = new SheepSaveData(); var s = new SheepSession(d); s.Start(SheepMode.Endless, SheepDifficulty.Normal, 22);
            foreach(var id in SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 22).SolutionIds) s.Game.Pick(id);
            s.Capture(); Assert(d.EndlessBest == 144); Assert(s.AdvanceWave()); Assert(s.Game.GetSnapshot().Score == 144);
            var p = d.Preferences.Copy(); p.ShuffleLimit = 9; s.SetPreferences(p);
            int previous = d.Endless.LayoutId; s.Start(SheepMode.Endless, SheepDifficulty.Normal, 23);
            var generated = SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 23, 2, 3, 9, previous);
            foreach(var id in generated.SolutionIds) Assert(s.Game.Pick(id).Changed);
            Assert(s.Game.Phase == SheepPhase.Won); s.Capture(); Assert(s.AdvanceWave());
            var wave = SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, generated.Snapshot.RandomState, 2, 3, 9, generated.Snapshot.LayoutId);
            foreach(var id in wave.SolutionIds) Assert(s.Game.Pick(id).Changed);
            s.Capture(); Assert(s.Game.GetSnapshot().Score == 288); Assert(d.EndlessBest == 144); Assert(s.IsCustomRun);
        });
        Console.WriteLine("Sheep session checks: " + passed + " passed, " + failed + " failed."); Environment.ExitCode=failed==0?0:1;
    }
    static void Assert(bool value) { if(!value) throw new Exception("assertion failed"); }
    static void Check(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS "+name); } catch(Exception e) {failed++;Console.WriteLine("FAIL "+name+": "+e.Message);} }
}
