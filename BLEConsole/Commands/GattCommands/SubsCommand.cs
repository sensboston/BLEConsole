using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Models;
using BLEConsole.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// Subscribe to characteristic value changes (notifications/indications)
    /// </summary>
    public class SubsCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "subs";
        public string[] Aliases => new[] { "sub" };
        public string Description => "Subscribe to characteristic value changes";
        public string Usage => "subs <characteristic> | subs <service>/<characteristic>";

        public SubsCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.SelectedDevice == null)
            {
                _output.WriteLine("No BLE device is connected.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(parameters))
            {
                _output.WriteLine("Nothing to subscribe, please specify characteristic name or #.");
                return 1;
            }

            var parts = parameters.Trim().Split('/');
            List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();
            string charName = string.Empty;

            // Parse parameter format: "service/characteristic" or just "characteristic"
            if (parts.Length == 2)
            {
                // Format: service/characteristic
                string serviceName = DeviceLookup.GetIdByNameOrNumber(context.Services, parts[0]);
                charName = parts[1];

                if (string.IsNullOrEmpty(serviceName))
                {
                    _output.WriteLine($"Service '{parts[0]}' not found.");
                    return 1;
                }

                var serviceAttr = context.Services.FirstOrDefault(s => s.Name.Equals(serviceName));
                if (serviceAttr?.service == null)
                {
                    _output.WriteLine("Service not found.");
                    return 1;
                }

                try
                {
                    var accessStatus = await serviceAttr.service.RequestAccessAsync();
                    if (accessStatus == DeviceAccessStatus.Allowed)
                    {
                        var result = await serviceAttr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                        if (result.Status == GattCommunicationStatus.Success)
                        {
                            foreach (var c in result.Characteristics)
                                chars.Add(new BluetoothLEAttributeDisplay(c));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Restricted service. Can't subscribe to characteristics: {ex.Message}");
                    return 1;
                }
            }
            else if (parts.Length == 1)
            {
                // Format: just characteristic (use selected service)
                if (context.SelectedService == null)
                {
                    _output.WriteLine("No service is selected.");
                    return 1;
                }
                chars = new List<BluetoothLEAttributeDisplay>(context.Characteristics);
                charName = parts[0];
            }
            else
            {
                _output.WriteLine("Invalid parameter format. Use: subs <characteristic> or subs <service>/<characteristic>");
                return 1;
            }

            // Find and subscribe to characteristic
            if (chars.Count == 0 || string.IsNullOrEmpty(charName))
            {
                _output.WriteLine("Nothing to subscribe, please specify characteristic name or #.");
                return 1;
            }

            string useName = DeviceLookup.GetIdByNameOrNumber(chars, charName);
            if (string.IsNullOrEmpty(useName))
            {
                _output.WriteLine($"Characteristic '{charName}' not found.");
                return 1;
            }

            var attr = chars.FirstOrDefault(c => c.Name.Equals(useName));
            if (attr?.characteristic == null)
            {
                _output.WriteLine($"Invalid characteristic {useName}");
                return 1;
            }

            // Check if already subscribed
            if (context.Subscribers.Contains(attr.characteristic))
            {
                _output.WriteLine($"Already subscribed to characteristic {useName}");
                return 1;
            }

            // Check if characteristic supports notify or indicate
            var charDisplay = new BluetoothLEAttributeDisplay(attr.characteristic);
            if (!charDisplay.CanNotify && !charDisplay.CanIndicate)
            {
                _output.WriteLine($"Characteristic {useName} does not support notify or indicate");
                return 1;
            }

            try
            {
                // Write CCCD descriptor to enable notifications/indications
                GattCommunicationStatus status;
                if (charDisplay.CanNotify)
                {
                    status = await attr.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                }
                else
                {
                    status = await attr.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                }

                if (status != GattCommunicationStatus.Success)
                {
                    _output.WriteLine($"Can't subscribe to characteristic {useName}");
                    return 1;
                }

                // Add to subscribers list
                context.Subscribers.Add(attr.characteristic);

                // Create and store event handler for proper unsubscription later
                TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (sender, args) =>
                {
                    context.OnValueChanged?.Invoke(sender, args);
                };
                context.ValueChangedHandlers[attr.characteristic] = handler;
                attr.characteristic.ValueChanged += handler;

                if (charDisplay.CanNotify)
                    _output.WriteLine($"Subscribed to characteristic {useName} (notify)");
                else
                    _output.WriteLine($"Subscribed to characteristic {useName} (indicate)");

                return 0;
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error subscribing to characteristic: {ex.Message}");
                return 1;
            }
        }
    }
}
