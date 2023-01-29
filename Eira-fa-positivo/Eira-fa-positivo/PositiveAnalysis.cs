using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Eira_fa_positivo
{
    public static class PositiveAnalysis
    {
        [FunctionName("PositiveAnalysis")]
        public static async Task Run([EventHubTrigger("hub-dt-updated", ConsumerGroup = "$Default", Connection = "connectionStringUpdated")] EventData[] events,
            [EventHub("hub-positivo", Connection = "positivoConnectionString")]IAsyncCollector<Positive> outputEventHubMessage, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(messageBody);
                    string twinId = eventData.Properties["cloudEvents:subject"].ToString();
                    double temp = Convert.ToDouble(data.patch[0].value, CultureInfo.InvariantCulture);
                    int hr = Convert.ToInt32(data.patch[1].value, CultureInfo.InvariantCulture);
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {twinId}-> TEMP: {temp}ºC, HR:{hr}bpm.");

                    if(temp > 37.5 && hr > 90)
                    {
                        log.LogInformation($"Positive inferred for user: {twinId}");
                        await outputEventHubMessage.AddAsync(new Positive
                        {
                            TwinId = twinId,
                            HeartRate = hr,
                            Temperature = temp
                        });
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

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

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
