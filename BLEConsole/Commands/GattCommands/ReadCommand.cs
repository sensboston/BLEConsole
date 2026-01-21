using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// Read from characteristic
    /// </summary>
    public class ReadCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "read";
        public string[] Aliases => new[] { "r" };
        public string Description => "Read value from characteristic";
        public string Usage => "read <characteristic_name>";

        public ReadCommand(IOutputWriter output)
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
                _output.WriteLine("Usage: read <characteristic_name>");
                return 1;
            }

            var characteristic = FindCharacteristic(context, parameters.Trim());
            if (characteristic == null)
                return 1;

            try
            {
                var result = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    var data = result.Value;
                    if (context.ReceivedDataFormats.Count > 1)
                    {
                        string formattedData = DataFormatter.FormatValueMultipleFormattes(data, context.ReceivedDataFormats, context.ByteOrder);
                        _output.WriteLine(formattedData);
                    }
                    else if (context.ReceivedDataFormats.Count == 1)
                    {
                        string formattedData = DataFormatter.FormatValue(data, context.ReceivedDataFormats[0], context.ByteOrder);
                        _output.WriteLine(formattedData);
                    }
                    else
                    {
                        // Default to UTF8 if no format specified
                        string formattedData = DataFormatter.FormatValue(data, Enums.DataFormat.UTF8, context.ByteOrder);
                        _output.WriteLine(formattedData);
                    }
                    return 0;
                }
                else
                {
                    _output.WriteLine($"Read failed: {result.Status}");
                    if (result.ProtocolError.HasValue)
                        _output.WriteLine($"Protocol error: {ProtocolErrorFormatter.FormatProtocolError(result.ProtocolError)}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error reading: {ex.Message}");
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

            if (!charDisplay.CanRead)
            {
                _output.WriteLine($"Characteristic '{name}' is not readable.");
                return null;
            }

            return charDisplay.characteristic;
        }
    }
}
