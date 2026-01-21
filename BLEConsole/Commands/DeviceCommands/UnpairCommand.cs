using System;
using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Devices.Enumeration;

namespace BLEConsole.Commands.DeviceCommands
{
    /// <summary>
    /// Unpair the currently connected BLE device
    /// </summary>
    public class UnpairCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "unpair";
        public string[] Aliases => new string[] { };
        public string Description => "Unpair the currently connected BLE device";
        public string Usage => "unpair";

        public UnpairCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.SelectedDevice == null)
            {
                _output.WriteLine("Nothing to unpair, no BLE device connected.");
                return 1;
            }

            var pairingInfo = context.SelectedDevice.DeviceInformation.Pairing;

            if (!pairingInfo.IsPaired && !context.IsPaired(context.SelectedDevice))
            {
                _output.WriteLine("Device is not paired.");
                return 0;
            }

            try
            {
                var result = await pairingInfo.UnpairAsync();

                if (result.Status == DeviceUnpairingResultStatus.Unpaired)
                {
                    context.SetPairingStatus(context.SelectedDevice, false);
                    _output.WriteLine("Device unpaired successfully.");
                    return 0;
                }
                else if (result.Status == DeviceUnpairingResultStatus.AlreadyUnpaired)
                {
                    context.SetPairingStatus(context.SelectedDevice, false);
                    _output.WriteLine("Device is already unpaired.");
                    return 0;
                }
                else
                {
                    _output.WriteLine($"Unable to unpair device: {result.Status}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _output.WriteError($"Unpair error: {ex.Message}");
                return 1;
            }
        }
    }
}
