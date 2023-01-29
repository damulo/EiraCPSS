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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eira_fa_dt
{
    public static class AddTwin
    {
        // The URL of your instance, starting with the protocol (https://)
        private const string adtInstanceUrl = "https://digital-twins-instance20211606162304.api.weu.digitaltwins.azure.net";

        // Your client / app registration ID
        private const string clientId = "<value-stored-in-an-user-secret>";

        // Your tenant / directory ID
        private const string tenantId = "<value-stored-in-an-user-secret>";

        // Your client secret from your app registration
        private const string secret = "<value-stored-in-an-user-secret>";

        [FunctionName("AddTwin")]
        public static async Task Run([EventHubTrigger("hub-dt-added", Connection = "hubDtAddedConnectionString")] EventData[] events, ILogger log)
        {
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            var exceptions = new List<Exception>();
            HttpClient httpClient = new HttpClient();
            // Authenticate with Digital Twins
            var cred = new ClientSecretCredential(tenantId, clientId, secret);
            // var cred = new ManagedIdentityCredential("https://digitaltwins.azure.net"); //  new DefaultAzureCredential(); //
            var client = new DigitalTwinsClient(
                new Uri(adtInstanceUrl),
                cred,
                new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
            log.LogInformation($"ADT service client connection created.");

            foreach (EventData eventData in events)
            {
                try
                {
                    string data = Encoding.UTF8.GetString(eventData.Body.Array);
                    JArray deviceArray = (JArray)JsonConvert.DeserializeObject(data);
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(deviceArray[0].ToString());

                    string iotHubDeviceId = (string)deviceMessage["id"];
                    string deviceId = (string)deviceMessage["data"]["twin"]["deviceId"];

                    var basicTwin = new BasicDigitalTwin
                    {
                        Id = deviceId,
                        // model Id of digital twin
                        Metadata = { ModelId = "dtmi:residencia:Usuario;1" },
                        Contents =
    {
        // digital twin properties
        // default facility: Estancia01
        { "HeartRate", 0 },
        { "Latitude", 0.0 },
        { "Longitude", 0.0 },
        { "Temperature", 0.0 },
        { "IdRoom", "Estancia01" },
    },
                    };

                    Response<BasicDigitalTwin> createDigitalTwinResponse = await client.CreateOrReplaceDigitalTwinAsync(basicTwin.Id, basicTwin);
                    Console.WriteLine($"Created digital twin '{createDigitalTwinResponse.Value.Id}'.");
                    var newTwinRelationshipPayload = new BasicRelationship
                    {
                        Id = "Estancia01_" + basicTwin.Id,
                        SourceId = "Estancia01",
                        TargetId = basicTwin.Id,
                        Name = "contains",
                        Properties =
                            {
                            }
                    };

                    Response<BasicRelationship> createRelationshipResponse = await client.CreateOrReplaceRelationshipAsync("Estancia01", "Estancia01_" + basicTwin.Id, newTwinRelationshipPayload);
                    Console.WriteLine($"Created relationship '{createRelationshipResponse.Value.Id}'.");

                    log.LogInformation($"C# Event Hub trigger function processed a message: {data}");
                    await Task.Yield();
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