using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Eira_fa_positivo
{
    public static class PositivePlanner
    {
        [FunctionName("PositivePlanner")]
        public static async Task Run([EventHubTrigger("hub-positivo", ConsumerGroup = "$Default", Connection = "positivoConnectionString")] EventData[] events,
            [EventHub("hub-contactos-positivo", Connection = "contactosPositivoConnectionString")]IAsyncCollector<string> outputEventHubMessage, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Positive>(messageBody);

                    await outputEventHubMessage.AddAsync(data.TwinId);
                    
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");

                    await Task.Yield();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

        public class Positive
        {
            public string TwinId { get; set; }
            public int HeartRate { get; set; }
            public double Temperature { get; set; }
        }

    }
}
