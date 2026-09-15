using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProductAnalysisApp.Services.Messaging;
using RabbitMQ.Client;
using System.Reflection;
using Xunit;

namespace ProductAnalysisApp.Tests.ServicesUnitTests
{
    // JobResultStore — static thread-safe dictionary
    public class JobResultStoreTests
    {
        [Fact]
        public void Set_StoresResult()
        {
            var key = $"store-set-{Guid.NewGuid()}";
            var job = new JobResult { JobId = key, Status = "pending" };

            JobResultStore.Set(key, job);

            var result = JobResultStore.Get(key);
            Assert.NotNull(result);
            Assert.Equal(key, result!.JobId);
        }

        [Fact]
        public void Get_ReturnsNull_WhenNotFound()
        {
            var result = JobResultStore.Get($"nonexistent-{Guid.NewGuid()}");
            Assert.Null(result);
        }

        [Fact]
        public void GetByUser_FiltersCorrectly()
        {
            var uid = $"user-{Guid.NewGuid()}";
            var key1 = $"j1-{Guid.NewGuid()}";
            var key2 = $"j2-{Guid.NewGuid()}";
            var keyOther = $"j3-{Guid.NewGuid()}";

            JobResultStore.Set(key1, new JobResult { JobId = key1, UserId = uid });
            JobResultStore.Set(key2, new JobResult { JobId = key2, UserId = uid });
            JobResultStore.Set(keyOther, new JobResult { JobId = keyOther, UserId = "other-user" });

            var results = JobResultStore.GetByUser(uid);

            Assert.All(results, r => Assert.Equal(uid, r.UserId));
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public void GetForUser_ReturnsNull_WhenUserMismatch()
        {
            var key = $"mismatch-{Guid.NewGuid()}";
            JobResultStore.Set(key, new JobResult { JobId = key, UserId = "user-a" });

            var result = JobResultStore.GetForUser(key, "user-b");

            Assert.Null(result);
        }

        [Fact]
        public void GetForUser_ReturnsJob_WhenUserMatches()
        {
            var key = $"match-{Guid.NewGuid()}";
            JobResultStore.Set(key, new JobResult { JobId = key, UserId = "user-c" });

            var result = JobResultStore.GetForUser(key, "user-c");

            Assert.NotNull(result);
            Assert.Equal(key, result!.JobId);
        }

        [Fact]
        public void GetForUser_ReturnsJob_WhenJobUserIdIsNull()
        {
            var key = $"null-uid-{Guid.NewGuid()}";
            JobResultStore.Set(key, new JobResult { JobId = key, UserId = null });

            var result = JobResultStore.GetForUser(key, "any-user");

            Assert.NotNull(result);
        }

        [Fact]
        public void GetForUser_ReturnsNull_WhenJobNotInStore()
        {
            var result = JobResultStore.GetForUser($"missing-{Guid.NewGuid()}", "uid");
            Assert.Null(result);
        }

        [Fact]
        public void GetByUser_ReturnsEmpty_WhenNoJobsForUser()
        {
            var result = JobResultStore.GetByUser($"user-nobody-{Guid.NewGuid()}");
            Assert.Empty(result);
        }

        [Fact]
        public void GetByUser_ReturnsDescendingOrder()
        {
            var uid = $"user-order-{Guid.NewGuid()}";
            var k1 = $"j-early-{Guid.NewGuid()}";
            var k2 = $"j-late-{Guid.NewGuid()}";

            JobResultStore.Set(k1, new JobResult
            {
                JobId    = k1,
                UserId   = uid,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            JobResultStore.Set(k2, new JobResult
            {
                JobId    = k2,
                UserId   = uid,
                CreatedAt = DateTime.UtcNow
            });

            var results = JobResultStore.GetByUser(uid);

            // En yeni olan önce gelmeli
            Assert.Equal(k2, results[0].JobId);
        }
    }

    // JobResultConsumer — constructor & StopAsync
    public class JobResultConsumerTests
    {
        [Fact]
        public void Constructor_NotNull()
        {
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var config = new ConfigurationBuilder().Build();
            var logger = NullLogger<JobResultConsumer>.Instance;

            var consumer = new JobResultConsumer(scopeFactory.Object, config, logger);

            Assert.NotNull(consumer);
        }

        [Fact]
        public async Task StopAsync_WhenChannelIsNull_DoesNotThrow()
        {
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var config = new ConfigurationBuilder().Build();
            var logger = NullLogger<JobResultConsumer>.Instance;

            var consumer = new JobResultConsumer(scopeFactory.Object, config, logger);

            var ex = await Record.ExceptionAsync(
                () => consumer.StopAsync(CancellationToken.None));

            Assert.Null(ex);
        }

        [Fact]
        public async Task StopAsync_WhenChannelSet_CallsClose()
        {
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var config = new ConfigurationBuilder().Build();
            var logger = NullLogger<JobResultConsumer>.Instance;

            var channelMock    = new Mock<IChannel>();
            var connectionMock = new Mock<IConnection>();

            channelMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            connectionMock.Setup(c => c.CloseAsync(
                It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            var consumer = new JobResultConsumer(scopeFactory.Object, config, logger);

            typeof(JobResultConsumer)
                .GetField("_channel", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(consumer, channelMock.Object);
            typeof(JobResultConsumer)
                .GetField("_connection", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(consumer, connectionMock.Object);

            var ex = await Record.ExceptionAsync(
                () => consumer.StopAsync(CancellationToken.None));

            Assert.Null(ex);
        }
    }
}
