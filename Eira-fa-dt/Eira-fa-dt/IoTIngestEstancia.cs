using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eira_fa_dt
{
    public static class IoTIngestEstancia
    {
        [FunctionName("IoTIngestEstancia")]
        public static async Task Run([EventHubTrigger("hub-estancias", ConsumerGroup = "$Default", Connection = "estanciasConnectionString")] EventData evento, ILogger log)
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

            try
            {
                // Authenticate with Digital Twins
                var cred = new ClientSecretCredential(tenantId, clientId, secret);
                // var cred = new ManagedIdentityCredential("https://digitaltwins.azure.net"); //  new DefaultAzureCredential(); // 
                var client = new DigitalTwinsClient(
                    new Uri(adtInstanceUrl),
                    cred,
                    new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation($"ADT service client connection created.");


                // <Find_device_ID_and_temperature>
                string data = Encoding.UTF8.GetString(evento.Body.Array);
                JArray deviceArray = (JArray)JsonConvert.DeserializeObject(data);
                JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(deviceArray[0].ToString());
                //JObject deviceProperties = (JObject)JsonConvert.DeserializeObject(evento.SystemProperties.ToString());
                //log.LogInformation(deviceMessage.ToString());
                string deviceId = (string)deviceMessage["data"]["systemProperties"]["iothub-connection-device-id"];

                var bodyString = deviceMessage["data"]["body"].ToString();

                var temperature = (string)deviceMessage["data"]["body"]["Temperature"];
                var co2Level = (string)deviceMessage["data"]["body"]["CO2Level"];
                var humidity = (string)deviceMessage["data"]["body"]["Humidity"];     
  
                
                    log.LogInformation($"Device:{deviceId} Temperature is:{temperature}, CO2Level is: {co2Level}, Humidity: {humidity}.");

                    //Response<BasicDigitalTwin> twinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>("Hab01");
                    //var twin = twinResponse.Value;
                    // <Update_twin_with_device_temperature>
                    var updateTwinData = new JsonPatchDocument();
                    if (!String.IsNullOrEmpty(temperature))
                        updateTwinData.AppendReplace("/Temperature", Convert.ToDouble(temperature, CultureInfo.InvariantCulture));
                    if (!String.IsNullOrEmpty(co2Level))
                        updateTwinData.AppendReplace("/Co2Level", Convert.ToDouble(co2Level, CultureInfo.InvariantCulture));
                    if (!String.IsNullOrEmpty(humidity))
                        updateTwinData.AppendReplace("/Humidity", Convert.ToDouble(humidity, CultureInfo.InvariantCulture));
               
                    await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);
                    // </Update_twin_with_device_temperature>
                    log.LogInformation("Twin (estancia) updated");
                
                // string messageBody = Encoding.UTF8.GetString(evento.Body.Array, evento.Body.Offset, evento.Body.Count);

                // Replace these two lines with your processing logic.
                //log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
                //await Task.Yield();
            }
            catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
