using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;

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

        // Current data format
        static DataFormat _dataFormat = DataFormat.UTF8;

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
        static bool _primed = false;

        static TimeSpan _timeout = TimeSpan.FromSeconds(3);

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

        static async Task MainAsync(string[] args)
        {
            // Start endless BLE device watcher
            var watcher = DeviceInformation.CreateWatcher(_aqsAllBLEDevices, _requestedBLEProperties, DeviceInformationKind.AssociationEndpoint);
            watcher.Added += (DeviceWatcher sender, DeviceInformation devInfo) =>
            {
                if (_deviceList.FirstOrDefault(d => d.Id.Equals(devInfo.Id) || d.Name.Equals(devInfo.Name)) == null) _deviceList.Add(devInfo);
            };
            watcher.Updated += (_, __) => {}; // We need handler for this event, even an empty!
            //Watch for a device being removed by the watcher
            //watcher.Removed += (DeviceWatcher sender, DeviceInformationUpdate devInfo) =>
            //{
            //    _deviceList.Remove(FindKnownDevice(devInfo.Id));
            //};
            watcher.EnumerationCompleted += (DeviceWatcher sender, object arg) => { sender.Stop(); };
            watcher.Stopped += (DeviceWatcher sender, object arg) => { _deviceList.Clear(); sender.Start(); };
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
                            if (_forEachDeviceCounter++ > _forEachDeviceNames.Count-1)
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

                    // Check for the end of input
                    if (Console.IsInputRedirected && string.IsNullOrEmpty(userInput))
                    {
                        _doWork = false;
                    }
                    else userInput = userInput?.TrimStart(new char[] { ' ', '\t' });

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
            switch (cmd)
            {
                case "if":
                    _inIfBlock++;
                    _exitCode = 0;
                    if(parameters != "")
                    {
                        string[] str = parameters.Split(' ');
                        await HandleSwitch(str[0], str.Skip(1).Aggregate((i, j) => i + " " + j));
                        _closingIfBlock = ( _exitCode > 0 );
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
                    if(_inIfBlock > 0)
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
                    ChangeDisplayFormat(parameters);
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
                    PairBluetooth(parameters);
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
                "\n  help, ?\t\t\t: show help information\n"+
                "  quit, q\t\t\t: quit from application\n"+
                "  list, ls [w]\t\t\t: show available BLE devices\n" +
                "  open <name> or <#>\t\t: connect to BLE device\n" +
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
                "  format [data_format], fmt\t: show/change display format, can be ASCII/UTF8/Dec/Hex/Bin\n" +
                "  set <service_name> or <#>\t: set current service (for read/write operations)\n" +
                "  read, r <name>**\t\t: read value from specific characteristic\n" +
                "  write, w <name>**<value>\t: write value to specific characteristic\n" +
                "  subs <name>**\t\t\t: subscribe to value change for specific characteristic\n" +
                "  unsubs <name>** [all]\t\t: unsubscribe from value change for specific characteristic or unsubs all for all\n" +
                "  wait\t\t\t\t: wait for notification event on value change (you must be subscribed, see above)\n" +
                "  foreach [device_mask]\t\t: starts devices enumerating loop\n" +
                "  endfor\t\t\t: end foreach loop\n"+
                "  if <cmd> <params>\t\t: start conditional block dependent on function returning w\\o error\n" +
                "    elif\t\t\t: another conditionals block\n" +
                "    else\t\t\t: if condition == false block\n" +
                "  endif\t\t\t\t: end conditional block\n\n" +
                "   * You can also use standard C language string formating characters like \\t, \\n etc. \n" +
                "  ** <name> could be \"service/characteristic\", or just a char name or # (for selected service)\n\n" +
                "  For additional information and examples please visit https://github.com/shelltechworks/BLEConsole \n"
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

        static async void PairBluetooth(string param)
        {
            DevicePairingResult result = null;
            DeviceInformationPairing pairingInformation = _selectedDevice.DeviceInformation.Pairing;

            await _selectedDevice.DeviceInformation.Pairing.UnpairAsync();

            if (pairingInformation.CanPair)
                result =  await _selectedDevice.DeviceInformation.Pairing.PairAsync(pairingInformation.ProtectionLevel);
            
        }

        static void ChangeDisplayFormat(string param)
        {
            if (!string.IsNullOrEmpty(param))
            {
                switch (param.ToLower())
                {
                    case "ascii":
                        _dataFormat = DataFormat.ASCII;
                        break;
                    case "utf8":
                        _dataFormat = DataFormat.UTF8;
                        break;
                    case "dec":
                    case "decimal":
                        _dataFormat = DataFormat.Dec;
                        break;
                    case "bin":
                    case "binary":
                        _dataFormat = DataFormat.Bin;
                        break;
                    case "hex":
                    case "hexdecimal":
                        _dataFormat = DataFormat.Hex;
                        break;
                    default:
                        break;
                }
            }
            Console.WriteLine($"Current display format: {_dataFormat.ToString()}");
        }

        static void Delay(string param)
        {
            uint milliseconds = (uint) _timeout.TotalMilliseconds;
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
            
            var names = _deviceList.OrderBy(d => d.Name).Where(d => !string.IsNullOrEmpty(d.Name)).Select(d => d.Name).ToList();
            if (string.IsNullOrEmpty(param))
            {
                for (int i = 0; i < names.Count(); i++)
                    Console.WriteLine($"#{i:00}: {names[i]}");
            }
            else if (param.Replace("/","").ToLower().Equals("w"))
            {
                if (names.Count > 0)
                {
                    // New formatting algorithm for "wide" output; we should avoid tabulations and use spaces only
                    int maxWidth = names.Max(n => n.Length);
                    int columns = Console.WindowWidth / (maxWidth + 5);
                    List<string>[] strColumn = new List<string>[columns];

                    for (int i = 0; i < names.Count; i++)
                    {
                        if (strColumn[i % columns] == null) strColumn[i % columns] = new List<string>();
                        strColumn[i % columns].Add(string.Format("#{0:00}: {1}   ", i, names[i]));
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
                    Console.WriteLine($"Device {_selectedDevice.Name} is connected.");
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
                var devs = _deviceList.OrderBy(d => d.Name).Where(d => !string.IsNullOrEmpty(d.Name)).ToList();
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
                            Console.WriteLine($"Connecting to {_selectedDevice.Name}.");

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
                                        for (int i = 0; i < characteristics.Count; i++)
                                        {
                                            var charToDisplay = new BluetoothLEAttributeDisplay(characteristics[i]);
                                            _characteristics.Add(charToDisplay);
                                            if (!Console.IsInputRedirected) Console.WriteLine($"#{i:00}: {charToDisplay.Name}\t{charToDisplay.Chars}");
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
                            if(!Console.IsOutputRedirected)
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
                                Console.WriteLine(Utilities.FormatValue(result.Value, _dataFormat));
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
                    var buffer = Utilities.FormatData(data, _dataFormat);
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
                                if(!Console.IsOutputRedirected)
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
                                var status = await attr.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                if (status == GattCommunicationStatus.Success)
                                {
                                    _subscribers.Add(attr.characteristic);
                                    attr.characteristic.ValueChanged += Characteristic_ValueChanged;
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
                    await sub.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                    sub.ValueChanged -= Characteristic_ValueChanged;
                }
                _subscribers.Clear();
            }
            // unsubscribe from specific event
            else
            {

            }
        }

        /// <summary>
        /// Event handler for ValueChanged callback
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        static void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            if (_primed)
            {
                var newValue = Utilities.FormatValue(args.CharacteristicValue, _dataFormat);

                if (Console.IsInputRedirected) Console.Write($"{newValue}");
                else Console.Write($"Value changed for {sender.Uuid}: {newValue}\nBLE: ");
                if (_notifyCompleteEvent != null)
                {
                    _notifyCompleteEvent.Set();
                    _notifyCompleteEvent = null;
                }
            }
            else  _primed = true; 
        }

        static DeviceInformation FindKnownDevice(string deviceId)
        {
            foreach (var device in _deviceList)
            {
                if(device.Id == deviceId)
                {
                    return device;
                }
            }
            return null;
        }
    }
}
