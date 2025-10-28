using System;

namespace BLEConsole.Core
{
    /// <summary>
    /// Console implementation of IOutputWriter
    /// </summary>
    public class ConsoleOutputWriter : IOutputWriter
    {
        public bool IsRedirected => Console.IsOutputRedirected;

        public void Write(string message)
        {
            if (!Console.IsOutputRedirected)
                Console.Write(message);
        }

        public void WriteLine(string message)
        {
            if (!Console.IsOutputRedirected)
                Console.WriteLine(message);
        }

        public void WriteError(string message)
        {
            if (!Console.IsOutputRedirected)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(message);
                Console.ForegroundColor = oldColor;
            }
        }
    }
}
