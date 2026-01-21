using System;
using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// Unsubscribe from characteristic value changes
    /// </summary>
    public class UnsubsCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "unsubs";
        public string[] Aliases => new string[] { };
        public string Description => "Unsubscribe from value change notifications";
        public string Usage => "unsubs <characteristic> | unsubs all";

        public UnsubsCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.Subscribers.Count == 0)
            {
                _output.WriteLine("No subscription for value changes found.");
                return 0;
            }

            if (string.IsNullOrWhiteSpace(parameters))
            {
                _output.WriteLine("Please specify characteristic name or # (for single subscription) or type \"unsubs all\" to remove all subscriptions");
                return 1;
            }

            string param = parameters.Trim().Replace("/", "").ToLower();

            // Unsubscribe from all value changed events
            if (param.Equals("all"))
            {
                await UnsubscribeAll(context);
                return 0;
            }
            else
            {
                // Single characteristic unsubscribe not supported yet
                _output.WriteLine("Not supported, please use \"unsubs all\"");
                return 1;
            }
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
