using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eira_fa_dt
{
    public static class GetTwins
    {
        // The URL of your instance, starting with the protocol (https://)
        private const string adtInstanceUrl = "https://digital-twins-instance20211606162304.api.weu.digitaltwins.azure.net"; //Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        // Your client / app registration ID
        private const string clientId = "<value-stored-in-an-user-secret>";

        // Your tenant / directory ID
        private const string tenantId = "<value-stored-in-an-user-secret>";

        // Your client secret from your app registration
        private const string secret = "<value-stored-in-an-user-secret>";
        private static List<string> estancias = new List<string> { "Estancia01", "Estancia02", "Estancia03", "Estancia04", "Estancia05", "Estancia06", "Estancia07", "Estancia08", "Estancia09", "Estancia10", "Estancia11", "Estancia12" };

        private static bool IsContainedInsidePolygon((double X, double Y)[] estancia, (double X, double Y) posicion)
        {
            bool result = false;
            int j = estancia.Length - 1;
            for (int i = 0; i < estancia.Length; i++)
            {
                if (estancia[i].Y < posicion.Y && estancia[j].Y >= posicion.Y || estancia[j].Y < posicion.Y && estancia[i].Y >= posicion.Y)
                {
                    if (estancia[i].X + (posicion.Y - estancia[i].Y) / (estancia[j].Y - estancia[i].Y) * (estancia[j].X - estancia[i].X) < posicion.X)
                    {
                        result = !result;
                    }
                }
                j = i;
            }
            return result;
        }

        [FunctionName("GetTwins")]
        public static async Task Run([EventHubTrigger("hub-dt-updated", ConsumerGroup = "$Default", Connection = "connectionStringUpdated")] EventData[] events,
            [EventHub("hub-dt-gemelos", Connection = "hubGemelosConnectionString")] IAsyncCollector<Twins> outputEventHubMessage, ILogger log)
        {
            var exceptions = new List<Exception>();
            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            HttpClient httpClient = new HttpClient();
            // Authenticate with Digital Twins
            var cred = new ClientSecretCredential(tenantId, clientId, secret);
            // var cred = new ManagedIdentityCredential("https://digitaltwins.azure.net"); //  new DefaultAzureCredential(); //
            var client = new DigitalTwinsClient(
                new Uri(adtInstanceUrl),
                cred,
                new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
            log.LogInformation($"ADT service client connection created.");
            var startTime = DateTime.UtcNow;
            log.LogInformation($"GetTwins execution initiated at {startTime}.");

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");

                    string twinId = eventData.Properties["cloudEvents:subject"].ToString();

                    var twinResponse = client.GetDigitalTwin<BasicDigitalTwin>(twinId);
                    var twin = twinResponse.Value;
                    string idEstancia = twin.Contents["IdRoom"].ToString();

                    var twins = new Twins
                    {
                        Twin1Id = twinId,
                        Latitude1 = Convert.ToDouble(twin.Contents["Latitude"].ToString(), CultureInfo.InvariantCulture),
                        Longitude1 = Convert.ToDouble(twin.Contents["Longitude"].ToString(), CultureInfo.InvariantCulture)
                    };

                    // crear relaci�n con nueva estancia

                    //get incoming relationships
                    
                    var incomingRelationships = client.GetIncomingRelationshipsAsync(twinId, token);
                    string sourceId = "";
                    string relationshipId = "";

                    await foreach (IncomingRelationship rel in incomingRelationships)
                    {
                        if (rel.RelationshipName == "contains")
                        {
                            sourceId = rel.SourceId;
                            relationshipId = rel.RelationshipId;
                            log.LogInformation($"Found faciity of {twinId}: {sourceId}");
                            break;
                        }
                    }
                    if (String.IsNullOrEmpty(sourceId))
                    {
                        return;
                    }

                    if (sourceId != idEstancia)
                    {
                        // Usuario moved to another Estancia

                        // deletes relationship with Estancia
                        await client.DeleteRelationshipAsync(sourceId, relationshipId);

                        AsyncPageable<IncomingRelationship> userIncomingRelationships = client.GetIncomingRelationshipsAsync(twinId, token);

                        await foreach (IncomingRelationship incomingRelationship in userIncomingRelationships)
                        {
                            log.LogInformation($"Found an incoming relationship '{incomingRelationship.RelationshipId}' from '{incomingRelationship.SourceId}'.");
                            try
                            {
                                await client.DeleteRelationshipAsync(incomingRelationship.SourceId, incomingRelationship.RelationshipId);
                            }
                            catch (Exception ex) { }
                            try
                            {
                                await client.DeleteRelationshipAsync(twinId, $"{twinId}_{incomingRelationship.SourceId}");
                            }catch (Exception ex) { }
                        }

                        // creates new Estancia relationship
                        var relContains = new BasicRelationship
                        {
                            Id = $"{idEstancia}_{twinId}",
                            SourceId = idEstancia,
                            TargetId = twinId,
                            Name = "contains"
                        };

                        await client.CreateOrReplaceRelationshipAsync<BasicRelationship>(idEstancia, relContains.Id, relContains);
                        log.LogInformation($"Relationship created '{relContains.Id}' from '{relContains.SourceId}' to {relContains.TargetId}.");
                    }

                    var relationships = client.GetRelationshipsAsync<BasicRelationship>(idEstancia, "contains", token);
                    await foreach (BasicRelationship rel in relationships)
                    {
                        if (rel.TargetId != twinId)
                        {
                            var response = client.GetDigitalTwin<BasicDigitalTwin>(rel.TargetId);
                            var twinValue = response.Value;
                            twins.Twin2Id = rel.TargetId;
                            twins.Latitude2 = Convert.ToDouble(twinValue.Contents["Latitude"].ToString(), CultureInfo.InvariantCulture);
                            twins.Longitude2 = Convert.ToDouble(twinValue.Contents["Longitude"].ToString(), CultureInfo.InvariantCulture);

                            await outputEventHubMessage.AddAsync(twins, token);
                        }
                    }
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            log.LogInformation($"GetTwins execution finished at {DateTime.UtcNow}. Time elapsed: {(DateTime.UtcNow - startTime).Seconds} seconds.");
            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)

                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
            
        }

    public class Twins
    {
        public string Twin1Id { get; set; }
        public double Latitude1 { get; set; }
        public double Longitude1 { get; set; }
        public string Twin2Id { get; set; }
        public double Latitude2 { get; set; }
        public double Longitude2 { get; set; }
    }
}
}