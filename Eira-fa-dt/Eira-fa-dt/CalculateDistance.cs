using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.EventHubs;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Eira_fa_dt
{
    public static class CalculateDistance
    {
        [FunctionName("CalculateDistance")]
        public static async Task Run(
            [EventHubTrigger("hub-dt-gemelos", ConsumerGroup = "$Default", Connection = "hubGemelosConnectionString")] EventData[] events,
            [EventHub("hub-distancia", Connection = "hubDistanciaConnectionString")]IAsyncCollector<DistanceEvent> outputEventHubMessage,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    var twins = JsonConvert.DeserializeObject<Twins>(messageBody);

                    var twin1Id = twins.Twin1Id;
                    var twin2Id = twins.Twin2Id;
                    var lat1 = twins.Latitude1 * (Math.PI / 180.0);
                    var long1 = twins.Longitude1 * (Math.PI / 180.0);
                    var lat2 = twins.Latitude2 * (Math.PI / 180.0);
                    var long2 = twins.Longitude2 * (Math.PI / 180.0) - long1;
                    var d1 = Math.Pow(Math.Sin((lat2 - lat1) / 2.0), 2.0) +
                                 Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(long2 / 2.0), 2.0);
                    var distance = 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d1), Math.Sqrt(1.0 - d1)));
                    log.LogInformation("Distance: " + distance);

                    var evento = new DistanceEvent { Twin1Id = twin1Id, Twin2Id = twin2Id, Distance = distance };
                    await outputEventHubMessage.AddAsync(evento);

                    log.LogInformation("Publishing event: " + JsonConvert.SerializeObject(evento));
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
            //return new OkObjectResult(JsonConvert.SerializeObject(distance));
        }

        public class DistanceEvent
        {
            public string Twin1Id { get; set; }
            public string Twin2Id { get; set; }
            public double Distance { get; set; }
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
