namespace Infinity.Core
{
    public interface ILogger
    {
        void WriteVerbose(string _msg);
        void WriteError(string _msg);
        void WriteWarning(string _msg);
        void WriteInfo(string _msg);
    }

    public class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        public void WriteVerbose(string _msg)
        {
        }

        public void WriteError(string _msg)
        {
        }

        public void WriteWarning(string _msg)
        {
        }

        public void WriteInfo(string _msg)
        {
        }
    }

    public class ConsoleLogger : ILogger
    {
        private bool Verbose;
        public ConsoleLogger(bool _verbose)
        {
            Verbose = _verbose;
        }

        public void WriteVerbose(string _msg)
        {
            if (Verbose)
            {
                Console.WriteLine($"{DateTime.Now} [VERBOSE] {_msg}");
            }
        }

        public void WriteWarning(string _msg)
        {
            Console.WriteLine($"{DateTime.Now} [WARN] {_msg}");
        }

        public void WriteError(string _msg)
        {
            Console.WriteLine($"{DateTime.Now} [ERROR] {_msg}");
        }

        public void WriteInfo(string _msg)
        {
            Console.WriteLine($"{DateTime.Now} [INFO] {_msg}");
        }
    }
}
