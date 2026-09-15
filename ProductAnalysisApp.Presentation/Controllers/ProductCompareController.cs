using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Extensions;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.Messaging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductCompareController : ControllerBase
    {
        private readonly IRabbitMqPublisher _rabbit;
        private readonly IRedisService _redis;

        public ProductCompareController(IRabbitMqPublisher rabbit, IRedisService redis)
        {
            _redis = redis;
            _rabbit = rabbit;
        }

        [HttpPost("cloudllmcompare")]
        [Authorize]
        public async Task<IActionResult> CompareCloudLlm([FromBody] UrlRequest request)
        {
            var firebaseUid = User.GetFirebaseUid()!;

            var jobId = await _rabbit.PublishAsync(
                RabbitMqPublisher.QueueCloudLlm,
                new { urls = request.Urls, userId = firebaseUid, language = request.Language });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = "cloud_llm_compare"
            });

            await _redis.SetSessionAsync(jobId, new List<ChatMessage>());

            return Accepted(new { jobId });
        }

        [HttpGet("cloudllmcompare/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetCloudLlmResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);

            if (result == null)
                return Ok(new { jobId, status = "pending" });

            if (result.Status == "completed")
            {
                var session = await _redis.GetSessionAsync(jobId);
                if (session.Count == 0 && result.Data != null)
                {
                    var content = result.Data is JsonElement el
                        ? el.GetRawText()
                        : JsonSerializer.Serialize(result.Data);

                    session.Add(new ChatMessage
                    {
                        Role = "assistant",
                        Content = content
                    });
                    await _redis.SetSessionAsync(jobId, session);
                }
            }

            return Ok(new { jobId, status = result.Status, data = result.Data });
        }

        [HttpPost("localllmcompare")]
        [Authorize]
        public async Task<IActionResult> CompareLocalLlm([FromBody] LocalLlmCompareRequest request)
        {
            var firebaseUid = User.GetFirebaseUid()!;

            var jobId = await _rabbit.PublishAsync(
             RabbitMqPublisher.QueueLocalLlm,
             new { products = request.Products, userId = firebaseUid, language = request.Language });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = "local_llm_compare"
            });

            await _redis.SetSessionAsync(jobId, new List<ChatMessage>());

            return Accepted(new { jobId });
        }

        [HttpGet("localllmcompare/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetLocalLlmResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);

            if (result == null)
                return Ok(new { jobId, status = "pending" });

            if (result.Status == "completed")
            {
                var session = await _redis.GetSessionAsync(jobId);
                if (session.Count == 0 && result.Data != null)
                {
                    var content = result.Data is JsonElement el
                        ? el.GetRawText()
                        : JsonSerializer.Serialize(result.Data);

                    session.Add(new ChatMessage
                    {
                        Role = "assistant",
                        Content = content
                    });
                    await _redis.SetSessionAsync(jobId, session);
                }
            }

            return Ok(new { jobId, status = result.Status, data = result.Data });
        }

        [HttpPost("chat-local")]
        [Authorize]
        public async Task<IActionResult> ContinueChatLocal([FromBody] ContinueChatRequest request)
        {
            var firebaseUid = User.GetFirebaseUid()!;

            var session = await _redis.GetSessionAsync(request.SessionId);
            if (session == null)
                return BadRequest(new { message = "Session bulunamadı." });

            var historySnapshot = new List<ChatMessage>(session);
            session.Add(new ChatMessage { Role = "user", Content = request.Message });
            await _redis.SetSessionAsync(request.SessionId, session);

            var jobId = await _rabbit.PublishAsync(
                RabbitMqPublisher.QueueChatLocal,
                new
                {
                    message = request.Message,
                    history = historySnapshot,
                    userId = firebaseUid,
                    sessionId = request.SessionId,
                    language = request.Language
                });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = "chat"
            });

            return Accepted(new { jobId, sessionId = request.SessionId });
        }

        [HttpPost("chat-cloud")]
        [Authorize]
        public async Task<IActionResult> ContinueChatCloud([FromBody] ContinueChatRequest request)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var session = await _redis.GetSessionAsync(request.SessionId);
            if (session == null)
                return BadRequest(new { message = "Session bulunamadı." });

            var historySnapshot = new List<ChatMessage>(session);
            session.Add(new ChatMessage { Role = "user", Content = request.Message });
            await _redis.SetSessionAsync(request.SessionId, session);

            var jobId = await _rabbit.PublishAsync(
                RabbitMqPublisher.QueueChatCloud,
                new
                {
                    message = request.Message,
                    history = historySnapshot,
                    userId = firebaseUid,
                    sessionId = request.SessionId,
                    language = request.Language
                });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = "chat_cloud"
            });

            return Accepted(new { jobId, sessionId = request.SessionId });
        }

        [HttpGet("result/{jobId}")]
        public async Task<IActionResult> GetResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);

            return Ok(result ?? new JobResult { JobId = jobId, Status = "pending" });
        }

        public class UrlRequest
        {
            public List<string> Urls { get; set; } = [];
            public string Language { get; set; } = "tr"; 
        }
        public class LocalLlmCompareRequest
        {
            public List<ProductForComparisonDto> Products { get; set; } = [];
            public string Language { get; set; } = "tr"; 
        }
        public class ContinueChatRequest
        {
            public string SessionId { get; set; } = "";
            public string Message { get; set; } = "";
            public string Language { get; set; } = "tr"; 
        }
    }
}
