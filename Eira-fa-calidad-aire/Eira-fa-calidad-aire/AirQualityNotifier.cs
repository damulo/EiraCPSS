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
    public static class AirQualityNotifier
    {
        [FunctionName("AirQualityNotifier")]
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
                    RestClient client = new RestClient
                    {
                        BaseUrl = new Uri("https://api.mailgun.net/v3"),
                        Authenticator =
                        new HttpBasicAuthenticator("api",
                                                    "<value-stored-in-an-user-secret>")
                    };
                    RestRequest request = new RestRequest();
                    request.AddParameter("domain", "sandbox159840cb96ac40aeaeb3399f67dff946.mailgun.org", ParameterType.UrlSegment);
                    request.Resource = "{domain}/messages";
                    request.AddParameter("from", "eira_cpss@sandbox159840cb96ac40aeaeb3399f67dff946.mailgun.org");
                    request.AddParameter("to", "dawidh.ml@gmail.com");
                    request.AddParameter("subject", $"{data.TwinId} needs to adjust air quality");
                    request.AddParameter("text", $"{data.Parameter}: {data.Value} is a {data.Reason} value.");
                    request.Method = Method.POST;
                    
                  // commented for evaluation
                  // var response = client.Execute(request);                  

                    var message = $"{data.TwinId} needs to adjust air quality. {data.Parameter}: {data.Value} is a {data.Reason} value.";

                    var commandMessage = new
             Microsoft.Azure.Devices.Message(Encoding.ASCII.GetBytes(message));

                    // send C2D message
                    await serviceClient.SendAsync(data.TwinId, commandMessage);


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

        public class AirDeficiency
        {
            public string TwinId { get; set; }
            public string Parameter { get; set; }
            public string Reason { get; set; }
            public double Value { get; set; }
        }

    }
}
