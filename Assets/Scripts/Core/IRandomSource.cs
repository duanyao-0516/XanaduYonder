namespace XanaduYonder.Core
{
    public interface IRandomSource
    {
        /// <summary>Returns a value in the range [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }
}
