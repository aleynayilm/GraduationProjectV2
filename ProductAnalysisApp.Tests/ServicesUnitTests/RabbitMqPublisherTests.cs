using Moq;
using ProductAnalysisApp.Services.Messaging;
using RabbitMQ.Client;
using System.Reflection;
using Xunit;

namespace ProductAnalysisApp.Tests.ServicesUnitTests
{
    public class RabbitMqPublisherTests
    {
        private static RabbitMqPublisher CreatePublisher(IConnection conn, IChannel ch)
        {
            return (RabbitMqPublisher)Activator.CreateInstance(
                typeof(RabbitMqPublisher),
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new object[] { conn, ch },
                null)!;
        }

        private static (Mock<IConnection> conn, Mock<IChannel> ch, RabbitMqPublisher pub) Build()
        {
            var connMock = new Mock<IConnection>();
            var chMock   = new Mock<IChannel>();
            var pub      = CreatePublisher(connMock.Object, chMock.Object);
            return (connMock, chMock, pub);
        }

        // Queue name constants 

        [Fact]
        public void QueueNames_AreCorrect()
        {
            Assert.Equal("job.scrape",          RabbitMqPublisher.QueueScrape);
            Assert.Equal("job.compare",         RabbitMqPublisher.QueueCompare);
            Assert.Equal("job.localllmcompare", RabbitMqPublisher.QueueLocalLlm);
            Assert.Equal("job.cloudllmcompare", RabbitMqPublisher.QueueCloudLlm);
            Assert.Equal("job.chat_local",      RabbitMqPublisher.QueueChatLocal);
            Assert.Equal("job.chat_cloud",      RabbitMqPublisher.QueueChatCloud);
            Assert.Equal("job.result",          RabbitMqPublisher.QueueResult);
            Assert.Equal("job.local_search",    RabbitMqPublisher.QueueLocalSearch);
            Assert.Equal("job.cloud_search",    RabbitMqPublisher.QueueCloudSearch);
        }

        // PublishAsync 

        [Fact]
        public async Task PublishAsync_ReturnsNonEmptyJobId()
        {
            var (_, chMock, pub) = Build();

            chMock.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

            var jobId = await pub.PublishAsync(RabbitMqPublisher.QueueScrape, new { Url = "https://example.com" });

            Assert.False(string.IsNullOrWhiteSpace(jobId));
            Assert.True(Guid.TryParse(jobId, out _));
        }

        [Fact]
        public async Task PublishAsync_CallsBasicPublishAsync()
        {
            var (_, chMock, pub) = Build();

            chMock.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

            await pub.PublishAsync(RabbitMqPublisher.QueueCompare, new { ProductId = "123" });

            chMock.Verify(c => c.BasicPublishAsync(
                "",
                RabbitMqPublisher.QueueCompare,
                false,
                It.IsAny<BasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishAsync_EachCall_ReturnsDifferentJobId()
        {
            var (_, chMock, pub) = Build();

            chMock.Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

            var id1 = await pub.PublishAsync(RabbitMqPublisher.QueueScrape, new { });
            var id2 = await pub.PublishAsync(RabbitMqPublisher.QueueScrape, new { });

            Assert.NotEqual(id1, id2);
        }

        // DisposeAsync 

        [Fact]
        public async Task DisposeAsync_ClosesChannelAndConnection()
        {
            var (connMock, chMock, pub) = Build();

            chMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            connMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await pub.DisposeAsync();

            chMock.Verify(c => c.CloseAsync(
                It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
            connMock.Verify(c => c.CloseAsync(
                It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
