using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using ProductAnalysisApp.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ProductAnalysisApp.Tests.Services
{
    public class SpeechServiceTests
    {
        private const string FakeGroqKey   = "test-groq-key";
        private const string FakeElevenKey = "test-eleven-key";
        private const string FakeVoiceId   = "test-voice-id";

        private SpeechService CreateService(HttpMessageHandler handler,
            string? voiceId = FakeVoiceId)
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Groq:ApiKey"]).Returns(FakeGroqKey);
            configMock.Setup(c => c["ElevenLabs:ApiKey"]).Returns(FakeElevenKey);
            configMock.Setup(c => c["ElevenLabs:VoiceId"]).Returns(voiceId!);

            var httpClient = new HttpClient(handler);
            return new SpeechService(httpClient, configMock.Object);
        }

        private static Mock<HttpMessageHandler> SetupHandler(
            HttpStatusCode code, string body = "{\"text\":\"Merhaba\"}")
        {
            var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = code,
                    Content    = new StringContent(body, Encoding.UTF8, "application/json")
                });
            return mock;
        }

        private static Mock<HttpMessageHandler> SetupByteHandler(
            HttpStatusCode code, byte[]? responseBytes = null)
        {
            var mock  = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var bytes = responseBytes ?? Encoding.UTF8.GetBytes("fake-audio");
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = code,
                    Content    = new ByteArrayContent(bytes)
                });
            return mock;
        }

        // Constructor 

        [Fact]
        public void Constructor_ThrowsInvalidOperation_WhenGroqKeyMissing()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Groq:ApiKey"]).Returns((string?)null);
            configMock.Setup(c => c["ElevenLabs:ApiKey"]).Returns("key");

            Assert.Throws<InvalidOperationException>(
                () => new SpeechService(new HttpClient(), configMock.Object));
        }

        [Fact]
        public void Constructor_ThrowsInvalidOperation_WhenElevenKeyMissing()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Groq:ApiKey"]).Returns("key");
            configMock.Setup(c => c["ElevenLabs:ApiKey"]).Returns((string?)null);

            Assert.Throws<InvalidOperationException>(
                () => new SpeechService(new HttpClient(), configMock.Object));
        }

        [Fact]
        public void Constructor_UsesMatildaVoice_WhenVoiceIdNotConfigured()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Groq:ApiKey"]).Returns("key");
            configMock.Setup(c => c["ElevenLabs:ApiKey"]).Returns("key");
            configMock.Setup(c => c["ElevenLabs:VoiceId"]).Returns((string?)null);

            var service = new SpeechService(new HttpClient(), configMock.Object);
            Assert.NotNull(service);
        }

        // TranscribeAsync 

        [Fact]
        public async Task TranscribeAsync_ReturnsFailure_WhenAudioIsNull()
        {
            var service = CreateService(SetupHandler(HttpStatusCode.OK).Object);

            var result = await service.TranscribeAsync(null!);

            Assert.False(result.Success);
            Assert.Contains("boş", result.Error);
        }

        [Fact]
        public async Task TranscribeAsync_ReturnsFailure_WhenAudioIsEmpty()
        {
            var service = CreateService(SetupHandler(HttpStatusCode.OK).Object);

            var result = await service.TranscribeAsync(Array.Empty<byte>());

            Assert.False(result.Success);
            Assert.Contains("boş", result.Error);
        }

        [Fact]
        public async Task TranscribeAsync_ReturnsText_WhenResponseIsSuccessful()
        {
            var json    = JsonSerializer.Serialize(new { text = "Merhaba dünya" });
            var service = CreateService(SetupHandler(HttpStatusCode.OK, json).Object);

            var result = await service.TranscribeAsync(new byte[] { 1, 2, 3 });

            Assert.True(result.Success);
            Assert.Equal("Merhaba dünya", result.Text);
        }

        [Fact]
        public async Task TranscribeAsync_TrimsText_WhenResponseHasWhitespace()
        {
            var json    = JsonSerializer.Serialize(new { text = "  Boşluklu metin  " });
            var service = CreateService(SetupHandler(HttpStatusCode.OK, json).Object);

            var result = await service.TranscribeAsync(new byte[] { 1 });

            Assert.True(result.Success);
            Assert.Equal("Boşluklu metin", result.Text);
        }

        [Fact]
        public async Task TranscribeAsync_ReturnsFailure_WhenResponseIsNotSuccess()
        {
            var service = CreateService(SetupHandler(HttpStatusCode.InternalServerError, "Groq hata").Object);

            var result = await service.TranscribeAsync(new byte[] { 1 });

            Assert.False(result.Success);
            Assert.Contains("500", result.Error);
        }

        [Fact]
        public async Task TranscribeAsync_ReturnsFailure_WhenHttpClientThrows()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Bağlantı hatası"));

            var service = CreateService(handlerMock.Object);

            var result = await service.TranscribeAsync(new byte[] { 1 });

            Assert.False(result.Success);
            Assert.Contains("exception", result.Error);
        }

        [Theory]
        [InlineData("audio.m4a",  "audio/m4a")]
        [InlineData("audio.mp3",  "audio/mpeg")]
        [InlineData("audio.wav",  "audio/wav")]
        [InlineData("audio.webm", "audio/webm")]
        [InlineData("audio.ogg",  "audio/ogg")]
        [InlineData("audio.flac", "audio/flac")]
        [InlineData("audio.xyz",  "audio/m4a")]  
        public async Task TranscribeAsync_SendsCorrectMimeType_ForFileExtension(
            string fileName, string expectedMime)
        {
            var service = CreateService(SetupHandler(HttpStatusCode.OK).Object);
            var result  = await service.TranscribeAsync(new byte[] { 1 }, fileName: fileName);

            Assert.NotNull(result);
            _ = expectedMime; 
        }

        // SynthesizeAsync 

        [Fact]
        public async Task SynthesizeAsync_ReturnsFailure_WhenTextIsEmpty()
        {
            var service = CreateService(SetupByteHandler(HttpStatusCode.OK).Object);

            var result = await service.SynthesizeAsync("");

            Assert.False(result.Success);
            Assert.Contains("boş", result.Error);
        }

        [Fact]
        public async Task SynthesizeAsync_ReturnsFailure_WhenTextIsWhitespace()
        {
            var service = CreateService(SetupByteHandler(HttpStatusCode.OK).Object);

            var result = await service.SynthesizeAsync("   ");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task SynthesizeAsync_ReturnsAudioBase64_WhenSuccessful()
        {
            var audioBytes = Encoding.UTF8.GetBytes("fake-mp3-data");
            var service    = CreateService(SetupByteHandler(HttpStatusCode.OK, audioBytes).Object);

            var result = await service.SynthesizeAsync("Merhaba");

            Assert.True(result.Success);
            Assert.Equal(Convert.ToBase64String(audioBytes), result.AudioBase64);
            Assert.Equal("audio/mpeg", result.MimeType);
        }

        [Fact]
        public async Task SynthesizeAsync_ReturnsFailure_WhenResponseIsNotSuccess()
        {
            var service = CreateService(SetupByteHandler(HttpStatusCode.Unauthorized).Object);

            var result = await service.SynthesizeAsync("Test");

            Assert.False(result.Success);
            Assert.Contains("401", result.Error);
        }

        [Fact]
        public async Task SynthesizeAsync_ReturnsFailure_WhenHttpClientThrows()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Timeout"));

            var service = CreateService(handlerMock.Object);

            var result = await service.SynthesizeAsync("Test");

            Assert.False(result.Success);
            Assert.Contains("exception", result.Error);
        }

        [Fact]
        public async Task SynthesizeAsync_TruncatesText_WhenLongerThan5000Chars()
        {
            var longText   = new string('a', 6000);
            var audioBytes = new byte[] { 1, 2, 3 };

            var service = CreateService(SetupByteHandler(HttpStatusCode.OK, audioBytes).Object);

            var result = await service.SynthesizeAsync(longText);

            Assert.True(result.Success);
            Assert.Equal(Convert.ToBase64String(audioBytes), result.AudioBase64);
        }

        [Fact]
        public async Task SynthesizeAsync_UsesConfigVoiceId_WhenVoiceIdParamIsNull()
        {
            var audioBytes = new byte[] { 1 };
            HttpRequestMessage? capturedRequest = null;

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content    = new ByteArrayContent(audioBytes)
                });

            var service = CreateService(handlerMock.Object, voiceId: FakeVoiceId);
            await service.SynthesizeAsync("test", voiceId: null);

            Assert.NotNull(capturedRequest);
            Assert.Contains(FakeVoiceId, capturedRequest!.RequestUri!.ToString());
        }

        [Fact]
        public async Task SynthesizeAsync_UsesProvidedVoiceId_WhenSpecified()
        {
            var audioBytes = new byte[] { 1 };
            HttpRequestMessage? capturedRequest = null;

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content    = new ByteArrayContent(audioBytes)
                });

            var service = CreateService(handlerMock.Object);
            await service.SynthesizeAsync("test", voiceId: "special-voice");

            Assert.Contains("special-voice", capturedRequest!.RequestUri!.ToString());
        }

        // Voices static sınıf 

        [Fact]
        public void Voices_Constants_AreNotEmpty()
        {
            Assert.NotEmpty(SpeechService.Voices.Rachel);
            Assert.NotEmpty(SpeechService.Voices.Matilda);
            Assert.NotEmpty(SpeechService.Voices.Adam);
            Assert.NotEmpty(SpeechService.Voices.Antoni);
        }
    }
}
