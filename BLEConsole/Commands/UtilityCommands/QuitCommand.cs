using System.Threading.Tasks;
using BLEConsole.Core;

namespace BLEConsole.Commands.UtilityCommands
{
    public class QuitCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "quit";
        public string[] Aliases => new[] { "q", "exit" };
        public string Description => "Exit the application";
        public string Usage => "quit";

        public QuitCommand(IOutputWriter output)
        {
            _output = output;
        }

        public Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (!_output.IsRedirected)
                _output.WriteLine("Bye!");
            
            // Special exit code to signal main loop to stop
            return Task.FromResult(-1);
        }
    }
}
