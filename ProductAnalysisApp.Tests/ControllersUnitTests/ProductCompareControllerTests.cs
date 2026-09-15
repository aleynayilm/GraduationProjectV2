using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.Messaging;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class ProductCompareControllerTests
    {
        private readonly Mock<IRabbitMqPublisher> _rabbitMock;
        private readonly Mock<IRedisService> _redisMock;
        private readonly ProductCompareController _controller;

        private const string FakeUid = "firebase-uid-123";

        public ProductCompareControllerTests()
        {
            _rabbitMock = new Mock<IRabbitMqPublisher>();
            _redisMock  = new Mock<IRedisService>();
            _controller = new ProductCompareController(_rabbitMock.Object, _redisMock.Object);
            _controller.ControllerContext = BuildControllerContext(FakeUid);
        }

        // Helpers

        private static ControllerContext BuildControllerContext(string uid)
        {
            var claims = new[] { new Claim("firebase_uid", uid) };   
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        private static JobResult PendingJob(string jobId, string uid, string type) => new()
        {
            JobId = jobId, Status = "pending", UserId = uid, JobType = type
        };

        private static JobResult CompletedJob(string jobId, string uid, object data) => new()
        {
            JobId = jobId, Status = "completed", UserId = uid,
            Data = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
        };

        // CompareCloudLlm 

        [Fact]
        public async Task CompareCloudLlm_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCloudLlm, It.IsAny<object>()))
                       .ReturnsAsync("job-cloud-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            var request = new ProductCompareController.UrlRequest
            {
                Urls = ["https://example.com/a", "https://example.com/b"]
            };

            var result = await _controller.CompareCloudLlm(request);

            var accepted = Assert.IsType<AcceptedResult>(result);
            var json = JsonSerializer.Serialize(accepted.Value);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("job-cloud-001", root.GetProperty("jobId").GetString());
        }

        [Fact]
        public async Task CompareCloudLlm_SetsJobAsPending_InRedis()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-cloud-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.CompareCloudLlm(new ProductCompareController.UrlRequest());

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.Status  == "pending" &&
                j.UserId  == FakeUid   &&
                j.JobType == "cloud_llm_compare")), Times.Once);
        }

        [Fact]
        public async Task CompareCloudLlm_InitializesEmptySession()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-cloud-003");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.CompareCloudLlm(new ProductCompareController.UrlRequest());

            _redisMock.Verify(r => r.SetSessionAsync(
                "job-cloud-003",
                It.Is<List<ChatMessage>>(s => s.Count == 0)), Times.Once);
        }

        // GetCloudLlmResult 

        [Fact]
        public async Task GetCloudLlmResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("job-x", FakeUid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetCloudLlmResult("job-x");

            var ok   = Assert.IsType<OkObjectResult>(result);
            var body = ok.Value!.ToString()!;
            Assert.Contains("pending", body);
        }

        [Fact]
        public async Task GetCloudLlmResult_ReturnsData_WhenJobCompleted()
        {
            var job = CompletedJob("job-y", FakeUid, new { answer = "Ürün A daha iyi" });
            _redisMock.Setup(r => r.GetJobForUserAsync("job-y", FakeUid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("job-y")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            var result = await _controller.GetCloudLlmResult("job-y");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task GetCloudLlmResult_PopulatesSession_WhenCompletedAndSessionEmpty()
        {
            var job = CompletedJob("job-z", FakeUid, new { answer = "Cevap" });
            _redisMock.Setup(r => r.GetJobForUserAsync("job-z", FakeUid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("job-z")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.GetCloudLlmResult("job-z");

            _redisMock.Verify(r => r.SetSessionAsync(
                "job-z",
                It.Is<List<ChatMessage>>(s => s.Count == 1 && s[0].Role == "assistant")), Times.Once);
        }

        [Fact]
        public async Task GetCloudLlmResult_DoesNotOverwriteSession_WhenSessionAlreadyHasMessages()
        {
            var job = CompletedJob("job-w", FakeUid, new { answer = "Eski cevap" });
            var existingSession = new List<ChatMessage> { new() { Role = "assistant", Content = "Var olan" } };

            _redisMock.Setup(r => r.GetJobForUserAsync("job-w", FakeUid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("job-w")).ReturnsAsync(existingSession);

            await _controller.GetCloudLlmResult("job-w");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        // CompareLocalLlm

        [Fact]
        public async Task CompareLocalLlm_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueLocalLlm, It.IsAny<object>()))
                       .ReturnsAsync("job-local-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            var request = new ProductCompareController.LocalLlmCompareRequest
            {
                Products = [new ProductForComparisonDto { Name = "Laptop A", Price = "1000" }]
            };

            var result = await _controller.CompareLocalLlm(request);

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("job-local-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task CompareLocalLlm_SetsJobAsPending_WithCorrectType()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-local-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.CompareLocalLlm(new ProductCompareController.LocalLlmCompareRequest());

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "local_llm_compare" &&
                j.Status  == "pending")), Times.Once);
        }

        // GetLocalLlmResult

        [Fact]
        public async Task GetLocalLlmResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync(It.IsAny<string>(), FakeUid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetLocalLlmResult("job-missing");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        // ContinueChatLocal

        [Fact]
        public async Task ContinueChatLocal_ReturnsBadRequest_WhenSessionIsNull()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-gone")).ReturnsAsync((List<ChatMessage>?)null!);

            var request = new ProductCompareController.ContinueChatRequest
            {
                SessionId = "ses-gone", Message = "Merhaba"
            };

            var result = await _controller.ContinueChatLocal(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ContinueChatLocal_ReturnsAccepted_WhenSessionExists()
        {
            var session = new List<ChatMessage> { new() { Role = "assistant", Content = "Analiz tamamlandı." } };
            _redisMock.Setup(r => r.GetSessionAsync("ses-ok")).ReturnsAsync(session);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatLocal, It.IsAny<object>()))
                       .ReturnsAsync("job-chat-001");

            var request = new ProductCompareController.ContinueChatRequest
            {
                SessionId = "ses-ok", Message = "Hangisi daha ucuz?"
            };

            var result = await _controller.ContinueChatLocal(request);

            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task ContinueChatLocal_AppendsUserMessageToSession()
        {
            var session = new List<ChatMessage>();
            _redisMock.Setup(r => r.GetSessionAsync("ses-append")).ReturnsAsync(session);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-chat-002");

            var request = new ProductCompareController.ContinueChatRequest
            {
                SessionId = "ses-append", Message = "Yeni soru"
            };

            await _controller.ContinueChatLocal(request);

            _redisMock.Verify(r => r.SetSessionAsync(
                "ses-append",
                It.Is<List<ChatMessage>>(s =>
                    s.Count == 1 &&
                    s[0].Role == "user" &&
                    s[0].Content == "Yeni soru")), Times.Once);
        }

        [Fact]
        public async Task ContinueChatLocal_PublishesCorrectPayload_ToRabbit()
        {
            var session = new List<ChatMessage> { new() { Role = "assistant", Content = "Var olan mesaj" } };
            _redisMock.Setup(r => r.GetSessionAsync("ses-pub")).ReturnsAsync(session);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatLocal, It.IsAny<object>()))
                       .ReturnsAsync("job-pub-001");

            var request = new ProductCompareController.ContinueChatRequest
            {
                SessionId = "ses-pub", Message = "Detay ver"
            };

            await _controller.ContinueChatLocal(request);

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueChatLocal,
                It.IsAny<object>()), Times.Once);
        }

        // ContinueChatCloud 

        [Fact]
        public async Task ContinueChatCloud_ReturnsBadRequest_WhenSessionIsNull()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-cloud-none")).ReturnsAsync((List<ChatMessage>?)null!);

            var request = new ProductCompareController.ContinueChatRequest
            {
                SessionId = "ses-cloud-none", Message = "Soru"
            };

            var result = await _controller.ContinueChatCloud(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ContinueChatCloud_SetsJobWithCorrectType()
        {
            var session = new List<ChatMessage>();
            _redisMock.Setup(r => r.GetSessionAsync("ses-cloud-ok")).ReturnsAsync(session);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatCloud, It.IsAny<object>()))
                       .ReturnsAsync("job-cloud-chat-001");

            await _controller.ContinueChatCloud(new ProductCompareController.ContinueChatRequest
            {
                SessionId = "ses-cloud-ok", Message = "Soru"
            });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "chat_cloud" &&
                j.UserId  == FakeUid)), Times.Once);
        }

        // GetResult 

        [Fact]
        public async Task GetResult_ReturnsJob_WhenFound()
        {
            var job = PendingJob("job-result-001", FakeUid, "scrape");
            _redisMock.Setup(r => r.GetJobForUserAsync("job-result-001", FakeUid)).ReturnsAsync(job);

            var result = await _controller.GetResult("job-result-001");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task GetResult_ReturnsPendingFallback_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync(It.IsAny<string>(), FakeUid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetResult("job-missing");

            var ok  = Assert.IsType<OkObjectResult>(result);
            var job = Assert.IsType<JobResult>(ok.Value);
            Assert.Equal("pending", job.Status);
            Assert.Equal("job-missing", job.JobId);
        }
    }
}
