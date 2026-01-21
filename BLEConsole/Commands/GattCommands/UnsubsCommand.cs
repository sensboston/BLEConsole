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

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// Unsubscribe from characteristic value changes
    /// </summary>
    public class UnsubsCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "unsubs";
        public string[] Aliases => new[] { "unsub" };
        public string Description => "Unsubscribe from value change notifications";
        public string Usage => "unsubs [all] | unsubs <characteristic> | unsubs <service>/<characteristic>";

        public UnsubsCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.Subscribers.Count == 0)
            {
                _output.WriteLine("No active subscriptions.");
                return 0;
            }

            string param = (parameters ?? "").Trim();

            // unsubs (no args) or unsubs all - unsubscribe from all
            if (string.IsNullOrEmpty(param) || param.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                await UnsubscribeAll(context);
                return 0;
            }

            // Parse parameter: "service/characteristic" or just "characteristic"
            var parts = param.Split('/');
            List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();
            string charName = string.Empty;

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
                    _output.WriteLine($"Restricted service. Can't access characteristics: {ex.Message}");
                    return 1;
                }
            }
            else if (parts.Length == 1)
            {
                // Format: just characteristic (use selected service)
                if (context.SelectedService == null)
                {
                    _output.WriteLine("No service is selected. Use 'set' first or specify service/characteristic.");
                    return 1;
                }
                chars = new List<BluetoothLEAttributeDisplay>(context.Characteristics);
                charName = parts[0];
            }
            else
            {
                _output.WriteLine("Invalid parameter format. Use: unsubs <characteristic> or unsubs <service>/<characteristic>");
                return 1;
            }

            // Find characteristic by name or number
            if (chars.Count == 0 || string.IsNullOrEmpty(charName))
            {
                _output.WriteLine("No characteristics available.");
                return 1;
            }

            string useName = DeviceLookup.GetIdByNameOrNumber(chars, charName);
            if (string.IsNullOrEmpty(useName))
            {
                _output.WriteLine($"Characteristic '{charName}' not found.");
                return 1;
            }

            var charAttr = chars.FirstOrDefault(c => c.Name.Equals(useName));
            if (charAttr?.characteristic == null)
            {
                _output.WriteLine($"Invalid characteristic '{useName}'.");
                return 1;
            }

            // Find this characteristic in subscribers list
            var subscriber = context.Subscribers.FirstOrDefault(s => s.Uuid == charAttr.characteristic.Uuid);
            if (subscriber == null)
            {
                _output.WriteLine($"Not subscribed to characteristic '{useName}'.");
                return 0;
            }

            // Unsubscribe
            return await UnsubscribeSingle(context, subscriber, useName);
        }

        private async Task<int> UnsubscribeSingle(BleContext context, GattCharacteristic subscriber, string name)
        {
            try
            {
                await subscriber.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);

                // Remove event handler
                if (context.ValueChangedHandlers.TryGetValue(subscriber, out var handler))
                {
                    subscriber.ValueChanged -= handler;
                    context.ValueChangedHandlers.Remove(subscriber);
                }

                context.Subscribers.Remove(subscriber);
                _output.WriteLine($"Unsubscribed from {name}.");
                return 0;
            }
            catch (Exception ex)
            {
                _output.WriteError($"Failed to unsubscribe from {name}: {ex.Message}");
                return 1;
            }
        }

        private async Task UnsubscribeAll(BleContext context)
        {
            int count = context.Subscribers.Count;
            foreach (var subscriber in context.Subscribers.ToList())
            {
                try
                {
                    await subscriber.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);

                    // Remove event handler
                    if (context.ValueChangedHandlers.TryGetValue(subscriber, out var handler))
                    {
                        subscriber.ValueChanged -= handler;
                        context.ValueChangedHandlers.Remove(subscriber);
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteError($"Failed to unsubscribe from {subscriber.Uuid}: {ex.Message}");
                }
            }
            context.Subscribers.Clear();
            _output.WriteLine($"Unsubscribed from {count} characteristic(s).");
        }
    }
}
