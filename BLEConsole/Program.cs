using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Credentials;
using BLEConsole.Models;
using BLEConsole.Enums;
using BLEConsole.Core;
using BLEConsole.Commands;
using BLEConsole.Commands.UtilityCommands;
using BLEConsole.Commands.DeviceCommands;
using BLEConsole.Commands.GattCommands;
using BLEConsole.Commands.ConfigCommands;

namespace BLEConsole
{
    class Program
    {
        static bool _doWork = true;
        static string CLRF = (Console.IsOutputRedirected) ? "" : "\r\n";

        // "Magic" string for all BLE devices
        static string _aqsAllBLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
        static string[] _requestedBLEProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.Bluetooth.Le.IsConnectable", };

        static List<DeviceInformation> _deviceList = new List<DeviceInformation>();
        static BluetoothLEDevice _selectedDevice = null;

        static List<BluetoothLEAttributeDisplay> _services = new List<BluetoothLEAttributeDisplay>();
        static BluetoothLEAttributeDisplay _selectedService = null;

        static List<BluetoothLEAttributeDisplay> _characteristics = new List<BluetoothLEAttributeDisplay>();
        static BluetoothLEAttributeDisplay _selectedCharacteristic = null;

        // Only one registered characteristic at a time.
        static List<GattCharacteristic> _subscribers = new List<GattCharacteristic>();

        // Pairing information and pin
        static readonly Dictionary<string, bool> _pairings = new Dictionary<string, bool>();
        static string _pair_pin = null;
        private static PasswordCredential _pair_pass_cred;

        // Current received data format
        static DataFormat _sendDataFormat = DataFormat.UTF8;

        // Current send data format
        static List<DataFormat> _receivedDataFormat = new List<DataFormat> { DataFormat.UTF8, DataFormat.Hex };

        static string _versionInfo;

        // Variables for "foreach" loop implementation
        static List<string> _forEachCommands = new List<string>();
        static List<string> _forEachDeviceNames = new List<string>();
        static int _forEachCmdCounter = 0;
        static int _forEachDeviceCounter = 0;
        static bool _forEachCollection = false;
        static bool _forEachExecution = false;
        static string _forEachDeviceMask = "";
        static int _inIfBlock = 0;
        static bool _failedConditional = false;
        static bool _closingIfBlock = false;
        static int _exitCode = 0;
        static ManualResetEvent _notifyCompleteEvent = null;
        static ManualResetEvent _delayEvent = null;

        static TimeSpan _timeout = TimeSpan.FromSeconds(3);

        // Command Pattern infrastructure
        static BleContext _context;
        static CommandRegistry _commandRegistry;
        static IOutputWriter _output;

        static void Main(string[] args)
        {
            // Get app name and version
            var name = Assembly.GetCallingAssembly().GetName();
            _versionInfo = string.Format($"{name.Name} ver. {name.Version.Major:0}.{name.Version.Minor:0}.{name.Version.Build:0}\n");
            if (!Console.IsInputRedirected) Console.WriteLine(_versionInfo);

            // Set Ctrl+Break/Ctrl+C handler
            Console.CancelKeyPress += Console_CancelKeyPress;

            // Run main loop
            MainAsync(args).Wait();

            // Return exit code to the shell
            // For scripting/batch processing, it's an ERRORLEVEL cmd.exe shell variable
            Environment.Exit(_exitCode);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            // If we're waiting for async results, let's abandon the wait
            if (_notifyCompleteEvent != null)
            {
                _notifyCompleteEvent.Set();
                _notifyCompleteEvent = null;
                e.Cancel = true;
            }
            // If we're waiting for "delay" command, let's abandon the wait
            else if (_delayEvent != null)
            {
                _delayEvent.Set();
                _delayEvent = null;
                e.Cancel = true;
            }
            // Otherwise, quit the app
            else
            {
                if (!Console.IsInputRedirected)
                    Console.WriteLine("\nBLEConsole is terminated");
                e.Cancel = false;
                _doWork = false;
            }
        }

        /// <summary>
        /// Register all Command Pattern commands
        /// </summary>
        static void RegisterCommands()
        {
            _output = new ConsoleOutputWriter();
            _commandRegistry = new CommandRegistry(_output);

            // Utility commands - HelpCommand needs registry to show all commands
            _commandRegistry.Register(new HelpCommand(_commandRegistry, _output));
            _commandRegistry.Register(new QuitCommand(_output));

            // Device commands
            _commandRegistry.Register(new ListCommand(_output));
            _commandRegistry.Register(new OpenCommand(_output));
            _commandRegistry.Register(new CloseCommand(_output));
            _commandRegistry.Register(new StatCommand(_output));
            _commandRegistry.Register(new DeviceInfoCommand(_output));

            // GATT commands
            _commandRegistry.Register(new SetCommand(_output));
            _commandRegistry.Register(new ReadCommand(_output));
            _commandRegistry.Register(new ReadAllCommand(_output));
            _commandRegistry.Register(new WriteCommand(_output));
            _commandRegistry.Register(new DescCommand(_output));
            _commandRegistry.Register(new ReadDescCommand(_output));
            _commandRegistry.Register(new WriteDescCommand(_output));
            _commandRegistry.Register(new SubsCommand(_output));
            _commandRegistry.Register(new UnsubsCommand(_output));
            _commandRegistry.Register(new MtuCommand(_output));

            // Config commands
            _commandRegistry.Register(new FormatCommand(_output));
            _commandRegistry.Register(new EndianCommand(_output));
        }

        /// <summary>
        /// Sync state from static variables to BleContext
        /// </summary>
        static void SyncToContext()
        {
            _context.SelectedDevice = _selectedDevice;
            _context.SelectedService = _selectedService;
            _context.SelectedCharacteristic = _selectedCharacteristic;
            _context.Timeout = _timeout;
            _context.SendDataFormat = _sendDataFormat;

            // Sync collections
            _context.Services.Clear();
            _context.Services.AddRange(_services);

            _context.Characteristics.Clear();
            _context.Characteristics.AddRange(_characteristics);

            _context.Subscribers.Clear();
            _context.Subscribers.AddRange(_subscribers);

            _context.ReceivedDataFormats.Clear();
            _context.ReceivedDataFormats.AddRange(_receivedDataFormat);
        }

        /// <summary>
        /// Sync state from BleContext back to static variables
        /// </summary>
        static void SyncFromContext()
        {
            _selectedDevice = _context.SelectedDevice;
            _selectedService = _context.SelectedService;
            _selectedCharacteristic = _context.SelectedCharacteristic;
            _timeout = _context.Timeout;
            _sendDataFormat = _context.SendDataFormat;

            // Sync collections
            _services.Clear();
            _services.AddRange(_context.Services);

            _characteristics.Clear();
            _characteristics.AddRange(_context.Characteristics);

            _subscribers.Clear();
            _subscribers.AddRange(_context.Subscribers);

            _receivedDataFormat.Clear();
            _receivedDataFormat.AddRange(_context.ReceivedDataFormats);
        }

        static async Task MainAsync(string[] args)
        {
            // Initialize Command Pattern infrastructure
            _context = new BleContext();
            _context.Timeout = _timeout;
            _context.SendDataFormat = _sendDataFormat;
            _context.ReceivedDataFormats.Clear();
            _context.ReceivedDataFormats.AddRange(_receivedDataFormat);

            // Set up callback for subscription notifications from Command Pattern
            _context.OnValueChanged = (sender, eventArgs) => Characteristic_ValueChanged(sender, eventArgs);

            RegisterCommands();

            // Start endless BLE device watcher
            var watcher = DeviceInformation.CreateWatcher(_aqsAllBLEDevices, _requestedBLEProperties, DeviceInformationKind.AssociationEndpoint);
            watcher.Added += (DeviceWatcher sender, DeviceInformation devInfo) =>
            {
                if (_deviceList.FirstOrDefault(d => d.Id.Equals(devInfo.Id) || d.Name.Equals(devInfo.Name)) == null)
                {
                    _deviceList.Add(devInfo);
                    _context.DiscoveredDevices.Add(devInfo);
                }
            };
            watcher.Updated += (DeviceWatcher sender, DeviceInformationUpdate diu) =>
            {
                _deviceList.FirstOrDefault(d => d.Id.Equals(diu.Id))?.Update(diu);
                _context.DiscoveredDevices.FirstOrDefault(d => d.Id.Equals(diu.Id))?.Update(diu);
            };

            //Watch for a device being removed by the watcher
            //watcher.Removed += (DeviceWatcher sender, DeviceInformationUpdate devInfo) =>
            //{
            //    _deviceList.Remove(FindKnownDevice(devInfo.Id));
            //};
            watcher.EnumerationCompleted += (DeviceWatcher sender, object arg) => { sender.Stop(); };
            watcher.Stopped += (DeviceWatcher sender, object arg) =>
            {
                _deviceList.Clear();
                _context.DiscoveredDevices.Clear();
                sender.Start();
            };
            watcher.Start();

            string cmd = string.Empty;
            bool skipPrompt = false;

            // Main loop
            while (_doWork)
            {
                if (!Console.IsInputRedirected && !skipPrompt)
                    Console.Write("BLE: ");

                skipPrompt = false;

                try
                {
                    var userInput = string.Empty;

                    // If we're inside "foreach" loop, process saved commands
                    if (_forEachExecution)
                    {
                        userInput = _forEachCommands[_forEachCmdCounter];
                        if (_forEachCmdCounter++ >= _forEachCommands.Count - 1)
                        {
                            _forEachCmdCounter = 0;
                            if (_forEachDeviceCounter++ > _forEachDeviceNames.Count - 1)
                            {
                                _forEachExecution = false;
                                _forEachCommands.Clear();
                                userInput = string.Empty;
                                skipPrompt = true;
                            }
                        }
                    }
                    // Otherwise read the stdin
                    else userInput = Console.ReadLine();

                    // Check if we are processing script file
                    if (Console.IsInputRedirected)
                    {
                        if (userInput == null)
                        {
                            //End of file, quit processing
                            _doWork = false;
                        }
                        else if (userInput.TrimStart().StartsWith("//"))
                        {
                            //Ignore input if commented (//) line
                            userInput = string.Empty;
                        }
                    }
                    else
                    {
                        //Sanitize user typed input
                        userInput = userInput?.TrimStart(new char[] { ' ', '\t' });
                    }

                    if (!string.IsNullOrEmpty(userInput))
                    {
                        string[] strs = userInput.Split(' ');
                        cmd = strs.First().ToLower();
                        string parameters = string.Join(" ", strs.Skip(1));

                        if (_forEachCollection && !cmd.Equals("endfor"))
                        {
                            _forEachCommands.Add(userInput);
                        }
                        if (cmd == "endif" || cmd == "elif" || cmd == "else")
                            _closingIfBlock = false;
                        else
                        {
                            if ((_inIfBlock > 0 && !_closingIfBlock) || _inIfBlock == 0)
                            {
                                await HandleSwitch(cmd, parameters);
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    Console.WriteLine(error.Message);
                }

                // We should wait for a little after writing
                // (in case we do have a notification event but don't wanna to wait by using command "wait")
                if (cmd.Equals("write") || cmd.Equals("w"))
                    Thread.Sleep(200);
            }
            watcher.Stop();
        }



        static async Task HandleSwitch(string cmd, string parameters)
        {
            // Try Command Pattern registry first
            if (_commandRegistry != null)
            {
                // Sync state from static vars to context
                SyncToContext();

                // Try to execute command via registry
                if (_commandRegistry.HasCommand(cmd))
                {
                    _exitCode = await _commandRegistry.ExecuteAsync(cmd, _context, parameters);

                    // Sync state back from context to static vars
                    SyncFromContext();

                    // Check for quit command (returns -1)
                    if (_exitCode == -1)
                    {
                        _doWork = false;
                        _exitCode = 0;
                    }

                    return;
                }
            }

            // Fall back to legacy switch statement for commands not yet migrated
            switch (cmd)
            {
                case "if":
                    _inIfBlock++;
                    _exitCode = 0;
                    if (parameters != "")
                    {
                        string[] str = parameters.Split(' ');
                        await HandleSwitch(str[0], str.Skip(1).Aggregate((i, j) => i + " " + j));
                        _closingIfBlock = (_exitCode > 0);
                        _failedConditional = _closingIfBlock;
                    }
                    break;

                case "elif":
                    if (_failedConditional)
                    {
                        _exitCode = 0;
                        if (parameters != "")
                        {
                            string[] str = parameters.Split(' ');
                            await HandleSwitch(str[0], str.Skip(1).Aggregate((i, j) => i + " " + j));
                            _closingIfBlock = (_exitCode > 0);
                            _failedConditional = _closingIfBlock;
                        }
                    }
                    else
                        _closingIfBlock = true;
                    break;

                case "else":
                    if (_failedConditional)
                    {
                        _exitCode = 0;
                        if (parameters != "")
                        {
                            string[] str = parameters.Split(' ');
                            await HandleSwitch(str[0], str.Skip(1).Aggregate((i, j) => i + " " + j));
                        }
                    }
                    else
                        _closingIfBlock = true;
                    break;

                case "endif":
                    if (_inIfBlock > 0)
                        _inIfBlock--;
                    _failedConditional = false;
                    break;

                case "foreach":
                    _forEachCollection = true;
                    _forEachDeviceMask = parameters.ToLower();
                    break;

                case "endfor":
                    if (string.IsNullOrEmpty(_forEachDeviceMask))
                        _forEachDeviceNames = _deviceList.OrderBy(d => d.Name).Where(d => !string.IsNullOrEmpty(d.Name)).Select(d => d.Name).ToList();
                    else
                        _forEachDeviceNames = _deviceList.OrderBy(d => d.Name).Where(d => d.Name.ToLower().StartsWith(_forEachDeviceMask)).Select(d => d.Name).ToList();
                    _forEachDeviceCounter = 0;
                    _forEachCmdCounter = 0;
                    _forEachCollection = false;
                    _forEachExecution = (_forEachCommands.Count > 0);
                    break;

                case "exit":
                case "q":
                case "quit":
                    _doWork = false;
                    break;

                case "cls":
                case "clr":
                case "clear":
                    Console.Clear();
                    break;

                case "?":
                case "help":
                    Help();
                    break;

                case "st":
                case "stat":
                    ShowStatus();
                    break;

                case "p":
                case "print":
                    if (_forEachExecution && _forEachDeviceCounter > 0)
                        parameters = parameters.Replace("$", _forEachDeviceNames[_forEachDeviceCounter - 1]);

                    _exitCode += PrintInformation(parameters);
                    break;

                case "ls":
                case "list":
                    ListDevices(parameters);
                    break;

                case "open":
                    if (_forEachExecution && _forEachDeviceCounter > 0)
                        parameters = parameters.Replace("$", _forEachDeviceNames[_forEachDeviceCounter - 1]);

                    _exitCode += await OpenDevice(parameters);
                    break;

                case "timeout":
                    ChangeTimeout(parameters);
                    break;

                case "delay":
                    Delay(parameters);
                    break;

                case "close":
                    CloseDevice();
                    break;

                case "fmt":
                case "format":
                    ChangeSendDataFormat(parameters);
                    ChangeReceivedDataFormat(parameters);
                    break;

                case "fmts":
                case "format_send":
                    ChangeSendDataFormat(parameters);
                    break;

                case "fmtr":
                case "format_receive":
                    ChangeReceivedDataFormat(parameters);
                    break;

                case "set":
                    _exitCode += await SetService(parameters);
                    break;

                case "r":
                case "read":
                    _exitCode += await ReadCharacteristic(parameters);
                    break;

                case "wait":
                    _notifyCompleteEvent = new ManualResetEvent(false);
                    _notifyCompleteEvent.WaitOne(_timeout);
                    _notifyCompleteEvent = null;
                    break;

                case "w":
                case "write":
                    _exitCode += await WriteCharacteristic(parameters);
                    break;

                case "subs":
                case "sub":
                    _exitCode += await SubscribeToCharacteristic(parameters);
                    break;

                case "unsub":
                case "unsubs":
                    Unsubscribe(parameters);
                    break;

                //experimental pairing function 
                case "pair":
                    _exitCode += await PairBluetooth(parameters);
                    break;

                case "unpair":
                    _exitCode += await UnPairBluetooth(parameters);
                    break;

                default:
                    Console.WriteLine("Unknown command. Type \"?\" for help.");
                    break;
            }
        }

        /// <summary>
        /// Displays app version and available commands
        /// </summary>
        static void Help()
        {
            Console.WriteLine(_versionInfo +
                "\n  help, ?\t\t\t: show help information\n" +
                "  quit, q\t\t\t: quit from application\n" +
                "  list, ls [w]\t\t\t: show available BLE devices\n" +
                "  open <name>, <#> or <address>\t: connect to BLE device\n" +
                "  delay <msec>\t\t\t: pause execution for a certain number of milliseconds\n" +
                "  timeout <sec>\t\t\t: show/change connection timeout, default value is 3 sec\n" +
                "  close\t\t\t\t: disconnect from currently connected device\n" +
                "  stat, st\t\t\t: shows current BLE device status\n" +
                "  print, p <text&vars>*\t\t: prints text and variables to stdout, where are variables are\n" +
                "  \t\t\t\t: %id - BlueTooth device ID\n" +
                "  \t\t\t\t: %addr - device BT address\n" +
                "  \t\t\t\t: %mac - device MAC address\n" +
                "  \t\t\t\t: %name - device BlueTooth name\n" +
                "  \t\t\t\t: %stat - device connection status\n" +
                "  \t\t\t\t: %NOW, %now, %HH, %hh, %mm, %ss, %D, %d, %T, %t, %z - date/time variables\n" +
                "  format [data_format], fmt\t: show/change display format for received and sent data, can be ASCII/UTF8/Dec/Hex/Bin\n" +
                "  format_send [data_format],\n  fmts\t\t\t\t: show/change data format for sending data, can be ASCII/UTF8/Dec/Hex/Bin\n" +
                "  format_rec [data_format,...],\n  fmtr\t\t\t\t: show/change display format for received data, comma separated list of type ASCII/UTF8/Dec/Hex/Bin\n" +
                "  set <service_name> or <#>\t: set current service (for read/write operations)\n" +
                "  read, r <name>**\t\t: read value from specific characteristic\n" +
                "  write, w <name>**<value>\t: write value to specific characteristic\n" +
                "  pair [<mode> [<params>]]\t: pair the currently connected BLE device\n" +
                "  \t\t\t\t: no mode               just pair\n" +
                "  \t\t\t\t: mode=ProvidePin <pin> pair with supplied pin \n" +
                "  \t\t\t\t: mode=ConfirmOnly      pair and confirm\n" +
                "  \t\t\t\t: mode=ConfirmPinMatch  pair and confirm that pin matches\n" +
                "  \t\t\t\t: mode=DisplayPin       pair and confirm that displayed pin matches\n" +
                "  \t\t\t\t: mode=ProvidePasswordCredential <username> <password>\n" +
                "  \t\t\t\t                        pair with supplied username and password\n" +
                "  unpair \t\t\t: unpair currently connected BLE device\n" +
                "  subs <name>**\t\t\t: subscribe to value change for specific characteristic\n" +
                "  unsubs <name>** [all]\t\t: unsubscribe from value change for specific characteristic or unsubs all for all\n" +
                "  wait\t\t\t\t: wait for notification event on value change (you must be subscribed, see above)\n" +
                "  foreach [device_mask]\t\t: starts devices enumerating loop\n" +
                "  endfor\t\t\t: end foreach loop\n" +
                "  if <cmd> <params>\t\t: start conditional block dependent on function returning w\\o error\n" +
                "    elif\t\t\t: another conditionals block\n" +
                "    else\t\t\t: if condition == false block\n" +
                "  endif\t\t\t\t: end conditional block\n\n" +
                "   * You can also use standard C language string formating characters like \\t, \\n etc. \n" +
                "  ** <name> could be \"service/characteristic\", or just a char name or # (for selected service)\n\n" +
                "  For additional information and examples please visit https://github.com/sensboston/BLEConsole \n"
                );
        }

        static int PrintInformation(string param)
        {
            // First, we need to check output string for variables
            string[] btVars = { "%mac", "%addr", "%name", "%stat", "%id" };
            bool hasBTVars = btVars.Any(param.Contains);

            int retVal = 0;
            if (_selectedDevice == null && hasBTVars)
            {
                retVal += 1;
            }
            else
            {
                if ((_selectedDevice != null && _selectedDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected) && hasBTVars)
                {
                    retVal += 1;
                }
                else
                {
                    param = param.Replace("%NOW", DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + " GMT " + DateTime.Now.ToLocalTime().ToString("%K"))
                                 .Replace("%now", DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString())
                                 .Replace("%HH", DateTime.Now.ToString("HH"))
                                 .Replace("%hh", DateTime.Now.ToString("hh"))
                                 .Replace("%mm", DateTime.Now.ToString("mm"))
                                 .Replace("%ss", DateTime.Now.ToString("ss"))
                                 .Replace("%D", DateTime.Now.ToLongDateString())
                                 .Replace("%d", DateTime.Now.ToShortDateString())
                                 .Replace("%T", DateTime.Now.ToLongTimeString() + " GMT " + DateTime.Now.ToLocalTime().ToString("%K"))
                                 .Replace("%t", DateTime.Now.ToShortTimeString())
                                 .Replace("%z", "GMT " + DateTime.Now.ToLocalTime().ToString("%K"))
                                 .Replace("\\t", "\t")
                                 .Replace("\\n", "\n")
                                 .Replace("\\r", "\r");

                    if (hasBTVars)
                    {
                        // This is more elegant way to get readable MAC address
                        var macAddress = Regex.Replace(_selectedDevice.BluetoothAddress.ToString("X"), @"(.{2})", "$1:").TrimEnd(':');

                        param = param.Replace("%mac", macAddress)
                                     .Replace("%addr", _selectedDevice.BluetoothAddress.ToString())
                                     .Replace("%name", _selectedDevice.Name)
                                     .Replace("%id", _selectedDevice.DeviceId)
                                     .Replace("%stat", (_selectedDevice.ConnectionStatus == BluetoothConnectionStatus.Connected).ToString());
                        //.Replace("%c", );
                    }
                    Console.Write(param + CLRF);
                }
            }

            return retVal;
        }

        //
        // Seems like the pairing information are not updated in
        // BluetoothLEDevice.DeviceInformation.Pairing
        // once you pair or unpair
        // So we need to keep track of it ourselves
        //        
        static bool IsPaired(BluetoothLEDevice device) =>
            _pairings.ContainsKey(_selectedDevice.DeviceId)
                ? _pairings[_selectedDevice.DeviceId]
                : device.DeviceInformation.Pairing.IsPaired;

        static void SetPairingCache(BluetoothLEDevice device, bool isPaired) =>
            _pairings[device.DeviceId] = isPaired;

        static async Task<int> UnPairBluetooth(string param)
        {
            if (_selectedDevice == null)
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.WriteLine("Nothing to unpair, no BLE device connected.");
                }
                return 1;
            }

            if (IsPaired(_selectedDevice))
            {
                var dur = await _selectedDevice.DeviceInformation.Pairing.UnpairAsync();
                if (dur.Status == DeviceUnpairingResultStatus.Unpaired)
                {
                    Console.WriteLine("Unpaired device");
                    SetPairingCache(_selectedDevice, false);
                }
                else
                {
                    Console.WriteLine($"Unable to unpair device:{dur.Status}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("Device is NOT paired");
            }
            return 0;
        }

        static async Task<int> PairBluetooth(string param)
        {
            if (_selectedDevice == null)
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.WriteLine("Nothing to pair, no BLE device connected.");
                }
                return 1;
            }

            if (IsPaired(_selectedDevice))
            {
                Console.WriteLine("Device is already paired");
                return 0;
            }
            if (!_selectedDevice.DeviceInformation.Pairing.CanPair)
            {
                Console.WriteLine("Device cannot be paired");
                return 1;
            }

            var pms = param.Split(' ');
            DevicePairingKinds? dpk = null;

            // pair pin 123456
            if (pms.Length == 2 && (pms[0] == "pin" || pms[0].Equals("ProvidePin", StringComparison.OrdinalIgnoreCase)))
            {
                _pair_pin = pms[1];
                dpk = DevicePairingKinds.ProvidePin;
            }
            // pair ConfirmOnly
            else if (pms.Length == 1 && pms[0].Equals("ConfirmOnly", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Not tested!
                dpk = DevicePairingKinds.ConfirmOnly;
            }
            // pair ConfirmPinMatch
            else if (pms.Length == 1 && pms[0].Equals("ConfirmPinMatch", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Not tested!
                dpk = DevicePairingKinds.ConfirmPinMatch;
            }
            // pair DisplayPin
            else if (pms.Length == 1 && pms[0].Equals("DisplayPin", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Not tested!
                dpk = DevicePairingKinds.DisplayPin;
            }
            // pair ProvidePasswordCredential user pass
            else if (pms.Length == 3 && pms[0].Equals("ProvidePasswordCredential", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Not tested!
                _pair_pass_cred = new PasswordCredential()
                {
                    UserName = pms[1],
                    Password = pms[2],
                };
                dpk = DevicePairingKinds.ProvidePasswordCredential;
            }
            else if (pms.Length == 0)
            {
                // just plain pairing
            }
            else
            {
                Console.WriteLine("Invalid parameters.  Please see see the help");
                return 1;
            }

            DevicePairingResult dur;
            if (dpk != null)
            {
                _selectedDevice.DeviceInformation.Pairing.Custom.PairingRequested += Custom_PairingRequested;
                dur = await _selectedDevice.DeviceInformation.Pairing.Custom.PairAsync((DevicePairingKinds)dpk);
                _pair_pin = null;
                _pair_pass_cred = null;
                _selectedDevice.DeviceInformation.Pairing.Custom.PairingRequested -= Custom_PairingRequested;
            }
            else
            {
                dur = await _selectedDevice.DeviceInformation.Pairing.PairAsync();
            }

            if (dur.Status == DevicePairingResultStatus.Paired)
            {
                SetPairingCache(_selectedDevice, true);
                Console.WriteLine("Paired device");
            }
            else
            {
                SetPairingCache(_selectedDevice, false);
                Console.WriteLine($"Unable to pair device:{dur.Status}");
            }

            return dur.Status == DevicePairingResultStatus.Paired ? 0 : 1;
        }

        private static void Custom_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            if (_pair_pass_cred != null)
            {
                args.AcceptWithPasswordCredential(_pair_pass_cred);
            }
            else if (_pair_pin != null)
            {
                args.Accept(_pair_pin);
            }
            else
            {
                args.Accept();
            }
        }

        static void ChangeSendDataFormat(string param)
        {
            if (!string.IsNullOrEmpty(param))
            {
                switch (param.ToLower())
                {
                    case "ascii":
                        _sendDataFormat = DataFormat.ASCII;
                        break;
                    case "utf8":
                        _sendDataFormat = DataFormat.UTF8;
                        break;
                    case "dec":
                    case "decimal":
                        _sendDataFormat = DataFormat.Dec;
                        break;
                    case "bin":
                    case "binary":
                        _sendDataFormat = DataFormat.Bin;
                        break;
                    case "hex":
                    case "hexdecimal":
                        _sendDataFormat = DataFormat.Hex;
                        break;
                    default:
                        break;
                }
            }
            Console.WriteLine($"Current send data format: {_sendDataFormat.ToString()}");
        }

        static void ChangeReceivedDataFormat(string param)
        {
            if (!string.IsNullOrEmpty(param))
            {
                _receivedDataFormat.Clear();
                var sendDataFormatSplit = param.ToLower().Replace(" ", "").Split(',');
                for (int dataFormat = 0; dataFormat < sendDataFormatSplit.Length; dataFormat++)
                {
                    String sendDataFormat = sendDataFormatSplit[dataFormat].ToLower();

                    switch (sendDataFormat)
                    {
                        case "ascii":
                            _receivedDataFormat.Add(DataFormat.ASCII);
                            break;
                        case "utf8":
                            _receivedDataFormat.Add(DataFormat.UTF8);
                            break;
                        case "dec":
                        case "decimal":
                            _receivedDataFormat.Add(DataFormat.Dec);
                            break;
                        case "bin":
                        case "binary":
                            _receivedDataFormat.Add(DataFormat.Bin);
                            break;
                        case "hex":
                        case "hexadecimal":
                            _receivedDataFormat.Add(DataFormat.Hex);
                            break;
                        default:
                            break;
                    }
                }
            }
            Console.Write($"Current received data format: ");
            for (int dataFormat = 0; dataFormat < _receivedDataFormat.Count; dataFormat++)
            {
                if (dataFormat == _receivedDataFormat.Count - 1)
                {
                    Console.WriteLine($"{_receivedDataFormat[dataFormat]}");
                }
                else
                {
                    Console.Write($"{_receivedDataFormat[dataFormat]}, ");
                }
            }
        }

        static void Delay(string param)
        {
            uint milliseconds = (uint)_timeout.TotalMilliseconds;
            uint.TryParse(param, out milliseconds);
            _delayEvent = new ManualResetEvent(false);
            _delayEvent.WaitOne((int)milliseconds, true);
            _delayEvent = null;
        }

        static void ChangeTimeout(string param)
        {
            if (!string.IsNullOrEmpty(param))
            {
                uint t;
                if (uint.TryParse(param, out t))
                {
                    if (t > 0 && t < 60)
                    {
                        _timeout = TimeSpan.FromSeconds(t);
                    }
                }
            }
            Console.WriteLine($"Device connection timeout (sec): {_timeout.TotalSeconds}");
        }

        /// <summary>
        /// List of available BLE devices
        /// </summary>
        /// <param name="param">optional, 'w' means "wide list"</param>
        static void ListDevices(string param)
        {
            var orderedDevices = _deviceList.OrderBy(d => d.Name);
            const int bdAddressLength = 17;
            String noAdvertisingName = "no_advertising_name";


            if (string.IsNullOrEmpty(param))
            {
                var orderedDevicesList = orderedDevices.ToList();
                string deviceName = "";
                string deviceId = "";

                Console.WriteLine("#    Address           Name");
                for (int i = 0; i < orderedDevicesList.Count(); i++)
                {
                    deviceName = orderedDevicesList[i].Name ?? "";
                    deviceName = deviceName == "" ? noAdvertisingName : deviceName;
                    deviceId = orderedDevicesList[i].Id ?? "";
                    deviceId = deviceId.Substring(Math.Max(0, deviceId.Length - bdAddressLength));
                    Console.WriteLine($"#{i:00}: {deviceId} {deviceName}");
                }
            }
            else if (param.Replace("/", "").ToLower().Equals("w"))
            {
                var names = orderedDevices.Select(d => d.Name == "" ? noAdvertisingName : d.Name).ToList();
                var ids = orderedDevices.Select(d => d.Id.Substring(Math.Max(0, d.Id.Length - bdAddressLength))).ToList();

                if (names.Count > 0)
                {
                    // New formatting algorithm for "wide" output; we should avoid tabulations and use spaces only
                    int maxWidth = names.Max(n => n.Length);
                    int columns = Console.WindowWidth / (maxWidth + 5);
                    List<string>[] strColumn = new List<string>[columns];

                    for (int i = 0; i < names.Count; i++)
                    {
                        if (strColumn[i % columns] == null) strColumn[i % columns] = new List<string>();
                        strColumn[i % columns].Add($"#{i}: {ids[i]} {names[i]}   ");
                    }

                    int maxNumColumns = Math.Min(columns, strColumn.Count(l => l != null));

                    for (int i = 0; i < maxNumColumns; i++)
                    {
                        int max = strColumn[i].Max(n => n.Length);
                        for (int j = 0; j < strColumn[i].Count; j++)
                            strColumn[i][j] += new string(' ', max - strColumn[i][j].Length);
                    }

                    for (int j = 0; j < strColumn[0].Count; j++)
                    {
                        string s = "";
                        for (int i = 0; i < maxNumColumns; i++)
                            if (j < strColumn[i].Count) s += strColumn[i][j];
                        Console.WriteLine(s.TrimEnd());
                    }
                }
            }
        }

        /// <summary>
        /// Show status of the currently selected BLE device
        /// </summary>
        static void ShowStatus()
        {
            if (_selectedDevice == null)
            {
                Console.WriteLine("No device connected.");
            }
            else
            {
                if (_selectedDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                {
                    Console.WriteLine($"Device {_selectedDevice.Name} is disconnected.");
                }
                else
                {
                    Console.WriteLine($"Device {_selectedDevice.Name} is connected" +
                        (IsPaired(_selectedDevice) ? " and is paired" : ", but is NOT paired"));
                    if (_services.Count() > 0)
                    {
                        // List all services
                        Console.WriteLine("Available services:");
                        for (int i = 0; i < _services.Count(); i++)
                            Console.WriteLine($"#{i:00}: {_services[i].Name}");

                        // If service is selected,
                        if (_selectedService != null)
                        {
                            Console.WriteLine($"Selected service: {_selectedService.Name}");

                            // List all characteristics
                            if (_characteristics.Count > 0)
                            {
                                Console.WriteLine("Available characteristics:");

                                for (int i = 0; i < _characteristics.Count(); i++)
                                    Console.WriteLine($"#{i:00}: {_characteristics[i].Name}\t{_characteristics[i].Chars}");

                                if (_selectedCharacteristic != null)
                                    Console.WriteLine($"Selected characteristic: {_selectedCharacteristic.Name}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Connect to the specific device by name or number, and make this device current
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        static async Task<int> OpenDevice(string deviceName)
        {
            int retVal = 0;
            if (!string.IsNullOrEmpty(deviceName))
            {
                //var devs = _deviceList.OrderBy(d => d.Name).Where(d => !string.IsNullOrEmpty(d.Name)).ToList();
                var devs = _deviceList.OrderBy(d => d.Name).ToList();
                string foundId = Utilities.GetIdByNameOrNumber(devs, deviceName);

                // If device is found, connect to device and enumerate all services
                if (!string.IsNullOrEmpty(foundId))
                {
                    _selectedCharacteristic = null;
                    _selectedService = null;
                    _services.Clear();

                    try
                    {
                        // only allow for one connection to be open at a time
                        if (_selectedDevice != null)
                            CloseDevice();

                        _selectedDevice = await BluetoothLEDevice.FromIdAsync(foundId).AsTask().TimeoutAfter(_timeout);
                        if (!Console.IsInputRedirected)
                        {
                            Console.WriteLine($"Connecting to {_selectedDevice.Name}. " +
                                (IsPaired(_selectedDevice) ? "It is paired" : "It is NOT paired"));
                        }

                        var result = await _selectedDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                        if (result.Status == GattCommunicationStatus.Success)
                        {
                            if (!Console.IsInputRedirected)
                                Console.WriteLine($"Found {result.Services.Count} services:");

                            for (int i = 0; i < result.Services.Count; i++)
                            {
                                var serviceToDisplay = new BluetoothLEAttributeDisplay(result.Services[i]);
                                _services.Add(serviceToDisplay);
                                if (!Console.IsInputRedirected)
                                    Console.WriteLine($"#{i:00}: {_services[i].Name}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Device {deviceName} is unreachable.");
                            retVal += 1;
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"Device {deviceName} is unreachable.");
                        retVal += 1;
                    }
                }
                else
                {
                    retVal += 1;
                }
            }
            else
            {
                Console.WriteLine("Device name can not be empty.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// Disconnect current device and clear list of services and characteristics
        /// </summary>
        static void CloseDevice()
        {
            // Remove all subscriptions
            if (_subscribers.Count > 0) Unsubscribe("all");

            if (_selectedDevice != null)
            {
                if (!Console.IsInputRedirected)
                    Console.WriteLine($"Device {_selectedDevice.Name} is disconnected.");

                _services?.ForEach((s) => { s.service?.Dispose(); });
                _services?.Clear();
                _characteristics?.Clear();
                _selectedDevice?.Dispose();
            }
        }

        /// <summary>
        /// Set active service for current device
        /// </summary>
        /// <param name="parameters"></param>
        static async Task<int> SetService(string serviceName)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(serviceName))
                {
                    string foundName = Utilities.GetIdByNameOrNumber(_services, serviceName);

                    // If device is found, connect to device and enumerate all services
                    if (!string.IsNullOrEmpty(foundName))
                    {
                        var attr = _services.FirstOrDefault(s => s.Name.Equals(foundName));
                        IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();

                        try
                        {
                            // Ensure we have access to the device.
                            var accessStatus = await attr.service.RequestAccessAsync();
                            if (accessStatus == DeviceAccessStatus.Allowed)
                            {
                                // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                                // and the new Async functions to get the characteristics of unpaired devices as well. 
                                var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                if (result.Status == GattCommunicationStatus.Success)
                                {
                                    characteristics = result.Characteristics;
                                    _selectedService = attr;
                                    _characteristics.Clear();
                                    if (!Console.IsInputRedirected) Console.WriteLine($"Selected service {attr.Name}.");

                                    if (characteristics.Count > 0)
                                    {
                                        int maxNameLength = 0;
                                        for (int i = 0; i < characteristics.Count; i++)
                                        {
                                            var charToDisplay = new BluetoothLEAttributeDisplay(characteristics[i]);
                                            _characteristics.Add(charToDisplay);
                                            maxNameLength = Math.Max(maxNameLength, charToDisplay.Name.Length);
                                        }
                                        if (!Console.IsInputRedirected)
                                        {
                                            for (int i = 0; i < characteristics.Count; i++)
                                            {
                                                var charToDisplay = new BluetoothLEAttributeDisplay(characteristics[i]);
                                                Console.WriteLine($"#{i:00}: {charToDisplay.Name.PadRight(maxNameLength)}   {charToDisplay.Chars}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!Console.IsOutputRedirected)
                                            Console.WriteLine("Service don't have any characteristic.");
                                        retVal += 1;
                                    }
                                }
                                else
                                {
                                    if (!Console.IsOutputRedirected)
                                        Console.WriteLine("Error accessing service.");
                                    retVal += 1;
                                }
                            }
                            // Not granted access
                            else
                            {
                                if (!Console.IsOutputRedirected)
                                    Console.WriteLine("Error accessing service.");
                                retVal += 1;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (!Console.IsOutputRedirected)
                                Console.WriteLine($"Restricted service. Can't read characteristics: {ex.Message}");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        if (!Console.IsOutputRedirected)
                            Console.WriteLine("Invalid service name or number");
                        retVal += 1;
                    }
                }
                else
                {
                    if (!Console.IsOutputRedirected)
                        Console.WriteLine("Invalid service name or number");
                    retVal += 1;
                }
            }
            else
            {
                if (!Console.IsOutputRedirected)
                    Console.WriteLine("Nothing to use, no BLE device connected.");
                retVal += 1;
            }

            return retVal;
        }

        /// <summary>
        /// This function reads data from the specific BLE characteristic 
        /// </summary>
        /// <param name="param"></param>
        static async Task<int> ReadCharacteristic(string param)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(param))
                {
                    List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();

                    string charName = string.Empty;
                    var parts = param.Split('/');
                    // Do we have parameter is in "service/characteristic" format?
                    if (parts.Length == 2)
                    {
                        string serviceName = Utilities.GetIdByNameOrNumber(_services, parts[0]);
                        charName = parts[1];

                        // If device is found, connect to device and enumerate all services
                        if (!string.IsNullOrEmpty(serviceName))
                        {
                            var attr = _services.FirstOrDefault(s => s.Name.Equals(serviceName));
                            IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();

                            try
                            {
                                // Ensure we have access to the device.
                                var accessStatus = await attr.service.RequestAccessAsync();
                                if (accessStatus == DeviceAccessStatus.Allowed)
                                {
                                    var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                    if (result.Status == GattCommunicationStatus.Success)
                                        characteristics = result.Characteristics;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Restricted service. Can't read characteristics: {ex.Message}");
                                retVal += 1;
                            }

                            foreach (var c in characteristics)
                                chars.Add(new BluetoothLEAttributeDisplay(c));
                        }
                    }
                    else if (parts.Length == 1)
                    {
                        if (_selectedService == null)
                        {
                            if (!Console.IsOutputRedirected)
                                Console.WriteLine("No service is selected.");
                        }
                        chars = new List<BluetoothLEAttributeDisplay>(_characteristics);
                        charName = parts[0];
                    }

                    // Read characteristic
                    if (chars.Count > 0 && !string.IsNullOrEmpty(charName))
                    {
                        string useName = Utilities.GetIdByNameOrNumber(chars, charName);
                        var attr = chars.FirstOrDefault(c => c.Name.Equals(useName));
                        if (attr != null && attr.characteristic != null)
                        {
                            // Read characteristic value
                            GattReadResult result = await attr.characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                            if (result.Status == GattCommunicationStatus.Success)
                                Console.WriteLine($"Read {result.Value.Length} bytes.\n{Utilities.FormatValueMultipleFormattes(result.Value, _receivedDataFormat)}");
                            else
                            {
                                Console.WriteLine($"Read failed: {result.Status} {Utilities.FormatProtocolError(result.ProtocolError)}");
                                retVal += 1;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Invalid characteristic {charName}");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Nothing to read, please specify characteristic name or #.");
                        retVal += 1;
                    }
                }
                else
                {
                    Console.WriteLine("Nothing to read, please specify characteristic name or #.");
                    retVal += 1;
                }
            }
            else
            {
                Console.WriteLine("No BLE device connected.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// This function writes data from the specific BLE characteristic 
        /// </summary>
        /// <param name="param">
        /// parameters should be:
        ///    [char_name] or [service_name/char_name] - specific characteristics
        ///    [data_value] - data to write; data will be interpreted depending of current display format,
        ///    wrong data format will cause write fail
        /// </param>
        /// <param name="userInput">
        /// we need whole user input (trimmed from spaces on beginning) in case of text input with spaces at the end
        static async Task<int> WriteCharacteristic(string param)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(param))
                {
                    List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();

                    string charName = string.Empty;

                    // First, split data from char name (it should be a second param)
                    var parts = param.Split(' ');
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("Insufficient data for write, please provide characteristic name and data.");
                        retVal += 1;
                        return retVal;
                    }

                    // Now try to convert data to the byte array by current format
                    string data = param.Substring(parts[0].Length + 1);
                    if (string.IsNullOrEmpty(data))
                    {
                        Console.WriteLine("Insufficient data for write.");
                        retVal += 1;
                        return retVal;
                    }
                    var buffer = Utilities.FormatData(data, _sendDataFormat);
                    if (buffer != null)
                    {
                        // Now process service/characteristic names
                        var charNames = parts[0].Split('/');

                        // Do we have parameter is in "service/characteristic" format?
                        if (charNames.Length == 2)
                        {
                            string serviceName = Utilities.GetIdByNameOrNumber(_services, charNames[0]);
                            charName = charNames[1];

                            // If device is found, connect to device and enumerate all services
                            if (!string.IsNullOrEmpty(serviceName))
                            {
                                var attr = _services.FirstOrDefault(s => s.Name.Equals(serviceName));
                                IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();
                                try
                                {
                                    // Ensure we have access to the device.
                                    var accessStatus = await attr.service.RequestAccessAsync();
                                    if (accessStatus == DeviceAccessStatus.Allowed)
                                    {
                                        var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                        if (result.Status == GattCommunicationStatus.Success)
                                            characteristics = result.Characteristics;
                                    }
                                    foreach (var c in characteristics)
                                        chars.Add(new BluetoothLEAttributeDisplay(c));
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Restricted service. Can't read characteristics: {ex.Message}");
                                    retVal += 1;
                                    return retVal;
                                }
                            }
                        }
                        else if (charNames.Length == 1)
                        {
                            if (_selectedService == null)
                            {
                                if (!Console.IsOutputRedirected)
                                    Console.WriteLine("No service is selected.");
                                retVal += 1;
                            }
                            chars = new List<BluetoothLEAttributeDisplay>(_characteristics);
                            charName = parts[0];
                        }

                        // Write characteristic
                        if (chars.Count > 0 && !string.IsNullOrEmpty(charName))
                        {
                            string useName = Utilities.GetIdByNameOrNumber(chars, charName);
                            var attr = chars.FirstOrDefault(c => c.Name.Equals(useName));
                            if (attr != null && attr.characteristic != null)
                            {
                                // Write data to characteristic
                                GattWriteResult result = await attr.characteristic.WriteValueWithResultAsync(buffer);
                                if (result.Status != GattCommunicationStatus.Success)
                                {
                                    if (!Console.IsOutputRedirected)
                                        Console.WriteLine($"Write failed: {result.Status} {Utilities.FormatProtocolError(result.ProtocolError)}");
                                    retVal += 1;
                                }
                            }
                            else
                            {
                                if (!Console.IsOutputRedirected)
                                    Console.WriteLine($"Invalid characteristic {charName}");
                                retVal += 1;
                            }
                        }
                        else
                        {
                            if (!Console.IsOutputRedirected)
                                Console.WriteLine("Please specify characteristic name or # for writing.");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        if (!Console.IsOutputRedirected)
                            Console.WriteLine("Incorrect data format.");
                        retVal += 1;
                    }
                }
            }
            else
            {
                if (!Console.IsOutputRedirected)
                    Console.WriteLine("No BLE device connected.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// This function used to add "ValueChanged" event subscription
        /// </summary>
        /// <param name="param"></param>
        static async Task<int> SubscribeToCharacteristic(string param)
        {
            int retVal = 0;
            if (_selectedDevice != null)
            {
                if (!string.IsNullOrEmpty(param))
                {
                    List<BluetoothLEAttributeDisplay> chars = new List<BluetoothLEAttributeDisplay>();

                    string charName = string.Empty;
                    var parts = param.Split('/');
                    // Do we have parameter is in "service/characteristic" format?
                    if (parts.Length == 2)
                    {
                        string serviceName = Utilities.GetIdByNameOrNumber(_services, parts[0]);
                        charName = parts[1];

                        // If device is found, connect to device and enumerate all services
                        if (!string.IsNullOrEmpty(serviceName))
                        {
                            var attr = _services.FirstOrDefault(s => s.Name.Equals(serviceName));
                            IReadOnlyList<GattCharacteristic> characteristics = new List<GattCharacteristic>();

                            try
                            {
                                // Ensure we have access to the device.
                                var accessStatus = await attr.service.RequestAccessAsync();
                                if (accessStatus == DeviceAccessStatus.Allowed)
                                {
                                    var result = await attr.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                                    if (result.Status == GattCommunicationStatus.Success)
                                        characteristics = result.Characteristics;
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!Console.IsOutputRedirected)
                                    Console.WriteLine($"Restricted service. Can't subscribe to characteristics: {ex.Message}");
                                retVal += 1;
                            }

                            foreach (var c in characteristics)
                                chars.Add(new BluetoothLEAttributeDisplay(c));
                        }
                    }
                    else if (parts.Length == 1)
                    {
                        if (_selectedService == null)
                        {
                            if (!Console.IsOutputRedirected)
                                Console.WriteLine("No service is selected.");
                            retVal += 1;
                            return retVal;
                        }
                        chars = new List<BluetoothLEAttributeDisplay>(_characteristics);
                        charName = parts[0];
                    }

                    // Read characteristic
                    if (chars.Count > 0 && !string.IsNullOrEmpty(charName))
                    {
                        string useName = Utilities.GetIdByNameOrNumber(chars, charName);
                        var attr = chars.FirstOrDefault(c => c.Name.Equals(useName));
                        if (attr != null && attr.characteristic != null)
                        {
                            // First, check for existing subscription
                            if (!_subscribers.Contains(attr.characteristic))
                            {
                                var charDisplay = new BluetoothLEAttributeDisplay(attr.characteristic);
                                if (!charDisplay.CanNotify && !charDisplay.CanIndicate)
                                {
                                    if (!Console.IsOutputRedirected)
                                        Console.WriteLine($"Characteristic {useName} does not support notify or indicate");
                                    retVal += 1;
                                    return retVal;
                                }

                                GattCommunicationStatus status;
                                if (charDisplay.CanNotify)
                                {
                                    status = await attr.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                }
                                else
                                {
                                    status = await attr.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Indicate);
                                }
                                if (status == GattCommunicationStatus.Success)
                                {
                                    _subscribers.Add(attr.characteristic);
                                    attr.characteristic.ValueChanged += Characteristic_ValueChanged;
                                    if (!Console.IsOutputRedirected)
                                    {
                                        if (charDisplay.CanNotify)
                                            Console.WriteLine($"Subscribed to characteristic {useName} (notify)");
                                        else
                                            Console.WriteLine($"Subscribed to characteristic {useName} (indicate)");
                                    }

                                }
                                else
                                {
                                    if (!Console.IsOutputRedirected)
                                        Console.WriteLine($"Can't subscribe to characteristic {useName}");
                                    retVal += 1;
                                }
                            }
                            else
                            {
                                if (!Console.IsOutputRedirected)
                                    Console.WriteLine($"Already subscribed to characteristic {useName}");
                                retVal += 1;
                            }
                        }
                        else
                        {
                            if (!Console.IsOutputRedirected)
                                Console.WriteLine($"Invalid characteristic {useName}");
                            retVal += 1;
                        }
                    }
                    else
                    {
                        if (!Console.IsOutputRedirected)
                            Console.WriteLine("Nothing to subscribe, please specify characteristic name or #.");
                        retVal += 1;
                    }
                }
                else
                {
                    if (!Console.IsOutputRedirected)
                        Console.WriteLine("Nothing to subscribe, please specify characteristic name or #.");
                    retVal += 1;
                }
            }
            else
            {
                if (!Console.IsOutputRedirected)
                    Console.WriteLine("No BLE device connected.");
                retVal += 1;
            }
            return retVal;
        }

        /// <summary>
        /// This function is used to unsubscribe from "ValueChanged" event
        /// </summary>
        /// <param name="param"></param>
        static async void Unsubscribe(string param)
        {
            if (_subscribers.Count == 0)
            {
                if (!Console.IsOutputRedirected)
                    Console.WriteLine("No subscription for value changes found.");
            }
            else if (string.IsNullOrEmpty(param))
            {
                if (!Console.IsOutputRedirected)
                    Console.WriteLine("Please specify characteristic name or # (for single subscription) or type \"unsubs all\" to remove all subscriptions");
            }
            // Unsubscribe from all value changed events
            else if (param.Replace("/", "").ToLower().Equals("all"))
            {
                foreach (var sub in _subscribers)
                {
                    if (!Console.IsOutputRedirected)
                        Console.WriteLine($"Unsubscribe from {sub.Uuid}");
                    await sub.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                    sub.ValueChanged -= Characteristic_ValueChanged;
                }
                _subscribers.Clear();
            }
            // unsubscribe from specific event
            else
            {
                if (!Console.IsOutputRedirected)
                    Console.WriteLine("Not supported, please use \"unsubs all\"");
            }
        }

        /// <summary>
        /// Event handler for ValueChanged callback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        static void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var newValue = Utilities.FormatValueMultipleFormattes(args.CharacteristicValue, _receivedDataFormat);

            if (Console.IsInputRedirected) Console.Write($"{newValue}");
            else Console.Write($"Value changed for {sender.Uuid} ({args.CharacteristicValue.Length} bytes):\n{newValue}\nBLE: ");
            if (_notifyCompleteEvent != null)
            {
                _notifyCompleteEvent.Set();
                _notifyCompleteEvent = null;
            }
        }

        static DeviceInformation FindKnownDevice(string deviceId)
        {
            foreach (var device in _deviceList)
            {
                if (device.Id == deviceId)
                {
                    return device;
                }
            }
            return null;
        }
    }
}
