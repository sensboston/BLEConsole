using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BLEConsole.Commands.GattCommands
{
    /// <summary>
    /// NEW FEATURE: Show MTU (Maximum Transmission Unit) size
    /// </summary>
    public class MtuCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "mtu";
        public string[] Aliases => new string[0];
        public string Description => "Show current MTU size";
        public string Usage => "mtu";

        public MtuCommand(IOutputWriter output)
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
                // Get MTU via GattSession
                var session = await GattSession.FromDeviceIdAsync(context.SelectedDevice.BluetoothDeviceId);
                if (session != null)
                {
                    _output.WriteLine($"Current MTU: {session.MaxPduSize} bytes");
                    _output.WriteLine($"Effective payload: {session.MaxPduSize - 3} bytes (MTU - 3 byte header)");
                    
                    session.Dispose();
                    return 0;
                }
                else
                {
                    _output.WriteLine("Unable to get MTU information.");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _output.WriteError($"Error getting MTU: {ex.Message}");
                return 1;
            }
        }
    }
}
