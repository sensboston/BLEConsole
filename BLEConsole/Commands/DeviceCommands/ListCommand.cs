using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;

namespace BLEConsole.Commands.DeviceCommands
{
    public class ListCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "list";
        public string[] Aliases => new[] { "ls" };
        public string Description => "List available BLE devices";
        public string Usage => "list [w]  (w = wide format with IDs)";

        public ListCommand(IOutputWriter output)
        {
            _output = output;
        }

        public Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            bool wideFormat = parameters?.Trim().ToLower() == "w";

            if (context.DiscoveredDevices.Count == 0)
            {
                _output.WriteLine("No BLE devices found.");
                return Task.FromResult(0);
            }

            // Sort devices by name to match OpenCommand behavior
            var sortedDevices = context.DiscoveredDevices.OrderBy(d => d.Name).ToList();

            if (wideFormat)
            {
                _output.WriteLine("#    ID                                                Name");
                for (int i = 0; i < sortedDevices.Count; i++)
                {
                    var device = sortedDevices[i];
                    _output.WriteLine($"#{i:00}: {device.Id,-50} {device.Name}");
                }
            }
            else
            {
                _output.WriteLine("#    Address           Name");
                for (int i = 0; i < sortedDevices.Count; i++)
                {
                    var device = sortedDevices[i];
                    string btAddr = device.Id.Split('-').Last();
                    if (btAddr.Length == 12)
                    {
                        btAddr = string.Join(":", Enumerable.Range(0, 6)
                            .Select(n => btAddr.Substring(n * 2, 2)));
                    }
                    _output.WriteLine($"#{i:00}: {btAddr,-18} {device.Name}");
                }
            }

            return Task.FromResult(0);
        }
    }
}
