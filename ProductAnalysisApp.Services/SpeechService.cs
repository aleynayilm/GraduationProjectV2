using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ProductAnalysisApp.Services
{

    public class SpeechService
    {
        private readonly HttpClient _http;
        private readonly string _groqKey;
        private readonly string _elevenKey;
        private readonly string _voiceId;

        private const string GroqBase = "https://api.groq.com/openai/";
        private const string GroqModel = "whisper-large-v3";

        // ElevenLabs
        private const string ElevenBase = "https://api.elevenlabs.io/";
        private const string ElevenModel = "eleven_multilingual_v2"; // Türkçe dahil 29 dil

        // Hazır sesler — ElevenLabs default kütüphanesi
        public static class Voices
        {
            public const string Rachel = "21m00Tcm4TlvDq8ikWAM"; // kadın, İngilizce ağırlıklı
            public const string Matilda = "XrExE9yKIg1WjnnlVkGX"; // kadın, çok dilli, Türkçe iyi
            public const string Adam = "pNInz6obpgDQGcFmaJgB"; // erkek
            public const string Antoni = "ErXwobaYiN019PkySvjV"; // erkek, doğal
        }

        public SpeechService(HttpClient http, IConfiguration config)
        {
            _http = http;

            _groqKey = config["Groq:ApiKey"]
                         ?? throw new InvalidOperationException("Groq:ApiKey eksik");
            _elevenKey = config["ElevenLabs:ApiKey"]
                         ?? throw new InvalidOperationException("ElevenLabs:ApiKey eksik");
            _voiceId = config["ElevenLabs:VoiceId"] ?? Voices.Matilda;
        }

        // STT: Ses → Metin (Groq Whisper) 

        /// <summary>
        /// Ses dosyasını metne çevirir.
        /// Desteklenen formatlar: m4a, mp3, wav, webm, ogg, flac (max 25MB)
        /// </summary>
        public async Task<TranscribeResult> TranscribeAsync(
            byte[] audioBytes,
            string fileName = "audio.m4a",
            string language = "tr")
        {
            if (audioBytes == null || audioBytes.Length == 0)
                return Fail<TranscribeResult>("Ses verisi boş");

            using var req = new HttpRequestMessage(HttpMethod.Post,
                                 GroqBase + "v1/audio/transcriptions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _groqKey);

            using var form = new MultipartFormDataContent();
            using var content = new ByteArrayContent(audioBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));

            form.Add(content, "file", fileName);
            form.Add(new StringContent(GroqModel), "model");
            form.Add(new StringContent(language), "language");
            form.Add(new StringContent("json"), "response_format");

            req.Content = form;

            try
            {
                var resp = await _http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return Fail<TranscribeResult>(
                        $"Groq STT {(int)resp.StatusCode}: {body}");

                using var doc = JsonDocument.Parse(body);
                var text = doc.RootElement.GetProperty("text").GetString() ?? "";
                return new TranscribeResult { Success = true, Text = text.Trim() };
            }
            catch (Exception ex)
            {
                return Fail<TranscribeResult>($"Groq STT exception: {ex.Message}");
            }
        }

        // TTS: Metin → Ses (ElevenLabs) 

        /// <summary>
        /// Metni MP3 formatında sese çevirir. base64 encoded döner.
        /// </summary>
        /// <param name="text">Seslendirilecek metin</param>
        /// <param name="voiceId">ElevenLabs voice ID — null ise config'deki kullanılır</param>
        /// <param name="stability">Ses tutarlılığı 0.0–1.0 (varsayılan 0.5)</param>
        /// <param name="similarityBoost">Orijinal sese benzerlik 0.0–1.0 (varsayılan 0.75)</param>
        public async Task<SynthesizeResult> SynthesizeAsync(
            string text,
            string? voiceId = null,
            double stability = 0.5,
            double similarityBoost = 0.75)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Fail<SynthesizeResult>("Metin boş");

            // ElevenLabs max 5000 karakter per istek
            if (text.Length > 5000)
                text = text[..5000];

            var vid = voiceId ?? _voiceId;
            var url = $"{ElevenBase}v1/text-to-speech/{vid}";
            var payload = JsonSerializer.Serialize(new
            {
                text,
                model_id = ElevenModel,
                voice_settings = new
                {
                    stability,
                    similarity_boost = similarityBoost,
                    style = 0.0,   // abartılı stil kapalı — doğal konuşma
                    use_speaker_boost = true
                }
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("xi-api-key", _elevenKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            try
            {
                var resp = await _http.SendAsync(req);

                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    return Fail<SynthesizeResult>(
                        $"ElevenLabs TTS {(int)resp.StatusCode}: {err}");
                }

                var audioBytes = await resp.Content.ReadAsByteArrayAsync();
                return new SynthesizeResult
                {
                    Success = true,
                    AudioBytes = audioBytes,
                    AudioBase64 = Convert.ToBase64String(audioBytes),
                    MimeType = "audio/mpeg"
                };
            }
            catch (Exception ex)
            {
                return Fail<SynthesizeResult>($"ElevenLabs TTS exception: {ex.Message}");
            }
        }

        // Yardımcılar 

        private static string GetMimeType(string fileName) =>
            Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".m4a" => "audio/m4a",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".webm" => "audio/webm",
                ".ogg" => "audio/ogg",
                ".flac" => "audio/flac",
                _ => "audio/m4a"
            };

        private static T Fail<T>(string error) where T : ResultBase, new() =>
            new T { Success = false, Error = error };
    }

    // DTO'lar 

    public abstract class ResultBase
    {
        public bool Success { get; set; }
        public string Error { get; set; } = "";
    }

    public class TranscribeResult : ResultBase
    {
        public string Text { get; set; } = "";
    }

    public class SynthesizeResult : ResultBase
    {
        public byte[] AudioBytes { get; set; } = [];
        public string AudioBase64 { get; set; } = "";
        public string MimeType { get; set; } = "audio/mpeg";
    }
}
