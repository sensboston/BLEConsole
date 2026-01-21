using System;
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
    /// Set active service for current device
    /// </summary>
    public class SetCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "set";
        public string[] Aliases => new string[] { };
        public string Description => "Set current service for read/write operations";
        public string Usage => "set <service_name_or_#>";

        public SetCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.SelectedDevice == null)
            {
                _output.WriteLine("Nothing to use, no BLE device connected.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(parameters))
            {
                _output.WriteLine("Invalid service name or number");
                return 1;
            }

            string serviceName = parameters.Trim();
            string foundName = DeviceLookup.GetIdByNameOrNumber(context.Services, serviceName);

            if (string.IsNullOrEmpty(foundName))
            {
                _output.WriteLine("Invalid service name or number");
                return 1;
            }

            var serviceAttr = context.Services.FirstOrDefault(s => s.Name.Equals(foundName));
            if (serviceAttr?.service == null)
            {
                _output.WriteLine("Service not found");
                return 1;
            }

            try
            {
                // Ensure we have access to the device
                var accessStatus = await serviceAttr.service.RequestAccessAsync();
                if (accessStatus != DeviceAccessStatus.Allowed)
                {
                    _output.WriteLine("Error accessing service.");
                    return 1;
                }

                // Get all the child characteristics of a service
                var result = await serviceAttr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                if (result.Status != GattCommunicationStatus.Success)
                {
                    _output.WriteLine("Error accessing service.");
                    return 1;
                }

                var characteristics = result.Characteristics;
                context.SelectedService = serviceAttr;
                context.Characteristics.Clear();

                _output.WriteLine($"Selected service {serviceAttr.Name}.");

                if (characteristics.Count == 0)
                {
                    _output.WriteLine("Service doesn't have any characteristics.");
                    return 1;
                }

                // Add characteristics to context and find max name length for formatting
                int maxNameLength = 0;
                for (int i = 0; i < characteristics.Count; i++)
                {
                    var charToDisplay = new BluetoothLEAttributeDisplay(characteristics[i]);
                    context.Characteristics.Add(charToDisplay);
                    maxNameLength = Math.Max(maxNameLength, charToDisplay.Name.Length);
                }

                // Display characteristics with properties
                for (int i = 0; i < context.Characteristics.Count; i++)
                {
                    var charToDisplay = context.Characteristics[i];
                    _output.WriteLine($"#{i:00}: {charToDisplay.Name.PadRight(maxNameLength)}   {charToDisplay.Chars}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Restricted service. Can't read characteristics: {ex.Message}");
                return 1;
            }
        }
    }
}
