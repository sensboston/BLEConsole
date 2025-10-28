namespace BLEConsole.Core
{
    /// <summary>
    /// Abstraction for console output. Allows for testing and alternative output targets.
    /// </summary>
    public interface IOutputWriter
    {
        void Write(string message);
        void WriteLine(string message);
        void WriteError(string message);
        bool IsRedirected { get; }
    }

    /// <summary>
    /// Console implementation of IOutputWriter
    /// </summary>
    public class ConsoleOutputWriter : IOutputWriter
    {
        public bool IsRedirected => System.Console.IsOutputRedirected;

        public void Write(string message)
        {
            if (!System.Console.IsOutputRedirected)
                System.Console.Write(message);
        }

        public void WriteLine(string message)
        {
            if (!System.Console.IsOutputRedirected)
                System.Console.WriteLine(message);
        }

        public void WriteError(string message)
        {
            if (!System.Console.IsOutputRedirected)
            {
                var oldColor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = System.ConsoleColor.Red;
                System.Console.WriteLine(message);
                System.Console.ForegroundColor = oldColor;
            }
        }
    }
}
