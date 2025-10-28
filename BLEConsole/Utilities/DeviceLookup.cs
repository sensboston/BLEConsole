using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Enumeration;

namespace BLEConsole.Utilities
{
    public static class DeviceLookup
    {
        /// <summary>
        /// This function checks whether the given string is a valid 6-byte bluetooth address.
        /// </summary>
        public static bool CheckForValidBluetoothAddress(string name)
        {
            const int bdAddressLength = 17;
            const int bdAddressColonCount = 5;

            if (name.Length != bdAddressLength) return false;
            if ((name.Split(':').Length - 1) != bdAddressColonCount) return false;

            string lowerCaseName = name.ToLower();
            foreach (char c in lowerCaseName)
            {
                if (((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c == ':')) == false)
                    return false;
            }

            char colon = ':';
            if ((name[2] != colon) || (name[5] != colon) || (name[8] != colon) ||
                (name[11] != colon) || (name[14] != colon))
                return false;

            return true;
        }

        /// <summary>
        /// This function is trying to find device or service or attribute by name or number
        /// </summary>
        public static string GetIdByNameOrNumber(object collection, string name)
        {
            string result = string.Empty;

            if (name[0] == '#')
            {
                int devNumber = -1;
                if (int.TryParse(name.Substring(1), out devNumber))
                {
                    if (collection is List<DeviceInformation>)
                    {
                        if (0 <= devNumber && devNumber < (collection as List<DeviceInformation>).Count)
                            result = (collection as List<DeviceInformation>)[devNumber].Id;
                        else
                            Console.WriteLine("Device number {0:00} is not in device list range", devNumber);
                    }
                    else
                    {
                        if (0 <= devNumber && devNumber < (collection as List<Models.BluetoothLEAttributeDisplay>).Count)
                            result = (collection as List<Models.BluetoothLEAttributeDisplay>)[devNumber].Name;
                    }
                }
                else if (!Console.IsOutputRedirected)
                    Console.WriteLine("Invalid device number {0}", name.Substring(1));
            }
            else if (CheckForValidBluetoothAddress(name))
            {
                var foundDevices = (collection as List<DeviceInformation>).Where(d => d.Id.ToLower().Contains(name.ToLower())).ToList();
                if (foundDevices.Count == 0)
                {
                    if (!Console.IsOutputRedirected)
                        Console.WriteLine("Can't connect to {0}.", name);
                }
                else if (foundDevices.Count == 1)
                    result = foundDevices.First().Id;
                else if (!Console.IsOutputRedirected)
                    Console.WriteLine("Found multiple devices with names started from {0}. Please provide an exact name.", name);
            }
            else
            {
                if (collection is List<DeviceInformation>)
                {
                    var foundDevices = (collection as List<DeviceInformation>).Where(d => d.Name.ToLower().StartsWith(name.ToLower())).ToList();
                    if (foundDevices.Count == 0)
                    {
                        if (!Console.IsOutputRedirected)
                            Console.WriteLine("Can't connect to {0}.", name);
                    }
                    else if (foundDevices.Count == 1)
                        result = foundDevices.First().Id;
                    else if (!Console.IsOutputRedirected)
                        Console.WriteLine("Found multiple devices with names started from {0}. Please provide an exact name.", name);
                }
                else
                {
                    var foundByUuid = (collection as List<Models.BluetoothLEAttributeDisplay>).Where(d => name.Equals(d.Uuid)).ToList();
                    if (foundByUuid.Count == 1)
                        return foundByUuid.First().Name;

                    var foundDispAttrs = (collection as List<Models.BluetoothLEAttributeDisplay>).Where(d => d.Name.ToLower().StartsWith(name.ToLower())).ToList();
                    if (foundDispAttrs.Count == 0)
                    {
                        if (Console.IsOutputRedirected)
                            Console.WriteLine("No service/characteristic found by name {0}.", name);
                    }
                    else if (foundDispAttrs.Count == 1)
                        result = foundDispAttrs.First().Name;
                    else if (Console.IsOutputRedirected)
                        Console.WriteLine("Found multiple services/characteristic with names started from {0}. Please provide an exact name.", name);
                }
            }
            return result;
        }
    }
}
