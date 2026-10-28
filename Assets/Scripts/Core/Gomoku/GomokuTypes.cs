namespace XanaduYonder.Core.Gomoku
{
    public enum GomokuStone { Empty = 0, Black = 1, White = 2 }
    public enum GomokuState { Playing = 0, BlackWon = 1, WhiteWon = 2, Draw = 3 }
    public enum GomokuMode { HumanVsAi = 0, Local = 1 }
    public enum GomokuDifficulty { Easy = 0, Normal = 1, Hard = 2 }
}
