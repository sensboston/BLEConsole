using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BLEConsole.Services
{
    /// <summary>
    /// Service for BLE device operations
    /// </summary>
    public interface IDeviceService
    {
        Task<int> OpenDeviceAsync(string deviceName);
        void CloseDevice();
        DeviceWatcher CreateDeviceWatcher();
    }
}
