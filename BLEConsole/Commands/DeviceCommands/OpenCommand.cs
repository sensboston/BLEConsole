using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Models;
using BLEConsole.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace BLEConsole.Commands.DeviceCommands
{
    /// <summary>
    /// Open connection to BLE device
    /// </summary>
    public class OpenCommand : ICommand
    {
        private readonly IOutputWriter _output;
        private string _pairingPin;

        public string Name => "open";
        public string[] Aliases => new string[] { };
        public string Description => "Connect to BLE device";
        public string Usage => "open <device_name_or_#_or_address> [pin]";

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

            // Parse parameters: device name and optional PIN
            string deviceName;
            string pin = null;
            var parts = parameters.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            deviceName = parts[0];
            if (parts.Length > 1)
                pin = parts[1].Trim();

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

                // Check pairing status and auto-pair if needed
                var pairingInfo = context.SelectedDevice.DeviceInformation.Pairing;
                bool canPair = pairingInfo.CanPair;
                bool isPaired = context.IsPaired(context.SelectedDevice);

                // Build connection message
                string pairingStatus = "";
                if (canPair)
                {
                    pairingStatus = isPaired ? " It is paired." : " It is not paired.";
                }
                _output.WriteLine($"Connecting to {context.SelectedDevice.Name}.{pairingStatus}");

                // Auto-pair if device supports pairing and is not paired
                if (canPair && !isPaired)
                {
                    await TryAutoPair(context, pin);
                }

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

        private async Task TryAutoPair(BleContext context, string pin)
        {
            try
            {
                var pairingInfo = context.SelectedDevice.DeviceInformation.Pairing;
                DevicePairingResult result;

                if (!string.IsNullOrEmpty(pin))
                {
                    // Pair with provided PIN
                    _output.WriteLine($"Attempting to pair with PIN...");
                    _pairingPin = pin;
                    pairingInfo.Custom.PairingRequested += OnPairingRequested;
                    result = await pairingInfo.Custom.PairAsync(DevicePairingKinds.ProvidePin);
                    pairingInfo.Custom.PairingRequested -= OnPairingRequested;
                    _pairingPin = null;
                }
                else
                {
                    // Try simple pairing first, with fallback to DisplayPin
                    _output.WriteLine($"Attempting to pair...");
                    pairingInfo.Custom.PairingRequested += OnPairingRequested;
                    result = await pairingInfo.Custom.PairAsync(
                        DevicePairingKinds.ConfirmOnly |
                        DevicePairingKinds.DisplayPin |
                        DevicePairingKinds.ConfirmPinMatch);
                    pairingInfo.Custom.PairingRequested -= OnPairingRequested;
                }

                if (result.Status == DevicePairingResultStatus.Paired)
                {
                    context.SetPairingStatus(context.SelectedDevice, true);
                    _output.WriteLine("Pairing successful.");
                }
                else if (result.Status == DevicePairingResultStatus.AlreadyPaired)
                {
                    context.SetPairingStatus(context.SelectedDevice, true);
                    _output.WriteLine("Device is already paired.");
                }
                else
                {
                    _output.WriteLine($"Pairing failed: {result.Status}. Continuing without pairing...");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Pairing error: {ex.Message}. Continuing without pairing...");
            }
        }

        private void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            switch (args.PairingKind)
            {
                case DevicePairingKinds.ConfirmOnly:
                    args.Accept();
                    break;

                case DevicePairingKinds.ProvidePin:
                    if (!string.IsNullOrEmpty(_pairingPin))
                        args.Accept(_pairingPin);
                    else
                        args.Accept();
                    break;

                case DevicePairingKinds.DisplayPin:
                    // Device is showing a PIN that user needs to confirm
                    _output.WriteLine($"Device PIN: {args.Pin}");
                    args.Accept();
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    // Both sides show PIN, user confirms they match
                    _output.WriteLine($"Confirm PIN: {args.Pin}");
                    args.Accept();
                    break;

                default:
                    args.Accept();
                    break;
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

                        // Remove event handler using stored reference
                        if (context.ValueChangedHandlers.TryGetValue(subscriber, out var handler))
                        {
                            subscriber.ValueChanged -= handler;
                            context.ValueChangedHandlers.Remove(subscriber);
                        }
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
