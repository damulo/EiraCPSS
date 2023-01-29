using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Eira_functionApp
{
    public static class IotIngest
    {
        [FunctionName("IotIngest")]
        public static async Task Run([EventHubTrigger("hub-usuarios", ConsumerGroup = "$Default", Connection = "usuariosConnectionString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            string adtInstanceUrl = "https://digital-twins-instance20211606162304.api.weu.digitaltwins.azure.net"; //Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

            // Your client / app registration ID
            string clientId = "<value-stored-in-an-user-secret>";
            // Your tenant / directory ID
            string tenantId = "<value-stored-in-an-user-secret>";
            // The URL of your instance, starting with the protocol (https://)
            //private static string adtInstanceUrl = "https://<your ADT instance>";
            // Your client secret from your app registration
            string secret = "<value-stored-in-an-user-secret>";

            HttpClient httpClient = new HttpClient();

            // Authenticate with Digital Twins
            var cred = new ClientSecretCredential(tenantId, clientId, secret);
            var client = new DigitalTwinsClient(
                new Uri(adtInstanceUrl),
                cred,
                new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
            log.LogInformation($"ADT service client connection created.");

            foreach (EventData evento in events)
            {
                try
                {
                    string data = Encoding.UTF8.GetString(evento.Body.Array);
                    JArray deviceArray = (JArray)JsonConvert.DeserializeObject(data);
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(deviceArray[0].ToString());

                    string deviceId = (string)deviceMessage["data"]["systemProperties"]["iothub-connection-device-id"];

                    var bodyString = deviceMessage["data"]["body"].ToString();

                    var temperature = (string)deviceMessage["data"]["body"]["Temperature"];
                    var hr = (string)deviceMessage["data"]["body"]["HeartRate"];
                    var lat = (string)deviceMessage["data"]["body"]["Latitude"];
                    var lon = (string)deviceMessage["data"]["body"]["Longitude"];
                    var estancia = (string)deviceMessage["data"]["body"]["IdRoom"];

                    if (!String.IsNullOrEmpty(temperature) || !String.IsNullOrEmpty(hr))
                    {
                        log.LogInformation($"Device:{deviceId} Temperature is:{temperature}, HeartRate is: {hr}, Lat: {lat}, Long: {lon}, Room: {estancia}.");

                        var updateTwinData = new JsonPatchDocument();
                        if (!String.IsNullOrEmpty(temperature))
                            updateTwinData.AppendReplace("/Temperature", Convert.ToDouble(temperature, CultureInfo.InvariantCulture));
                        if (!String.IsNullOrEmpty(hr))
                            updateTwinData.AppendReplace("/HeartRate", Convert.ToDouble(hr, CultureInfo.InvariantCulture));
                        if (!String.IsNullOrEmpty(hr))
                            updateTwinData.AppendReplace("/Latitude", Convert.ToDouble(lat, CultureInfo.InvariantCulture));
                        if (!String.IsNullOrEmpty(hr))
                            updateTwinData.AppendReplace("/Longitude", Convert.ToDouble(lon, CultureInfo.InvariantCulture));
                        if (!String.IsNullOrEmpty(estancia))
                            updateTwinData.AppendReplace("/IdRoom", estancia);

                        await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);

                        log.LogInformation("Twin updated");
                    }
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}