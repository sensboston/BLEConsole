using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Models;
using BLEConsole.Utils;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// NEW FEATURE: Batch read all readable characteristics in a service
    /// </summary>
    public class ReadAllCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "read-all";
        public string[] Aliases => new[] { "ra" };
        public string Description => "Read all characteristics in a service";
        public string Usage => "read-all [service_name]";

        public ReadAllCommand(IOutputWriter output)
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

            // Determine which service to use
            GattDeviceService targetService = null;
            string serviceName = null;

            if (!string.IsNullOrWhiteSpace(parameters))
            {
                // Service specified in parameters
                var serviceDisplay = FindService(context, parameters.Trim());
                if (serviceDisplay == null)
                    return 1;

                targetService = serviceDisplay.service;
                serviceName = serviceDisplay.Name;
            }
            else
            {
                // Use currently selected service
                if (context.SelectedService == null)
                {
                    _output.WriteLine("No service selected. Use 'set <service>' first or specify service name.");
                    return 1;
                }

                targetService = context.SelectedService.service;
                serviceName = context.SelectedService.Name;
            }

            try
            {
                // Get all characteristics for the service
                var charResult = await targetService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);

                if (charResult.Status != GattCommunicationStatus.Success)
                {
                    _output.WriteLine($"Failed to get characteristics: {charResult.Status}");
                    return 1;
                }

                var characteristics = charResult.Characteristics;
                var readableChars = characteristics
                    .Where(c => c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                    .ToList();

                if (readableChars.Count == 0)
                {
                    _output.WriteLine($"No readable characteristics found in service '{serviceName}'.");
                    return 0;
                }

                _output.WriteLine($"Reading {readableChars.Count} characteristic(s) from '{serviceName}':");
                _output.WriteLine(new string('-', 80));

                var results = new List<ReadResult>();
                int successCount = 0;
                int failCount = 0;

                // Read all characteristics
                for (int i = 0; i < readableChars.Count; i++)
                {
                    var characteristic = readableChars[i];
                    var charName = GetCharacteristicName(characteristic);

                    try
                    {
                        var readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                        if (readResult.Status == GattCommunicationStatus.Success)
                        {
                            var data = readResult.Value;
                            string value = DataFormatter.FormatValue(data, context.ReceivedDataFormats[0]);

                            results.Add(new ReadResult
                            {
                                Index = i,
                                Name = charName,
                                Success = true,
                                Value = value,
                                Length = (int)data.Length
                            });
                            successCount++;
                        }
                        else
                        {
                            string error = readResult.ProtocolError.HasValue
                                ? ProtocolErrorFormatter.FormatProtocolError(readResult.ProtocolError)
                                : readResult.Status.ToString();

                            results.Add(new ReadResult
                            {
                                Index = i,
                                Name = charName,
                                Success = false,
                                Error = error
                            });
                            failCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ReadResult
                        {
                            Index = i,
                            Name = charName,
                            Success = false,
                            Error = ex.Message
                        });
                        failCount++;
                    }
                }

                // Display results in table format
                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        _output.WriteLine($"  #{result.Index:00}: {result.Name,-40} [{result.Length,4} bytes] {result.Value}");
                    }
                    else
                    {
                        _output.WriteLine($"  #{result.Index:00}: {result.Name,-40} [ERROR] {result.Error}");
                    }
                }

                _output.WriteLine(new string('-', 80));
                _output.WriteLine($"Summary: {successCount} succeeded, {failCount} failed");

                return failCount > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error reading characteristics: {ex.Message}");
                return 1;
            }
        }

        private BluetoothLEAttributeDisplay FindService(BleContext context, string serviceName)
        {
            var name = DeviceLookup.GetIdByNameOrNumber(context.Services, serviceName);
            if (string.IsNullOrEmpty(name))
                return null;

            var serviceDisplay = context.Services.FirstOrDefault(s => s.Name == name);
            if (serviceDisplay?.service == null)
            {
                _output.WriteLine($"Service '{name}' not found.");
                return null;
            }

            return serviceDisplay;
        }

        private string GetCharacteristicName(GattCharacteristic characteristic)
        {
            if (IsSigDefinedUuid(characteristic.Uuid))
            {
                ushort shortId = UuidConverter.ConvertUuidToShortId(characteristic.Uuid);
                string name = Enum.GetName(typeof(Enums.GattNativeCharacteristicUuid), shortId);
                if (name != null)
                    return name;
                return $"0x{shortId:X4}";
            }
            else
            {
                if (!string.IsNullOrEmpty(characteristic.UserDescription))
                    return characteristic.UserDescription;
                return $"Custom: {characteristic.Uuid}";
            }
        }

        private static bool IsSigDefinedUuid(Guid uuid)
        {
            var bluetoothBaseUuid = new Guid("00000000-0000-1000-8000-00805F9B34FB");
            var bytes = uuid.ToByteArray();
            bytes[0] = 0;
            bytes[1] = 0;
            var baseUuid = new Guid(bytes);
            return baseUuid == bluetoothBaseUuid;
        }

        private class ReadResult
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public bool Success { get; set; }
            public string Value { get; set; }
            public int Length { get; set; }
            public string Error { get; set; }
        }
    }
}
