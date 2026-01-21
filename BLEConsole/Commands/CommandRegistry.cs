using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;

namespace BLEConsole.Commands
{
    /// <summary>
    /// Registry and dispatcher for all BLE console commands
    /// </summary>
    public class CommandRegistry
    {
        private readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
        private readonly IOutputWriter _output;

        public CommandRegistry(IOutputWriter output)
        {
            _output = output;
        }

        /// <summary>
        /// Register a command and all its aliases
        /// </summary>
        public void Register(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            // Register primary name
            _commands[command.Name] = command;

            // Register all aliases
            if (command.Aliases != null)
            {
                foreach (var alias in command.Aliases)
                {
                    if (!string.IsNullOrWhiteSpace(alias))
                        _commands[alias] = command;
                }
            }
        }

        /// <summary>
        /// Execute a command by name
        /// </summary>
        public async Task<int> ExecuteAsync(string commandName, BleContext context, string parameters)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                return 0;

            if (_commands.TryGetValue(commandName, out var command))
            {
                try
                {
                    return await command.ExecuteAsync(context, parameters ?? string.Empty);
                }
                catch (Exception ex)
                {
                    _output.WriteError($"Error executing '{commandName}': {ex.Message}");
                    return 1;
                }
            }

            _output.WriteLine($"Unknown command: {commandName}");
            _output.WriteLine("Type 'help' for available commands.");
            return 1;
        }

        /// <summary>
        /// Get all registered commands (unique by Name)
        /// </summary>
        public IEnumerable<ICommand> GetAllCommands()
        {
            return _commands.Values.Distinct();
        }

        /// <summary>
        /// Check if a command exists
        /// </summary>
        public bool HasCommand(string commandName)
        {
            return _commands.ContainsKey(commandName);
        }
    }
}
