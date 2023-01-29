using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Authenticators;
using Microsoft.Azure.Devices;

namespace Eira_fa_calidad_aire
{
    public static class AirQualityC2D
    {
        [FunctionName("AirQualityC2D")]
        public static async Task Run([EventHubTrigger("hub-notificacion-calidad-aire", Connection = "calidadAireNotificacionConnectionString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            var connectionString = "<value-stored-in-an-user-secret>";
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<AirDeficiency>(messageBody);
                    //enviar notificacion

                    var message = $"{data.TwinId} needs to adjust air quality. {data.Parameter}: {data.Value} is a {data.Reason} value.";



                    var commandMessage = new
             Microsoft.Azure.Devices.Message(Encoding.ASCII.GetBytes(message));
                    await serviceClient.SendAsync(data.TwinId, commandMessage);


                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
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
