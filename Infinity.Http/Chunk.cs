﻿namespace Infinity.Http
{
    /// <summary>
    /// A chunk of data, used when reading from a request where the Transfer-Encoding header includes 'chunked'.
    /// </summary>
    public class Chunk
    {
        /// <summary>
        /// Length of the data.
        /// </summary>
        public int Length
        {
            get
            {
                return length;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Length));
                }

                length = value;
            }
        }

        /// <summary>
        /// Data.
        /// </summary>
        public byte[] Data { get; set; } = null;

        /// <summary>
        /// Any additional metadata that appears on the length line after the length hex value and semicolon.
        /// </summary>
        public string Metadata { get; set; } = null;

        /// <summary>
        /// Indicates whether or not this is the final chunk, i.e. the chunk length received was zero.
        /// </summary>
        public bool IsFinal { get; set; } = false;

        private int length = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Chunk()
        {
        }
    }
}
