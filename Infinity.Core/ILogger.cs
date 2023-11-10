﻿namespace Infinity.Core
{
    public interface ILogger
    {
        void WriteVerbose(string msg);
        void WriteError(string msg);
        void WriteWarning(string msg);
        void WriteInfo(string msg);
    }

    public class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        public void WriteVerbose(string msg)
        {
        }

        public void WriteError(string msg)
        {
        }

        public void WriteWarning(string msg)
        {
        }

        public void WriteInfo(string msg)
        {
        }
    }

    public class ConsoleLogger : ILogger
    {
        private bool Verbose;
        public ConsoleLogger(bool verbose)
        {
            Verbose = verbose;
        }

        public void WriteVerbose(string msg)
        {
            if (Verbose)
            {
                Console.WriteLine($"{DateTime.Now} [VERBOSE] {msg}");
            }
        }

        public void WriteWarning(string msg)
        {
            Console.WriteLine($"{DateTime.Now} [WARN] {msg}");
        }

        public void WriteError(string msg)
        {
            Console.WriteLine($"{DateTime.Now} [ERROR] {msg}");
        }

        public void WriteInfo(string msg)
        {
            Console.WriteLine($"{DateTime.Now} [INFO] {msg}");
        }
    }
}
