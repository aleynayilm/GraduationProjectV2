using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Extensions;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VoiceChatController : ControllerBase
    {
        private readonly ISpeechService _speech;
        private readonly IRabbitMqPublisher _rabbit;
        private readonly IRedisService _redis;

        public VoiceChatController(
            ISpeechService speech,
            IRabbitMqPublisher rabbit,
            IRedisService redis)
        {
            _speech = speech;
            _rabbit = rabbit;
            _redis = redis;
        }


        [HttpPost("transcribe")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Transcribe([FromForm] TranscribeRequest request)
        {
            if (request.Audio == null || request.Audio.Length == 0)
                return BadRequest(new { message = "Ses dosyası boş veya eksik." });

            if (request.Audio.Length > 25 * 1024 * 1024)
                return BadRequest(new { message = "Ses dosyası 25MB sınırını aşıyor." });

            byte[] audioBytes;
            using (var ms = new MemoryStream())
            {
                await request.Audio.CopyToAsync(ms);
                audioBytes = ms.ToArray();
            }

            var language = request.Language ?? "tr";
            var result = await _speech.TranscribeAsync(
                audioBytes,
                fileName: request.Audio.FileName ?? "audio.m4a",
                language: language);

            if (!result.Success)
                return StatusCode(502, new { message = "STT hatası.", detail = result.Error });

            if (string.IsNullOrWhiteSpace(result.Text))
                return Ok(new { text = "", language, message = "Ses tanınamadı." });

            return Ok(new { text = result.Text, language });
        }


        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] VoiceChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { message = "Metin boş." });

            var firebaseUid = User.GetFirebaseUid()!;
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            var session = await _redis.GetSessionAsync(sessionId);
            if (session == null)
            {
                session = new List<ChatMessage>();
                await _redis.SetSessionAsync(sessionId, session);
            }

            var historySnapshot = new List<ChatMessage>(session);

            session.Add(new ChatMessage { Role = "user", Content = request.Text });
            await _redis.SetSessionAsync(sessionId, session);

            var queue = request.Mode?.ToLower() == "cloud"
                ? RabbitMqPublisher.QueueChatCloud
                : RabbitMqPublisher.QueueChatLocal;

            var jobId = await _rabbit.PublishAsync(queue, new
            {
                message = request.Text,
                history = historySnapshot,
                userId = firebaseUid,
                sessionId = sessionId,
                language = request.Language
            });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = request.Mode == "cloud" ? "voice_chat_cloud" : "voice_chat_local"
            });

            return Accepted(new { jobId, sessionId });
        }


        [HttpGet("chat/{jobId}")]
        public async Task<IActionResult> GetChatResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);

            if (result == null)
                return Ok(new { jobId, status = "pending" });

            if (result.Status == "completed" && result.Data != null)
            {
                var sessionId = result.Data is JsonElement el
                    && el.TryGetProperty("sessionId", out var sid)
                    ? sid.GetString()
                    : null;

                if (sessionId != null)
                {
                    var reply = result.Data is JsonElement dataEl
                        && dataEl.TryGetProperty("reply", out var r)
                        ? r.GetString() ?? ""
                        : "";

                    if (!string.IsNullOrEmpty(reply))
                    {
                        var session = await _redis.GetSessionAsync(sessionId) ?? [];
                        session.Add(new ChatMessage { Role = "assistant", Content = reply });
                        await _redis.SetSessionAsync(sessionId, session);
                    }
                }
            }

            return Ok(new { jobId, status = result.Status, data = result.Data });
        }


        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] SynthesizeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { message = "Metin boş." });

            var result = await _speech.SynthesizeAsync(
                request.Text,
                voiceId: request.VoiceId,
                stability: request.Stability > 0 ? request.Stability : 0.5,
                similarityBoost: request.SimilarityBoost > 0 ? request.SimilarityBoost : 0.75);

            if (!result.Success)
                return StatusCode(502, new { message = "TTS hatası.", detail = result.Error });

            return Ok(new
            {
                audioBase64 = result.AudioBase64,
                mimeType = result.MimeType,
                characterCount = request.Text.Length
            });
        }

        // Request modelleri 

        public class TranscribeRequest
        {
            public IFormFile? Audio { get; set; }   
            public string? Language { get; set; } = "tr";
        }

        public class VoiceChatRequest
        {
            public string Text { get; set; } = "";
            public string? SessionId { get; set; }
            public string? Mode { get; set; } = "local"; 
            public string Language { get; set; } = "tr";   
        }

        public class SynthesizeRequest
        {
            public string Text { get; set; } = "";
            public string? VoiceId { get; set; }   
            public double Stability { get; set; } = 0.5;
            public double SimilarityBoost { get; set; } = 0.75;
        }
    }
}
