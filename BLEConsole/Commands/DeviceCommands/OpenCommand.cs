using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Models;
using BLEConsole.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.DeviceCommands
{
    /// <summary>
    /// Open connection to BLE device
    /// </summary>
    public class OpenCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "open";
        public string[] Aliases => new string[] { };
        public string Description => "Connect to BLE device";
        public string Usage => "open <device_name_or_#_or_address>";

        public OpenCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                _output.WriteLine("Device name cannot be empty.");
                return 1;
            }

            string deviceName = parameters.Trim();
            var devices = context.DiscoveredDevices.OrderBy(d => d.Name).ToList();
            string foundId = DeviceLookup.GetIdByNameOrNumber(devices, deviceName);

            if (string.IsNullOrEmpty(foundId))
            {
                _output.WriteLine($"Device '{deviceName}' not found in device list.");
                return 1;
            }

            try
            {
                // Only allow one connection at a time - close existing connection
                if (context.SelectedDevice != null)
                {
                    await CloseExistingDevice(context);
                }

                // Clear state
                context.SelectedCharacteristic = null;
                context.SelectedService = null;
                context.Services.Clear();

                // Connect to device with timeout
                context.SelectedDevice = await BluetoothLEDevice.FromIdAsync(foundId)
                    .AsTask()
                    .TimeoutAfter(context.Timeout);

                if (context.SelectedDevice == null)
                {
                    _output.WriteLine($"Device {deviceName} is unreachable.");
                    return 1;
                }

                bool isPaired = context.IsPaired(context.SelectedDevice);
                _output.WriteLine($"Connecting to {context.SelectedDevice.Name}. " +
                    (isPaired ? "It is paired" : "It is NOT paired"));

                // Get GATT services
                var result = await context.SelectedDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                if (result.Status != GattCommunicationStatus.Success)
                {
                    _output.WriteLine($"Device {deviceName} is unreachable.");
                    return 1;
                }

                _output.WriteLine($"Found {result.Services.Count} services:");

                // Add services to context and display them
                for (int i = 0; i < result.Services.Count; i++)
                {
                    var serviceToDisplay = new BluetoothLEAttributeDisplay(result.Services[i]);
                    context.Services.Add(serviceToDisplay);
                    _output.WriteLine($"#{i:00}: {serviceToDisplay.Name}");
                }

                return 0;
            }
            catch (TimeoutException)
            {
                _output.WriteLine($"Connection to device {deviceName} timed out.");
                return 1;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Device {deviceName} is unreachable. Error: {ex.Message}");
                return 1;
            }
        }

        private async Task CloseExistingDevice(BleContext context)
        {
            // Remove all subscriptions
            if (context.Subscribers.Count > 0)
            {
                foreach (var subscriber in context.Subscribers)
                {
                    try
                    {
                        await subscriber.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.None);
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }
                context.Subscribers.Clear();
            }

            // Clean up device
            if (context.SelectedDevice != null)
            {
                _output.WriteLine($"Device {context.SelectedDevice.Name} is disconnected.");

                context.Services?.ForEach((s) => { s.service?.Dispose(); });
                context.Services?.Clear();
                context.Characteristics?.Clear();
                context.SelectedDevice?.Dispose();
                context.SelectedDevice = null;
            }
        }
    }
}
