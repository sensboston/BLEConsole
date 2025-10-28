using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// NEW FEATURE: List descriptors for a characteristic
    /// </summary>
    public class DescCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "desc";
        public string[] Aliases => new[] { "descriptors" };
        public string Description => "List descriptors for a characteristic";
        public string Usage => "desc <characteristic_name>";

        public DescCommand(IOutputWriter output)
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

            if (string.IsNullOrWhiteSpace(parameters))
            {
                _output.WriteLine("Usage: desc <characteristic_name>");
                return 1;
            }

            var charName = BLEConsole.Utilities.GetIdByNameOrNumber(context.Characteristics, parameters.Trim());
            if (string.IsNullOrEmpty(charName))
                return 1;

            var characteristic = context.Characteristics.FirstOrDefault(c => c.Name == charName);
            if (characteristic?.characteristic == null)
            {
                _output.WriteLine($"Characteristic '{charName}' not found.");
                return 1;
            }

            try
            {
                var result = await characteristic.characteristic.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
                
                if (result.Status != GattCommunicationStatus.Success)
                {
                    _output.WriteLine($"Failed to get descriptors: {result.Status}");
                    return 1;
                }

                if (result.Descriptors.Count == 0)
                {
                    _output.WriteLine("No descriptors found for this characteristic.");
                    return 0;
                }

                _output.WriteLine($"Descriptors for {charName}:");
                for (int i = 0; i < result.Descriptors.Count; i++)
                {
                    var desc = result.Descriptors[i];
                    string descName = GetDescriptorName(desc.Uuid);
                    _output.WriteLine($"  #{i:00}: {descName}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error getting descriptors: {ex.Message}");
                return 1;
            }
        }

        private string GetDescriptorName(Guid uuid)
        {
            ushort shortId = Utils.UuidConverter.ConvertUuidToShortId(uuid);
            string name = Enum.GetName(typeof(Enums.GattNativeDescriptorUuid), shortId);
            return name ?? $"0x{shortId:X4}";
        }
    }
}
