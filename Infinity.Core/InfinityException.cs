namespace Infinity.Core
{
    /// <summary>
    ///     Wrapper for exceptions thrown
    /// </summary>
    [Serializable]
    public class InfinityException : Exception
    {
        public InfinityException(string msg) : base(msg)
        {
        }

        public InfinityException(string msg, Exception e) : base(msg, e)
        {
        }
    }
}
