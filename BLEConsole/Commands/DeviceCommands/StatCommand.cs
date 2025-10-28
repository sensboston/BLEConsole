using System.Threading.Tasks;
using BLEConsole.Core;

namespace BLEConsole.Commands.DeviceCommands
{
    public class StatCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "stat";
        public string[] Aliases => new[] { "st", "status" };
        public string Description => "Show current device status";
        public string Usage => "stat";

        public StatCommand(IOutputWriter output)
        {
            _output = output;
        }

        public Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.SelectedDevice == null)
            {
                _output.WriteLine("No device connected.");
                return Task.FromResult(0);
            }

            var device = context.SelectedDevice;
            _output.WriteLine($"Device name: {device.Name}");
            _output.WriteLine($"Device ID: {device.DeviceId}");
            _output.WriteLine($"Connection status: {device.ConnectionStatus}");
            _output.WriteLine($"Paired: {context.IsPaired(device)}");

            if (context.SelectedService != null)
            {
                _output.WriteLine($"Selected service: {context.SelectedService.Name}");
            }

            return Task.FromResult(0);
        }
    }
}
