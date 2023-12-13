﻿namespace Infinity.Core
{
    /// <summary>
    ///     Wrapper for exceptions thrown
    /// </summary>
    [Serializable]
    public class InfinityException : Exception
    {
        public InfinityException(string _msg) : base(_msg)
        {
        }

        public InfinityException(string _msg, Exception _e) : base(_msg, _e)
        {
        }
    }
}
