using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Devices.Bluetooth;

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

            if (device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                _output.WriteLine($"Device {device.Name} is disconnected.");
                return Task.FromResult(0);
            }

            // Device is connected
            bool isPaired = context.IsPaired(device);
            _output.WriteLine($"Device {device.Name} is connected" +
                (isPaired ? " and is paired." : ", but is NOT paired."));

            // List all services
            if (context.Services.Count > 0)
            {
                _output.WriteLine("Available services:");
                for (int i = 0; i < context.Services.Count; i++)
                {
                    _output.WriteLine($"#{i:00}: {context.Services[i].Name}");
                }

                // If service is selected
                if (context.SelectedService != null)
                {
                    _output.WriteLine($"Selected service: {context.SelectedService.Name}");

                    // List all characteristics
                    if (context.Characteristics.Count > 0)
                    {
                        _output.WriteLine("Available characteristics:");
                        for (int i = 0; i < context.Characteristics.Count; i++)
                        {
                            _output.WriteLine($"#{i:00}: {context.Characteristics[i].Name}\t{context.Characteristics[i].Chars}");
                        }

                        if (context.SelectedCharacteristic != null)
                        {
                            _output.WriteLine($"Selected characteristic: {context.SelectedCharacteristic.Name}");
                        }
                    }
                }
            }

            return Task.FromResult(0);
        }
    }
}
