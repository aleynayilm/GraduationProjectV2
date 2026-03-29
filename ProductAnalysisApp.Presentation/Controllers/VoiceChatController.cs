using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Extensions;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    /// <summary>
    /// Sesli chat endpoint'leri.
    ///
    /// Akış:
    ///   1. POST /transcribe  → ses dosyası → Groq Whisper → metin
    ///   2. POST /chat        → metin → RabbitMQ (mevcut chat-local/cloud) → LLM cevabı
    ///   3. POST /synthesize  → LLM cevabı → ElevenLabs TTS → mp3 base64
    ///
    /// Mobile'da her adım sırayla çağrılır.
    /// /chat async (job pattern) çalıştığı için GET /chat/{jobId} ile polling yapılır.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VoiceChatController : ControllerBase
    {
        private readonly SpeechService _speech;
        private readonly RabbitMqPublisher _rabbit;
        private readonly RedisService _redis;

        public VoiceChatController(
            SpeechService speech,
            RabbitMqPublisher rabbit,
            RedisService redis)
        {
            _speech = speech;
            _rabbit = rabbit;
            _redis = redis;
        }

        // 1. STT: Ses → Metin 

        /// <summary>
        /// Mobile'dan gelen ses kaydını metne çevirir.
        /// Content-Type: multipart/form-data
        /// Form field: "audio" (dosya) + "language" (opsiyonel, varsayılan "tr")
        /// </summary>
        [HttpPost("transcribe")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Transcribe([FromForm] TranscribeRequest request)
        {
            if (request.Audio == null || request.Audio.Length == 0)
                return BadRequest(new { message = "Ses dosyası boş veya eksik." });

            // Boyut kontrolü — Whisper max 25MB
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

        // 2. Chat: Metin → LLM (mevcut job pattern) 

        /// <summary>
        /// Transkript metni LLM'e gönderir.
        /// Mevcut chat-local / chat-cloud ile aynı RabbitMQ akışını kullanır.
        /// Async job döner — GET /chat/{jobId} ile sonucu polling ile al.
        /// </summary>
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] VoiceChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { message = "Metin boş." });

            var firebaseUid = User.GetFirebaseUid()!;
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            // Session yoksa boş oluştur
            var session = await _redis.GetSessionAsync(sessionId);
            if (session == null)
            {
                session = new List<ChatMessage>();
                await _redis.SetSessionAsync(sessionId, session);
            }

            var historySnapshot = new List<ChatMessage>(session);

            // Kullanıcı mesajını session'a ekle
            session.Add(new ChatMessage { Role = "user", Content = request.Text });
            await _redis.SetSessionAsync(sessionId, session);

            // Mode'a göre kuyruk seç — local: Mistral, cloud: Gemini
            var queue = request.Mode?.ToLower() == "cloud"
                ? RabbitMqPublisher.QueueChatCloud
                : RabbitMqPublisher.QueueChatLocal;

            var jobId = await _rabbit.PublishAsync(queue, new
            {
                message = request.Text,
                history = historySnapshot,
                userId = firebaseUid,
                sessionId = sessionId
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

        /// <summary>
        /// Chat job sonucunu polling ile döner.
        /// Status "completed" olduğunda data.reply mevcuttur.
        /// </summary>
        [HttpGet("chat/{jobId}")]
        public async Task<IActionResult> GetChatResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);

            if (result == null)
                return Ok(new { jobId, status = "pending" });

            // Completed ise session'a assistant cevabını ekle
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

        // 3. TTS: Metin → Ses 

        /// <summary>
        /// LLM cevabını sese çevirir. MP3 base64 döner.
        /// Mobile'da base64'ü decode edip direkt çal.
        /// </summary>
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
            public IFormFile? Audio { get; set; }        // multipart dosya
            public string? Language { get; set; } = "tr";
        }

        public class VoiceChatRequest
        {
            public string Text { get; set; } = "";
            public string? SessionId { get; set; }
            public string? Mode { get; set; } = "local"; // "local" | "cloud"
        }

        public class SynthesizeRequest
        {
            public string Text { get; set; } = "";
            public string? VoiceId { get; set; }   // null → config'deki varsayılan (Matilda)
            public double Stability { get; set; } = 0.5;
            public double SimilarityBoost { get; set; } = 0.75;
        }
    }
}
