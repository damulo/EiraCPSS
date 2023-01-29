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

namespace Eira_fa_contacto
{
    public static class PutMaskNotifier
    {
        [FunctionName("PutMaskNotifier")]
        public static async Task Run([EventHubTrigger("hub-distancia2m", Connection = "distancia2mConnectionString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    //enviar notificacion
                    RestClient client = new RestClient();
                    client.BaseUrl = new Uri("https://api.mailgun.net/v3");
                    client.Authenticator =
                        new HttpBasicAuthenticator("api",
                                                    "<value-stored-in-an-user-secret>");
                    RestRequest request = new RestRequest();
                    request.AddParameter("domain", "sandbox159840cb96ac40aeaeb3399f67dff946.mailgun.org", ParameterType.UrlSegment);
                    request.Resource = "{domain}/messages";
                    request.AddParameter("from", "eira_cpss@sandbox159840cb96ac40aeaeb3399f67dff946.mailgun.org");
                    request.AddParameter("to", "dawidh.ml@gmail.com");
                    request.AddParameter("subject", "Hello");
                    request.AddParameter("template", "template_mascarilla");
                    request.Method = Method.POST;
                    
                    // commented for evaluation
                    // var response = client.Execute(request);

                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Notificaci�n enviada: {messageBody}");




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
    }
}
