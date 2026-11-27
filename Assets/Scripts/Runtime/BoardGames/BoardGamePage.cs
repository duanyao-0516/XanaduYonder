using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed partial class BoardGamePage : MonoBehaviour
    {
        private const float CheckersComputerTurnPause = 1f;
        private enum ScreenKind { Setup, Formation, Match, Tutorial, Replay }
        private enum PanelKind { None, Rules, Settings, Resume, Replace, Undo, Promote, Result, Record, Leave, Lessons, Preferences, Feedback, Manage }
        private RectTransform root, view;
        private Action leave;
        private Func<bool> canUseHost, sound, haptics;
        private Func<bool, bool, bool> setFeedback;
        private GameFeedback feedback;
        private string directory;
        private BoardGameId game;
        private ScreenKind screen, setupOrigin, tutorialOrigin;
        private PanelKind panel;
        private BoardSaveStore store;
        private BoardProfile profile;
        private BoardMatch match;
        private BoardMatchOptions draft, formationOptions;
        private int[][] formations;
        private ArmyGame formationBoard;
        private (BoardMatchOptions Options, int[][] Ranks, ArmyGame Board, int Side, bool Edited, bool ReplaceDamaged) formationBeforePractice;
        private int forming, selected = -1, preview = -1, lessonIndex;
        private BoardLesson[] lessons;
        private IBoardPosition lessonPosition;
        private bool lessonDone, trainingFormation, formationEdited, formationReplacementApproved, coverAfterTutorial, coverAfterSetup;
        private readonly List<int> route = new List<int>();
        private BoardMove lessonLastMove = BoardMove.None;
        private BoardMove pendingMove = BoardMove.None;
        private BoardMove suggestion = BoardMove.None;
        private BoardMove xiangqiMotion = BoardMove.None;
        private float xiangqiMotionStartedAt;
        private int xiangqiMotionActor;
        private BoardMove[] promotions = Array.Empty<BoardMove>();
        private bool shown, foreground, paused, handoff, saveFailed, feedbackSaveFailed, aiError, routeIsStep;
        private bool lastFeedbackWasSound;
        private string warning, notice;
        private Vector2 layoutSize;
        private CancellationTokenSource cancellation;
        private Task<BoardMove> search;
        private bool searchingHint;
        private BoardMatch searchOwner;
        private int searchSide, searchRevision;
        private float nextSearchTime;
        private int seed;
        private IBoardPosition cachedPosition;
        private int cachedRevision = -1;
        private IReadOnlyList<BoardMove> cachedMoves = Array.Empty<BoardMove>();
        private ResultPresentation result;

        internal string SaveError => saveFailed ? "棋局尚未保存，可在设置中重试" : warning;
        internal BoardMatch Match => match;
        private bool Allowed => shown && foreground && canUseHost();
        private bool InMatch => screen == ScreenKind.Match;
        private bool CanReplay => InMatch && match.MoveCount > 0 && (game != BoardGameId.Army || match.Terminal);
        private bool CanHint => profile.HintsEnabled && Allowed && HumanTurn && panel == PanelKind.None &&
            search == null && suggestion == BoardMove.None && match.HintsUsed < BoardMatch.HintLimit &&
            (game != BoardGameId.Checkers || !LegalMoves().Contains(BoardMove.Pass));
        private bool HasOpposingSeatLabels => game == BoardGameId.Chess || game == BoardGameId.Xiangqi;
        private bool DirectMoves => InMatch && !profile.ConfirmBoardMove;
        private bool ArmyLocal => game == BoardGameId.Army && InMatch && match.Options.Local;
        private bool XiangqiAnimating => xiangqiMotion != BoardMove.None;
        private bool HumanTurn => InMatch && !store.ProtectDamaged && !match.Terminal && !match.Computer && !paused && !handoff &&
            !aiError && !XiangqiAnimating;
        private bool CanClaimDraw => game == BoardGameId.Chess && Allowed && HumanTurn && LegalMoves().Contains(BoardMove.ClaimDraw);
        private bool FormationNeedsReplacement => !trainingFormation && store.ProtectDamaged && !formationReplacementApproved;
        private bool PracticingFormationRestriction => trainingFormation && lessons[lessonIndex].Kind == BoardLessonKind.FormationRestriction;
        private IBoardPosition CurrentPosition => screen == ScreenKind.Tutorial ? lessonPosition : InMatch ? match.Position : formationBoard;
        private bool CanSelect => Allowed && panel == PanelKind.None && !handoff &&
            (HumanTurn || screen == ScreenKind.Formation || (screen == ScreenKind.Tutorial && !lessonDone && lessons[lessonIndex].UsesBoard));

        internal void Initialize(RectTransform root, string directory, Action leave, Func<bool> canUseHost,
            Func<bool> sound, Func<bool> haptics, Func<bool, bool, bool> setFeedback, GameFeedback feedback)
        {
            this.root = root; this.directory = directory; this.leave = leave;
            this.canUseHost = canUseHost; this.sound = sound; this.haptics = haptics; this.setFeedback = setFeedback; this.feedback = feedback;
            result = ResultPresentation.For(this, root);
            result.UseExistingReopenAction();
            root.gameObject.SetActive(false);
        }

        internal void Enter(BoardGameId id, bool active)
        {
            CancelSearch();
            xiangqiMotion = BoardMove.None;
            game = id;
            store = new BoardSaveStore(directory, id);
            profile = store.Load(out warning);
            match = null;
            suggestion = BoardMove.None;
            panel = PanelKind.None;
            renderedPanel = PanelKind.None;
            dialogFocus.Clear();
            ClearDraft();
            saveFailed = feedbackSaveFailed = false;
            shown = true;
            foreground = active;
            paused = handoff = aiError = false;
            root.gameObject.SetActive(true);
            if (profile.Matches.TryGetValue(profile.Options.Slot, out var saved)) ActivateMatch(saved, true);
            else if (game == BoardGameId.Army) BeginFormation(profile.Options);
            else ActivateMatch(new BoardMatch(game, profile.Options), false);
            Render();
        }

        internal void Leave()
        {
            if (!shown) return;
            CancelSearch();
            xiangqiMotion = BoardMove.None;
            result?.Reset();
            shown = false;
            root.gameObject.SetActive(false);
            DestroyView();
            ClearDraft();
            match = null;
            formationBoard = null;
            formations = null;
            replay = null;
            formationBeforePractice = default;
            trainingFormation = false;
            lessonPosition = null;
            lessons = null;
        }

        internal void SetForeground(bool active)
        {
            foreground = active;
            if (!shown) return;
            if (active) { Render(); return; }
            if (screen == ScreenKind.Replay)
            {
                autoReplay = manualReplay = false;
                if (!match.Terminal) paused = true;
                Render();
                return;
            }
            if (panel != PanelKind.Result) panel = PanelKind.None;
            ClearDraft();
            CancelSearch();
            bool inTutorial = screen == ScreenKind.Tutorial || trainingFormation;
            ScreenKind activeScreen = inTutorial ? tutorialOrigin : screen == ScreenKind.Setup ? setupOrigin : screen;
            if (activeScreen == ScreenKind.Match && !match.Terminal)
                paused = true;
            if (activeScreen == ScreenKind.Formation && (trainingFormation ? formationBeforePractice.Options : formationOptions).Local)
                RequireHandoff(inTutorial);
            Render();
        }

        private void RequireHandoff(bool inTutorial)
        {
            if (inTutorial) coverAfterTutorial = true;
            else if (screen == ScreenKind.Setup) coverAfterSetup = true;
            else handoff = true;
        }

        internal void Back(Func<bool> confirmExit)
        {
            if (panel != PanelKind.None) { ClosePanel(); return; }
            if (screen == ScreenKind.Replay) { ExitReplay(); return; }
            if (screen == ScreenKind.Setup) { CancelSetup(); return; }
            if (screen == ScreenKind.Tutorial) { ExitTutorial(); return; }
            if (handoff) { if (confirmExit()) RequestLeave(); return; }
            if (screen == ScreenKind.Formation && (trainingFormation || match != null)) { CancelFormation(); return; }
            if (confirmExit()) RequestLeave();
        }

        private void RequestLeave()
        {
            if (!saveFailed) { leave(); return; }
            OpenPanel(PanelKind.Leave);
        }

        private void OpenSetup()
        {
            CancelSearch();
            xiangqiMotion = BoardMove.None;
            ClearDraft();
            setupOrigin = screen;
            draft = screen == ScreenKind.Formation ? formationOptions : profile.Options;
            screen = ScreenKind.Setup;
            panel = PanelKind.None;
            coverAfterSetup = false;
            Render();
        }

        private void CancelSetup()
        {
            screen = setupOrigin;
            handoff = coverAfterSetup;
            Render();
        }

        private void ActivateMatch(BoardMatch value, bool resume)
        {
            CancelSearch();
            xiangqiMotion = BoardMove.None;
            profile.Options = value.Options;
            match = value;
            suggestion = BoardMove.None;
            screen = ScreenKind.Match;
            panel = PanelKind.None;
            ClearDraft();
            aiError = false;
            paused = resume && !match.Terminal;
            handoff = ArmyLocal && !paused && !match.Terminal;
            nextSearchTime = 0;
            result.Reset();
        }

        private void RequestNew()
        {
            if (profile.Matches.ContainsKey(draft.Slot) || store.ProtectDamaged) OpenPanel(PanelKind.Replace);
            else BeginNew();
        }

        private void BeginNew()
        {
            if (game == BoardGameId.Army)
            {
                BeginFormation(draft, store.ProtectDamaged);
                Render();
            }
            else StartMatch(new BoardMatch(game, draft));
        }

        private void BeginFormation(BoardMatchOptions options, bool replaceDamaged = false)
        {
            panel = PanelKind.None;
            handoff = false;
            ClearDraft();
            trainingFormation = formationEdited = false;
            formationOptions = options;
            formationReplacementApproved = replaceDamaged;
            formations = new[] { ArmyGame.CreateFormation(1709), ArmyGame.CreateFormation(2619) };
            forming = options.Local ? 0 : options.Human;
            if (!options.Local) formations[1 - forming] = ArmyGame.CreateFormation(System.Environment.TickCount ^ 48271);
            RefreshFormation();
            screen = ScreenKind.Formation;
        }

        private void RefreshFormation() { formationBoard = new ArmyGame(formations[0], formations[1]); }

        private void SetFormation(bool random)
        {
            formations[forming] = ArmyGame.CreateFormation(random ? System.Environment.TickCount ^ ++seed : 1709 + forming * 910);
            RefreshFormation();
            formationEdited = true;
            ClearDraft();
            Render();
        }

        private void FormationPoint(int point)
        {
            bool restriction = PracticingFormationRestriction;
            if (restriction && (formationEdited || point != (selected < 0 ? lessons[lessonIndex].From : lessons[lessonIndex].To))) return;
            int slot = Array.FindIndex(FormationSlots(forming), at => at == point);
            if (slot < 0 || formations[forming][slot] == 0) return;
            if (selected < 0) selected = point;
            else
            {
                int other = Array.FindIndex(FormationSlots(forming), at => at == selected);
                int[] attempt = (int[])formations[forming].Clone();
                (attempt[slot], attempt[other]) = (attempt[other], attempt[slot]);
                if (ArmyGame.IsFormationValid(attempt))
                {
                    formations[forming] = attempt;
                    RefreshFormation();
                    formationEdited |= !restriction && slot != other;
                    selected = -1;
                    notice = null;
                }
                else if (restriction)
                {
                    selected = -1;
                    formationEdited = true;
                    notice = lessons[lessonIndex].Outcome;
                }
                else notice = "军旗只在大本营，地雷只在后两排，炸弹避开首排";
            }
            Render();
        }

        private static int[] FormationSlots(int side) => Enumerable.Range(0, 30).Select(slot => ArmyGame.FormationPoint(side, slot)).ToArray();

        private void ConfirmFormation()
        {
            if (FormationNeedsReplacement) return;
            if (trainingFormation)
            {
                if (!formationEdited) { notice = "先完成一次合法交换，或试用一套阵型"; Render(); return; }
                FinishTrainingFormation(true);
                return;
            }
            ClearDraft();
            if (formationOptions.Local && forming == 0)
            {
                forming = 1;
                handoff = true;
                Render();
                return;
            }
            StartMatch(new BoardMatch(game, formationOptions, formations[0], formations[1]));
            formationBoard = null;
            formations = null;
        }

        private void PracticeFormation()
        {
            formationBeforePractice = (formationOptions, formations, formationBoard, forming, formationEdited, formationReplacementApproved);
            BeginFormation(BoardMatchOptions.Default(BoardGameId.Army));
            trainingFormation = true;
            Render();
        }

        private void CancelFormation()
        {
            if (trainingFormation) FinishTrainingFormation(false);
            else if (match == null) RequestLeave();
            else
            {
                formationBoard = null;
                formations = null;
                ActivateMatch(match, true);
                Render();
            }
        }

        private void FinishTrainingFormation(bool done)
        {
            trainingFormation = false;
            (formationOptions, formations, formationBoard, forming, formationEdited, formationReplacementApproved) = formationBeforePractice;
            formationBeforePractice = default;
            screen = ScreenKind.Tutorial;
            lessonDone = done;
            ClearDraft();
            Render();
        }

        private void StartMatch(BoardMatch value)
        {
            ActivateMatch(value, false);
            profile.Matches[value.Options.Slot] = value;
            store.AllowReplacement();
            warning = null;
            Save();
            Render();
        }

        private void Save()
        {
            if (store == null || store.ProtectDamaged) return;
            saveFailed = !store.Save(profile);
            if (!saveFailed) warning = null;
        }

        private IReadOnlyList<BoardMove> LegalMoves()
        {
            var position = CurrentPosition;
            int revision = InMatch ? match.Revision : 0;
            if (cachedPosition != position || cachedRevision != revision)
            {
                cachedPosition = position;
                cachedRevision = revision;
                cachedMoves = position?.LegalMoves() ?? Array.Empty<BoardMove>();
            }
            return cachedMoves;
        }

        private IEnumerable<int> Targets()
        {
            if (!CanSelect || selected < 0 || screen == ScreenKind.Formation || routeIsStep) return Array.Empty<int>();
            if (game != BoardGameId.Checkers) return LegalMoves().Where(move => move.From == selected).Select(move => move.To);
            var jump = (ChineseCheckersGame)CurrentPosition;
            var next = jump.Continuations(selected, route);
            return route.Count > 0 ? next : next.Concat(LegalMoves().Where(move => move.From == selected && move.PathLength == 0).Select(move => move.To));
        }

        private void Point(int point)
        {
            if (!CanSelect) return;
            if (screen == ScreenKind.Formation) { FormationPoint(point); return; }
            if (screen == ScreenKind.Tutorial && lessons[lessonIndex].Kind == BoardLessonKind.Blocked)
            {
                TryBlockedLesson(point);
                return;
            }
            int destination = route.Count > 0 ? route[route.Count - 1] : preview;
            if (point == destination)
            {
                TryConfirmMove();
                return;
            }
            var position = CurrentPosition;
            if (selected >= 0 && Targets().Contains(point))
            {
                notice = null;
                if (game == BoardGameId.Checkers)
                {
                    routeIsStep = route.Count == 0 && !((ChineseCheckersGame)position).Continuations(selected, route).Contains(point);
                    route.Add(point);
                }
                else
                {
                    var choices = LegalMoves().Where(move => move.From == selected && move.To == point).ToArray();
                    preview = point;
                    promotions = choices.Length > 1 ? choices : Array.Empty<BoardMove>();
                    pendingMove = choices.Length == 1 ? choices[0] : BoardMove.None;
                }
                if (DirectMoves && TryConfirmMove()) return;
                Render();
                return;
            }
            var piece = position.PieceAt(point, position.CurrentSide);
            if (piece.Kind > 0 && piece.Side == position.CurrentSide)
            {
                if (screen == ScreenKind.Tutorial && lessons[lessonIndex].From != point)
                { notice = "先选中本节要求的棋子"; Render(); return; }
                ClearDraft();
                selected = point;
                Render();
            }
        }

        private BoardMove Prepared()
        {
            if (game != BoardGameId.Checkers) return pendingMove;
            if (routeIsStep)
            {
                foreach (var move in LegalMoves())
                    if (move.From == selected && move.To == route[0] && move.PathLength == 0) return move;
                return BoardMove.None;
            }
            return selected >= 0 && route.Count != 0 ?
                ((ChineseCheckersGame)CurrentPosition).CompleteTurn(selected, route) : BoardMove.None;
        }

        private bool TryConfirmMove()
        {
            if (promotions.Length > 0)
            {
                OpenPanel(PanelKind.Promote);
                return true;
            }
            var chosen = Prepared();
            if (chosen == BoardMove.None) return false;
            Commit(chosen);
            return true;
        }

        private void Commit(BoardMove move)
        {
            if (screen == ScreenKind.Tutorial)
            {
                var lesson = lessons[lessonIndex];
                bool expected = lesson.Kind == BoardLessonKind.ClaimDraw ? move == BoardMove.ClaimDraw :
                    lesson.Kind == BoardLessonKind.Pass ? move == BoardMove.Pass : lesson.Kind == BoardLessonKind.Move &&
                    move.From == lesson.From && move.To == lesson.To && (lesson.Promotion == 0 || lesson.Promotion == move.Choice) &&
                    (lesson.Route.Length == 0 || move.Path.SequenceEqual(lesson.Route));
                if (!expected)
                {
                    ClearDraft();
                    notice = lesson.Promotion != 0 && move.Choice != 0 ? "请选本节指定的升变棋子" : "试试本节提示的落点";
                    Render();
                    return;
                }
                if (!lessonPosition.TryPlay(move)) return;
                ClearDraft();
                lessonLastMove = move;
                lessonDone = true;
                StartXiangqiMotion(move);
                Render();
                return;
            }
            if (!InMatch) return;
            bool computerMoved = match.Computer;
            int actor = match.Position.CurrentSide;
            if (!match.Play(move)) return;
            CancelSearch();
            ClearDraft();
            suggestion = BoardMove.None;
            aiError = false;
            handoff = ArmyLocal && !match.Terminal;
            profile.Matches[match.Options.Slot] = match;
            Save();
            feedback?.Play(match.Terminal, sound(), haptics());
            StartXiangqiMotion(move, actor);
            if (match.Terminal)
            {
                result.Gate(match, () => Allowed && InMatch && match != null && match.Terminal && !XiangqiAnimating,
                    () => OpenPanel(PanelKind.Result));
                if (!XiangqiAnimating) panel = PanelKind.Result;
            }
            bool betweenComputers = game == BoardGameId.Checkers && match.Options.Seats > 2 && computerMoved && match.Computer;
            bool afterHuman = game != BoardGameId.Checkers && !computerMoved && match.Computer;
            float pause = betweenComputers ? CheckersComputerTurnPause : afterHuman ? .5f : game == BoardGameId.Checkers ? .09f : .12f;
            nextSearchTime = Time.unscaledTime + pause;
            Render();
        }

        private void StartXiangqiMotion(BoardMove move, int actor = 0)
        {
            if (game != BoardGameId.Xiangqi || move.From < 0 || move.To < 0) return;
            xiangqiMotion = move;
            xiangqiMotionStartedAt = Time.unscaledTime;
            xiangqiMotionActor = actor;
        }

        private void ClearDraft()
        {
            selected = preview = -1;
            pendingMove = BoardMove.None;
            promotions = Array.Empty<BoardMove>();
            route.Clear();
            routeIsStep = false;
            notice = null;
        }

        private void RequestUndo()
        {
            if (!InMatch || !match.CanUndo || !Allowed) return;
            if (match.Options.Local) OpenPanel(PanelKind.Undo);
            else Undo();
        }

        private void ClaimDraw()
        {
            if (!CanClaimDraw) return;
            panel = PanelKind.None;
            Commit(BoardMove.ClaimDraw);
        }

        private void Undo()
        {
            CancelSearch();
            if (!match.Undo()) { ClosePanel(); return; }
            xiangqiMotion = BoardMove.None;
            ClearDraft();
            suggestion = BoardMove.None;
            paused = handoff = aiError = false;
            panel = PanelKind.None;
            Save();
            Render();
        }

        private void Pause()
        {
            if (!InMatch || match.Terminal) return;
            CancelSearch();
            xiangqiMotion = BoardMove.None;
            ClearDraft();
            panel = PanelKind.None;
            paused = true;
            Render();
        }

        private void Continue()
        {
            if (!Allowed) return;
            handoff = false;
            if (screen != ScreenKind.Tutorial) paused = aiError = false;
            Render();
        }

        private void OpenPanel(PanelKind kind)
        {
            if (kind != panel) dialogFocus.Remove(kind);
            CancelSearch();
            if (kind == PanelKind.Record)
            {
                int viewer = -1;
                if (game == BoardGameId.Army && !handoff && (!match.Options.Local || !paused))
                    viewer = match.Options.Local ? match.Position.CurrentSide : match.Options.Human;
                recordLines = match.DescribeHistory(viewer);
                recordScrollOffset = 0;
            }
            panel = kind;
            Render();
        }

        private void ClosePanel()
        {
            if (panel == PanelKind.Promote) { ClearDraft(); }
            switch (panel)
            {
                case PanelKind.Resume: panel = PanelKind.Manage; break;
                case PanelKind.Record:
                case PanelKind.Preferences:
                case PanelKind.Feedback:
                case PanelKind.Manage: panel = PanelKind.Settings; break;
                default: panel = PanelKind.None; break;
            }
            Render();
        }

        private void SetFeedback(bool soundSetting, bool value)
        {
            lastFeedbackWasSound = soundSetting;
            feedbackSaveFailed = !setFeedback(soundSetting, value);
            Render();
        }

        private void StartTutorial()
        {
            panel = PanelKind.None;
            tutorialOrigin = screen;
            coverAfterTutorial = (ArmyLocal && !paused && !match.Terminal) || (screen == ScreenKind.Formation && formationOptions.Local);
            screen = ScreenKind.Tutorial;
            lessons = BoardTutorial.Create(game);
            lessonIndex = 0;
            profile.TutorialSeen = true;
            Save();
            StartLesson();
        }

        private void ExitTutorial()
        {
            xiangqiMotion = BoardMove.None;
            screen = tutorialOrigin;
            handoff = coverAfterTutorial;
            lessons = null;
            lessonPosition = null;
            ClearDraft();
            Render();
        }

        private void StartLesson()
        {
            xiangqiMotion = BoardMove.None;
            panel = PanelKind.None;
            lessonPosition = lessons[lessonIndex].Position.Copy();
            lessonLastMove = BoardMove.None;
            lessonDone = handoff = false;
            ClearDraft();
            Render();
        }

        private void NextLesson()
        {
            lessonIndex++;
            if (lessonIndex >= lessons.Length) ExitTutorial();
            else StartLesson();
        }

        private void TryBlockedLesson(int point)
        {
            var lesson = lessons[lessonIndex];
            if (point == lesson.From)
            {
                selected = point;
                notice = null;
                Render();
            }
            else if (point == lesson.To && selected == lesson.From &&
                !lessonPosition.LegalMoves().Any(move => move.From == lesson.From && move.To == lesson.To))
            {
                ClearDraft();
                lessonDone = true;
                Render();
            }
        }

        private void Update()
        {
            if (!shown) return;
            if (XiangqiAnimating && Time.unscaledTime >= xiangqiMotionStartedAt + BoardSurface.XiangqiMotionSeconds)
            {
                xiangqiMotion = BoardMove.None;
                if (InMatch && match.Terminal && panel == PanelKind.None && Allowed) panel = PanelKind.Result;
                Render();
            }
            if (layoutSize != root.rect.size) Render();
            if (screen == ScreenKind.Replay) { UpdateReplay(); return; }
            if (search != null && search.IsCompleted)
            {
                var finished = search;
                var owner = searchOwner;
                int side = searchSide, revision = searchRevision;
                bool hint = searchingHint;
                search = null;
                searchOwner = null;
                searchingHint = false;
                cancellation.Dispose();
                cancellation = null;
                if (!Allowed || !InMatch || paused || panel != PanelKind.None || handoff ||
                    owner != match || side != match.Position.CurrentSide || revision != match.Revision) { _ = finished.Exception; return; }
                if (hint)
                {
                    _ = finished.Exception;
                    ReceiveHint(finished.Status == TaskStatus.RanToCompletion ? finished.Result : BoardMove.None);
                    return;
                }
                if (finished.Status == TaskStatus.RanToCompletion && finished.Result != BoardMove.None)
                {
                    int before = match.Revision;
                    Commit(finished.Result);
                    if (match.Revision != before) return;
                }
                _ = finished.Exception;
                aiError = true;
                Render();
            }
            if (search != null || !Allowed || !InMatch || store.ProtectDamaged || match.Terminal || !match.Computer || paused || handoff ||
                aiError || XiangqiAnimating || panel != PanelKind.None || Time.unscaledTime < nextSearchTime) return;
            BeginSearch(false);
        }

        private void BeginSearch(bool hint)
        {
            searchingHint = hint;
            searchOwner = match;
            searchSide = match.Position.CurrentSide;
            searchRevision = match.Revision;
            var position = match.Position;
            BoardSearch strategy;
            if (hint && position is ChessGame chess) strategy = chess.CaptureHintSearch();
            else if (hint && DirectMoves && position is ChineseCheckersGame checkers) strategy = checkers.CaptureSingleTapHintSearch();
            else strategy = position.CaptureSearch();
            var difficulty = hint ? BoardDifficulty.Hard : match.Options.Difficulty;
            int salt = ++seed;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            search = Task.Run(() => strategy(difficulty, salt, token), token);
        }

        private void CancelSearch()
        {
            if (search == null) return;
            var old = search;
            var token = cancellation;
            search = null;
            searchOwner = null;
            searchingHint = false;
            cancellation = null;
            token.Cancel();
            old.ContinueWith(finished =>
            {
                _ = finished.Exception;
                token.Dispose();
            }, TaskScheduler.Default);
        }

        private void OnDisable() { CancelSearch(); }
        private void OnDestroy() { CancelSearch(); }
    }
}
