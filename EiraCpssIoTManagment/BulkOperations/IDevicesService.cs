using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BulkOperations
{
    public interface IDevicesService
    {
        Task AddDevices(int numberOfDevices, int from, string baseName);
        Task DeleteDevice(string deviceId);
        Task<bool> DeleteAllDevices(string deleteBaseName);
        Task<Device> GetDevice(string deviceId);
        Task<IEnumerable<Device>> GetDevices();
        Task ReceiveC2dAsync(DeviceClient deviceClient);
        Task ReceiveMessagesFromAllFacilities();
        Task SendCloudMessageForAllFacilities();
    }
}