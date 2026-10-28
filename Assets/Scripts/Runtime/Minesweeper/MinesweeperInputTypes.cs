using System;
using System.Collections.Generic;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>Unscaled viewport design coordinates, x right and y down.</summary>
    public struct MinesweeperInputPosition
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public MinesweeperInputPosition(double x, double y) { X = x; Y = y; }
    }

    public struct MinesweeperInputRect
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public MinesweeperInputRect(double x, double y, double width, double height)
        { X = x; Y = y; Width = width; Height = height; }
    }

    public enum MinesweeperInputPhase { Began, Moved, Stationary, Ended, Canceled }
    public enum MinesweeperGestureAction { None, ShortPress, ToggleFlag, OverviewLocated }

    public struct MinesweeperInputContact
    {
        public int Id { get; private set; }
        public MinesweeperInputPosition Position { get; private set; }
        public MinesweeperInputPhase Phase { get; private set; }
        public bool SecondaryButton { get; private set; }
        public MinesweeperInputContact(int id, double x, double y, MinesweeperInputPhase phase, bool secondaryButton = false)
        { Id = id; Position = new MinesweeperInputPosition(x, y); Phase = phase; SecondaryButton = secondaryButton; }
    }

    /// <summary>A whole input frame, including contacts ending this frame and contacts outside the viewport.</summary>
    public sealed class MinesweeperInputFrame
    {
        public long TimeMilliseconds { get; private set; }
        public string GameId { get; private set; }
        public bool ViewAllowed { get; private set; }
        public bool CellAllowed { get; private set; }
        public bool FlagMode { get; private set; }
        public IReadOnlyList<MinesweeperInputContact> Contacts { get; private set; }
        public MinesweeperInputFrame(long timeMilliseconds, string gameId, bool viewAllowed, bool cellAllowed,
            bool flagMode, IReadOnlyList<MinesweeperInputContact> contacts)
        {
            if (timeMilliseconds < 0) throw new ArgumentOutOfRangeException("timeMilliseconds");
            if (string.IsNullOrEmpty(gameId)) throw new ArgumentException("A current game identity is required.", "gameId");
            if (contacts == null) throw new ArgumentNullException("contacts");
            var copy = new MinesweeperInputContact[contacts.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = contacts[i];
            TimeMilliseconds = timeMilliseconds; GameId = gameId; ViewAllowed = viewAllowed;
            CellAllowed = cellAllowed; FlagMode = flagMode; Contacts = Array.AsReadOnly(copy);
        }
    }

    public sealed class MinesweeperGestureResult
    {
        public MinesweeperGestureAction Action { get; private set; }
        public int CellIndex { get; private set; }
        public string GameId { get; private set; }
        public bool FlagMode { get; private set; }
        public int PreviewCellIndex { get; private set; }
        public bool ViewChanged { get; private set; }
        public bool GestureCompleted { get; private set; }
        internal MinesweeperGestureResult(MinesweeperGestureAction action, int cellIndex, string gameId,
            bool flagMode, int previewCellIndex, bool viewChanged, bool gestureCompleted)
        {
            Action = action; CellIndex = cellIndex; GameId = gameId; FlagMode = flagMode;
            PreviewCellIndex = previewCellIndex; ViewChanged = viewChanged; GestureCompleted = gestureCompleted;
        }
    }
}
