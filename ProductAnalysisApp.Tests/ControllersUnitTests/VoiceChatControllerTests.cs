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
    public class VoiceChatControllerTests : ControllerTestBase
    {
        private readonly Mock<ISpeechService>      _speechMock;
        private readonly Mock<IRabbitMqPublisher>  _rabbitMock;
        private readonly Mock<IRedisService>       _redisMock;
        private readonly VoiceChatController      _controller;

        public VoiceChatControllerTests()
        {
            _speechMock = new Mock<ISpeechService>();
            _rabbitMock = new Mock<IRabbitMqPublisher>();
            _redisMock  = new Mock<IRedisService>();

            _controller = new VoiceChatController(
                _speechMock.Object,
                _rabbitMock.Object,
                _redisMock.Object);

            SetAuthenticatedUser(_controller);
        }

        // Transcribe 

        [Fact]
        public async Task Transcribe_ReturnsBadRequest_WhenAudioIsNull()
        {
            var request = new VoiceChatController.TranscribeRequest { Audio = null };

            var result = await _controller.Transcribe(request);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("boş", bad.Value!.ToString());
        }

        [Fact]
        public async Task Transcribe_ReturnsBadRequest_WhenAudioIsEmpty()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(0);
            var request = new VoiceChatController.TranscribeRequest { Audio = fileMock.Object };

            var result = await _controller.Transcribe(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Transcribe_ReturnsBadRequest_WhenAudioExceeds25MB()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(26 * 1024 * 1024L);
            var request = new VoiceChatController.TranscribeRequest { Audio = fileMock.Object };

            var result = await _controller.Transcribe(request);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("25MB", bad.Value!.ToString());
        }

        [Fact]
        public async Task Transcribe_Returns502_WhenSpeechServiceFails()
        {
            var fileMock = BuildFormFile("audio.m4a", new byte[100]);
            _speechMock
                .Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TranscribeResult { Success = false, Error = "Groq bağlanamadı" });

            var request = new VoiceChatController.TranscribeRequest { Audio = fileMock, Language = "tr" };

            var result = await _controller.Transcribe(request);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, status.StatusCode);
        }

        [Fact]
        public async Task Transcribe_ReturnsOkWithEmptyText_WhenTranscriptIsWhitespace()
        {
            var fileMock = BuildFormFile("audio.m4a", new byte[100]);
            _speechMock
                .Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TranscribeResult { Success = true, Text = "   " });

            var request = new VoiceChatController.TranscribeRequest { Audio = fileMock };

            var result = await _controller.Transcribe(request);

            var ok   = Assert.IsType<OkObjectResult>(result);
            var body = ok.Value!.ToString()!;
            Assert.Contains("Ses tanınamadı", body);
        }

        [Fact]
        public async Task Transcribe_ReturnsOkWithText_WhenSuccessful()
        {
            var fileMock = BuildFormFile("audio.mp3", new byte[512]);
            _speechMock
                .Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TranscribeResult { Success = true, Text = "Merhaba dünya" });

            var request = new VoiceChatController.TranscribeRequest { Audio = fileMock, Language = "tr" };

            var result = await _controller.Transcribe(request);

            var ok   = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Merhaba dünya", ok.Value!.ToString());
        }

        [Fact]
        public async Task Transcribe_UsesDefaultLanguage_WhenLanguageIsNull()
        {
            var fileMock = BuildFormFile("audio.wav", new byte[100]);
            _speechMock
                .Setup(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "tr"))
                .ReturnsAsync(new TranscribeResult { Success = true, Text = "Test" });

            var request = new VoiceChatController.TranscribeRequest { Audio = fileMock, Language = null };

            var result = await _controller.Transcribe(request);

            _speechMock.Verify(s => s.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "tr"), Times.Once);
        }

        // Chat 

        [Fact]
        public async Task Chat_ReturnsBadRequest_WhenTextIsEmpty()
        {
            var request = new VoiceChatController.VoiceChatRequest { Text = "" };

            var result = await _controller.Chat(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Chat_ReturnsBadRequest_WhenTextIsWhitespace()
        {
            var request = new VoiceChatController.VoiceChatRequest { Text = "   " };

            var result = await _controller.Chat(request);

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
                       .ReturnsAsync("job-voice-001");

            var request = new VoiceChatController.VoiceChatRequest
            {
                Text = "Bana ürün öner", Mode = "local", SessionId = "ses-001"
            };

            var result = await _controller.Chat(request);

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("job-voice-001", accepted.Value!.ToString());
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
                       .ReturnsAsync("job-voice-cloud-001");

            var request = new VoiceChatController.VoiceChatRequest
            {
                Text = "Karşılaştır", Mode = "cloud"
            };

            await _controller.Chat(request);

            _rabbitMock.Verify(r => r.PublishAsync(RabbitMqPublisher.QueueChatCloud, It.IsAny<object>()), Times.Once);
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
                       .ReturnsAsync("job-voice-local-null");

            var request = new VoiceChatController.VoiceChatRequest { Text = "Soru", Mode = null };

            await _controller.Chat(request);

            _rabbitMock.Verify(r => r.PublishAsync(RabbitMqPublisher.QueueChatLocal, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task Chat_CreatesNewSession_WhenSessionIsNull()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync((List<ChatMessage>?)null);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-new-session");

            var request = new VoiceChatController.VoiceChatRequest { Text = "Merhaba" };

            var result = await _controller.Chat(request);

            Assert.IsType<AcceptedResult>(result);
            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()), Times.AtLeast(1));
        }

        [Fact]
        public async Task Chat_GeneratesNewSessionId_WhenSessionIdNotProvided()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-gen-session");

            var request = new VoiceChatController.VoiceChatRequest { Text = "Soru", SessionId = null };

            var result = await _controller.Chat(request);

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("sessionId", accepted.Value!.ToString());
        }

        [Fact]
        public async Task Chat_SetsJobWithCorrectType_LocalMode()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-type-local");

            await _controller.Chat(new VoiceChatController.VoiceChatRequest { Text = "Test", Mode = "local" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "voice_chat_local" && j.Status == "pending")), Times.Once);
        }

        [Fact]
        public async Task Chat_SetsJobWithCorrectType_CloudMode()
        {
            _redisMock.Setup(r => r.GetSessionAsync(It.IsAny<string>()))
                      .ReturnsAsync(new List<ChatMessage>());
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-type-cloud");

            await _controller.Chat(new VoiceChatController.VoiceChatRequest { Text = "Test", Mode = "cloud" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "voice_chat_cloud")), Times.Once);
        }

        // GetChatResult 

        [Fact]
        public async Task GetChatResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync(It.IsAny<string>(), TestFirebaseUid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetChatResult("job-missing");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetChatResult_ReturnsData_WhenJobFound()
        {
            var job = new JobResult { JobId = "job-found", Status = "pending", UserId = TestFirebaseUid };
            _redisMock.Setup(r => r.GetJobForUserAsync("job-found", TestFirebaseUid)).ReturnsAsync(job);

            var result = await _controller.GetChatResult("job-found");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task GetChatResult_AppendsAssistantReply_WhenJobCompletedWithReply()
        {
            var data = new { sessionId = "ses-complete", reply = "İşte cevabım" };
            var job = new JobResult
            {
                JobId = "job-complete", Status = "completed", UserId = TestFirebaseUid,
                Data  = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
            };

            _redisMock.Setup(r => r.GetJobForUserAsync("job-complete", TestFirebaseUid)).ReturnsAsync(job);
            _redisMock.Setup(r => r.GetSessionAsync("ses-complete")).ReturnsAsync([]);
            _redisMock.Setup(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()))
                      .Returns(Task.CompletedTask);

            await _controller.GetChatResult("job-complete");

            _redisMock.Verify(r => r.SetSessionAsync(
                "ses-complete",
                It.Is<List<ChatMessage>>(s => s.Any(m => m.Role == "assistant" && m.Content == "İşte cevabım"))),
                Times.Once);
        }

        [Fact]
        public async Task GetChatResult_DoesNotAppend_WhenReplyIsEmpty()
        {
            var data = new { sessionId = "ses-empty-reply", reply = "" };
            var job = new JobResult
            {
                JobId = "job-empty-reply", Status = "completed", UserId = TestFirebaseUid,
                Data  = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(data))
            };

            _redisMock.Setup(r => r.GetJobForUserAsync("job-empty-reply", TestFirebaseUid)).ReturnsAsync(job);

            await _controller.GetChatResult("job-empty-reply");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
        }

        [Fact]
        public async Task GetChatResult_DoesNotAppend_WhenDataIsNull()
        {
            var job = new JobResult
            {
                JobId = "job-null-data", Status = "completed", UserId = TestFirebaseUid,
                Data  = null
            };

            _redisMock.Setup(r => r.GetJobForUserAsync("job-null-data", TestFirebaseUid)).ReturnsAsync(job);

            var result = await _controller.GetChatResult("job-null-data");

            _redisMock.Verify(r => r.SetSessionAsync(It.IsAny<string>(), It.IsAny<List<ChatMessage>>()),
                Times.Never);
            Assert.IsType<OkObjectResult>(result);
        }

        // Synthesize 

        [Fact]
        public async Task Synthesize_ReturnsBadRequest_WhenTextIsEmpty()
        {
            var request = new VoiceChatController.SynthesizeRequest { Text = "" };

            var result = await _controller.Synthesize(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Synthesize_ReturnsBadRequest_WhenTextIsWhitespace()
        {
            var request = new VoiceChatController.SynthesizeRequest { Text = "   " };

            var result = await _controller.Synthesize(request);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Synthesize_Returns502_WhenSpeechServiceFails()
        {
            _speechMock
                .Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new SynthesizeResult { Success = false, Error = "ElevenLabs hata" });

            var request = new VoiceChatController.SynthesizeRequest { Text = "Merhaba" };

            var result = await _controller.Synthesize(request);

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(502, status.StatusCode);
        }

        [Fact]
        public async Task Synthesize_ReturnsOkWithAudioBase64_WhenSuccessful()
        {
            var audioBytes = Encoding.UTF8.GetBytes("fake-audio-data");
            _speechMock
                .Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new SynthesizeResult
                {
                    Success     = true,
                    AudioBytes  = audioBytes,
                    AudioBase64 = Convert.ToBase64String(audioBytes),
                    MimeType    = "audio/mpeg"
                });

            var request = new VoiceChatController.SynthesizeRequest { Text = "Ürün fiyatı 100 TL" };

            var result = await _controller.Synthesize(request);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("audio/mpeg", ok.Value!.ToString());
        }

        [Fact]
        public async Task Synthesize_UsesDefaultStability_WhenZero()
        {
            _speechMock
                .Setup(s => s.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string?>(),
                    0.5, 0.75))
                .ReturnsAsync(new SynthesizeResult { Success = true, AudioBase64 = "abc", MimeType = "audio/mpeg" });

            var request = new VoiceChatController.SynthesizeRequest
            {
                Text = "Test", Stability = 0, SimilarityBoost = 0
            };

            await _controller.Synthesize(request);

            _speechMock.Verify(s => s.SynthesizeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), 0.5, 0.75), Times.Once);
        }

        [Fact]
        public async Task Synthesize_PassesCustomVoiceId_WhenProvided()
        {
            _speechMock
                .Setup(s => s.SynthesizeAsync(It.IsAny<string>(), "custom-voice-id",
                    It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(new SynthesizeResult { Success = true, AudioBase64 = "x", MimeType = "audio/mpeg" });

            var request = new VoiceChatController.SynthesizeRequest
            {
                Text = "Ses testi", VoiceId = "custom-voice-id", Stability = 0.6, SimilarityBoost = 0.8
            };

            await _controller.Synthesize(request);

            _speechMock.Verify(s => s.SynthesizeAsync(
                It.IsAny<string>(), "custom-voice-id", 0.6, 0.8), Times.Once);
        }

        // Yardımcılar 

        private static IFormFile BuildFormFile(string fileName, byte[] data)
        {
            var stream   = new MemoryStream(data);
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(data.Length);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, CancellationToken>((s, _) => stream.CopyTo(s))
                    .Returns(Task.CompletedTask);
            return fileMock.Object;
        }
    }
}
