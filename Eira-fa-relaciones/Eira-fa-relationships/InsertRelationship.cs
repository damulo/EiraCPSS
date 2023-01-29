using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Eira_fa_relationships
{
    public static class InsertRelationship
    {
        [FunctionName("InsertRelationship")]
        public static async Task Run([EventHubTrigger("hub-contacto", ConsumerGroup = "$Default", Connection = "contactoConnectionString")] EventData[] events,
             ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    var data = JsonConvert.DeserializeObject<Relation>(messageBody);
                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");

                    var str = Environment.GetEnvironmentVariable("sqldb_connection");
                    using (SqlConnection conn = new SqlConnection(str))
                    {
                        conn.Open();
                        var text = $"INSERT INTO Relationships VALUES('{data.SourceId}', '{data.TargetId}', {data.Distance}, '{data.RelationshipType}', '{DateTime.UtcNow}');";
                        log.LogInformation(text);
                        using (SqlCommand cmd = new SqlCommand(text, conn))
                        {
                            // Execute the command and log the # rows affected.
                            var rows = await cmd.ExecuteNonQueryAsync();
                            log.LogInformation($"{rows} rows were inserted.");
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

        public class Relation
        {
            public string SourceId { get; set; }
            public string TargetId { get; set; }
            public double Distance { get; set; }
            public string RelationshipType { get; set; }
        }

    }
}
