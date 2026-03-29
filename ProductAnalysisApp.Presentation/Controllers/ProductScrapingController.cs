using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Extensions;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.Messaging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductScrapingController : ControllerBase
    {
        private readonly IServiceManager _manager;
        private readonly RabbitMqPublisher _rabbit;
        private readonly RedisService _redis;
        private readonly ILogger<ProductScrapingController> _logger;

        public ProductScrapingController(
            IServiceManager manager, RabbitMqPublisher rabbit,
            ILogger<ProductScrapingController> logger, RedisService redis)
        {
            _manager = manager;
            _rabbit = rabbit;
            _logger = logger;
            _redis = redis;
        }

        // Mevcut: BS4 scrape + karsilastirma

        [HttpPost("scrape")]
        [Authorize]
        public async Task<ActionResult> ScrapeProduct([FromBody] ScrapeRequest request)
        {
            var firebaseUid = User.GetFirebaseUid()!;

            var jobId = await _rabbit.PublishAsync(
                RabbitMqPublisher.QueueCompare,
                new { url = request.Url, userId = firebaseUid });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = "scrape",
                CreatedAt = DateTime.UtcNow
            });

            var recent = await _manager.SearchHistoryService.GetRecentSearchesAsync(firebaseUid);
            if (!recent.Any(h => string.Equals(h.SearchUrl, request.Url, StringComparison.OrdinalIgnoreCase)))
                await _manager.SearchHistoryService.AddSearchAsync(request.Url, firebaseUid);

            _logger.LogInformation("[JOB] scrape: {JobId} | {Uid}", jobId, firebaseUid);
            return Accepted(new { jobId });
        }

        [HttpGet("scrape/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetScrapeResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);
            if (result == null) return Ok(new { jobId, status = "pending" });
            return Ok(new { jobId, status = result.Status, data = result.Data });
        }

        [HttpGet("jobs")]
        [Authorize]
        public async Task<IActionResult> GetMyJobs()
        {
            var firebaseUid = User.GetFirebaseUid()!;
            return Ok(await _redis.GetJobsByUserAsync(firebaseUid));
        }

        [HttpGet("search-history")]
        [Authorize]
        public async Task<IActionResult> GetSearchHistory()
        {
            var firebaseUid = User.GetFirebaseUid()!;
            return Ok(await _manager.SearchHistoryService.GetRecentSearchesAsync(firebaseUid));
        }

        // Tavily + RAG + Mistral 

        [HttpPost("local-search")]
        [Authorize]
        public async Task<IActionResult> LocalSearch([FromBody] SearchRequest request)
        {
            var firebaseUid = User.GetFirebaseUid()!;

            var jobId = await _rabbit.PublishAsync(
                RabbitMqPublisher.QueueLocalSearch,
                new
                {
                    input = request.Input,
                    userId = firebaseUid,
                    sessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                    language = request.Language
                });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = "local_search"
            });

            _logger.LogInformation("[JOB] local-search: {JobId}", jobId);
            return Accepted(new { jobId });
        }

        [HttpGet("local-search/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetLocalSearchResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);
            if (result == null) return Ok(new { jobId, status = "pending" });
            return Ok(new { jobId, status = result.Status, data = result.Data });
        }

        // Tavily + RAG + Gemini 

        [HttpPost("cloud-search")]
        [Authorize]
        public async Task<IActionResult> CloudSearch([FromBody] SearchRequest request)
        {
            var firebaseUid = User.GetFirebaseUid()!;

            var jobId = await _rabbit.PublishAsync(
                RabbitMqPublisher.QueueCloudSearch,
                new
                {
                    input = request.Input,
                    userId = firebaseUid,
                    sessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                    language = request.Language
                });

            await _redis.SetJobAsync(new JobResult
            {
                JobId = jobId,
                Status = "pending",
                UserId = firebaseUid,
                JobType = "cloud_search"
            });

            _logger.LogInformation("[JOB] cloud-search: {JobId}", jobId);
            return Accepted(new { jobId });
        }

        [HttpGet("cloud-search/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetCloudSearchResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);
            if (result == null) return Ok(new { jobId, status = "pending" });
            return Ok(new { jobId, status = result.Status, data = result.Data });
        }

        // Request modelleri 

        public class SearchRequest
        {
            public string Input { get; set; } = "";
            public string? SessionId { get; set; }
            public string Language { get; set; } = "tr"; // "tr" | "en"
        }
    }
}
