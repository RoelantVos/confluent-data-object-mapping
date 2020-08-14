using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.SyncOverAsync;
using Confluent.SchemaRegistry.Serdes;
using confluent_lib;
using DataWarehouseAutomation;

namespace confluent_consumer
{
    class Program
    {
        static async Task Main()
        {
            // Setting up the configurations, saved in a local file
            ClientConfig kafkaConfig = await ConfluentHelper.LoadKafkaConfiguration(@"D:\Git_Repositories\confluent-configuration.txt", null);
            var consumerConfig = new ConsumerConfig(kafkaConfig)
            {
                GroupId = "data-object-mapping-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                EnablePartitionEof = true
            };

            // Array of topics to work with
            string[] topics = new string[] {"dataObjectMappings"};

            // Cancellation input key (token)
            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // prevent the process from terminating.
                cts.Cancel();
            };

            using (var consumer = new ConsumerBuilder<string, DataObjectMapping>(consumerConfig)
                .SetValueDeserializer(new JsonDeserializer<DataObjectMapping>().AsSyncOverAsync())
                .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
                .SetStatisticsHandler((_, json) => Console.WriteLine($"Statistics: {json}"))
                .SetPartitionsAssignedHandler((c, partitions) =>
                {
                    Console.WriteLine($"Assigned partitions: [{string.Join(", ", partitions)}]");
                    // possibly manually specify start offsets or override the partition assignment provided by
                    // the consumer group by returning a list of topic/partition/offsets to assign to, e.g.:
                    // 
                    //return partitions.Select(tp => new TopicPartitionOffset(tp, externalOffsets[tp]));
                })
                .SetPartitionsRevokedHandler((c, partitions) =>
                {
                    Console.WriteLine($"Revoking assignment: [{string.Join(", ", partitions)}]");
                })
                .Build())
            {
                consumer.Subscribe(topics);
                

                // Manual assignment (not automatic subscribe)
                //var partitionList = topics.Select(topic => new TopicPartitionOffset(topic, 0, Offset.Beginning)).ToList();
                //consumer.Assign(partitionList);

                try
                {
                    while (true)
                    {
                        try
                        {
                            var consumeResult = consumer.Consume(cts.Token);
                            // Note: End of partition notification has not been enabled, so
                            // it is guaranteed that the ConsumeResult instance corresponds
                            // to a Message, and not a PartitionEOF event.
                            if (consumeResult.IsPartitionEOF)
                            {
                                Console.WriteLine($"Reached end of topic {consumeResult.Topic}, partition {consumeResult.Partition}, offset {consumeResult.Offset}.");

                                continue;
                            }

                            Console.WriteLine($"Received message at {consumeResult.TopicPartitionOffset}: ${consumeResult.Message.Value.sourceDataObject.name}");
                        }
                        catch (ConsumeException e)
                        {
                            Console.WriteLine($"Consume error: {e.Error.Reason}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Closing consumer.");
                    consumer.Close();
                }
            }

            Console.WriteLine("Press any key to stop the application.");
            Console.ReadKey();
        }
    }
}
