using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BLEConsole.Core
{
    /// <summary>
    /// Contains all BLE application state. Replaces static variables from Program.cs
    /// </summary>
    public class BleContext
    {
        // Device management
        public List<DeviceInformation> DiscoveredDevices { get; } = new List<DeviceInformation>();
        public BluetoothLEDevice SelectedDevice { get; set; }

        // Service/Characteristic management  
        public List<Models.BluetoothLEAttributeDisplay> Services { get; } = new List<Models.BluetoothLEAttributeDisplay>();
        public Models.BluetoothLEAttributeDisplay SelectedService { get; set; }
        public List<Models.BluetoothLEAttributeDisplay> Characteristics { get; } = new List<Models.BluetoothLEAttributeDisplay>();
        public Models.BluetoothLEAttributeDisplay SelectedCharacteristic { get; set; }

        // Subscriptions
        public List<Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic> Subscribers { get; } = new List<Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristic>();

        // Configuration
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
        public Enums.DataFormat SendDataFormat { get; set; } = Enums.DataFormat.UTF8;
        public List<Enums.DataFormat> ReceivedDataFormats { get; } = new List<Enums.DataFormat> { Enums.DataFormat.UTF8, Enums.DataFormat.Hex };

        // Pairing cache
        private readonly Dictionary<string, bool> _pairings = new Dictionary<string, bool>();

        public bool IsPaired(BluetoothLEDevice device)
        {
            if (device == null) return false;
            return _pairings.ContainsKey(device.DeviceId) 
                ? _pairings[device.DeviceId] 
                : device.DeviceInformation.Pairing.IsPaired;
        }

        public void SetPairingStatus(BluetoothLEDevice device, bool isPaired)
        {
            if (device != null)
                _pairings[device.DeviceId] = isPaired;
        }
    }
}
