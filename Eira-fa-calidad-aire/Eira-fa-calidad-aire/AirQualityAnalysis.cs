using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Eira_fa_calidad_aire
{
    public static class AirQualityAnalysis
    {
        [FunctionName("AirQualityAnalysis")]
        public static async Task Run([EventHubTrigger("hub-dt-estancia-updated", ConsumerGroup = "$Default", Connection = "estanciaUpdatedConnectionString")] EventData[] events,
            [EventHub("hub-calidad-aire", Connection = "calidadAireConnectionString")]IAsyncCollector<AirDeficiency> outputEventHubMessage, ILogger log)
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
                    double co2Level = Convert.ToDouble(data.patch[2].value, CultureInfo.InvariantCulture);
                    double humidity = Convert.ToDouble(data.patch[1].value, CultureInfo.InvariantCulture);
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");

                    if(temp > 25)
                    {
                        await outputEventHubMessage.AddAsync(new AirDeficiency
                        {
                            TwinId = twinId,
                            Parameter = "Temperature",
                            Reason = "High",
                            Value= temp
                        });
                    }
                    else if(temp < 23)
                    {
                        await outputEventHubMessage.AddAsync(new AirDeficiency
                        {
                            TwinId = twinId,
                            Parameter = "Temperature",
                            Reason = "Low",
                            Value = temp
                        });
                    }

                    if(humidity > 70)
                    {
                        await outputEventHubMessage.AddAsync(new AirDeficiency
                        {
                            TwinId = twinId,
                            Parameter = "Humidity",
                            Reason = "High",
                            Value = humidity
                        });
                    }
                    else if(humidity < 30)
                    {
                        await outputEventHubMessage.AddAsync(new AirDeficiency
                        {
                            TwinId = twinId,
                            Parameter = "Humidity",
                            Reason = "Low",
                            Value = humidity
                        });
                    }
                    if(co2Level > 500)
                    {
                        await outputEventHubMessage.AddAsync(new AirDeficiency
                        {
                            TwinId = twinId,
                            Parameter = "CO2Level",
                            Reason = "High",
                            Value = co2Level
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

        public class AirDeficiency
        {
            public string TwinId { get; set; }
            public string Parameter { get; set; }
            public string Reason { get; set; }
            public double Value { get; set; }
        }

    }
}
