using System;
using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Devices.Enumeration;
using Windows.Security.Credentials;

namespace BLEConsole.Commands.DeviceCommands
{
    /// <summary>
    /// Pair the currently connected BLE device
    /// </summary>
    public class PairCommand : ICommand
    {
        private readonly IOutputWriter _output;
        private string _pairingPin;
        private PasswordCredential _pairingCredential;

        public string Name => "pair";
        public string[] Aliases => new string[] { };
        public string Description => "Pair the currently connected BLE device";
        public string Usage => "pair [pin <code>] | [mode=ProvidePin <code>] | [ConfirmOnly] | [DisplayPin] | [ConfirmPinMatch]";

        public PairCommand(IOutputWriter output)
        {
            _output = output;
        }

        public async Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (context.SelectedDevice == null)
            {
                _output.WriteLine("Nothing to pair, no BLE device connected.");
                return 1;
            }

            var pairingInfo = context.SelectedDevice.DeviceInformation.Pairing;

            if (!pairingInfo.CanPair)
            {
                _output.WriteLine("Device does not support pairing.");
                return 1;
            }

            if (pairingInfo.IsPaired || context.IsPaired(context.SelectedDevice))
            {
                _output.WriteLine("Device is already paired.");
                return 0;
            }

            // Parse parameters - support both "pin 123456" and "mode=ProvidePin 123456" syntax
            DevicePairingKinds? pairingKind = null;
            var paramStr = (parameters ?? "").Trim();
            var parts = paramStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Handle mode=<Mode> syntax
            if (parts.Length >= 1 && parts[0].StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
            {
                var mode = parts[0].Substring(5); // Remove "mode=" prefix
                if (mode.Equals("ProvidePin", StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Length >= 2)
                    {
                        _pairingPin = parts[1];
                        pairingKind = DevicePairingKinds.ProvidePin;
                    }
                    else
                    {
                        _output.WriteLine("PIN code required for ProvidePin mode. Usage: pair mode=ProvidePin <code>");
                        return 1;
                    }
                }
                else if (mode.Equals("ConfirmOnly", StringComparison.OrdinalIgnoreCase))
                {
                    pairingKind = DevicePairingKinds.ConfirmOnly;
                }
                else if (mode.Equals("DisplayPin", StringComparison.OrdinalIgnoreCase))
                {
                    pairingKind = DevicePairingKinds.DisplayPin;
                }
                else if (mode.Equals("ConfirmPinMatch", StringComparison.OrdinalIgnoreCase))
                {
                    pairingKind = DevicePairingKinds.ConfirmPinMatch;
                }
                else
                {
                    _output.WriteLine($"Unknown pairing mode: {mode}");
                    _output.WriteLine("Valid modes: ProvidePin, ConfirmOnly, DisplayPin, ConfirmPinMatch");
                    return 1;
                }
            }
            // Handle legacy "pin 123456" or "ProvidePin 123456" syntax
            else if (parts.Length >= 2 && (parts[0].Equals("pin", StringComparison.OrdinalIgnoreCase) ||
                                       parts[0].Equals("ProvidePin", StringComparison.OrdinalIgnoreCase)))
            {
                _pairingPin = parts[1];
                pairingKind = DevicePairingKinds.ProvidePin;
            }
            else if (parts.Length >= 1 && parts[0].Equals("ConfirmOnly", StringComparison.OrdinalIgnoreCase))
            {
                pairingKind = DevicePairingKinds.ConfirmOnly;
            }
            else if (parts.Length >= 1 && parts[0].Equals("ConfirmPinMatch", StringComparison.OrdinalIgnoreCase))
            {
                pairingKind = DevicePairingKinds.ConfirmPinMatch;
            }
            else if (parts.Length >= 1 && parts[0].Equals("DisplayPin", StringComparison.OrdinalIgnoreCase))
            {
                pairingKind = DevicePairingKinds.DisplayPin;
            }
            else if (parts.Length >= 3 && parts[0].Equals("ProvidePasswordCredential", StringComparison.OrdinalIgnoreCase))
            {
                _pairingCredential = new PasswordCredential
                {
                    UserName = parts[1],
                    Password = parts[2]
                };
                pairingKind = DevicePairingKinds.ProvidePasswordCredential;
            }

            try
            {
                DevicePairingResult result;

                if (pairingKind != null)
                {
                    pairingInfo.Custom.PairingRequested += OnPairingRequested;
                    result = await pairingInfo.Custom.PairAsync(pairingKind.Value);
                    pairingInfo.Custom.PairingRequested -= OnPairingRequested;
                    _pairingPin = null;
                    _pairingCredential = null;
                }
                else
                {
                    // Simple pairing
                    result = await pairingInfo.PairAsync();
                }

                if (result.Status == DevicePairingResultStatus.Paired)
                {
                    context.SetPairingStatus(context.SelectedDevice, true);
                    _output.WriteLine("Pairing successful.");
                    return 0;
                }
                else if (result.Status == DevicePairingResultStatus.AlreadyPaired)
                {
                    context.SetPairingStatus(context.SelectedDevice, true);
                    _output.WriteLine("Device is already paired.");
                    return 0;
                }
                else
                {
                    _output.WriteLine($"Pairing failed: {result.Status}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                _output.WriteError($"Pairing error: {ex.Message}");
                return 1;
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
                    _output.WriteLine($"Device PIN: {args.Pin}");
                    args.Accept();
                    break;

                case DevicePairingKinds.ConfirmPinMatch:
                    _output.WriteLine($"Confirm PIN: {args.Pin}");
                    args.Accept();
                    break;

                case DevicePairingKinds.ProvidePasswordCredential:
                    if (_pairingCredential != null)
                        args.AcceptWithPasswordCredential(_pairingCredential);
                    else
                        args.Accept();
                    break;

                default:
                    args.Accept();
                    break;
            }
        }
    }
}
