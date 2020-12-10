using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using confluent_lib;
using DataWarehouseAutomation;
using Newtonsoft.Json;


namespace confluent_producer
{


    class LocalSystemPerformance
    {
        public float cpuPercentage { get; set; }
        public float memPercentage { get; set; }

        public LocalSystemPerformance()
        {
            var localPercentage = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            localPercentage.NextValue();

            this.cpuPercentage = localPercentage.NextValue();
            this.memPercentage = new PerformanceCounter("Memory", "Available MBytes").NextValue();
        }
    }
    

    


    class Program
    {
        /// <summary>
        /// Generic parameters reusable throughout the code, set in the Main() task.
        /// </summary>
        internal static class GlobalParameters
        {
            // Kafka configuration settings
            internal static ClientConfig clientConfig { get; set; }
            internal static SchemaRegistryConfig schemaRegistryConfig { get; set; }

            // Array of topics to work with in this example
            internal static string[] topics { get; } = new string[] { "dataObjectMappings", "systemPerformance" };
        }

        static async Task Main()
        {
            // Setting up the configuration for the Kafka client and Schema registry, saved in a local file
            GlobalParameters.clientConfig = await ConfluentHelper.LoadKafkaConfiguration(@"C:\Github\confluent-configuration.txt", null);
            GlobalParameters.schemaRegistryConfig = await ConfluentHelper.LoadSchemaRegistryConfiguration(@"C:\Github\schemaregistry-configuration.txt");

            // Clear topic, if existing (reset environment)
            await ConfluentHelper.DeleteTopic(GlobalParameters.topics[0], GlobalParameters.clientConfig);

            // Create topic, if not existing yet
            await ConfluentHelper.CreateTopicIfNotExists(GlobalParameters.topics[0], 1, 3, GlobalParameters.clientConfig);
            await ConfluentHelper.CreateTopicIfNotExists(GlobalParameters.topics[1], 1, 3, GlobalParameters.clientConfig);

            // Start a file watcher to monitor input directory for mapping Json files
            // Event handles on file detection trigger the publishing actions
            WatchForFiles(AppDomain.CurrentDomain.BaseDirectory + @"\examples-publication");


            for (int i = 0; i < 100; i++)
            {
                var bla = new LocalSystemPerformance();
                await PublishEventSystemPerformance(bla, GlobalParameters.schemaRegistryConfig, GlobalParameters.clientConfig, GlobalParameters.topics);
            }


            // Start waiting until Escape is pressed
            Console.WriteLine();
            Console.WriteLine("Press ESC to quit.");
            do
            {
                while (!Console.KeyAvailable)
                {
                    // Wait for anything to happen in the designated directory for the file watcher
                }
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);

            // END OF APPLICATION
        }



        private static void WatchForFiles(string publicationPath)
        {

            if (!Directory.Exists(publicationPath))
            {
                Directory.CreateDirectory(publicationPath);
            }

            // Object initialiser
            FileSystemWatcher fileSystemWatcher = new FileSystemWatcher
            {

                //Path = @"D:\Git_Repositories\roelant-confluent\examples_publication",
                Path = publicationPath,
                Filter = "*.json",
                EnableRaisingEvents = true
            };

            // Event handles for the file watcher
            fileSystemWatcher.Created += FileSystemWatcher_Created;
            fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
            fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;

            Console.Write($"Listening for new or updated files in {publicationPath}.");
        }

        private static async void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            Console.WriteLine($"The file {e.OldName} has been renamed to {e.Name}.");
            // Do nothing
        }

        private static async void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"The file {e.Name} has been deleted.");
            // Do nothing
        }

        private static async void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"The file {e.Name} has been updated.");
            await DeserializeMappingFile(e);
        }

        private static async void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"A new file {e.Name} has been detected.");
            await DeserializeMappingFile(e);
        }

        private static async Task DeserializeMappingFile(FileSystemEventArgs e)
        {
            var jsonInput = File.ReadAllText(e.FullPath);
            DataObjectMappingList deserialisedMapping = JsonConvert.DeserializeObject<DataObjectMappingList>(jsonInput);

            foreach (DataObjectMapping individualMapping in deserialisedMapping.dataObjectMappingList)
            {
                await PublishEvent(individualMapping, GlobalParameters.schemaRegistryConfig, GlobalParameters.clientConfig, GlobalParameters.topics);
            }
        }

        public static async Task PublishEvent(DataObjectMapping dataObjectMapping, SchemaRegistryConfig schemaRegistryConfig, ClientConfig kafkaConfig, string[] topics)
        {
            using (var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig))
            {
                //using (var producer = new Producer<string, string>(config, new StringSerializer(Encoding.UTF8), new StringSerializer(Encoding.UTF8)))

                // Produce events to the topic
                var producer = new ProducerBuilder<string, DataObjectMapping>(kafkaConfig)
                    .SetValueSerializer(new JsonSerializer<DataObjectMapping>(schemaRegistry, new JsonSerializerConfig { BufferBytes = 100 }))
                    .Build();

                var localMessage = new Message<string, DataObjectMapping>();
                localMessage.Key = "DataObjectMapping";
                localMessage.Value = dataObjectMapping;

                // Synchronous producer, does not work with Json serialisation
                //producer.Produce(topics[0], localMessage, SyncHandler);

                // Create asynchronous task (and wait for it)
                var delivery = producer.ProduceAsync(topics[0], localMessage);
                await delivery.ContinueWith(AsyncHandlerDataObjectMapping);

                producer.Flush(TimeSpan.FromSeconds(10));
            }

            return;
        }


        public static async Task PublishEventSystemPerformance(LocalSystemPerformance input, SchemaRegistryConfig schemaRegistryConfig, ClientConfig kafkaConfig, string[] topics)
        {
            using (var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig))
            {
                //using (var producer = new Producer<string, string>(config, new StringSerializer(Encoding.UTF8), new StringSerializer(Encoding.UTF8)))

                // Produce events to the topic
                var producer = new ProducerBuilder<string, LocalSystemPerformance>(kafkaConfig)
                    .SetValueSerializer(new JsonSerializer<LocalSystemPerformance>(schemaRegistry, new JsonSerializerConfig { BufferBytes = 100 }))
                    .Build();

                var localMessage = new Message<string, LocalSystemPerformance>();
                localMessage.Key = "LocalSystemPerformance";
                localMessage.Value = input;

                // Synchronous producer, does not work with Json serialisation
                //producer.Produce(topics[0], localMessage, SyncHandler);

                // Create asynchronous task (and wait for it)
                var delivery = producer.ProduceAsync(topics[1], localMessage);
                await delivery.ContinueWith(AsyncHandlerLocalSystemPerformance);

                producer.Flush(TimeSpan.FromSeconds(10));
            }

            return;
        }



        // Delegate function for the result handling of the publication (delivery report).
        public static void SyncHandler(DeliveryReport<string, DataObjectMapping> inputDeliveryReport)
        {
            if (inputDeliveryReport.Error.Code != ErrorCode.NoError)
            {
                Console.WriteLine($"Failed to deliver message: {inputDeliveryReport.Error.Reason}");
            }
            else
            {
                Console.WriteLine($"Produced message {inputDeliveryReport.Value} to topic {inputDeliveryReport.Topic} for partition {inputDeliveryReport.Partition} and offset {inputDeliveryReport.Offset}");
            }
        }

        // Local function for result handling (DeliveryResult) in asynchronous mode.
        public static void AsyncHandlerDataObjectMapping(Task<DeliveryResult<string, DataObjectMapping>> inputDeliveryResult)
        {
            if (inputDeliveryResult.IsFaulted)
            {
                Console.WriteLine($"Failed to deliver message: {inputDeliveryResult.Result.Key}");
            }
            else
            {
                Console.WriteLine(
                    $"Produced message {inputDeliveryResult.Result.Value.sourceDataObject.name}-{inputDeliveryResult.Result.Value.targetDataObject.name} to topic {inputDeliveryResult.Result.Topic} for partition {inputDeliveryResult.Result.Partition} and offset {inputDeliveryResult.Result.Offset}");
            }
        }

        // Local function for result handling (DeliveryResult) in asynchronous mode.
        public static void AsyncHandlerLocalSystemPerformance(Task<DeliveryResult<string, LocalSystemPerformance>> input)
        {
            if (input.IsFaulted)
            {
                Console.WriteLine($"Failed to deliver message: {input.Result.Key}");
            }
            else
            {
                Console.WriteLine(
                    $"Produced message {input.Result.Value} to topic {input.Result.Topic} for partition {input.Result.Partition} and offset {input.Result.Offset}");
            }
        }

    }
}
