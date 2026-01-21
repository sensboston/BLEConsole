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
    /// NEW FEATURE: Read descriptor value
    /// </summary>
    public class ReadDescCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "read-desc";
        public string[] Aliases => new[] { "rd" };
        public string Description => "Read descriptor value";
        public string Usage => "read-desc <characteristic>/<descriptor>";

        public ReadDescCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.SelectedDevice == null)
            {
                _output.WriteLine("No device is connected. Use 'open' first.");
                return 1;
            }

            if (context.SelectedService == null)
            {
                _output.WriteLine("No service selected. Use 'set' first.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(parameters) || !parameters.Contains("/"))
            {
                _output.WriteLine("Usage: read-desc <characteristic>/<descriptor>");
                _output.WriteLine("Example: read-desc DeviceName/ClientCharacteristicConfiguration");
                return 1;
            }

            var parts = parameters.Split('/');
            if (parts.Length != 2)
            {
                _output.WriteLine("Usage: read-desc <characteristic>/<descriptor>");
                return 1;
            }

            string charName = parts[0].Trim();
            string descName = parts[1].Trim();

            // Find characteristic
            var characteristic = FindCharacteristic(context, charName);
            if (characteristic == null)
                return 1;

            try
            {
                // Get descriptors
                var descResult = await characteristic.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
                if (descResult.Status != GattCommunicationStatus.Success)
                {
                    _output.WriteLine($"Failed to get descriptors: {descResult.Status}");
                    return 1;
                }

                // Find descriptor by name or number
                GattDescriptor descriptor = null;

                if (descName.StartsWith("#"))
                {
                    if (int.TryParse(descName.Substring(1), out int index))
                    {
                        if (index >= 0 && index < descResult.Descriptors.Count)
                            descriptor = descResult.Descriptors[index];
                    }
                }
                else
                {
                    // Find by UUID or name
                    foreach (var desc in descResult.Descriptors)
                    {
                        string name = GetDescriptorName(desc.Uuid);
                        if (name.Equals(descName, StringComparison.OrdinalIgnoreCase) ||
                            desc.Uuid.ToString().Equals(descName, StringComparison.OrdinalIgnoreCase))
                        {
                            descriptor = desc;
                            break;
                        }
                    }
                }

                if (descriptor == null)
                {
                    _output.WriteLine($"Descriptor '{descName}' not found.");
                    return 1;
                }

                // Read descriptor value
                var result = await descriptor.ReadValueAsync(BluetoothCacheMode.Uncached);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    var data = result.Value;
                    if (context.ReceivedDataFormats.Count > 1)
                    {
                        string formattedData = DataFormatter.FormatValueMultipleFormattes(data, context.ReceivedDataFormats);
                        _output.WriteLine(formattedData);
                    }
                    else
                    {
                        string formattedData = DataFormatter.FormatValue(data, context.ReceivedDataFormats[0]);
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
                _output.WriteError($"Error reading descriptor: {ex.Message}");
                return 1;
            }
        }

        private GattCharacteristic FindCharacteristic(BleContext context, string charName)
        {
            var name = DeviceLookup.GetIdByNameOrNumber(context.Characteristics, charName);
            if (string.IsNullOrEmpty(name))
                return null;

            var charDisplay = context.Characteristics.FirstOrDefault(c => c.Name == name);
            if (charDisplay?.characteristic == null)
            {
                _output.WriteLine($"Characteristic '{name}' not found.");
                return null;
            }

            return charDisplay.characteristic;
        }

        private string GetDescriptorName(Guid uuid)
        {
            ushort shortId = UuidConverter.ConvertUuidToShortId(uuid);
            string name = Enum.GetName(typeof(Enums.GattNativeDescriptorUuid), shortId);
            return name ?? $"0x{shortId:X4}";
        }
    }
}
