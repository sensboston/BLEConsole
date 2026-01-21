using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;

namespace BLEConsole.Commands.UtilityCommands
{
    public class HelpCommand : ICommand
    {
        private readonly CommandRegistry _registry;
        private readonly IOutputWriter _output;

        public string Name => "help";
        public string[] Aliases => new[] { "?" };
        public string Description => "Show available commands";
        public string Usage => "help [command_name]";

        public HelpCommand(CommandRegistry registry, IOutputWriter output)
        {
            _registry = registry;
            _output = output;
        }

        public Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                ShowAllCommands();
            }
            else
            {
                ShowCommandHelp(parameters.Trim());
            }
            return Task.FromResult(0);
        }

        private void ShowAllCommands()
        {
            _output.WriteLine("Available commands:");
            _output.WriteLine("");

            foreach (var cmd in _registry.GetAllCommands().OrderBy(c => c.Name))
            {
                var aliases = cmd.Aliases != null && cmd.Aliases.Length > 0 
                    ? $" ({string.Join(", ", cmd.Aliases)})" 
                    : "";
                _output.WriteLine($"  {cmd.Name,-20}{aliases,-15} - {cmd.Description}");
            }

            _output.WriteLine("");
            _output.WriteLine("Type 'help <command>' for detailed usage information.");
        }

        private void ShowCommandHelp(string commandName)
        {
            var cmd = _registry.GetAllCommands().FirstOrDefault(c => 
                c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));

            if (cmd != null)
            {
                _output.WriteLine($"Command: {cmd.Name}");
                if (cmd.Aliases != null && cmd.Aliases.Length > 0)
                    _output.WriteLine($"Aliases: {string.Join(", ", cmd.Aliases)}");
                _output.WriteLine($"Description: {cmd.Description}");
                _output.WriteLine($"Usage: {cmd.Usage}");
            }
            else
            {
                _output.WriteLine($"Unknown command: {commandName}");
            }
        }
    }
}
