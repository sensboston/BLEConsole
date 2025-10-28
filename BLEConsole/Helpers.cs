using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace BLEConsole
{
    // ========================================================================
    // BACKWARD COMPATIBILITY LAYER
    // This file provides aliases to the new refactored structure
    // ========================================================================

    // Type aliases for backward compatibility with existing code
    using AttributeType = Enums.AttributeType;
    using DataFormat = Enums.DataFormat;
    using GattNativeServiceUuid = Enums.GattNativeServiceUuid;
    using GattNativeCharacteristicUuid = Enums.GattNativeCharacteristicUuid;
    using GattNativeDescriptorUuid = Enums.GattNativeDescriptorUuid;
    using ProtocolErrorCode = Enums.ProtocolErrorCode;
    using BluetoothLEAttributeDisplay = Models.BluetoothLEAttributeDisplay;
    using BluetoothLEDeviceDisplay = Models.BluetoothLEDeviceDisplay;

    /// <summary>
    /// Utilities class provides backward compatibility
    /// All methods now forward to specialized utility classes
    /// </summary>
    public static class Utilities
    {
        // UUID Conversion
        public static ushort ConvertUuidToShortId(Guid uuid) =>
            BLEConsole.Utilities.UuidConverter.ConvertUuidToShortId(uuid);

        // Data formatting
        public static byte[] ReadBufferToBytes(IBuffer buffer) =>
            BLEConsole.Utilities.DataFormatter.ReadBufferToBytes(buffer);

        public static string FormatValue(IBuffer buffer, DataFormat format) =>
            BLEConsole.Utilities.DataFormatter.FormatValue(buffer, format);

        public static string FormatValueMultipleFormattes(IBuffer buffer, List<DataFormat> formatList) =>
            BLEConsole.Utilities.DataFormatter.FormatValueMultipleFormattes(buffer, formatList);

        public static IBuffer FormatData(string data, DataFormat format) =>
            BLEConsole.Utilities.DataFormatter.FormatData(data, format);

        // Device lookup
        public static bool CheckForValidBluetoothAddress(string name) =>
            BLEConsole.Utilities.DeviceLookup.CheckForValidBluetoothAddress(name);

        public static string GetIdByNameOrNumber(object collection, string name) =>
            BLEConsole.Utilities.DeviceLookup.GetIdByNameOrNumber(collection, name);

        // Protocol error formatting
        public static string FormatProtocolError(byte? protocolError) =>
            BLEConsole.Utilities.ProtocolErrorFormatter.FormatProtocolError(protocolError);
    }

    /// <summary>
    /// Task extensions for timeout support
    /// </summary>
    public static class TaskExtensions
    {
        public static Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout) =>
            BLEConsole.Utilities.TaskExtensions.TimeoutAfter(task, timeout);
    }
}
