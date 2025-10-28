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
        public void Write(string message) => System.Console.Write(message);
        public void WriteLine(string message) => System.Console.WriteLine(message);
        public void WriteError(string message) => System.Console.Error.WriteLine(message);
        public bool IsRedirected => System.Console.IsOutputRedirected;
    }
}
