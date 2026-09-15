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
    public class ProductCompareControllerFullTests
    {
        private readonly Mock<IRabbitMqPublisher> _rabbitMock;
        private readonly Mock<IRedisService> _redisMock;
        private readonly ProductCompareController _controller;
        private const string Uid = "firebase-uid-compare-001";

        public ProductCompareControllerFullTests()
        {
            _rabbitMock = new Mock<IRabbitMqPublisher>();
            _redisMock = new Mock<IRedisService>();
            _controller = new ProductCompareController(_rabbitMock.Object, _redisMock.Object);
            SetUser(_controller, Uid);
        }

        // Helpers 

        private static void SetUser(ControllerBase ctrl, string uid)
        {
            var claims    = new[] { new Claim("firebase_uid", uid) };
            var identity  = new ClaimsIdentity(claims, "TestAuth");
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private static JobResult MakeJob(string jobId, string status, string uid,
                                         string jobType, object? data = null)
        {
            return new JobResult
            {
                JobId    = jobId, Status = status, UserId = uid, JobType = jobType,
                Data     = data is null ? null
                           : JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
            };
        }

        private void SetupPublish(string queue, string jobId)
            => _rabbitMock.Setup(r => r.PublishAsync(queue, It.IsAny<object>()))
                          .ReturnsAsync(jobId);

        private void SetupRedisSet()
        {
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
        }

        // CompareCloudLlm  POST /cloudllmcompare

        [Fact]
        public async Task CompareCloudLlm_ReturnsAccepted_WithJobId()
        {
            SetupPublish(RabbitMqPublisher.QueueCloudLlm, "cld-001");
            SetupRedisSet();

            var result = await _controller.CompareCloudLlm(
                new ProductCompareController.UrlRequest { Urls = ["https://a.com"], Language = "tr" });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("cld-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task CompareCloudLlm_SetsJobPending_WithCorrectFields()
        {
            SetupPublish(RabbitMqPublisher.QueueCloudLlm, "cld-002");
            SetupRedisSet();

            await _controller.CompareCloudLlm(new ProductCompareController.UrlRequest());

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.Status  == "pending" &&
                j.UserId  == Uid &&
                j.JobType == "cloud_llm_compare")), Times.Once);
        }

        [Fact]
        public async Task CompareCloudLlm_InitializesEmptySession()
        {
            SetupPublish(RabbitMqPublisher.QueueCloudLlm, "cld-003");
            SetupRedisSet();

            await _controller.CompareCloudLlm(new ProductCompareController.UrlRequest());

            _redisMock.Verify(r => r.SetSessionAsync(
                "cld-003",
                It.Is<List<ChatMessage>>(s => s.Count == 0)), Times.Once);
        }

        [Fact]
        public async Task CompareCloudLlm_PublishesToCorrectQueue()
        {
            SetupPublish(RabbitMqPublisher.QueueCloudLlm, "cld-004");
            SetupRedisSet();

            await _controller.CompareCloudLlm(new ProductCompareController.UrlRequest
            {
                Urls = ["https://x.com", "https://y.com"]
            });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueCloudLlm, It.IsAny<object>()), Times.Once);
        }

        // GetCloudLlmResult  GET /cloudllmcompare/{jobId}

        [Fact]
        public async Task GetCloudLlmResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("j-x", Uid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetCloudLlmResult("j-x");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetCloudLlmResult_ReturnsOk_WhenJobIsPending()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("j-pend", Uid))
                      .ReturnsAsync(MakeJob("j-pend", "pending", Uid, "cloud_llm_compare"));

            var result = await _controller.GetCloudLlmResult("j-pend");

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetCloudLlmResult_PopulatesSession_WhenCompletedAndSessionEmpty()
        {
            var job = MakeJob("j-comp", "completed", Uid, "cloud_llm_compare",
                              new { answer = "Ürün A daha iyi" });
            _redisMock.Setup(r => r.GetJobForUserAsync("j-comp", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("j-comp")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.GetCloudLlmResult("j-comp");

            _redisMock.Verify(r => r.SetSessionAsync(
                "j-comp",
                It.Is<List<ChatMessage>>(s => s.Count == 1 && s[0].Role == "assistant")),
                Times.Once);
        }

        [Fact]
        public async Task GetCloudLlmResult_DoesNotOverwriteSession_WhenSessionNotEmpty()
        {
            var job = MakeJob("j-full", "completed", Uid, "cloud_llm_compare",
                              new { answer = "Cevap" });
            _redisMock.Setup(r => r.GetJobForUserAsync("j-full", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("j-full"))
                      .ReturnsAsync([new ChatMessage { Role = "assistant", Content = "Mevcut" }]);

            await _controller.GetCloudLlmResult("j-full");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        [Fact]
        public async Task GetCloudLlmResult_DoesNotPopulateSession_WhenDataIsNull()
        {
            var job = MakeJob("j-null", "completed", Uid, "cloud_llm_compare");
            _redisMock.Setup(r => r.GetJobForUserAsync("j-null", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("j-null")).ReturnsAsync([]);

            await _controller.GetCloudLlmResult("j-null");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        [Fact]
        public async Task GetCloudLlmResult_SerializesNonJsonElementData()
        {
            var job = new JobResult
            {
                JobId = "j-str", Status = "completed", UserId = Uid,
                Data  = "plain string data"
            };
            _redisMock.Setup(r => r.GetJobForUserAsync("j-str", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("j-str")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            var result = await _controller.GetCloudLlmResult("j-str");

            Assert.IsType<OkObjectResult>(result);
        }

        // CompareLocalLlm  POST /localllmcompare

        [Fact]
        public async Task CompareLocalLlm_ReturnsAccepted_WithJobId()
        {
            SetupPublish(RabbitMqPublisher.QueueLocalLlm, "loc-001");
            SetupRedisSet();

            var result = await _controller.CompareLocalLlm(
                new ProductCompareController.LocalLlmCompareRequest
                {
                    Products = [new ProductForComparisonDto { Name = "A", Price = "100" }]
                });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("loc-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task CompareLocalLlm_SetsJobPending_WithCorrectType()
        {
            SetupPublish(RabbitMqPublisher.QueueLocalLlm, "loc-002");
            SetupRedisSet();

            await _controller.CompareLocalLlm(new ProductCompareController.LocalLlmCompareRequest());

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "local_llm_compare" &&
                j.Status  == "pending" &&
                j.UserId  == Uid)), Times.Once);
        }

        [Fact]
        public async Task CompareLocalLlm_InitializesEmptySession()
        {
            SetupPublish(RabbitMqPublisher.QueueLocalLlm, "loc-003");
            SetupRedisSet();

            await _controller.CompareLocalLlm(new ProductCompareController.LocalLlmCompareRequest());

            _redisMock.Verify(r => r.SetSessionAsync(
                "loc-003", It.Is<List<ChatMessage>>(s => s.Count == 0)), Times.Once);
        }

        // GetLocalLlmResult  GET /localllmcompare/{jobId}

        [Fact]
        public async Task GetLocalLlmResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("lj-x", Uid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetLocalLlmResult("lj-x");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetLocalLlmResult_PopulatesSession_WhenCompletedAndSessionEmpty()
        {
            var job = MakeJob("lj-c", "completed", Uid, "local_llm_compare",
                              new { comparison = "sonuç" });
            _redisMock.Setup(r => r.GetJobForUserAsync("lj-c", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("lj-c")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.GetLocalLlmResult("lj-c");

            _redisMock.Verify(r => r.SetSessionAsync(
                "lj-c",
                It.Is<List<ChatMessage>>(s => s.Count == 1 && s[0].Role == "assistant")),
                Times.Once);
        }

        [Fact]
        public async Task GetLocalLlmResult_DoesNotOverwriteSession_WhenNotEmpty()
        {
            var job = MakeJob("lj-f", "completed", Uid, "local_llm_compare", new { x = 1 });
            _redisMock.Setup(r => r.GetJobForUserAsync("lj-f", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("lj-f"))
                      .ReturnsAsync([new ChatMessage { Role = "assistant", Content = "Var olan" }]);

            await _controller.GetLocalLlmResult("lj-f");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        [Fact]
        public async Task GetLocalLlmResult_DoesNotPopulateSession_WhenDataNull()
        {
            var job = MakeJob("lj-n", "completed", Uid, "local_llm_compare");
            _redisMock.Setup(r => r.GetJobForUserAsync("lj-n", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("lj-n")).ReturnsAsync([]);

            await _controller.GetLocalLlmResult("lj-n");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        // ContinueChatLocal  POST /chat-local

        [Fact]
        public async Task ContinueChatLocal_ReturnsBadRequest_WhenSessionIsNull()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-gone"))
                      .ReturnsAsync((List<ChatMessage>?)null!);

            var result = await _controller.ContinueChatLocal(
                new ProductCompareController.ContinueChatRequest
                { SessionId = "ses-gone", Message = "Merhaba" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ContinueChatLocal_ReturnsAccepted_WhenSessionExists()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-ok"))
                      .ReturnsAsync([new ChatMessage { Role = "assistant", Content = "Init" }]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatLocal, It.IsAny<object>()))
                       .ReturnsAsync("chat-001");

            var result = await _controller.ContinueChatLocal(
                new ProductCompareController.ContinueChatRequest
                { SessionId = "ses-ok", Message = "Devam" });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("chat-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task ContinueChatLocal_AppendsUserMessage_ToSession()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-msg")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("chat-002");

            await _controller.ContinueChatLocal(new ProductCompareController.ContinueChatRequest
            { SessionId = "ses-msg", Message = "Yeni soru" });

            _redisMock.Verify(r => r.SetSessionAsync(
                "ses-msg",
                It.Is<List<ChatMessage>>(s =>
                    s.Any(m => m.Role == "user" && m.Content == "Yeni soru"))),
                Times.Once);
        }

        [Fact]
        public async Task ContinueChatLocal_SetsJobWithChatType()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-jt")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("chat-003");

            await _controller.ContinueChatLocal(new ProductCompareController.ContinueChatRequest
            { SessionId = "ses-jt", Message = "?" });

            _redisMock.Verify(r => r.SetJobAsync(
                It.Is<JobResult>(j => j.JobType == "chat" && j.UserId == Uid)), Times.Once);
        }

        [Fact]
        public async Task ContinueChatLocal_IncludesSessionId_InAcceptedResponse()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-ret")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("chat-ret");

            var result = await _controller.ContinueChatLocal(
                new ProductCompareController.ContinueChatRequest
                { SessionId = "ses-ret", Message = "Test" });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("ses-ret", accepted.Value!.ToString());
        }

        // ContinueChatCloud  POST /chat-cloud

        [Fact]
        public async Task ContinueChatCloud_ReturnsBadRequest_WhenSessionIsNull()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-cloud-none"))
                      .ReturnsAsync((List<ChatMessage>?)null!);

            var result = await _controller.ContinueChatCloud(
                new ProductCompareController.ContinueChatRequest
                { SessionId = "ses-cloud-none", Message = "Soru" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task ContinueChatCloud_ReturnsAccepted_WhenSessionExists()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-cloud-ok")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatCloud, It.IsAny<object>()))
                       .ReturnsAsync("cloud-chat-001");

            var result = await _controller.ContinueChatCloud(
                new ProductCompareController.ContinueChatRequest
                { SessionId = "ses-cloud-ok", Message = "Karşılaştır" });

            Assert.IsType<AcceptedResult>(result);
        }

        [Fact]
        public async Task ContinueChatCloud_SetsJobWithChatCloudType()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-cc")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("cloud-chat-002");

            await _controller.ContinueChatCloud(new ProductCompareController.ContinueChatRequest
            { SessionId = "ses-cc", Message = "Test" });

            _redisMock.Verify(r => r.SetJobAsync(
                It.Is<JobResult>(j => j.JobType == "chat_cloud" && j.UserId == Uid)), Times.Once);
        }

        [Fact]
        public async Task ContinueChatCloud_AppendsUserMessage()
        {
            _redisMock.Setup(r => r.GetSessionAsync("ses-ccm")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("cloud-chat-003");

            await _controller.ContinueChatCloud(new ProductCompareController.ContinueChatRequest
            { SessionId = "ses-ccm", Message = "Cloud soru" });

            _redisMock.Verify(r => r.SetSessionAsync(
                "ses-ccm",
                It.Is<List<ChatMessage>>(s =>
                    s.Any(m => m.Role == "user" && m.Content == "Cloud soru"))),
                Times.Once);
        }

        // GetResult  GET /result/{jobId}

        [Fact]
        public async Task GetResult_ReturnsJob_WhenFound()
        {
            var job = MakeJob("gr-001", "completed", Uid, "scrape");
            _redisMock.Setup(r => r.GetJobForUserAsync("gr-001", Uid)).ReturnsAsync(job);

            var result = await _controller.GetResult("gr-001");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task GetResult_ReturnsPendingFallback_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("gr-miss", Uid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetResult("gr-miss");

            var ok  = Assert.IsType<OkObjectResult>(result);
            var job = Assert.IsType<JobResult>(ok.Value);
            Assert.Equal("pending",  job.Status);
            Assert.Equal("gr-miss",  job.JobId);
        }
    }
}
