using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Eira_functionApp
{
    public static class TwinUpdater
    {
        // The URL of your instance, starting with the protocol (https://)
        const string adtInstanceUrl = "https://digital-twins-instance20211606162304.api.weu.digitaltwins.azure.net"; //Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        // Your client / app registration ID
        const string clientId = "<value-stored-in-an-user-secret>";
        // Your tenant / directory ID
        const string tenantId = "<value-stored-in-an-user-secret>";
        // Your client secret from your app registration
        const string secret = "<value-stored-in-an-user-secret>";

        [FunctionName("TwinUpdater")]
        public static async Task Run([EventHubTrigger("hub-dt-distancia", ConsumerGroup = "$Default", Connection = "hubDistanciaConnectionString")] EventData[] events, ILogger log)
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
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
                    var distanciaInfo = JsonConvert.DeserializeObject<DistanceEvent>(messageBody);

                    var twin1 = await client.GetDigitalTwinAsync<BasicDigitalTwin>(distanciaInfo.Twin1Id);
                    var twin2 = await client.GetDigitalTwinAsync<BasicDigitalTwin>(distanciaInfo.Twin2Id);

                    if (distanciaInfo.Distance > 2 || twin1.Value.Contents["IdRoom"].ToString() != twin2.Value.Contents["IdRoom"].ToString())
                    {
                        await client.DeleteRelationshipAsync(distanciaInfo.Twin1Id, $"{distanciaInfo.Twin1Id}_{distanciaInfo.Twin2Id}", null, token);
                        await client.DeleteRelationshipAsync(distanciaInfo.Twin2Id, $"{distanciaInfo.Twin2Id}_{distanciaInfo.Twin1Id}", null, token);
                    }
                    else
                    {
                        var rel1To2 = new BasicRelationship
                        {
                            Id = $"{distanciaInfo.Twin1Id}_{distanciaInfo.Twin2Id}",
                            SourceId = distanciaInfo.Twin1Id,
                            TargetId = distanciaInfo.Twin2Id,
                            Name = "relate",
                            Properties =
                                {
                                    { "Distance", distanciaInfo.Distance }
                                }
                        };
                        var rel2To1 = new BasicRelationship
                        {
                            Id = $"{distanciaInfo.Twin2Id}_{distanciaInfo.Twin1Id}",
                            SourceId = distanciaInfo.Twin2Id,
                            TargetId = distanciaInfo.Twin1Id,
                            Name = "relate",
                            Properties =
                                {
                                    { "Distance", distanciaInfo.Distance }
                                }
                        };
                        await client.CreateOrReplaceRelationshipAsync(distanciaInfo.Twin1Id, $"{distanciaInfo.Twin1Id}_{distanciaInfo.Twin2Id}", rel1To2);
                        await client.CreateOrReplaceRelationshipAsync(distanciaInfo.Twin2Id, $"{distanciaInfo.Twin2Id}_{distanciaInfo.Twin1Id}", rel2To1);
                    }

                    // Replace these two lines with your processing logic.

                    await Task.Yield();
                }
                catch(RequestFailedException rfe)
                {

                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                    log.LogError(e, $"Exception type: {e.GetType()}");
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

        public class DistanceEvent
        {
            public string Twin1Id { get; set; }
            public string Twin2Id { get; set; }
            public double Distance { get; set; }
        }

    }
}