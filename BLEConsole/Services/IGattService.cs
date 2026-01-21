using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace BLEConsole.Services
{
    /// <summary>
    /// Service for GATT operations (read/write characteristics)
    /// </summary>
    public interface IGattService
    {
        Task<int> ReadCharacteristicAsync(string characteristicName);
        Task<int> WriteCharacteristicAsync(string characteristicName, string data, bool withoutResponse = false);
        Task<int> SubscribeToCharacteristicAsync(string characteristicName);
        Task<int> UnsubscribeFromCharacteristicAsync(string characteristicName);
        Task<int> SetServiceAsync(string serviceName);
        
        // New features
        Task<int> ListDescriptorsAsync(string characteristicName);
        Task<int> ReadDescriptorAsync(string characteristicName, string descriptorName);
        Task<int> WriteDescriptorAsync(string characteristicName, string descriptorName, string data);
        int GetMtuSize();
    }
}
