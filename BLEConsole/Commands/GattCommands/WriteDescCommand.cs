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
    /// NEW FEATURE: Write descriptor value
    /// </summary>
    public class WriteDescCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "write-desc";
        public string[] Aliases => new[] { "wd" };
        public string Description => "Write descriptor value";
        public string Usage => "write-desc <characteristic>/<descriptor> <value>";

        public WriteDescCommand(IOutputWriter output)
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

            if (context.SelectedService == null)
            {
                _output.WriteLine("No service selected. Use 'set' first.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(parameters) || !parameters.Contains("/"))
            {
                _output.WriteLine("Usage: write-desc <characteristic>/<descriptor> <value>");
                _output.WriteLine("Example: write-desc DeviceName/ClientCharacteristicConfiguration 01 00");
                return 1;
            }

            // Parse: "char/desc value"
            var firstSlash = parameters.IndexOf('/');
            var charName = parameters.Substring(0, firstSlash).Trim();
            var remaining = parameters.Substring(firstSlash + 1).TrimStart();

            var spaceIndex = remaining.IndexOf(' ');
            if (spaceIndex == -1)
            {
                _output.WriteLine("Usage: write-desc <characteristic>/<descriptor> <value>");
                return 1;
            }

            var descName = remaining.Substring(0, spaceIndex).Trim();
            var value = remaining.Substring(spaceIndex + 1).Trim();

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

                // Find descriptor
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

                // Format data
                var buffer = DataFormatter.FormatData(value, context.SendDataFormat);
                if (buffer == null)
                {
                    _output.WriteLine("Failed to format data.");
                    return 1;
                }

                // Write descriptor value
                var result = await descriptor.WriteValueAsync(buffer);

                if (result == GattCommunicationStatus.Success)
                {
                    if (!_output.IsRedirected)
                        _output.WriteLine($"Wrote {buffer.Length} bytes to descriptor");
                    return 0;
                }
                else
                {
                    _output.WriteLine($"Write failed: {result}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error writing descriptor: {ex.Message}");
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
