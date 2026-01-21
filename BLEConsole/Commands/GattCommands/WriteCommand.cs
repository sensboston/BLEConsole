using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Utils;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// Write to characteristic (with optional Write Without Response)
    /// </summary>
    public class WriteCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "write";
        public string[] Aliases => new[] { "w" };
        public string Description => "Write value to characteristic";
        public string Usage => "write [-nr] <characteristic_name> <value>";

        public WriteCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.SelectedDevice == null)
            {
                _output.WriteLine("No device connected. Use 'open' first.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(parameters))
            {
                _output.WriteLine("Usage: write [-nr] <char_name> <value>");
                _output.WriteLine("  -nr : Write without response (faster, no ACK)");
                return 1;
            }

            // Parse parameters
            bool withoutResponse = parameters.TrimStart().StartsWith("-nr");
            if (withoutResponse)
                parameters = parameters.Substring(parameters.IndexOf("-nr") + 3).TrimStart();

            var parts = parameters.Split(new[] { ' ' }, 2);
            if (parts.Length < 2)
            {
                _output.WriteLine("Usage: write [-nr] <char_name> <value>");
                return 1;
            }

            string charName = parts[0];
            string value = parts[1];

            // Find characteristic
            var characteristic = FindCharacteristic(context, charName);
            if (characteristic == null)
                return 1;

            // Format data
            var buffer = DataFormatter.FormatData(value, context.SendDataFormat);
            if (buffer == null)
            {
                _output.WriteLine("Failed to format data.");
                return 1;
            }

            try
            {
                // Choose write option
                var writeOption = withoutResponse 
                    ? GattWriteOption.WriteWithoutResponse 
                    : GattWriteOption.WriteWithResponse;

                var result = await characteristic.WriteValueWithResultAsync(buffer, writeOption);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    string mode = withoutResponse ? " (no response)" : "";
                    if (!_output.IsRedirected)
                        _output.WriteLine($"Wrote {buffer.Length} bytes{mode}");
                    return 0;
                }
                else
                {
                    _output.WriteLine($"Write failed: {result.Status}");
                    if (result.ProtocolError.HasValue)
                        _output.WriteLine($"Protocol error: {ProtocolErrorFormatter.FormatProtocolError(result.ProtocolError)}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error writing: {ex.Message}");
                return 1;
            }
        }

        private GattCharacteristic FindCharacteristic(BleContext context, string charName)
        {
            if (context.SelectedService == null)
            {
                _output.WriteLine("No service selected. Use 'set' first.");
                return null;
            }

            var name = DeviceLookup.GetIdByNameOrNumber(context.Characteristics, charName);
            if (string.IsNullOrEmpty(name))
                return null;

            var charDisplay = context.Characteristics.FirstOrDefault(c => c.Name == name);
            if (charDisplay?.characteristic == null)
            {
                _output.WriteLine($"Characteristic '{name}' not found.");
                return null;
            }

            if (!charDisplay.CanWrite)
            {
                _output.WriteLine($"Characteristic '{name}' is not writable.");
                return null;
            }

            return charDisplay.characteristic;
        }
    }
}
