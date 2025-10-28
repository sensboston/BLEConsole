using System;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using BLEConsole.Enums;
using BLEConsole.Utils;

namespace BLEConsole.Models
{
    /// <summary>
    /// Represents the display of an attribute - both characteristics and services.
    /// </summary>
    public class BluetoothLEAttributeDisplay
    {
        public GattCharacteristic characteristic;
        public GattDescriptor descriptor;

        public GattDeviceService service;

        public BluetoothLEAttributeDisplay(GattDeviceService service)
        {
            this.service = service;
            AttributeDisplayType = AttributeType.Service;
        }

        public BluetoothLEAttributeDisplay(GattCharacteristic characteristic)
        {
            this.characteristic = characteristic;
            AttributeDisplayType = AttributeType.Characteristic;
        }

        public string Chars => (CanRead ? "R" : " ") + (CanWrite ? "W" : " ") + (CanNotify ? "N" : " ") + (CanIndicate ? "I": " ");

        public bool CanRead
        {
            get
            {
                return this.characteristic != null ? this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read) : false;
            }
        }

        public bool CanWrite
        {
            get
            {
                return this.characteristic != null ?
                    (this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
                     this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse) ||
                     this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.ReliableWrites) ||
                     this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WritableAuxiliaries))
                    : false;
            }
        }

        public bool CanNotify
        {
            get
            {
                return this.characteristic != null ? this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) : false;
            }
        }

        public bool CanIndicate
        {
            get
            {
                return this.characteristic != null ? this.characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate) : false;
            }
        }


        public string Name
        {
            get
            {
                switch (AttributeDisplayType)
                {
                    case AttributeType.Service:
                        if (IsSigDefinedUuid(service.Uuid))
                        {
                            ushort shortId = UuidConverter.ConvertUuidToShortId(service.Uuid);
                            string serviceName = Enum.GetName(typeof(Enums.GattNativeServiceUuid), shortId);
                            if (serviceName != null)
                            {
                                return serviceName;
                            }
                            return String.Format("0x{0:X4}", shortId);
                        }
                        return "Custom Service: " + service.Uuid;
                    case AttributeType.Characteristic:
                        if (IsSigDefinedUuid(characteristic.Uuid))
                        {
                            ushort shortId = UuidConverter.ConvertUuidToShortId(characteristic.Uuid);
                            string characteristicName = Enum.GetName(typeof(Enums.GattNativeCharacteristicUuid), shortId);
                            if (characteristicName != null)
                            {
                                return characteristicName;
                            }
                            return String.Format("0x{0:X4}", shortId);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(characteristic.UserDescription))
                            {
                                return characteristic.UserDescription;
                            }

                            else
                            {
                                return "Custom Characteristic: " + characteristic.Uuid;
                            }
                        }

                    default:
                        break;
                }
                return "Invalid";
            }
        }

        public string Uuid
        {
            get
            {
                switch (AttributeDisplayType)
                {
                    case AttributeType.Service:
                        return service.Uuid.ToString();
                    case AttributeType.Characteristic:
                        return characteristic.Uuid.ToString();
                    default:
                        break;
                }
                return "Invalid";
            }
        }

        public AttributeType AttributeDisplayType { get; }

        /// <summary>
        /// The SIG has a standard base value for Assigned UUIDs. In order to determine if a UUID is SIG defined,
        /// zero out the unique section and compare the base sections.
        /// </summary>
        /// <param name="uuid">The UUID to determine if SIG assigned</param>
        /// <returns></returns>
        private static bool IsSigDefinedUuid(Guid uuid)
        {
            var bluetoothBaseUuid = new Guid("00000000-0000-1000-8000-00805F9B34FB");

            var bytes = uuid.ToByteArray();
            // Zero out the first and second bytes
            // Note how each byte gets flipped in a section - 1234 becomes 34 12
            // Example Guid: 35918bc9-1234-40ea-9779-889d79b753f0
            //                   ^^^^
            // bytes output = C9 8B 91 35 34 12 EA 40 97 79 88 9D 79 B7 53 F0
            //                ^^ ^^
            bytes[0] = 0;
            bytes[1] = 0;
            var baseUuid = new Guid(bytes);
            return baseUuid == bluetoothBaseUuid;
        }
    }
}
