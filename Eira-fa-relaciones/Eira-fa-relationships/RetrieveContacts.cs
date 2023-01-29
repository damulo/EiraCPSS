using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace Eira_fa_relationships
{
    public static class RetrieveContacts
    {
        [FunctionName("RetrieveContacts")]
        public static async Task Run([EventHubTrigger("hub-contactos-positivo", ConsumerGroup = "$Default", Connection = "contactosPositivoConnectionString")] EventData[] events,
            [EventHub("hub-contactos-notificar-positivo", Connection = "contactosNotificarPositivoConnectionString")]IAsyncCollector<string> outputEventHubMessage, ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string twinId = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);

                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {twinId}");

                    var str = Environment.GetEnvironmentVariable("sqldb_connection");
                    using (SqlConnection conn = new SqlConnection(str))
                    {
                        conn.Open();
                        var text = $"SELECT TargetId FROM Relationships WHERE SourceId = '{twinId}' AND Timestamp BETWEEN '{DateTime.UtcNow.AddDays(-5)}' AND '{DateTime.UtcNow}';";
                        log.LogInformation(text);
                        using (SqlCommand cmd = new SqlCommand(text, conn))
                        {
                            // Execute the command and log the # rows affected.
                            SqlDataReader contactos = cmd.ExecuteReader();
                            while (contactos.Read())
                            {
                                await outputEventHubMessage.AddAsync(contactos["TargetId"].ToString());
                            }
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

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
