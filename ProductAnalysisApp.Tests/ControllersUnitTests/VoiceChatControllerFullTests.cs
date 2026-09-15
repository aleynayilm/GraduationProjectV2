using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.Messaging;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class VoiceChatControllerFullTests
    {
        private readonly Mock<ISpeechService>     _speechMock;
        private readonly Mock<IRabbitMqPublisher> _rabbitMock;
        private readonly Mock<IRedisService>      _redisMock;
        private readonly VoiceChatController     _controller;
        private const string Uid = "firebase-uid-voice-001";

        public VoiceChatControllerFullTests()
        {
            _speechMock = new Mock<ISpeechService>();
            _rabbitMock = new Mock<IRabbitMqPublisher>();
            _redisMock  = new Mock<IRedisService>();

            _controller = new VoiceChatController(
                _speechMock.Object, _rabbitMock.Object, _redisMock.Object);
            SetUser(_controller, Uid);
        }

        private static void SetUser(ControllerBase ctrl, string uid)
        {
            var claims   = new[] { new Claim("firebase_uid", uid) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        // FormFile builder 

        private static IFormFile BuildFormFile(string name, byte[] data)
        {
            var stream = new MemoryStream(data);
            var mock   = new Mock<IFormFile>();
            mock.Setup(f => f.FileName).Returns(name);
            mock.Setup(f => f.Length).Returns(data.Length);
            mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((s, _) => stream.CopyTo(s))
                .Returns(Task.CompletedTask);
            return mock.Object;
        }

        // Transcribe  POST /transcribe

        [Fact]
        public async Task Transcribe_ReturnsBadRequest_WhenAudioIsNull()
        {
            var result = await _controller.Transcribe(
                new VoiceChatController.TranscribeRequest { Audio = null });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("boş", bad.Value!.ToString());
        }

        [Fact]
        public async Task Transcribe_ReturnsBadRequest_WhenAudioLengthIsZero()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(0);

            var result = await _controller.Transcribe(
                new VoiceChatController.TranscribeRequest { Audio = fileMock.Object });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Transcribe_ReturnsBadRequest_WhenFileExceeds25MB()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(26 * 1024 * 1024L);

            var result = await _controller.Transcribe(
                new VoiceChatController.TranscribeRequest { Audio = fileMock.Object });

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("25MB", bad.Value!.ToString());
        }

        [Fact]
        public async Task Transcribe_Returns502_WhenSpeechServiceFails()
        {
            _speechMock.Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TranscribeResult { Success = false, Error = "Groq hatası" });

            var result = await _controller.Transcribe(new VoiceChatController.TranscribeRequest
            {
                Audio    = BuildFormFile("audio.m4a", new byte[100]),
                Language = "tr"
            });

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, status.StatusCode);
        }

        [Fact]
        public async Task Transcribe_ReturnsOkWithEmptyText_WhenTranscriptIsWhitespace()
        {
            _speechMock.Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TranscribeResult { Success = true, Text = "   " });

            var result = await _controller.Transcribe(new VoiceChatController.TranscribeRequest
            {
                Audio = BuildFormFile("audio.mp3", new byte[50])
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Ses tanınamadı", ok.Value!.ToString());
        }

        [Fact]
        public async Task Transcribe_ReturnsOkWithText_WhenSuccessful()
        {
            _speechMock.Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TranscribeResult { Success = true, Text = "Ürünü karşılaştır" });

            var result = await _controller.Transcribe(new VoiceChatController.TranscribeRequest
            {
                Audio    = BuildFormFile("audio.wav", new byte[512]),
                Language = "tr"
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Ürünü karşılaştır", ok.Value!.ToString());
        }

        [Fact]
        public async Task Transcribe_UsesDefaultLanguage_WhenLanguageIsNull()
        {
            _speechMock.Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(),
                    It.IsAny<string>(), "tr"))
                .ReturnsAsync(new TranscribeResult { Success = true, Text = "Test" });

            await _controller.Transcribe(new VoiceChatController.TranscribeRequest
            {
                Audio    = BuildFormFile("rec.m4a", new byte[10]),
                Language = null
            });

            _speechMock.Verify(s => s.TranscribeAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), "tr"), Times.Once);
        }

        // Chat  POST /chat

        [Fact]
        public async Task Chat_ReturnsBadRequest_WhenTextIsEmpty()
        {
            var result = await _controller.Chat(
                new VoiceChatController.VoiceChatRequest { Text = "" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Chat_ReturnsBadRequest_WhenTextIsWhitespace()
        {
            var result = await _controller.Chat(
                new VoiceChatController.VoiceChatRequest { Text = "   " });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Chat_ReturnsAccepted_WhenTextIsValid_LocalMode()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatLocal, It.IsAny<object>()))
                       .ReturnsAsync("vc-001");

            var result = await _controller.Chat(new VoiceChatController.VoiceChatRequest
            {
                Text = "Bana ürün öner", Mode = "local", SessionId = "ses-vc-001"
            });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("vc-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task Chat_PublishesToCloudQueue_WhenModeIsCloud()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatCloud, It.IsAny<object>()))
                       .ReturnsAsync("vc-cloud-001");

            await _controller.Chat(new VoiceChatController.VoiceChatRequest
            { Text = "Karşılaştır", Mode = "cloud" });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueChatCloud, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task Chat_PublishesToLocalQueue_WhenModeIsNull()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueChatLocal, It.IsAny<object>()))
                       .ReturnsAsync("vc-local-null");

            await _controller.Chat(new VoiceChatController.VoiceChatRequest
            { Text = "Soru", Mode = null });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueChatLocal, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task Chat_CreatesAndSetsEmptySession_WhenSessionIsNull()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync((List<ChatMessage>?)null);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("vc-newsess");

            var result = await _controller.Chat(new VoiceChatController.VoiceChatRequest
            { Text = "Merhaba" });

            Assert.IsType<AcceptedResult>(result);
            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task Chat_GeneratesSessionId_WhenNotProvided()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("vc-gen-sess");

            var result = await _controller.Chat(new VoiceChatController.VoiceChatRequest
            { Text = "Soru", SessionId = null });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("sessionId", accepted.Value!.ToString());
        }

        [Fact]
        public async Task Chat_SetsJobType_VoiceChatLocal_WhenModeIsLocal()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("vc-type-local");

            await _controller.Chat(new VoiceChatController.VoiceChatRequest
            { Text = "Test", Mode = "local" });

            _redisMock.Verify(r => r.SetJobAsync(
                It.Is<JobResult>(j => j.JobType == "voice_chat_local")), Times.Once);
        }

        [Fact]
        public async Task Chat_SetsJobType_VoiceChatCloud_WhenModeIsCloud()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("vc-type-cloud");

            await _controller.Chat(new VoiceChatController.VoiceChatRequest
            { Text = "Test", Mode = "cloud" });

            _redisMock.Verify(r => r.SetJobAsync(
                It.Is<JobResult>(j => j.JobType == "voice_chat_cloud")), Times.Once);
        }

        // GetChatResult  GET /chat/{jobId}

        [Fact]
        public async Task GetChatResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("vcr-x", Uid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetChatResult("vcr-x");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetChatResult_ReturnsOk_WhenJobFoundAndPending()
        {
            var job = new JobResult { JobId = "vcr-pend", Status = "pending", UserId = Uid };
            _redisMock.Setup(r => r.GetJobForUserAsync("vcr-pend", Uid)).ReturnsAsync(job);

            var result = await _controller.GetChatResult("vcr-pend");

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetChatResult_AppendsAssistantReply_WhenCompletedWithReply()
        {
            var data = new { sessionId = "ses-reply", reply = "İşte cevabım" };
            var job = new JobResult
            {
                JobId = "vcr-done", Status = "completed", UserId = Uid,
                Data  = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
            };
            _redisMock.Setup(r => r.GetJobForUserAsync("vcr-done", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("ses-reply")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.GetChatResult("vcr-done");

            _redisMock.Verify(r => r.SetSessionAsync(
                "ses-reply",
                It.Is<List<ChatMessage>>(s =>
                    s.Any(m => m.Role == "assistant" && m.Content == "İşte cevabım"))),
                Times.Once);
        }

        [Fact]
        public async Task GetChatResult_DoesNotAppend_WhenReplyIsEmpty()
        {
            var data = new { sessionId = "ses-empty", reply = "" };
            var job = new JobResult
            {
                JobId = "vcr-empty", Status = "completed", UserId = Uid,
                Data  = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
            };
            _redisMock.Setup(r => r.GetJobForUserAsync("vcr-empty", Uid)).ReturnsAsync(job);

            await _controller.GetChatResult("vcr-empty");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        [Fact]
        public async Task GetChatResult_DoesNotAppend_WhenNoSessionIdInData()
        {
            var data = new { reply = "Cevap var ama sessionId yok" };
            var job = new JobResult
            {
                JobId = "vcr-nosid", Status = "completed", UserId = Uid,
                Data  = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
            };
            _redisMock.Setup(r => r.GetJobForUserAsync("vcr-nosid", Uid)).ReturnsAsync(job);

            await _controller.GetChatResult("vcr-nosid");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        [Fact]
        public async Task GetChatResult_DoesNotAppend_WhenDataIsNull()
        {
            var job = new JobResult
            {
                JobId = "vcr-ndata", Status = "completed", UserId = Uid, Data = null
            };
            _redisMock.Setup(r => r.GetJobForUserAsync("vcr-ndata", Uid)).ReturnsAsync(job);

            var result = await _controller.GetChatResult("vcr-ndata");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task GetChatResult_HandlesNullSession_WhenGetSessionReturnsNull()
        {
            var data = new { sessionId = "ses-null-get", reply = "Cevap" };
            var job = new JobResult
            {
                JobId = "vcr-snull", Status = "completed", UserId = Uid,
                Data  = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
            };
            _redisMock.Setup(r => r.GetJobForUserAsync("vcr-snull", Uid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("ses-null-get"))
                      .ReturnsAsync((List<ChatMessage>?)null);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            // ?? [] operatörü null'u boş liste yapar, exception fırlatmamalı
            var result = await _controller.GetChatResult("vcr-snull");

            Assert.IsType<OkObjectResult>(result);
        }

        // Synthesize  POST /synthesize

        [Fact]
        public async Task Synthesize_ReturnsBadRequest_WhenTextIsEmpty()
        {
            var result = await _controller.Synthesize(
                new VoiceChatController.SynthesizeRequest { Text = "" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Synthesize_ReturnsBadRequest_WhenTextIsWhitespace()
        {
            var result = await _controller.Synthesize(
                new VoiceChatController.SynthesizeRequest { Text = "   " });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Synthesize_Returns502_WhenSpeechServiceFails()
        {
            _speechMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new SynthesizeResult { Success = false, Error = "ElevenLabs hatası" });

            var result = await _controller.Synthesize(
                new VoiceChatController.SynthesizeRequest { Text = "Test" });

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, status.StatusCode);
        }

        [Fact]
        public async Task Synthesize_ReturnsOkWithAudio_WhenSuccessful()
        {
            var bytes = Encoding.UTF8.GetBytes("fake-audio");
            _speechMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new SynthesizeResult
                {
                    Success     = true,
                    AudioBase64 = Convert.ToBase64String(bytes),
                    MimeType    = "audio/mpeg"
                });

            var result = await _controller.Synthesize(
                new VoiceChatController.SynthesizeRequest { Text = "Merhaba dünya" });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("audio/mpeg", ok.Value!.ToString());
        }

        [Fact]
        public async Task Synthesize_UsesDefaultStability_WhenStabilityIsZero()
        {
            _speechMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(),
                    It.IsAny<string?>(), 0.5, 0.75))
                .ReturnsAsync(new SynthesizeResult { Success = true, MimeType = "audio/mpeg" });

            await _controller.Synthesize(new VoiceChatController.SynthesizeRequest
            {
                Text = "Test", Stability = 0, SimilarityBoost = 0
            });

            _speechMock.Verify(s => s.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), 0.5, 0.75), Times.Once);
        }

        [Fact]
        public async Task Synthesize_UsesCustomValues_WhenProvided()
        {
            _speechMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(),
                    "custom-voice", 0.7, 0.9))
                .ReturnsAsync(new SynthesizeResult { Success = true, MimeType = "audio/mpeg" });

            await _controller.Synthesize(new VoiceChatController.SynthesizeRequest
            {
                Text = "Test", VoiceId = "custom-voice", Stability = 0.7, SimilarityBoost = 0.9
            });

            _speechMock.Verify(s => s.SynthesizeAsync(
                It.IsAny<string>(), "custom-voice", 0.7, 0.9), Times.Once);
        }

        [Fact]
        public async Task Synthesize_IncludesCharacterCount_InResponse()
        {
            const string text = "On karakter.";
            _speechMock.Setup(s => s.SynthesizeAsync(It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new SynthesizeResult
                {
                    Success = true, AudioBase64 = "abc", MimeType = "audio/mpeg"
                });

            var result = await _controller.Synthesize(
                new VoiceChatController.SynthesizeRequest { Text = text });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains(text.Length.ToString(), ok.Value!.ToString());
        }
    }
}
