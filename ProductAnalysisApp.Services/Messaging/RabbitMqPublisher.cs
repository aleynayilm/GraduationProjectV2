using ProductAnalysisApp.Services.Contracts;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Messaging
{
    public class RabbitMqPublisher : IAsyncDisposable, IRabbitMqPublisher
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;

        public const string QueueScrape = "job.scrape";
        public const string QueueCompare = "job.compare";
        public const string QueueLocalLlm = "job.localllmcompare";
        public const string QueueCloudLlm = "job.cloudllmcompare";
        public const string QueueChatLocal = "job.chat_local";
        public const string QueueChatCloud = "job.chat_cloud";
        public const string QueueResult = "job.result";
        public const string QueueLocalSearch = "job.local_search";   // Tavily + RAG + Mistral
        public const string QueueCloudSearch = "job.cloud_search";   // Tavily + RAG + Gemini

        private static readonly string[] AllQueues =
        [
            QueueScrape, QueueCompare, QueueLocalLlm, QueueCloudLlm,
            QueueChatLocal, QueueChatCloud, QueueResult,
            QueueLocalSearch, QueueCloudSearch
        ];

        private RabbitMqPublisher(IConnection conn, IChannel ch)
        {
            _connection = conn;
            _channel = ch;
        }

        public static async Task<RabbitMqPublisher> CreateAsync(string hostName = "localhost")
        {
            var factory = new ConnectionFactory { HostName = hostName };
            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            foreach (var q in AllQueues)
                await channel.QueueDeclareAsync(q, durable: true, exclusive: false,
                                                autoDelete: false, arguments: null);

            return new RabbitMqPublisher(connection, channel);
        }

        // RabbitMqPublisher.cs — Services/Messaging
        public async Task<string> PublishAsync(string queueName, object payload)
        {
            var jobId = Guid.NewGuid().ToString();

            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                           JsonSerializer.Serialize(payload))!;
            dict["jobId"] = jobId;

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dict));
            var props = new BasicProperties { Persistent = true };

            await _channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: false,
                basicProperties: props,
                body: body);

            return jobId;
        }

        public async ValueTask DisposeAsync()
        {
            await _channel.CloseAsync();
            await _connection.CloseAsync();
        }
    }
}
