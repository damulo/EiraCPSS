using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Eira_fa_calidad_aire
{
    public static class AirQualityPlanner
    {
        [FunctionName("AirQualityPlanner")]
        public static async Task Run([EventHubTrigger("hub-calidad-aire", ConsumerGroup = "$Default", Connection = "calidadAireConnectionString")] EventData[] events,
            [EventHub("hub-notificacion-calidad-aire", Connection = "calidadAireNotificacionConnectionString")]IAsyncCollector<AirDeficiency> outputEventHubMessage, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<AirDeficiency>(messageBody);
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");

                    await outputEventHubMessage.AddAsync(data);

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

        public class AirDeficiency
        {
            public string TwinId { get; set; }
            public string Parameter { get; set; }
            public string Reason { get; set; }
            public double Value { get; set; }
        }

    }
}
