using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
using System.Text;
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
        public ProductScrapingController(IServiceManager manager, RabbitMqPublisher rabbit, ILogger<ProductScrapingController> logger, RedisService redis)
        {
            _manager = manager;
            _rabbit = rabbit;
            _logger = logger;
            _redis = redis;
        }
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
            var alreadyExists = recent.Any(h =>
                string.Equals(h.SearchUrl, request.Url, StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
                await _manager.SearchHistoryService.AddSearchAsync(request.Url, firebaseUid);

            _logger.LogInformation(
                "[JOB] Scrape kuyruğa alındı: {JobId} | User: {Uid}", jobId, firebaseUid);

            return Accepted(new { jobId });
        }

        [HttpGet("scrape/{jobId}")]
        [Authorize]
        public async Task<IActionResult> GetScrapeResult(string jobId)
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var result = await _redis.GetJobForUserAsync(jobId, firebaseUid);

            if (result == null)
                return Ok(new { jobId, status = "pending" });

            return Ok(new { jobId, status = result.Status, data = result.Data });
        }

        [HttpGet("jobs")]
        [Authorize]
        public IActionResult GetMyJobs()
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var jobs = JobResultStore.GetByUser(firebaseUid)
                .Select(j => new { j.JobId, j.Status, j.JobType, j.CreatedAt, j.FinishedAt });

            return Ok(jobs);
        }

        [HttpGet("search-history")]
        [Authorize]
        public async Task<IActionResult> GetSearchHistory()
        {
            var firebaseUid = User.GetFirebaseUid()!;
            var history = await _manager.SearchHistoryService.GetRecentSearchesAsync(firebaseUid);
            return Ok(history);
        }
    }
}