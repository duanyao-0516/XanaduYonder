using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Tetris
{
    public enum TetrisPiece { None, I, O, T, J, L, S, Z }
    public enum TetrisCommand { Left, Right, Clockwise, CounterClockwise, HardDrop, Hold }

    public struct TetrisCell
    {
        public readonly int X, Y;
        public TetrisCell(int x, int y) { X = x; Y = y; }
    }

    public struct TetrisStepResult
    {
        public bool Changed, Locked, Held, GameOver;
        public int ClearedLines;
        public long ScoreDelta;
        public int[] ClearedRows;
    }

    public static class TetrisRules
    {
        public const int BoardWidth = 10, BoardHeight = 22, HiddenRows = 2, VisibleRows = 20;
        public const int TickRate = 60, LockDelayTicks = 30, MaxLockResets = 15;
        public const double StepSeconds = 1.0 / TickRate;

        // Rule version 1 quantizes to the nearest fixed step, with half ticks rounded up.
        public static int GravityIntervalTicks(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException("level");
            if (level >= 11) return 12;
            return (int)Math.Round(60.0 * Math.Pow(0.85, level - 1), MidpointRounding.AwayFromZero);
        }
    }

    public static class TetrisPieces
    {
        private static readonly TetrisCell[][][] Shapes = CreateShapes();

        public static TetrisCell[] GetCells(TetrisPiece piece, int rotation)
        {
            if (!IsPiece(piece, true)) throw new ArgumentOutOfRangeException("piece");
            if (rotation < 0 || rotation > 3) throw new ArgumentOutOfRangeException("rotation");
            if (piece == TetrisPiece.None) return new TetrisCell[0];
            return (TetrisCell[])Shapes[(int)piece - 1][rotation].Clone();
        }

        internal static bool IsPiece(TetrisPiece piece, bool allowNone)
        {
            return (int)piece >= (allowNone ? 0 : 1) && (int)piece <= 7;
        }

        internal static TetrisCell Cell(TetrisPiece piece, int rotation, int index)
        {
            return Shapes[(int)piece - 1][rotation][index];
        }

        private static TetrisCell[][][] CreateShapes()
        {
            // All pieces use a 4x4 local frame. JLSTZ rotate around (1,1); I around (1.5,1.5).
            int[][] spawn = {
                new[] { 0,1, 1,1, 2,1, 3,1 }, new[] { 1,0, 2,0, 1,1, 2,1 },
                new[] { 1,0, 0,1, 1,1, 2,1 }, new[] { 0,0, 0,1, 1,1, 2,1 },
                new[] { 2,0, 0,1, 1,1, 2,1 }, new[] { 1,0, 2,0, 0,1, 1,1 },
                new[] { 0,0, 1,0, 1,1, 2,1 }
            };
            var result = new TetrisCell[7][][];
            for (int p = 0; p < 7; p++) {
                result[p] = new TetrisCell[4][];
                for (int r = 0; r < 4; r++) {
                    result[p][r] = new TetrisCell[4];
                    for (int c = 0; c < 4; c++) {
                        int x = spawn[p][c * 2], y = spawn[p][c * 2 + 1];
                        if (p != 1) for (int turn = 0; turn < r; turn++) { int nextX = (p == 0 ? 3 : 2) - y; y = x; x = nextX; }
                        result[p][r][c] = new TetrisCell(x, y);
                    }
                }
            }
            return result;
        }
    }

    [Serializable]
    public sealed class TetrisSnapshot
    {
        public const int Width = 10, Height = 22;
        public int RulesVersion = 1;
        public TetrisPiece[] Cells;
        public TetrisPiece ActivePiece, HoldPiece;
        public int Rotation, X, Y;
        public bool CanHold;
        public TetrisPiece[] NextQueue, Bag;
        public uint RngState;
        public long Score;
        public int Lines, GravityTicks, SoftDropTicks, LockTicks, LockResets;
        public bool LockStarted, GameOver;
        public long ElapsedTicks, PieceSerial;
        public int Level { get { return 1 + Lines / 10; } }

        public TetrisSnapshot Copy()
        {
            var copy = (TetrisSnapshot)MemberwiseClone();
            copy.Cells = Cells == null ? null : (TetrisPiece[])Cells.Clone();
            copy.NextQueue = NextQueue == null ? null : (TetrisPiece[])NextQueue.Clone();
            copy.Bag = Bag == null ? null : (TetrisPiece[])Bag.Clone();
            return copy;
        }

        public bool TryValidate(out string error)
        {
            error = Validate(); return error == null;
        }

        private string Validate()
        {
            if (RulesVersion != 1) return "Unsupported rules version.";
            if (Cells == null || Cells.Length != Width * Height) return "Board must contain 220 cells.";
            for (int i = 0; i < Cells.Length; i++) if (!TetrisPieces.IsPiece(Cells[i], true)) return "Unknown board cell.";
            if (!TetrisPieces.IsPiece(ActivePiece, GameOver) || !TetrisPieces.IsPiece(HoldPiece, true)) return "Unknown piece.";
            if (Rotation < 0 || Rotation > 3 || (ActivePiece == TetrisPiece.O && Rotation != 0)) return "Invalid rotation.";
            if (X < -3 || X > 9 || Y < -3 || Y > 21) return "Invalid active origin.";
            if (NextQueue == null || NextQueue.Length != 3) return "Preview must contain three pieces.";
            for (int i = 0; i < NextQueue.Length; i++) if (!TetrisPieces.IsPiece(NextQueue[i], false)) return "Invalid preview piece.";
            if (Bag == null || Bag.Length > 6) return "Invalid remaining bag size.";
            int mask = 0;
            for (int i = 0; i < Bag.Length; i++) {
                if (!TetrisPieces.IsPiece(Bag[i], false)) return "Invalid bag piece.";
                int bit = 1 << (int)Bag[i]; if ((mask & bit) != 0) return "Repeated piece inside a bag."; mask |= bit;
            }
            // The latest queue suffix and remaining bag are one seven-bag. The prefix, if any,
            // belongs to the previous bag. Validate both without constraining active/Hold order.
            int suffixLength = Math.Min(3, 7 - Bag.Length), prefixMask = 0;
            for (int i = 0; i < 3; i++) {
                int bit = 1 << (int)NextQueue[i];
                if (i < 3 - suffixLength) {
                    if ((prefixMask & bit) != 0) return "Repeated preview piece inside previous bag.";
                    prefixMask |= bit;
                } else {
                    if ((mask & bit) != 0) return "Preview contradicts remaining bag.";
                    mask |= bit;
                }
            }
            if (RngState == 0) return "RNG state cannot be zero.";
            if (Score < 0 || Lines < 0 || ElapsedTicks < 0 || PieceSerial < 1) return "Negative or empty progress counter.";
            if (GravityTicks < 0 || GravityTicks >= TetrisRules.GravityIntervalTicks(Level) || SoftDropTicks < 0 || SoftDropTicks >= 3) return "Invalid fall timer.";
            if (GravityTicks != 0 && SoftDropTicks != 0) return "Natural and soft-drop timers cannot run together.";
            if (LockTicks < 0 || LockTicks > TetrisRules.LockDelayTicks || LockResets < 0 || LockResets > TetrisRules.MaxLockResets) return "Invalid lock budget.";
            if (!LockStarted && (LockTicks != 0 || LockResets != 0)) return "Lock budget used before contact.";
            if (!CanHold && HoldPiece == TetrisPiece.None) return "Used Hold requires a held piece.";
            for (int y = 0; y < Height; y++) {
                bool full = true; for (int x = 0; x < Width; x++) if (Cells[y * Width + x] == TetrisPiece.None) { full = false; break; }
                if (full) return "A completed row must already have been cleared.";
            }
            bool hidden = HasHiddenCells();
            if (GameOver) {
                if (X != 3 || Y != 0 || Rotation != 0 || LockStarted || LockTicks != 0 || LockResets != 0 || GravityTicks != 0 || SoftDropTicks != 0) return "Terminal spawn phase is inconsistent.";
                if (ActivePiece == TetrisPiece.None) return hidden && CanHold ? null : "Hidden-row terminal state needs hidden cells and completed lock phase.";
                return hidden && !Fits(ActivePiece, 0, 3, 0) ? null : "Blocked-spawn terminal state needs a collision.";
            }
            if (hidden) return "A live board cannot retain hidden locked cells.";
            if (!Fits(ActivePiece, Rotation, X, Y)) return "Active piece overlaps the board or boundary.";
            bool grounded = !Fits(ActivePiece, Rotation, X, Y + 1);
            if (grounded && (!LockStarted || LockTicks == 0)) return "Ground contact must start or complete locking immediately.";
            return null;
        }

        internal bool HasHiddenCells()
        {
            for (int i = 0; i < Width * TetrisRules.HiddenRows; i++) if (Cells[i] != TetrisPiece.None) return true;
            return false;
        }

        internal bool Fits(TetrisPiece piece, int rotation, int x, int y)
        {
            for (int i = 0; i < 4; i++) {
                var cell = TetrisPieces.Cell(piece, rotation, i); int bx = x + cell.X, by = y + cell.Y;
                if (bx < 0 || bx >= Width || by < 0 || by >= Height || Cells[by * Width + bx] != TetrisPiece.None) return false;
            }
            return true;
        }
    }

    public sealed class TetrisGame
    {
        private readonly TetrisSnapshot state;
        private static readonly int[] NoRows = new int[0];
        private static readonly int[] LineScores = { 0, 100, 300, 500, 800 };

        // SRS offsets use board coordinates (+y down). Indexed by source rotation and direction.
        // Numeric rule facts checked against NullpoMino StandardWallkick.java; implementation is independent.
        private static readonly int[,,] NormalClockwise = {
            { {0,0}, {-1,0}, {-1,-1}, {0,2}, {-1,2} },
            { {0,0}, {1,0}, {1,1}, {0,-2}, {1,-2} },
            { {0,0}, {1,0}, {1,-1}, {0,2}, {1,2} },
            { {0,0}, {-1,0}, {-1,1}, {0,-2}, {-1,-2} }
        };
        private static readonly int[,,] NormalCounterClockwise = {
            { {0,0}, {1,0}, {1,-1}, {0,2}, {1,2} },
            { {0,0}, {1,0}, {1,1}, {0,-2}, {1,-2} },
            { {0,0}, {-1,0}, {-1,-1}, {0,2}, {-1,2} },
            { {0,0}, {-1,0}, {-1,1}, {0,-2}, {-1,-2} }
        };
        private static readonly int[,,] IClockwise = {
            { {0,0}, {-2,0}, {1,0}, {-2,1}, {1,-2} },
            { {0,0}, {-1,0}, {2,0}, {-1,-2}, {2,1} },
            { {0,0}, {2,0}, {-1,0}, {2,-1}, {-1,2} },
            { {0,0}, {1,0}, {-2,0}, {1,2}, {-2,-1} }
        };
        private static readonly int[,,] ICounterClockwise = {
            { {0,0}, {-1,0}, {2,0}, {-1,-2}, {2,1} },
            { {0,0}, {2,0}, {-1,0}, {2,-1}, {-1,2} },
            { {0,0}, {1,0}, {-2,0}, {1,2}, {-2,-1} },
            { {0,0}, {-2,0}, {1,0}, {-2,1}, {1,-2} }
        };

        public TetrisGame(uint seed)
        {
            state = new TetrisSnapshot {
                Cells = new TetrisPiece[TetrisSnapshot.Width * TetrisSnapshot.Height],
                NextQueue = new TetrisPiece[3], Bag = new TetrisPiece[0],
                RngState = seed == 0 ? 0x6d2b79f5u : seed, CanHold = true
            };
            for (int i = 0; i < 3; i++) state.NextQueue[i] = DrawBag();
            Spawn(TakeNext());
        }

        public TetrisGame(TetrisSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            string error; if (!snapshot.TryValidate(out error)) throw new ArgumentException(error, "snapshot");
            state = snapshot.Copy();
        }

        public TetrisSnapshot GetSnapshot() { return state.Copy(); }
        public long PieceSerial { get { return state.PieceSerial; } }
        public bool GameOver { get { return state.GameOver; } }

        public int GhostY
        {
            get {
                if (state.GameOver) return state.Y;
                int y = state.Y; while (state.Fits(state.ActivePiece, state.Rotation, state.X, y + 1)) y++;
                return y;
            }
        }

        public TetrisStepResult Execute(TetrisCommand command)
        {
            var result = EmptyResult();
            if (state.GameOver || command < TetrisCommand.Left || command > TetrisCommand.Hold) return result;
            long scoreBefore = state.Score;
            if (command == TetrisCommand.HardDrop) {
                int y = GhostY; AddScore((y - state.Y) * 2L); state.Y = y; Lock(ref result);
            } else if (command == TetrisCommand.Hold) {
                if (!state.CanHold) return result;
                var outgoing = state.ActivePiece;
                var incoming = state.HoldPiece == TetrisPiece.None ? TakeNext() : state.HoldPiece;
                state.HoldPiece = outgoing; state.CanHold = false; Spawn(incoming);
                result.Changed = true; result.Held = true;
            } else {
                bool moved;
                if (command == TetrisCommand.Left || command == TetrisCommand.Right) {
                    int nextX = state.X + (command == TetrisCommand.Left ? -1 : 1);
                    moved = state.Fits(state.ActivePiece, state.Rotation, nextX, state.Y);
                    if (moved) state.X = nextX;
                } else moved = Rotate(command == TetrisCommand.Clockwise);
                if (moved) {
                    result.Changed = true;
                    if (state.LockStarted && state.LockTicks > 0 && state.LockResets < TetrisRules.MaxLockResets) {
                        state.LockResets++; state.LockTicks = TetrisRules.LockDelayTicks;
                    }
                    UpdateContact(ref result);
                }
            }
            result.ScoreDelta = state.Score - scoreBefore; result.GameOver = state.GameOver; return result;
        }

        public TetrisStepResult Tick(bool softDrop)
        {
            var result = EmptyResult(); if (state.GameOver) return result;
            long scoreBefore = state.Score;
            state.ElapsedTicks = Increment(state.ElapsedTicks); result.Changed = true;
            if (state.LockStarted && state.LockTicks > 0) state.LockTicks--;
            bool fall;
            if (softDrop) {
                state.GravityTicks = 0; state.SoftDropTicks++;
                fall = state.SoftDropTicks == 3;
                if (fall) state.SoftDropTicks = 0;
            } else {
                state.SoftDropTicks = 0; state.GravityTicks++;
                fall = state.GravityTicks >= TetrisRules.GravityIntervalTicks(state.Level);
                if (fall) state.GravityTicks = 0;
            }
            if (fall && state.Fits(state.ActivePiece, state.Rotation, state.X, state.Y + 1)) {
                state.Y++; if (softDrop) AddScore(1);
            }
            UpdateContact(ref result);
            result.ScoreDelta = state.Score - scoreBefore; result.GameOver = state.GameOver; return result;
        }

        private TetrisStepResult EmptyResult()
        {
            return new TetrisStepResult { GameOver = state.GameOver, ClearedRows = NoRows };
        }

        private bool Rotate(bool clockwise)
        {
            if (state.ActivePiece == TetrisPiece.O) return false;
            int nextRotation = (state.Rotation + (clockwise ? 1 : 3)) % 4;
            var kicks = state.ActivePiece == TetrisPiece.I ? (clockwise ? IClockwise : ICounterClockwise) : (clockwise ? NormalClockwise : NormalCounterClockwise);
            for (int i = 0; i < 5; i++) {
                int x = state.X + kicks[state.Rotation, i, 0], y = state.Y + kicks[state.Rotation, i, 1];
                if (!state.Fits(state.ActivePiece, nextRotation, x, y)) continue;
                state.X = x; state.Y = y; state.Rotation = nextRotation; return true;
            }
            return false;
        }

        private void UpdateContact(ref TetrisStepResult result)
        {
            if (state.Fits(state.ActivePiece, state.Rotation, state.X, state.Y + 1)) return;
            if (!state.LockStarted) { state.LockStarted = true; state.LockTicks = TetrisRules.LockDelayTicks; }
            else if (state.LockTicks == 0) Lock(ref result);
        }

        private void Lock(ref TetrisStepResult result)
        {
            int level = state.Level;
            for (int i = 0; i < 4; i++) {
                var cell = TetrisPieces.Cell(state.ActivePiece, state.Rotation, i);
                state.Cells[(state.Y + cell.Y) * TetrisSnapshot.Width + state.X + cell.X] = state.ActivePiece;
            }
            var rows = new List<int>(4);
            for (int y = 0; y < TetrisSnapshot.Height; y++) {
                bool full = true;
                for (int x = 0; x < TetrisSnapshot.Width; x++) if (state.Cells[y * TetrisSnapshot.Width + x] == TetrisPiece.None) { full = false; break; }
                if (full) rows.Add(y);
            }
            int writeY = TetrisSnapshot.Height - 1;
            for (int y = TetrisSnapshot.Height - 1; y >= 0; y--) {
                if (rows.Contains(y)) continue;
                if (writeY != y) Array.Copy(state.Cells, y * TetrisSnapshot.Width, state.Cells, writeY * TetrisSnapshot.Width, TetrisSnapshot.Width);
                writeY--;
            }
            if (writeY >= 0) Array.Clear(state.Cells, 0, (writeY + 1) * TetrisSnapshot.Width);
            AddScore((long)LineScores[rows.Count] * level);
            state.Lines = state.Lines > int.MaxValue - rows.Count ? int.MaxValue : state.Lines + rows.Count;
            result.Changed = true; result.Locked = true; result.ClearedLines = rows.Count; result.ClearedRows = rows.Count == 0 ? NoRows : rows.ToArray();
            state.CanHold = true;
            if (state.HasHiddenCells()) {
                state.GameOver = true; state.ActivePiece = TetrisPiece.None; ResetPieceTimers();
            } else Spawn(TakeNext());
        }

        private void Spawn(TetrisPiece piece)
        {
            state.ActivePiece = piece; ResetPieceTimers(); state.PieceSerial = Increment(state.PieceSerial);
            if (!state.Fits(piece, 0, 3, 0)) { state.GameOver = true; return; }
            if (!state.Fits(piece, 0, 3, 1)) { state.LockStarted = true; state.LockTicks = TetrisRules.LockDelayTicks; }
        }

        private void ResetPieceTimers()
        {
            state.X = 3; state.Y = 0; state.Rotation = 0;
            state.GravityTicks = 0; state.SoftDropTicks = 0;
            state.LockStarted = false; state.LockTicks = 0; state.LockResets = 0;
        }

        private TetrisPiece TakeNext()
        {
            TetrisPiece piece = state.NextQueue[0]; state.NextQueue[0] = state.NextQueue[1]; state.NextQueue[1] = state.NextQueue[2]; state.NextQueue[2] = DrawBag(); return piece;
        }

        private TetrisPiece DrawBag()
        {
            if (state.Bag.Length == 0) {
                state.Bag = new[] { TetrisPiece.I, TetrisPiece.O, TetrisPiece.T, TetrisPiece.J, TetrisPiece.L, TetrisPiece.S, TetrisPiece.Z };
                for (int i = 6; i > 0; i--) {
                    int j = NextRandom(i + 1); var value = state.Bag[i]; state.Bag[i] = state.Bag[j]; state.Bag[j] = value;
                }
            }
            var piece = state.Bag[0]; var remaining = new TetrisPiece[state.Bag.Length - 1];
            Array.Copy(state.Bag, 1, remaining, 0, remaining.Length); state.Bag = remaining; return piece;
        }

        private int NextRandom(int bound)
        {
            // xorshift32 with rejection sampling. State is entirely present in the snapshot.
            uint limit = unchecked(0u - (uint)bound) % (uint)bound;
            uint value;
            do { value = state.RngState; value ^= value << 13; value ^= value >> 17; value ^= value << 5; state.RngState = value; } while (value < limit);
            return (int)(value % (uint)bound);
        }

        private void AddScore(long points)
        {
            state.Score = state.Score > long.MaxValue - points ? long.MaxValue : state.Score + points;
        }

        private static long Increment(long value) { return value == long.MaxValue ? value : value + 1; }
    }
}
