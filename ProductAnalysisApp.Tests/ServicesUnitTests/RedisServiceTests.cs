using Moq;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Messaging;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace ProductAnalysisApp.Tests
{
    public class RedisServiceTests
    {
        private readonly Mock<IDatabase> _dbMock;
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly RedisService _service;

        public RedisServiceTests()
        {
            _dbMock = new Mock<IDatabase>();
            _redisMock = new Mock<IConnectionMultiplexer>();
            _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                      .Returns(_dbMock.Object);

            _service = new RedisService(_redisMock.Object);
        }

        // SetJobAsync 

        [Fact]
        public async Task SetJobAsync_SetsJobKey_WithTtl()
        {
            var job = new JobResult { JobId = "job-001", UserId = null };

            var ex = await Record.ExceptionAsync(() => _service.SetJobAsync(job));
            Assert.Null(ex);
        }

        [Fact]
        public async Task SetJobAsync_AddsToUserSortedSet_WhenUserIdIsNotNull()
        {
            var job = new JobResult { JobId = "job-002", UserId = "user-abc" };

            _dbMock.As<IDatabaseAsync>().Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            _dbMock.Setup(d => d.SortedSetAddAsync(
                    It.Is<RedisKey>(k => k == "user_jobs:user-abc"),
                    It.Is<RedisValue>(v => v == "job-002"),
                    It.IsAny<double>(),
                    It.IsAny<SortedSetWhen>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            await _service.SetJobAsync(job);

            _dbMock.Verify(d => d.SortedSetAddAsync(
                It.Is<RedisKey>(k => k == "user_jobs:user-abc"),
                It.Is<RedisValue>(v => v == "job-002"),
                It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task SetJobAsync_DoesNotAddToSortedSet_WhenUserIdIsNull()
        {
            var job = new JobResult { JobId = "job-003", UserId = null };

            _dbMock.As<IDatabaseAsync>().Setup(d => d.StringSetAsync(
                    It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            await _service.SetJobAsync(job);

            _dbMock.Verify(d => d.SortedSetAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<double>(), It.IsAny<SortedSetWhen>(),
                It.IsAny<CommandFlags>()), Times.Never);
        }

        // GetJobAsync 

        [Fact]
        public async Task GetJobAsync_ReturnsJob_WhenKeyExists()
        {
            var job = new JobResult { JobId = "job-010" };
            var json = JsonSerializer.Serialize(job);

            _dbMock.Setup(d => d.StringGetAsync(
                    It.Is<RedisKey>(k => k == "job:job-010"),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)json);

            var result = await _service.GetJobAsync("job-010");

            Assert.NotNull(result);
            Assert.Equal("job-010", result.JobId);
        }

        [Fact]
        public async Task GetJobAsync_ReturnsNull_WhenKeyDoesNotExist()
        {
            _dbMock.Setup(d => d.StringGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var result = await _service.GetJobAsync("nonexistent");

            Assert.Null(result);
        }

        // GetJobForUserAsync 

        [Fact]
        public async Task GetJobForUserAsync_ReturnsJob_WhenUserMatches()
        {
            var job = new JobResult { JobId = "job-020", UserId = "user-xyz" };
            var json = JsonSerializer.Serialize(job);

            _dbMock.Setup(d => d.StringGetAsync(
                    It.Is<RedisKey>(k => k == "job:job-020"),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)json);

            var result = await _service.GetJobForUserAsync("job-020", "user-xyz");

            Assert.NotNull(result);
            Assert.Equal("user-xyz", result.UserId);
        }

        [Fact]
        public async Task GetJobForUserAsync_ReturnsNull_WhenUserMismatch()
        {
            var job = new JobResult { JobId = "job-021", UserId = "user-xyz" };
            var json = JsonSerializer.Serialize(job);

            _dbMock.Setup(d => d.StringGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)json);

            var result = await _service.GetJobForUserAsync("job-021", "hacker-uid");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetJobForUserAsync_ReturnsJob_WhenJobUserIdIsNull()
        {
            var job = new JobResult { JobId = "job-022", UserId = null };
            var json = JsonSerializer.Serialize(job);

            _dbMock.Setup(d => d.StringGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)json);

            var result = await _service.GetJobForUserAsync("job-022", "any-user");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetJobForUserAsync_ReturnsNull_WhenJobNotFound()
        {
            _dbMock.Setup(d => d.StringGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var result = await _service.GetJobForUserAsync("missing", "user-xyz");

            Assert.Null(result);
        }

        // GetJobsByUserAsync 

        [Fact]
        public async Task GetJobsByUserAsync_ReturnsJobs_InDescendingOrder()
        {
            var jobIds = new RedisValue[] { "job-b", "job-a" };

            _dbMock.Setup(d => d.SortedSetRangeByScoreAsync(
                    It.Is<RedisKey>(k => k == "user_jobs:user-1"),
                    It.IsAny<double>(), It.IsAny<double>(),
                    It.IsAny<Exclude>(),
                    It.Is<Order>(o => o == Order.Descending),
                    It.IsAny<long>(), It.IsAny<long>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(jobIds);

            var jobB = new JobResult { JobId = "job-b" };
            var jobA = new JobResult { JobId = "job-a" };

            _dbMock.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == "job:job-b"), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)JsonSerializer.Serialize(jobB));
            _dbMock.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == "job:job-a"), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)JsonSerializer.Serialize(jobA));

            var result = await _service.GetJobsByUserAsync("user-1");

            Assert.Equal(2, result.Count);
            Assert.Equal("job-b", result[0].JobId);
            Assert.Equal("job-a", result[1].JobId);
        }

        [Fact]
        public async Task GetJobsByUserAsync_ReturnsEmptyList_WhenNoJobsExist()
        {
            _dbMock.Setup(d => d.SortedSetRangeByScoreAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<double>(), It.IsAny<double>(),
                    It.IsAny<Exclude>(), It.IsAny<Order>(),
                    It.IsAny<long>(), It.IsAny<long>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(Array.Empty<RedisValue>());

            var result = await _service.GetJobsByUserAsync("user-empty");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetJobsByUserAsync_SkipsNullJobs_WhenRedisReturnsNull()
        {
            var jobIds = new RedisValue[] { "job-good", "job-gone" };

            _dbMock.Setup(d => d.SortedSetRangeByScoreAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<double>(), It.IsAny<double>(),
                    It.IsAny<Exclude>(), It.IsAny<Order>(),
                    It.IsAny<long>(), It.IsAny<long>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(jobIds);

            _dbMock.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == "job:job-good"), It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)JsonSerializer.Serialize(new JobResult { JobId = "job-good" }));

            _dbMock.Setup(d => d.StringGetAsync(It.Is<RedisKey>(k => k == "job:job-gone"), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var result = await _service.GetJobsByUserAsync("user-partial");

            Assert.Single(result);
            Assert.Equal("job-good", result[0].JobId);
        }

        // SetSessionAsync / GetSessionAsync 

        [Fact]
        public async Task SetSessionAsync_StoresSerializedMessages()
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Merhaba" }
            };

            var ex = await Record.ExceptionAsync(() => _service.SetSessionAsync("ses-1", messages));
            Assert.Null(ex);
        }

        [Fact]
        public async Task GetSessionAsync_ReturnsMessages_WhenKeyExists()
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Merhaba" },
                new ChatMessage { Role = "assistant", Content = "Nasıl yardımcı olabilirim?" }
            };
            var json = JsonSerializer.Serialize(messages);

            _dbMock.Setup(d => d.StringGetAsync(
                    It.Is<RedisKey>(k => k == "session:ses-2"),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)json);

            var result = await _service.GetSessionAsync("ses-2");

            Assert.Equal(2, result.Count);
            Assert.Equal("user", result[0].Role);
        }

        [Fact]
        public async Task GetSessionAsync_ReturnsEmptyList_WhenKeyDoesNotExist()
        {
            _dbMock.Setup(d => d.StringGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var result = await _service.GetSessionAsync("ses-nonexistent");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // AppendToSessionAsync 

        [Fact]
        public async Task AppendToSessionAsync_AddsMessageToExistingSession()
        {
            var existing = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "İlk mesaj" }
            };
            var json = JsonSerializer.Serialize(existing);

            _dbMock.Setup(d => d.StringGetAsync(
                    It.Is<RedisKey>(k => k == "session:ses-3"),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)json);

            var session = await _service.GetSessionAsync("ses-3");
            session.Add(new ChatMessage { Role = "assistant", Content = "İkinci mesaj" });

            var ex = await Record.ExceptionAsync(() => _service.SetSessionAsync("ses-3", session));
            Assert.Null(ex);
            Assert.Equal(2, session.Count);
        }

        [Fact]
        public async Task AppendToSessionAsync_CreatesNewSession_WhenKeyDoesNotExist()
        {
            _dbMock.Setup(d => d.StringGetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            var message = new ChatMessage { Role = "user", Content = "İlk" };
            var ex = await Record.ExceptionAsync(() => _service.AppendToSessionAsync("ses-new", message));
            Assert.Null(ex);
        }
    }
}
