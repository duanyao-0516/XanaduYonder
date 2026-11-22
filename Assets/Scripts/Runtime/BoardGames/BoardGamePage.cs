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
        private enum ScreenKind { Menu, Setup, Formation, Match, Tutorial }
        private enum PanelKind { None, Rules, Settings, Replace, Undo, Promote, Result, Record, Leave }
        private RectTransform root, view;
        private Action leave;
        private Action<Action> openGlobalSettings;
        private Func<bool> canUseHost, sound, haptics;
        private GameFeedback feedback;
        private string directory;
        private BoardGameId game;
        private ScreenKind screen, setupOrigin;
        private PanelKind panel;
        private BoardSaveStore store;
        private BoardProfile profile;
        private BoardMatch match;
        private BoardMatchOptions draft;
        private int[][] formations;
        private ArmyGame formationBoard;
        private int forming, selected = -1, preview = -1, lessonIndex;
        private BoardLesson[] lessons;
        private IBoardPosition lessonPosition;
        private bool lessonDone, lessonRetreated, trainingFormation, formationEdited;
        private readonly List<int> route = new List<int>();
        private BoardMove pendingMove = BoardMove.None;
        private BoardMove[] promotions = Array.Empty<BoardMove>();
        private bool shown, foreground, paused, handoff, saveFailed, aiError, routeIsStep;
        private string warning, notice;
        private Vector2 layoutSize;
        private CancellationTokenSource cancellation;
        private Task<BoardMove> search;
        private BoardMatch searchOwner;
        private int searchSide, searchRevision;
        private float nextSearchTime;
        private int seed;
        private IBoardPosition cachedPosition;
        private int cachedRevision = -1;
        private IReadOnlyList<BoardMove> cachedMoves = Array.Empty<BoardMove>();
        private ResultPresentation result;

        internal string SaveError => saveFailed ? "棋局尚未保存，可点「重试保存」" : warning;
        internal BoardMatch Match => match;
        private bool Allowed => shown && foreground && canUseHost();
        private bool InMatch => screen == ScreenKind.Match;
        private bool ArmyLocal => game == BoardGameId.Army && InMatch && match.Options.Local;
        private bool HumanTurn => InMatch && !match.Terminal && !match.Computer && !paused && !handoff && !aiError;
        private IBoardPosition CurrentPosition => screen == ScreenKind.Tutorial ? lessonPosition : InMatch ? match.Position : formationBoard;
        private bool CanSelect => Allowed && panel == PanelKind.None && !handoff &&
            (HumanTurn || screen == ScreenKind.Formation || (screen == ScreenKind.Tutorial && !lessonDone));

        internal void Initialize(RectTransform root, string directory, Action leave, Action<Action> openGlobalSettings,
            Func<bool> canUseHost, Func<bool> sound, Func<bool> haptics, GameFeedback feedback)
        {
            this.root = root; this.directory = directory; this.leave = leave; this.openGlobalSettings = openGlobalSettings;
            this.canUseHost = canUseHost; this.sound = sound; this.haptics = haptics; this.feedback = feedback;
            result = ResultPresentation.For(this, root);
            result.UseExistingReopenAction();
            root.gameObject.SetActive(false);
        }

        internal void Enter(BoardGameId id, bool active)
        {
            CancelSearch();
            game = id;
            store = new BoardSaveStore(directory, id);
            profile = store.Load(out warning);
            screen = ScreenKind.Menu;
            match = null;
            panel = PanelKind.None;
            ClearDraft();
            saveFailed = false;
            shown = true;
            foreground = active;
            paused = handoff = aiError = false;
            root.gameObject.SetActive(true);
            Render();
        }

        internal void Leave()
        {
            if (!shown) return;
            CancelSearch();
            result?.Reset();
            shown = false;
            root.gameObject.SetActive(false);
            DestroyView();
            ClearDraft();
            match = null;
            formationBoard = null;
            formations = null;
            lessonPosition = null;
        }

        internal void SetForeground(bool active)
        {
            foreground = active;
            if (!shown || active) return;
            if (panel != PanelKind.Result) panel = PanelKind.None;
            ClearDraft();
            CancelSearch();
            if (InMatch && !match.Terminal)
            {
                paused = true;
                if (ArmyLocal) handoff = true;
            }
            if (screen == ScreenKind.Formation && draft.Local) handoff = true;
            Render();
        }

        internal void Back(Func<bool> confirmExit)
        {
            if (panel != PanelKind.None) { ClosePanel(); return; }
            if (screen == ScreenKind.Setup) { screen = setupOrigin; Render(); return; }
            if (screen == ScreenKind.Formation) { CancelFormation(); return; }
            if (screen == ScreenKind.Tutorial) { ReturnToMenu(); return; }
            if (InMatch && !handoff) { ReturnToMenu(); return; }
            if (confirmExit()) RequestLeave();
        }

        private void RequestLeave()
        {
            if (!saveFailed) { leave(); return; }
            OpenPanel(PanelKind.Leave);
        }

        private void ReturnToMenu()
        {
            CancelSearch();
            panel = PanelKind.None;
            handoff = paused = aiError = false;
            ClearDraft();
            match = null;
            formationBoard = null;
            formations = null;
            trainingFormation = false;
            lessonPosition = null;
            screen = ScreenKind.Menu;
            Render();
        }

        private void OpenSetup()
        {
            CancelSearch();
            ClearDraft();
            setupOrigin = screen;
            draft = profile.Options;
            screen = ScreenKind.Setup;
            Render();
        }

        private void ResumeSlot(BoardMatch saved)
        {
            profile.Options = saved.Options;
            match = saved;
            screen = ScreenKind.Match;
            panel = PanelKind.None;
            ClearDraft();
            aiError = false;
            paused = !match.Terminal;
            handoff = ArmyLocal && !match.Terminal;
            result.Reset();
            Render();
        }

        private void RequestNew()
        {
            if (profile.Matches.ContainsKey(draft.Slot) || store.ProtectDamaged) OpenPanel(PanelKind.Replace);
            else BeginNew();
        }

        private void BeginNew()
        {
            panel = PanelKind.None;
            paused = handoff = false;
            ClearDraft();
            if (game != BoardGameId.Army) { StartMatch(new BoardMatch(game, draft)); return; }
            trainingFormation = formationEdited = false;
            formations = new[] { ArmyGame.CreateFormation(1709), ArmyGame.CreateFormation(draft.Local ? 2619 : System.Environment.TickCount) };
            forming = draft.Local ? 0 : draft.Human;
            if (!draft.Local) formations[1 - forming] = ArmyGame.CreateFormation(System.Environment.TickCount ^ 48271);
            RefreshFormation();
            screen = ScreenKind.Formation;
            Render();
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
                    formationEdited |= slot != other;
                    selected = -1;
                    notice = null;
                }
                else notice = "军旗只在大本营，地雷只在后两排，炸弹避开首排";
            }
            Render();
        }

        private static int[] FormationSlots(int side) => Enumerable.Range(0, 30).Select(slot => ArmyGame.FormationPoint(side, slot)).ToArray();

        private void ConfirmFormation()
        {
            if (trainingFormation)
            {
                if (!formationEdited) { notice = "先完成一次合法交换，或试用一套阵型"; Render(); return; }
                FinishTrainingFormation(true);
                return;
            }
            ClearDraft();
            if (draft.Local && forming == 0)
            {
                forming = 1;
                handoff = true;
                Render();
                return;
            }
            StartMatch(new BoardMatch(game, draft, formations[0], formations[1]));
            formationBoard = null;
            formations = null;
        }

        private void PracticeFormation()
        {
            ClearDraft();
            trainingFormation = true;
            formationEdited = false;
            forming = 0;
            draft = BoardMatchOptions.Default(BoardGameId.Army);
            formations = new[] { ArmyGame.CreateFormation(121), ArmyGame.CreateFormation(245) };
            RefreshFormation();
            screen = ScreenKind.Formation;
            Render();
        }

        private void CancelFormation()
        {
            if (trainingFormation) FinishTrainingFormation(false);
            else ReturnToMenu();
        }

        private void FinishTrainingFormation(bool done)
        {
            trainingFormation = false;
            formationBoard = null;
            formations = null;
            screen = ScreenKind.Tutorial;
            lessonDone = done;
            ClearDraft();
            Render();
        }

        private void StartMatch(BoardMatch value)
        {
            CancelSearch();
            match = value;
            profile.Options = value.Options;
            profile.Matches[value.Options.Slot] = value;
            screen = ScreenKind.Match;
            paused = aiError = false;
            panel = PanelKind.None;
            handoff = ArmyLocal;
            result.Reset();
            ClearDraft();
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
            if (screen == ScreenKind.Tutorial && lessons[lessonIndex].From < 0) return;
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
                    if (choices.Length > 1)
                    {
                        promotions = choices;
                        OpenPanel(PanelKind.Promote);
                        return;
                    }
                    pendingMove = choices[0];
                    if (game == BoardGameId.Army && InMatch && !profile.ConfirmArmyMove) { Commit(pendingMove); return; }
                }
                Render();
                return;
            }
            if (route.Count != 0) return;
            var piece = position.PieceAt(point, position.CurrentSide);
            if (piece.Kind > 0 && piece.Side == position.CurrentSide)
            {
                if (screen == ScreenKind.Tutorial && lessons[lessonIndex].From != point)
                { notice = "先选中本节要求的棋子"; Render(); return; }
                selected = point;
                preview = -1;
                pendingMove = BoardMove.None;
                notice = null;
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

        private void ConfirmMove()
        {
            var chosen = Prepared();
            if (chosen != BoardMove.None) Commit(chosen);
        }

        private void Commit(BoardMove move)
        {
            if (screen == ScreenKind.Tutorial)
            {
                var lesson = lessons[lessonIndex];
                if (lesson.UndoAfterHop > 0 && !lessonRetreated)
                { notice = "先跳两次，再点「退一跳」后补回这一跳"; Render(); return; }
                if (move.From != lesson.From || move.To != lesson.To ||
                    (lesson.Route.Length != 0 && !move.Path.SequenceEqual(lesson.Route)))
                { ClearDraft(); notice = "试试本节提示的落点"; Render(); return; }
                if (!lessonPosition.TryPlay(move)) return;
                ClearDraft();
                lessonDone = true;
                if (game == BoardGameId.Army && lessonIndex + 1 == lessons.Length) handoff = true;
                Render();
                return;
            }
            if (!InMatch || !match.Play(move)) return;
            CancelSearch();
            ClearDraft();
            aiError = false;
            handoff = ArmyLocal && !match.Terminal;
            nextSearchTime = Time.unscaledTime + (game == BoardGameId.Checkers ? .09f : .12f);
            Save();
            feedback?.Play(match.Terminal, sound(), haptics());
            if (match.Terminal)
            {
                result.Gate(match, () => Allowed && InMatch && match != null && match.Terminal, () => OpenPanel(PanelKind.Result));
                panel = PanelKind.Result;
            }
            Render();
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

        private void UndoRoute()
        {
            if (route.Count == 0) return;
            if (screen == ScreenKind.Tutorial && lessons[lessonIndex].UndoAfterHop > 0 &&
                route.Count >= lessons[lessonIndex].UndoAfterHop) lessonRetreated = true;
            route.RemoveAt(route.Count - 1);
            routeIsStep = false;
            Render();
        }

        private void RequestUndo()
        {
            if (!InMatch || !match.CanUndo || !Allowed) return;
            if (match.Options.Local) OpenPanel(PanelKind.Undo);
            else Undo();
        }

        private void Undo()
        {
            CancelSearch();
            if (!match.Undo()) { ClosePanel(); return; }
            ClearDraft();
            paused = handoff = aiError = false;
            panel = PanelKind.None;
            Save();
            Render();
        }

        private void Pause()
        {
            if (!InMatch || match.Terminal) return;
            CancelSearch();
            ClearDraft();
            paused = true;
            handoff = ArmyLocal;
            Render();
        }

        private void Continue()
        {
            if (!Allowed) return;
            paused = handoff = aiError = false;
            Render();
        }

        private void OpenPanel(PanelKind kind)
        {
            CancelSearch();
            panel = kind;
            Render();
        }

        private void ClosePanel()
        {
            if (panel == PanelKind.Promote) { ClearDraft(); }
            panel = PanelKind.None;
            Render();
        }

        private void OpenGlobal()
        {
            panel = PanelKind.None;
            if (InMatch && !match.Terminal) Pause();
            else Render();
            openGlobalSettings(Render);
        }

        private void StartTutorial()
        {
            panel = PanelKind.None;
            screen = ScreenKind.Tutorial;
            lessons = BoardTutorial.Create(game);
            lessonIndex = 0;
            profile.TutorialSeen = true;
            Save();
            StartLesson();
        }

        private void StartLesson()
        {
            lessonPosition = lessons[lessonIndex].Position.Copy();
            lessonDone = lessonRetreated = handoff = false;
            ClearDraft();
            Render();
        }

        private void NextLesson()
        {
            lessonIndex++;
            if (lessonIndex >= lessons.Length) ReturnToMenu();
            else StartLesson();
        }

        private void Update()
        {
            if (!shown) return;
            if (layoutSize != root.rect.size) Render();
            if (search != null && search.IsCompleted)
            {
                var finished = search;
                var owner = searchOwner;
                int side = searchSide, revision = searchRevision;
                search = null;
                searchOwner = null;
                cancellation.Dispose();
                cancellation = null;
                if (!Allowed || !InMatch || paused || panel != PanelKind.None || handoff ||
                    owner != match || side != match.Position.CurrentSide || revision != match.Revision) { _ = finished.Exception; return; }
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
            if (search != null || !Allowed || !InMatch || match.Terminal || !match.Computer || paused || handoff ||
                aiError || panel != PanelKind.None || Time.unscaledTime < nextSearchTime) return;
            searchOwner = match;
            searchSide = match.Position.CurrentSide;
            searchRevision = match.Revision;
            var strategy = match.Position.CaptureSearch();
            var difficulty = match.Options.Difficulty;
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
