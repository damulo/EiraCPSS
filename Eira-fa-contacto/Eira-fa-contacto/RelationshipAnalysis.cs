using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Eira_fa_contacto
{
    public static class RelationshipAnalysis
    {
        [FunctionName("RelationshipAnalysis")]
        public static async Task Run([EventHubTrigger("hub-dt-relationship-updated", ConsumerGroup = "$Default", Connection = "relationshipConnectionString")] EventData[] events,
            [EventHub("hub-contacto", Connection = "contactoConnectionString")]IAsyncCollector<Relation> outputRelations, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    var relacion = new Relation();
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    var data = JsonConvert.DeserializeObject<dynamic>(messageBody);
                    var type = eventData.Properties["cloudEvents:type"].ToString();
                    var subject = eventData.Properties["cloudEvents:subject"].ToString();

                    relacion.SourceId = subject.Split('/')[2].Split('_')[0];
                    relacion.TargetId = subject.Split('/')[2].Split('_')[1];
                    relacion.RelationshipType = type;
                    if(type == "Microsoft.DigitalTwins.Relationship.Create" || type == "Microsoft.DigitalTwins.Relationship.Delete")
                    {
                        relacion.Distance = (double)data.Distance;
                    }
                    else
                    {
                        relacion.Distance = (double)data.patch[0].value.Distance;
                    }
                    log.LogInformation($"Type: {type}");
                    log.LogInformation($"Subject: {subject}");

                    if(relacion.Distance < 3)
                    {
                        await outputRelations.AddAsync(relacion);
                    }

                    //log.LogInformation($"Data: {messageBody}");
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

        public class Relation
        {
            public string SourceId { get; set; }
            public string TargetId { get; set; }
            public double Distance { get; set; }
            public string RelationshipType { get; set; }
        }

    }
}
