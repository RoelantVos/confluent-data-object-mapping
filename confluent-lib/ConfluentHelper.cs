using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Confluent.SchemaRegistry;

namespace confluent_lib
{
    /// <summary>
    /// Reusable helper methods available consumers and producers.
    /// </summary>
    public class ConfluentHelper
    {
        /// <summary>
        /// Load and create Confluent Schema Registry configuration from file.
        /// </summary>
        /// <param name="configurationFilePath"></param>
        /// <returns></returns>
        public static async Task<SchemaRegistryConfig> LoadSchemaRegistryConfiguration(string configurationFilePath)
        {
            try
            {
                var localSchemaRegistryConfig = File.ReadAllLines(configurationFilePath)
                    .Where(line => !line.StartsWith("#"))
                    .ToDictionary(
                        line => line.Substring(0, line.IndexOf('=')),
                        line => line.Substring(line.IndexOf('=') + 1));

                var clientConfig = new SchemaRegistryConfig();

                AuthCredentialsSource localBasicAuth = AuthCredentialsSource.UserInfo;

                var localBasicAuthCredentialsSource = localSchemaRegistryConfig["BasicAuthCredentialsSource"];
                if (localBasicAuthCredentialsSource == "USER_INFO")
                {
                    localBasicAuth = AuthCredentialsSource.UserInfo;
                }

                clientConfig.Url = localSchemaRegistryConfig["Url"];
                clientConfig.BasicAuthCredentialsSource = localBasicAuth;
                clientConfig.BasicAuthUserInfo = localSchemaRegistryConfig["BasicAuthUserInfo"];
                clientConfig.MaxCachedSchemas = int.Parse(localSchemaRegistryConfig["MaxCachedSchemas"]);

                return clientConfig;
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    $"An error occured reading the Schema Registry configuration file from '{configurationFilePath}': {e.Message}");
                Environment.Exit(1);

                // Avoid not-all-paths-return-value compiler error.
                return null;
            }
        }

        /// <summary>
        /// Load a Confluent Kafka configuration into memory.
        /// </summary>
        /// <param name="configurationFilePath"></param>
        /// <param name="certificateDirectory"></param>
        /// <returns></returns>
        public static async Task<ClientConfig> LoadKafkaConfiguration(string configurationFilePath, string certificateDirectory)
        {
            try
            {
                var localConfig = File.ReadAllLines(configurationFilePath)
                    .Where(line => !line.StartsWith("#"))
                    .ToDictionary(
                        line => line.Substring(0, line.IndexOf('=')),
                        line => line.Substring(line.IndexOf('=') + 1));

                var clientConfig = new ClientConfig(localConfig);

                // In case a certificate directory is specified.
                if (certificateDirectory != null)
                {
                    clientConfig.SslCaLocation = certificateDirectory;
                }

                return clientConfig;
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occured reading the Confluent configuration file from '{configurationFilePath}': {e.Message}");
                Environment.Exit(1);

                // Avoid not-all-paths-return-value compiler error.
                return null;
            }
        }


        /// <summary>
        /// Awaitable task to delete an array of Kafka topics, if the topic(s) exists.
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static async Task DeleteTopic(string topic, ClientConfig configuration)
        {
            using (var adminClient = new AdminClientBuilder(new AdminClientConfig(configuration)).Build())
            {
                try
                {
                    await adminClient.DeleteTopicsAsync(new[] {topic});
                    Console.WriteLine($"Topic {topic} deleted.");
                }
                catch (DeleteTopicsException ex)
                {
                    if (ex.Results[0].Error.Code != ErrorCode.Local_UnknownTopic)
                    {
                        Console.WriteLine($"An error occured while removing topic {topic}: {ex.Results[0].Error.Reason}");
                    }
                    else
                    {
                        Console.WriteLine($"Topic {topic} did not exists.");
                    }
                }
            }
        }

        /// <summary>
        /// Awaitable task to create a single topic by name, if it does not exist yet.
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="numPartitions"></param>
        /// <param name="replicationFactor"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static async Task CreateTopicIfNotExists(string topic, int numPartitions, short replicationFactor, ClientConfig configuration)
        {
            using (var adminClient = new AdminClientBuilder(configuration).Build())
            {
                try
                {
                    var topicSpecification = new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = numPartitions,
                        ReplicationFactor = replicationFactor
                    };

                    // CreateTopicAsync can take a list, but for here only a single topic is passed as input.
                    await adminClient.CreateTopicsAsync(new List<TopicSpecification> { topicSpecification });
                    Console.WriteLine($"Topic {topic} created.");
                }
                catch (CreateTopicsException ex)
                {
                    if (ex.Results[0].Error.Code != ErrorCode.TopicAlreadyExists)
                    {
                        Console.WriteLine($"An error occured creating topic {topic}: {ex.Results[0].Error.Reason}");
                    }
                    else
                    {
                        Console.WriteLine($"Topic {topic} already exists.");
                    }
                }
            }
        }
    }
}
