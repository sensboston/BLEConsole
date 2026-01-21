using System.Threading.Tasks;
using BLEConsole.Core;

namespace BLEConsole.Commands
{
    /// <summary>
    /// Interface for all BLE console commands
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Primary command name (e.g., "read", "write")
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Command aliases (e.g., "r" for "read")
        /// </summary>
        string[] Aliases { get; }

        /// <summary>
        /// Short description for help text
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Detailed usage information
        /// </summary>
        string Usage { get; }

        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="context">BLE application context</param>
        /// <param name="parameters">Command parameters as string</param>
        /// <returns>Exit code (0 = success, >0 = error)</returns>
        Task<int> ExecuteAsync(BleContext context, string parameters);
    }
}
