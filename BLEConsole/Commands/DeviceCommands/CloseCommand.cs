using System;
using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.DeviceCommands
{
    /// <summary>
    /// Close connection to currently connected device
    /// </summary>
    public class CloseCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "close";
        public string[] Aliases => new string[] { };
        public string Description => "Disconnect from currently connected device";
        public string Usage => "close";

        public CloseCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            // Remove all subscriptions first
            if (context.Subscribers.Count > 0)
            {
                await UnsubscribeAll(context);
            }

            if (context.SelectedDevice != null)
            {
                _output.WriteLine($"Device {context.SelectedDevice.Name} is disconnected.");

                // Clean up services
                context.Services?.ForEach((s) => { s.service?.Dispose(); });
                context.Services?.Clear();

                // Clean up characteristics
                context.Characteristics?.Clear();

                // Dispose device
                context.SelectedDevice?.Dispose();
                context.SelectedDevice = null;

                // Clear selected service and characteristic
                context.SelectedService = null;
                context.SelectedCharacteristic = null;
            }
            else
            {
                _output.WriteLine("No device is connected.");
            }

            return 0;
        }

        private async Task UnsubscribeAll(BleContext context)
        {
            foreach (var subscriber in context.Subscribers)
            {
                try
                {
                    _output.WriteLine($"Unsubscribe from {subscriber.Uuid}");
                    await subscriber.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);

                    // Remove event handler using stored reference
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
        }
    }
}
