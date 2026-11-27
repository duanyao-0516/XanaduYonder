using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.BakeryBasket;

namespace XanaduYonder.Runtime.BakeryBasket
{
    internal sealed class BakeryPage : MonoBehaviour
    {
        internal const string DisplayName = "抓大鹅";
        private const float HighlightTime = .085f;
        private const float FlightTime = .29f;
        private const float MatchTime = .17f;
        private const float ShakeTime = 1.24f;
        private readonly System.Random entryRandom = new System.Random();
        private BakeryWorld world;
        private readonly Dictionary<BakeryTheme, BakeryModelCache> modelsByTheme = new Dictionary<BakeryTheme, BakeryModelCache>();
        private BakeryRound round;
        private RawImage image;
        private BakeryPointer input;
        private Button straightButton, taperedButton, shakeButton, moveOutButton;
        private readonly Button[] reserveButtons = new Button[BakeryRound.ReserveSlotCount];
        private Text score, bottom, observation, message;
        private Text moveOutLabel;
        private BakeryPreferences preferences;
        private GameFeedback feedback;
        private Func<bool> hostAcceptsInput, sound, haptics;
        private bool tapered;
        private bool entranceCountsPending;
        private bool waitingForRest, hasShakeResult;
        private float restTimer, shakeTimer, nextShakeSample, accumulator, pressedAt;
        private int pressed = -1;
        private readonly List<Flight> flights = new List<Flight>();
        private readonly List<int> displayedTray = new List<int>(BakeryRound.SlotCount);
        private int[] matching;
        private float matchElapsed;
        private List<int> shakePieces;
        private HashSet<int> stillHidden;
        private int shakeBefore, shakeCount, shakeAfter;
        internal int NewlyVisibleDuringShake { get; private set; }

        internal BakeryWorld World => world;
        internal BakeryRound Round => round;
        internal IReadOnlyList<int> DisplayedTray => displayedTray;
        internal BakeryTheme ActiveTheme { get; private set; }

        private sealed class Flight
        {
            public int Id, Slot;
            public int[] Match;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Elapsed, Scale;
            public bool FromReserve;
        }

        internal void Initialize(RectTransform root, Action lobby, Func<bool> acceptsInput, Func<bool> soundEnabled,
            Func<bool> hapticsEnabled, GameFeedback commonFeedback, BakeryPreferences display, Action help, Action settings)
        {
            hostAcceptsInput = acceptsInput;
            sound = soundEnabled;
            haptics = hapticsEnabled;
            feedback = commonFeedback;
            preferences = display;

            var nav = UiFactory.Band(root, "Bakery navigation", 0, 102, 0);
            UiFactory.NavigationButton(nav, UiIconKind.Home, lobby, size: 62);
            bottom = UiFactory.Label(nav, "", 126, 34, 246, 62, 19, UiFactory.Ink);
            score = UiFactory.Label(nav, "", 374, 34, 148, 62, 22, UiFactory.Accent, TextAnchor.MiddleRight, FontStyle.Bold);
            UiFactory.NavigationButton(nav, UiIconKind.Book, help, size: 62);
            UiFactory.NavigationButton(nav, UiIconKind.Settings, settings, size: 62);

            var heading = UiFactory.Band(root, "Bakery heading", 105, 120, .10f);
            UiFactory.Label(heading, DisplayName, 32, 0, 656, 73, 59, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(heading, "轻按看轮廓，松手取走。三件同类一起消除。", 34, 77, 654, 38, 22, UiFactory.Muted);

            var mode = UiFactory.Band(root, "Basket comparison", 241, 60, .18f);
            straightButton = DialogUi.ActionButton(mode, "直壁篮", 32, 0, 322, 58, () => StartRound(false));
            taperedButton = DialogUi.ActionButton(mode, "收底篮", 366, 0, 322, 58, () => StartRound(true));

            var table = UiFactory.Band(root, "Bakery play table", 306, BakeryWorld.ViewHeight, .48f);
            var stage = UiFactory.Panel(table, "Bakery board", 32, 0, BakeryWorld.ViewWidth, BakeryWorld.ViewHeight,
                UiFactory.Hex("EEE8DA")).rectTransform;
            UiFactory.Panel(stage, "Reserve background", 14, BakeryWorld.ReserveTop - 6, 628, 66, UiFactory.Hex("E5D9C0"));
            for (int i = 0; i < BakeryRound.ReserveSlotCount; i++)
                UiFactory.Panel(stage, "Bakery reserve " + (i + 1), BakeryWorld.ReserveLeft + i * BakeryWorld.ReservePitch,
                    BakeryWorld.ReserveTop, BakeryWorld.ReserveWidth, BakeryWorld.ReserveHeight, UiFactory.Hex("FFF5DF"));
            UiFactory.Panel(stage, "Tray background", 14, BakeryWorld.TrayTop - 9, 628, 90, UiFactory.Hex("C8D8D5"));
            for (int i = 0; i < BakeryRound.SlotCount; i++)
                UiFactory.Panel(stage, "Bakery slot " + (i + 1), BakeryWorld.TrayLeft + i * BakeryWorld.SlotPitch,
                    BakeryWorld.TrayTop, BakeryWorld.SlotWidth, BakeryWorld.SlotHeight, UiFactory.Hex("FFFAEF"));
            var screen = UiFactory.Rect(stage, "Bakery 3D viewport", 0, 0, BakeryWorld.ViewWidth, BakeryWorld.ViewHeight);
            image = screen.gameObject.AddComponent<RawImage>();
            input = screen.gameObject.AddComponent<BakeryPointer>();
            input.Initialize(this, screen);
            for (int i = 0; i < reserveButtons.Length; i++)
            {
                int slot = i;
                var hit = UiFactory.Panel(stage, "Return reserve " + (i + 1),
                    BakeryWorld.ReserveLeft + i * BakeryWorld.ReservePitch, BakeryWorld.ReserveTop,
                    BakeryWorld.ReserveWidth, BakeryWorld.ReserveHeight, Color.clear, false);
                reserveButtons[i] = UiFactory.ConfigureButton(hit, () => ReturnFromReserve(slot));
            }
            var status = UiFactory.Panel(stage, "Bakery result text", 62, 633, 532, 42, UiFactory.Paper);
            message = UiFactory.Label(status.transform, "", 12, 0, 508, 42, 23, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);

            var actions = UiFactory.Band(root, "Bakery actions", 1197, 69, .83f);
            DialogUi.ActionButton(actions, "重开同一篮", 32, 0, 194, 66, () => StartRound(tapered));
            moveOutButton = UiFactory.Button(actions, "移出 3 件", 238, 0, 194, 66, () => MoveOut());
            moveOutLabel = moveOutButton.GetComponentInChildren<Text>();
            shakeButton = UiFactory.Button(actions, "颠一下", 444, 0, 244, 66, Shake, true);
            var notes = UiFactory.Band(root, "Bakery observation", 1267, 51, 1);
            observation = UiFactory.Label(notes, "", 32, 0, 656, 51, 20, UiFactory.Muted, TextAnchor.MiddleCenter);
        }

        internal void Enter(bool animateOpening = true)
        {
            gameObject.SetActive(true);
            ActiveTheme = preferences.ChooseForEntry(entryRandom);
            StartRound(false, animateOpening);
        }

        internal void Leave()
        {
            if (world == null) return;
            input.Clear();
            ReleaseWorldAndModels();
            gameObject.SetActive(false);
        }

        internal void Suspend()
        {
            if (world != null) input.Clear();
        }

        internal void StartRound(bool useTapered, bool animateOpening = false)
        {
            input.Clear();
            world?.Dispose();
            world = null;
            tapered = useTapered;
            pressed = -1;
            flights.Clear();
            displayedTray.Clear();
            matching = null;
            shakePieces = null;
            stillHidden = null;
            NewlyVisibleDuringShake = 0;
            waitingForRest = false;
            hasShakeResult = false;
            accumulator = 0;
            round = new BakeryRound(BakeryOpeningPoses.Seed);
            if (!modelsByTheme.TryGetValue(ActiveTheme, out var models))
            {
                models = new BakeryModelCache(BakeryThemeCatalog.Get(ActiveTheme));
                modelsByTheme.Add(ActiveTheme, models);
            }
            world = new BakeryWorld(image, round, BakeryOpeningPoses.Seed, tapered, ActiveTheme,
                animateOpening: animateOpening, models: models);
            entranceCountsPending = world.IsEntering;
            SetMode(straightButton, !tapered);
            SetMode(taperedButton, tapered);
            observation.text = "两个篮子使用同一批物件，可随时切换重开。";
            ApplyDisplay();
        }

        internal void ChangeTheme(BakeryTheme theme)
        {
            ActiveTheme = theme;
            StartRound(tapered);
        }

        internal void ApplyDisplay()
        {
            bottom.gameObject.SetActive(preferences.ShowBottom);
            score.gameObject.SetActive(preferences.ShowRemaining);
            if (hasShakeResult) RefreshShakeResult();
            if (world.IsEntering) bottom.text = "";
            else RefreshBottom();
            RefreshProgress();
        }

        private static void SetMode(Button button, bool active)
        {
            button.image.color = active ? UiFactory.Accent : UiFactory.Hex("E7EDE4");
            button.GetComponentInChildren<Text>().color = active ? Color.white : UiFactory.Ink;
        }

        private bool CanAct => world != null && round.Phase == BakeryPhase.Playing && hostAcceptsInput();

        internal bool Press(Vector2 viewport)
        {
            if (!CanAct || pressed >= 0) return false;
            if (world.IsEntering)
            {
                world.FinishEntrance();
                return false;
            }
            pressed = world.Pick(viewport);
            if (pressed < 0) return false;
            pressedAt = Time.unscaledTime;
            world.Mark(pressed);
            world.Render();
            return true;
        }

        internal void Release(bool take)
        {
            if (pressed < 0) return;
            int id = pressed;
            pressed = -1;
            if (!take || !CanAct)
            {
                world.Mark(-1);
                world.Render();
                return;
            }
            int slot = round.Tray.Count;
            int[] match = round.Take(id);
            EnqueueFlight(id, slot, match, -Mathf.Max(.035f, HighlightTime - (Time.unscaledTime - pressedAt)));
            world.RemoveFromBasket(id);
            waitingForRest = true;
            restTimer = .60f;
            RefreshProgress();
        }

        private void EnqueueFlight(int id, int slot, int[] match, float offset, bool fromReserve = false)
        {
            var piece = world.Pieces[id];
            flights.Add(new Flight
            {
                Id = id, Slot = slot, Match = match,
                Position = piece.Root.position, Rotation = piece.Root.rotation,
                Elapsed = offset, Scale = piece.Root.localScale.x, FromReserve = fromReserve
            });
        }

        private bool TraySettled => flights.Count == 0 && matching == null;
        private bool CanTransfer => world != null && hostAcceptsInput() && pressed < 0 && TraySettled;

        internal bool MoveOut()
        {
            if (!CanTransfer || !round.MoveOut()) return false;
            displayedTray.Clear();
            displayedTray.AddRange(round.Tray);
            PlaceDisplayedTray();
            PlaceReserve();
            feedback?.Pulse(haptics());
            RefreshProgress();
            world.Render();
            return true;
        }

        internal bool ReturnFromReserve(int slot)
        {
            if (!CanTransfer || slot < 0 || slot >= round.Reserve.Count) return false;
            int id = round.Reserve[slot];
            int traySlot = round.Tray.Count;
            int[] match = round.ReturnFromReserve(slot);
            if (match == null) return false;
            EnqueueFlight(id, traySlot, match, 0, fromReserve: true);
            RefreshProgress();
            return true;
        }

        private void PlaceReserve()
        {
            for (int i = 0; i < round.Reserve.Count; i++) world.PlaceReserve(round.Reserve[i], i);
        }

        private void PlaceDisplayedTray()
        {
            for (int i = 0; i < displayedTray.Count; i++) world.PlaceTray(displayedTray[i], i);
        }

        internal void Shake()
        {
            if (!CanAct || pressed >= 0 || shakePieces != null) return;
            world.FinishEntrance();
            shakePieces = world.BottomSmall();
            stillHidden = new HashSet<int>();
            foreach (int id in shakePieces)
                if (!world.TryFindVisiblePoint(id, out _)) stillHidden.Add(id);
            shakeBefore = shakePieces.Count - stillHidden.Count;
            NewlyVisibleDuringShake = 0;
            hasShakeResult = false;
            shakeTimer = ShakeTime;
            nextShakeSample = .09f;
            observation.text = "正在颠篮，可以点取翻出来的物件。";
            world.Shake();
            feedback?.Pulse(haptics());
            RefreshProgress();
        }

        private void Update()
        {
            if (world == null || !hostAcceptsInput()) return;
            Advance(Mathf.Min(Time.unscaledDeltaTime, .10f));
        }

        internal void Advance(float delta, bool render = true)
        {
            if (world.IsEntering)
            {
                world.AdvanceEntrance(delta, render);
                return;
            }
            if (entranceCountsPending)
            {
                entranceCountsPending = false;
                RefreshBottom();
            }
            bool animated = matching != null || flights.Count > 0;
            accumulator += delta;
            while (accumulator >= BakeryWorld.Step)
            {
                accumulator -= BakeryWorld.Step;
                world.Simulate();
            }
            if (matching != null) AdvanceMatch(delta);
            if (flights.Count > 0) AdvanceFlights(delta);
            if (shakePieces != null)
            {
                shakeTimer -= delta;
                nextShakeSample -= delta;
                if (nextShakeSample <= 0 || shakeTimer <= 0)
                {
                    NewlyVisibleDuringShake += stillHidden.RemoveWhere(id => world.TryFindVisiblePoint(id, out _));
                    nextShakeSample = .12f;
                }
                if (shakeTimer <= 0) FinishShake();
            }
            if (waitingForRest)
            {
                restTimer -= delta;
                if (restTimer <= 0) { waitingForRest = false; RefreshBottom(); }
            }
            if (render) world.RenderWhenChanged(animated);
        }

        private void AdvanceFlights(float delta)
        {
            for (int index = 0; index < flights.Count; index++)
            {
                var flight = flights[index];
                // Every piece can fly at once. Only the oldest can land, once the tray has finished clearing.
                float limit = index == 0 && matching == null ? FlightTime : FlightTime * .76f;
                flight.Elapsed = Mathf.Min(flight.Elapsed + delta, limit);
                if (flight.Elapsed < 0) continue;
                if (world.Selected == flight.Id) world.Mark(-1);
                world.MoveToTray(flight.Id, flight.Slot, flight.Position, flight.Rotation, flight.Scale, flight.Elapsed / FlightTime);
                if (flight.Elapsed < FlightTime) continue;
                flights.RemoveAt(index--);
                displayedTray.Add(flight.Id);
                if (flight.FromReserve) PlaceReserve();
                if (flight.Match.Length > 0)
                {
                    matching = flight.Match;
                    matchElapsed = 0;
                }
                feedback?.Play(flight.Match.Length > 0, sound(), haptics());
                RefreshProgress();
            }
        }

        private void AdvanceMatch(float delta)
        {
            matchElapsed += delta;
            float fraction = Mathf.Clamp01(matchElapsed / MatchTime);
            foreach (int id in matching) world.ShrinkMatch(id, fraction);
            if (fraction < 1) return;
            foreach (int id in matching) displayedTray.Remove(id);
            matching = null;
            PlaceDisplayedTray();
            RefreshProgress();
        }

        private void FinishShake()
        {
            shakeCount = shakePieces.Count;
            shakeAfter = world.VisibleCount(shakePieces);
            hasShakeResult = true;
            RefreshShakeResult();
            shakePieces = null;
            stillHidden = null;
            RefreshBottom();
            RefreshProgress();
        }

        private void RefreshShakeResult()
        {
            observation.text = !preferences.ShowBottom ? "颠篮结束，可以继续取物。" :
                shakeCount == 0 ? "颠篮前，篮底没有小件可比较。" :
                $"颠前篮底小件 {shakeCount} 件 · 翻出 {NewlyVisibleDuringShake} 件\n可点 {shakeBefore} 件 → 颠篮结束时 {shakeAfter} 件";
        }

        private void RefreshProgress()
        {
            score.text = $"篮中 {round.Remaining} / {BakeryRound.PieceCount}";
            shakeButton.interactable = round.Phase == BakeryPhase.Playing && shakePieces == null;
            moveOutButton.interactable = TraySettled && round.CanMoveOut;
            int moveCount = round.CanMoveOut ? round.MoveOutCount : BakeryRound.MaxMoveOutCount;
            moveOutLabel.text = $"移出 {moveCount} 件";
            for (int i = 0; i < reserveButtons.Length; i++)
                reserveButtons[i].interactable = TraySettled && round.Phase == BakeryPhase.Playing && i < round.Reserve.Count;
            message.text = !TraySettled ? "" :
                round.Phase == BakeryPhase.Cleared ? "篮子清空了！" :
                round.Phase == BakeryPhase.Full ? (round.CanMoveOut ? $"消除栏满了，可以移出 {moveCount} 件。" : "消除栏满了，重开再试。") :
                round.Remaining == 0 && round.Reserve.Count > 0 ? "点暂存物件，放回消除栏消除。" : "";
            message.transform.parent.gameObject.SetActive(message.text.Length > 0);
        }

        private void RefreshBottom()
        {
            if (!preferences.ShowBottom) return;
            List<int> ids = world.BottomSmall();
            bottom.text = $"篮底小件 {ids.Count}  ·  可点约 {world.VisibleCount(ids)}";
        }

        private void OnDisable() => Suspend();
        private void OnDestroy() => ReleaseWorldAndModels();

        private void ReleaseWorldAndModels()
        {
            world?.Dispose();
            world = null;
            foreach (var models in modelsByTheme.Values) models.Dispose();
            modelsByTheme.Clear();
        }
    }

    internal sealed class BakeryPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, ICancelHandler
    {
        private BakeryPage owner;
        private RectTransform viewport;
        private int? pointer;
        private Vector2 start;

        internal void Initialize(BakeryPage page, RectTransform area) { owner = page; viewport = area; }

        public void OnPointerDown(PointerEventData data)
        {
            if (pointer.HasValue || data.button != PointerEventData.InputButton.Left || !Position(data, out start)) return;
            if (owner.Press(start)) pointer = data.pointerId;
        }

        public void OnDrag(PointerEventData data)
        {
            if (pointer == data.pointerId && !WithinTap(data)) Clear();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (pointer != data.pointerId) return;
            pointer = null;
            bool cancelledTouch = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId == data.pointerId && touch.phase == TouchPhase.Canceled) cancelledTouch = true;
            }
            owner.Release(!cancelledTouch && WithinTap(data));
        }

        public void OnCancel(BaseEventData data) => Clear();

        internal void Clear()
        {
            if (!pointer.HasValue) return;
            pointer = null;
            owner.Release(false);
        }

        private bool WithinTap(PointerEventData data) => Position(data, out var at) &&
            at.x >= 0 && at.x <= 1 && at.y >= BakeryWorld.PickBottom && at.y <= 1 && (at - start).sqrMagnitude <= .035f * .035f;

        private bool Position(PointerEventData data, out Vector2 position)
        {
            position = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, data.position, data.pressEventCamera, out var local)) return false;
            var bounds = viewport.rect;
            position = new Vector2((local.x - bounds.xMin) / bounds.width, (local.y - bounds.yMin) / bounds.height);
            return true;
        }
    }
}
