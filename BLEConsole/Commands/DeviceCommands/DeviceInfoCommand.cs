using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.DeviceCommands
{
    /// <summary>
    /// NEW FEATURE: Automatically read Device Information Service
    /// </summary>
    public class DeviceInfoCommand : ICommand
    {
        private readonly IOutputWriter _output;

        // Device Information Service UUID
        private static readonly Guid DeviceInformationServiceUuid = new Guid("0000180a-0000-1000-8000-00805f9b34fb");

        // Standard DIS characteristic UUIDs
        private static readonly Guid ManufacturerNameUuid = new Guid("00002a29-0000-1000-8000-00805f9b34fb");
        private static readonly Guid ModelNumberUuid = new Guid("00002a24-0000-1000-8000-00805f9b34fb");
        private static readonly Guid SerialNumberUuid = new Guid("00002a25-0000-1000-8000-00805f9b34fb");
        private static readonly Guid HardwareRevisionUuid = new Guid("00002a27-0000-1000-8000-00805f9b34fb");
        private static readonly Guid FirmwareRevisionUuid = new Guid("00002a26-0000-1000-8000-00805f9b34fb");
        private static readonly Guid SoftwareRevisionUuid = new Guid("00002a28-0000-1000-8000-00805f9b34fb");
        private static readonly Guid SystemIdUuid = new Guid("00002a23-0000-1000-8000-00805f9b34fb");
        private static readonly Guid PnpIdUuid = new Guid("00002a50-0000-1000-8000-00805f9b34fb");

        public string Name => "device-info";
        public string[] Aliases => new[] { "di", "info" };
        public string Description => "Read Device Information Service";
        public string Usage => "device-info";

        public DeviceInfoCommand(IOutputWriter output)
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

            try
            {
                // Get Device Information Service
                var servicesResult = await context.SelectedDevice.GetGattServicesForUuidAsync(
                    DeviceInformationServiceUuid,
                    BluetoothCacheMode.Uncached);

                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    _output.WriteLine("Device Information Service not available.");
                    return 1;
                }

                if (servicesResult.Services.Count == 0)
                {
                    _output.WriteLine("Device Information Service not found.");
                    return 1;
                }

                var disService = servicesResult.Services[0];
                _output.WriteLine("Device Information:");
                _output.WriteLine("==================");

                // Try to read all standard DIS characteristics
                await TryReadCharacteristic(disService, ManufacturerNameUuid, "Manufacturer");
                await TryReadCharacteristic(disService, ModelNumberUuid, "Model Number");
                await TryReadCharacteristic(disService, SerialNumberUuid, "Serial Number");
                await TryReadCharacteristic(disService, HardwareRevisionUuid, "Hardware Revision");
                await TryReadCharacteristic(disService, FirmwareRevisionUuid, "Firmware Revision");
                await TryReadCharacteristic(disService, SoftwareRevisionUuid, "Software Revision");
                await TryReadCharacteristic(disService, SystemIdUuid, "System ID");
                await TryReadCharacteristic(disService, PnpIdUuid, "PnP ID");

                return 0;
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error reading device info: {ex.Message}");
                return 1;
            }
        }

        private async Task TryReadCharacteristic(GattDeviceService service, Guid uuid, string name)
        {
            try
            {
                var charResult = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Uncached);

                if (charResult.Status == GattCommunicationStatus.Success && charResult.Characteristics.Count > 0)
                {
                    var characteristic = charResult.Characteristics[0];

                    // Check if characteristic supports read
                    if (!characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                    {
                        return;
                    }

                    var readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                    if (readResult.Status == GattCommunicationStatus.Success)
                    {
                        var buffer = readResult.Value;

                        // Format based on characteristic type
                        string value;
                        if (uuid == SystemIdUuid)
                        {
                            // System ID is 8 bytes: 5-byte manufacturer identifier + 3-byte organizationally unique identifier
                            value = DataFormatter.FormatValue(buffer, Enums.DataFormat.Hex);
                        }
                        else if (uuid == PnpIdUuid)
                        {
                            // PnP ID has specific format: Vendor ID Source (1 byte) + Vendor ID (2 bytes) + Product ID (2 bytes) + Product Version (2 bytes)
                            value = DataFormatter.FormatValue(buffer, Enums.DataFormat.Hex);
                        }
                        else
                        {
                            // All other characteristics are strings
                            value = DataFormatter.FormatValue(buffer, Enums.DataFormat.UTF8);
                        }

                        _output.WriteLine($"  {name,-20}: {value}");
                    }
                }
            }
            catch
            {
                // Silently skip characteristics that can't be read
            }
        }
    }
}
