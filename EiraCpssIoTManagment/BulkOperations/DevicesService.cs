using BulkOperations.Models;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Message = Microsoft.Azure.Devices.Client.Message;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;

namespace BulkOperations
{
    /// <summary>
    /// Class for interoperate with IoTHub, Implementing IDeviceService
    /// </summary>
    public class DevicesService : IDevicesService
    {
        private readonly IConfiguration _configuration;

        // Limit telemetry values
        private readonly double minCO2Level;

        private readonly double maxCO2Level;
        private readonly double minThresholdCO2Level;
        private readonly double maxThresholdCO2Level;
        private readonly double minHumidity;
        private readonly double maxHumidity;
        private readonly double minThresholdHumidity;
        private readonly double maxThresholdHumidity;
        private readonly double minTemperature;
        private readonly double maxTemperature;
        private readonly double minThresholdTemperature;
        private readonly double maxThresholdTemperature;

        private readonly int minHeartRate;
        private readonly int maxHeartRate;
        private readonly int thresholdHeartRate;

        private readonly double minUserTemperature;
        private readonly double maxUserTemperature;
        private readonly double thresholdUserTemperature;

        private readonly string _registryManagerConnectionString;
        private static RegistryManager _registryManager;
        private readonly List<Facility> estancias;

        private readonly Random rand = new Random();

        public List<Device> Devices { get; set; }
        public ObservableCollection<string> Messages { get; private set; } = new ObservableCollection<string>();

        public DevicesService()
        {
            var builder = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            _configuration = builder.Build();

            minCO2Level = _configuration.GetValue<double>("minCO2Level");
            maxCO2Level = _configuration.GetValue<double>("maxCO2Level");
            minThresholdCO2Level = _configuration.GetValue<double>("minThresholdCO2Level");
            maxThresholdCO2Level = _configuration.GetValue<double>("maxThresholdCO2Level");
            minHumidity = _configuration.GetValue<double>("minHumidity");
            maxHumidity = _configuration.GetValue<double>("maxHumidity");
            minThresholdHumidity = _configuration.GetValue<double>("minThresholdHumidity");
            maxThresholdHumidity = _configuration.GetValue<double>("maxThresholdHumidity");
            minTemperature = _configuration.GetValue<double>("minTemperature");
            maxTemperature = _configuration.GetValue<double>("maxTemperature");
            minThresholdTemperature = _configuration.GetValue<double>("minThresholdTemperature");
            maxThresholdTemperature = _configuration.GetValue<double>("maxThresholdTemperature");

            minHeartRate = _configuration.GetValue<int>("minHeartRate");
            maxHeartRate = _configuration.GetValue<int>("maxHeartRate");
            thresholdHeartRate = _configuration.GetValue<int>("thresholdHeartRate");

            minUserTemperature = _configuration.GetValue<double>("minUserTemperature");
            maxUserTemperature = _configuration.GetValue<double>("maxUserTemperature");
            thresholdUserTemperature = _configuration.GetValue<double>("thresholdUserTemperature");

            _registryManagerConnectionString = _configuration.GetValue<string>("connectionString");

            _registryManager = RegistryManager.CreateFromConnectionString(_registryManagerConnectionString);

            estancias = GetEstanciasFromJson().ToList();
        }

        /// <summary>
        /// Adds a specific number of devices from a given base name and a start index
        /// </summary>
        /// <param name="numberOfDevices"></param>
        /// <param name="from"></param>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public async Task AddDevices(int numberOfDevices, int from, string baseName)
        {
            for (int i = 0; i < numberOfDevices; i++)
            {
                string newDeviceId = baseName + (from + i).ToString().PadLeft(4, '0');

                await AddDeviceAsync(newDeviceId);
            }
        }

        /// <summary>
        /// Deletes all devices matching a given string
        /// </summary>
        /// <param name="deleteBaseName"></param>
        /// <returns></returns>
        public async Task<bool> DeleteAllDevices(string deleteBaseName)
        {
            var devicesToDelete = Devices.Where(d => d.Id.Contains(deleteBaseName)).ToList();
            Console.WriteLine($"{devicesToDelete.Count} devices found.");
            if (devicesToDelete.Any())
            {
                Console.WriteLine($"Deleting...");
                var result = await _registryManager.RemoveDevices2Async(devicesToDelete);
                return result.IsSuccessful;
            }
            else return false;
        }

        /// <summary>
        /// Deletes a device by Id
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public async Task DeleteDevice(string deviceId)
        {
            await _registryManager.RemoveDeviceAsync(deviceId);
        }

        /// <summary>
        /// Gets a specific device by Id
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        public async Task<Device> GetDevice(string deviceId)
        {
            return await _registryManager.GetDeviceAsync(deviceId);
        }

        /// <summary>
        /// Gets all devices
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<Device>> GetDevices()
        {
            var devices = await _registryManager.GetDevicesAsync(int.MaxValue);
            Devices = (List<Device>)devices;
            return Devices;
        }

        public async Task ReceiveC2dAsync(DeviceClient deviceClient)
        {
            Console.WriteLine("\nReceiving cloud to device messages from service");

            while (true)
            {
                try
                {
                    Message receivedMessage = await deviceClient.ReceiveAsync();
                    if (receivedMessage == null) continue;

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    var message = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                    Console.WriteLine("Received message: {0}",
                    message);
                    Console.ResetColor();
                    Messages.Add(message);
                    await deviceClient.CompleteAsync(receivedMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    break;
                }
            }
        }

        public async Task ReceiveMessagesFromAllFacilities()
        {
            foreach (var device in Devices.Where(d => d.Id.Contains("Facility")))
            {
                var connectionString = $"HostName=eira-iot-hub.azure-devices.net;DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
                var deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);
                await ReceiveC2dAsync(deviceClient);

                deviceClient.Dispose();
            }
        }

        public async Task SendCloudMessageForAllFacilities()
        {
            foreach (var device in Devices.Where(d => d.Id.Contains("Facility")))
            {
                await SendCloudToDeviceMessageAsync(device);
            }
        }

        public async Task SendDeviceToCloudMessagesAsync(Device device)
        {
            if (estancias.Count == 0)
            {
                return;
            }

            var estancia = estancias.ElementAt(rand.Next(0, estancias.Count));
            var (x, y) = GetRandomPoint(estancia);

            double currentTemperature = GetUserCurrentTemperature(rand);
            int currentHeartRate = GetCurrentHeartRate(rand);

            double currentLatitude = y;

            double currentLongitude = x;

            var properties = new Dictionary<string, object>
            {
                { "Temperature", currentTemperature },
                { "HeartRate", currentHeartRate },
                { "Latitude", currentLatitude },
                { "Longitude", currentLongitude },
                { "IdRoom", estancia.Id }
            };
            _ = PnpConvention.CreateComponentPropertyPatch("Wearable", properties);
            Microsoft.Azure.Devices.Client.Message msg = PnpConvention.CreateMessage(properties, "Wearable");

            Console.WriteLine("Preparando envío de datos de usuario...");
            try
            {
                var connectionString = $"HostName=eira-iot-hub.azure-devices.net;DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
                var deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);

                await deviceClient.SendEventAsync(msg);
                Console.WriteLine("{0} > Enviado mensaje a {1}: {2}ºC, {3}lpm, Lat: {4}, Long: {5}, Facility: {6}", DateTime.Now, device.Id, currentTemperature, currentHeartRate, currentLatitude, currentLongitude, estancia.Id);
                await deviceClient.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar mensaje a {device.Id}: {ex.Message}");
            }
        }

        public async Task SendEstanciaToCloudMessagesAsync(Device device)
        {
            double currentTemperature = GetCurrentTemperature(rand);
            double currentCO2Level = GetCurrentCO2Level(rand);
            double currentHumidity = GetCurrentHumidity(rand);

            var properties = new Dictionary<string, object>
            {
                { "Temperature", currentTemperature },
                { "CO2Level", currentCO2Level },
                { "Humidity", currentHumidity }
            };

            _ = PnpConvention.CreateComponentPropertyPatch("Room", properties);
            Message msg = PnpConvention.CreateMessage(properties, "Room");

            Console.WriteLine("Preparando envío de datos de estancia...");

            var connectionString = $"HostName=eira-iot-hub.azure-devices.net;DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
            var deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);

            await deviceClient.SendEventAsync(msg).ConfigureAwait(false);
            Console.WriteLine("{0} > Enviado mensaje a {1}: Temp: {2}ºC, CO2: {3}ppm, Humidity: {4}", DateTime.Now, device.Id, currentTemperature, currentCO2Level, currentHumidity);
            await deviceClient.DisposeAsync();
        }

        private double GetCurrentHumidity(Random rand)
        {
            var prob = rand.NextDouble();
            if (prob > 0.2)
            {
                return Math.Round((rand.NextDouble() * (maxThresholdHumidity - minThresholdHumidity)) + minThresholdHumidity, 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                if (prob > 0.6)
                {
                    return Math.Round((rand.NextDouble() * (maxHumidity - maxThresholdHumidity)) + maxThresholdHumidity, 1, MidpointRounding.AwayFromZero);
                }
                else
                {
                    return Math.Round((rand.NextDouble() * (minThresholdHumidity - minHumidity)) + minHumidity, 1, MidpointRounding.AwayFromZero);
                }
            }
        }

        private double GetCurrentCO2Level(Random rand)
        {
            var prob = rand.NextDouble();
            if (prob < 0.2)
            {
                return Math.Round((rand.NextDouble() * (maxThresholdCO2Level - minThresholdCO2Level)) + minThresholdCO2Level, 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                if (prob >= 0.2)
                {
                    return Math.Round((rand.NextDouble() * (maxCO2Level - maxThresholdCO2Level)) + maxThresholdCO2Level, 1, MidpointRounding.AwayFromZero);
                }
                else
                {
                    return Math.Round((rand.NextDouble() * (minThresholdCO2Level - minCO2Level)) + minCO2Level, 1, MidpointRounding.AwayFromZero);
                }
            }
        }

        private double GetCurrentTemperature(Random rand)
        {
            var prob = rand.NextDouble();
            if (prob < 0.2)
            {
                return Math.Round((rand.NextDouble() * (maxThresholdTemperature - minThresholdTemperature)) + minThresholdTemperature, 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                if (prob >= 0.2)
                {
                    return Math.Round((rand.NextDouble() * (maxTemperature - maxThresholdTemperature)) + maxThresholdTemperature, 1, MidpointRounding.AwayFromZero);
                }
                else
                {
                    return Math.Round((rand.NextDouble() * (minThresholdTemperature - minTemperature)) + minTemperature, 1, MidpointRounding.AwayFromZero);
                }
            }
        }

        public async Task SendMessageFromAllEstancias()
        {
            foreach (var device in Devices.Where(d => d.Id.Contains("Facility")))
            {
                await SendEstanciaToCloudMessagesAsync(device);
            }
        }

        public async Task SendMessageFromSpecificDevice(string deviceName)
        {
            var device = Devices.FirstOrDefault(d => d.Id.Contains(deviceName));

            await SendDeviceToCloudMessagesAsync(device);
        }

        public async Task SendMessageFromAllUsers()
        {
            foreach (var device in Devices.Where(d => d.Id.Contains("Usuario")))
            {
                await SendDeviceToCloudMessagesAsync(device);
            }
        }

        public async Task SendMessageFromAllUsers(int deviceIndex, int devicesCount)
        {
            var devices = Devices.Where(d => d.Id.Contains("Usuario")).Skip(deviceIndex).Take(devicesCount);
            foreach (var device in devices)
            {
                await SendDeviceToCloudMessagesAsync(device);
            }
        }

        public async Task SendMessageFromAllDevices(int intervalInSeconds, int durationInMinutes)
        {
            if (intervalInSeconds < 1 || intervalInSeconds > durationInMinutes * 60)
            {
                return;
            }

            var cycles = durationInMinutes * 60 / intervalInSeconds;

            Console.WriteLine($"Comienzo de envío a intervalos de {intervalInSeconds}s durante {durationInMinutes} minutos.");

            foreach (var i in Enumerable.Range(1, cycles))
            {
                Console.WriteLine($"{DateTime.Now} > Ciclo {i} de {cycles}.");
                await SendMessageFromAllUsers();
                await SendMessageFromAllEstancias();
                Console.WriteLine($"{DateTime.Now} > Fin de ciclo {i}.");
                await Task.Delay(intervalInSeconds * 1000);
            }
        }

        public async Task SendMessageFromAllDevicesRanged(int intervalInSeconds, int durationInMinutes, int initialDeviceIndex, int devicesCount, bool includeEstancias)
        {
            if (intervalInSeconds < 1 || intervalInSeconds > durationInMinutes * 60)
            {
                return;
            }

            var cycles = durationInMinutes * 60 / intervalInSeconds;

            Console.WriteLine($"Comienzo de envío a intervalos de {intervalInSeconds}s durante {durationInMinutes} minutos.");

            foreach (var i in Enumerable.Range(1, cycles))
            {
                Console.WriteLine($"{DateTime.Now} > Ciclo {i} de {cycles}.");
                await SendMessageFromAllUsers(initialDeviceIndex, devicesCount);
                if (includeEstancias)
                {
                    await SendMessageFromAllEstancias();
                }
                Console.WriteLine($"{DateTime.Now} > Fin de ciclo {i}.");
                await Task.Delay(intervalInSeconds * 1000);
            }
        }

        private int GetCurrentHeartRate(Random rand)
        {
            var prob = rand.NextDouble();
            if (prob > 0.2)
            {
                return (int)(rand.NextDouble() * (thresholdHeartRate - minHeartRate)) + minHeartRate;
            }
            else
            {
                return (int)(rand.NextDouble() * (maxHeartRate - thresholdHeartRate)) + thresholdHeartRate;
            }
        }

        private double GetUserCurrentTemperature(Random rand)
        {
            var prob = rand.NextDouble();
            if (prob < 0.8)
            {
                return Math.Round((rand.NextDouble() * (thresholdUserTemperature - minUserTemperature)) + minUserTemperature, 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                return Math.Round((rand.NextDouble() * (maxUserTemperature - thresholdUserTemperature)) + thresholdUserTemperature, 1, MidpointRounding.AwayFromZero);
            }
        }


        private static async Task SendCloudToDeviceMessageAsync(Device device)
        {
            var commandMessage = new
             Microsoft.Azure.Devices.Message(Encoding.ASCII.GetBytes($"Mensaje de: {device.Id}"));

            var connectionString = "HostName=eira-iot-hub.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey=L0xJ0avSPdQxZ+k6dTjbkNmYY/md1BvX+wq9PLGb5To=";
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);

            await serviceClient.SendAsync(device.Id, commandMessage);

            serviceClient.Dispose();
        }

        private async Task AddDeviceAsync(string deviceId)
        {
            Device device;
            try
            {
                Console.WriteLine("New device:");

                var d = new Device(deviceId);

                device = await _registryManager.AddDeviceAsync(d);
            }
            catch (DeviceAlreadyExistsException)
            {
                Console.WriteLine("Already existing device:");
                device = await _registryManager.GetDeviceAsync(deviceId);
            }

            Console.WriteLine("Generated device key: {0}",
            device.Authentication.SymmetricKey.PrimaryKey);
        }

        private IEnumerable<Facility> GetEstanciasFromJson()
        {
            var path = Directory.GetCurrentDirectory();

            using StreamReader reader = new StreamReader(path + "/Estancias.json");
            var facilities = JsonSerializer.Deserialize<IEnumerable<Facility>>(reader.ReadToEndAsync().Result);
            return facilities;
        }

        private (double x, double y) GetRandomPoint(Facility estancia)
        {
            return (rand.Next(0, 1) == 1)
                ? GetRandomPointInsideTriangle(new PointF((float)estancia.TopLeftX, (float)estancia.TopLeftY), new PointF((float)estancia.TopRightX, (float)estancia.TopRightY), new PointF((float)estancia.BottomLeftX, (float)estancia.BottomLeftY))
                : GetRandomPointInsideTriangle(new PointF((float)estancia.BottomRightX, (float)estancia.BottomRightY), new PointF((float)estancia.TopRightX, (float)estancia.TopRightY), new PointF((float)estancia.BottomLeftX, (float)estancia.BottomLeftY));
        }

        private (double x, double y) GetRandomPointInsideTriangle(PointF A, PointF B, PointF C)
        {
            var p = rand.NextDouble();
            var q = rand.NextDouble();

            if (p + q > 1)
            {
                p = 1 - p;
                q = 1 - q;
            }

            // A + AB * p + BC * q
            var x = A.X + (B.X - A.X) * p + (C.X - A.X) * q;
            var y = A.Y + (B.Y - A.Y) * p + (C.Y - A.Y) * q;

            return (x, y);
        }
    }
}