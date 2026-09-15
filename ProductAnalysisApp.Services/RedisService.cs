using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.Messaging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    public class RedisService: IRedisService
    {
        private readonly IDatabase _db;
        private readonly TimeSpan _jobTtl = TimeSpan.FromHours(24);
        private readonly TimeSpan _sessionTtl = TimeSpan.FromHours(2);

        public RedisService(IConnectionMultiplexer redis)
            => _db = redis.GetDatabase();

        public async Task SetJobAsync(JobResult job)
        {
            var key = $"job:{job.JobId}";
            var json = JsonSerializer.Serialize(job);
            await _db.StringSetAsync(key, json, _jobTtl);

            if (job.UserId != null)
                await _db.SortedSetAddAsync(
                    $"user_jobs:{job.UserId}",
                    job.JobId,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public async Task<JobResult?> GetJobAsync(string jobId)
        {
            var val = await _db.StringGetAsync($"job:{jobId}");
            return val.IsNull ? null : JsonSerializer.Deserialize<JobResult>(val!);
        }

        public async Task<JobResult?> GetJobForUserAsync(string jobId, string firebaseUid)
        {
            var job = await GetJobAsync(jobId);
            if (job == null) return null;
            if (job.UserId != null && job.UserId != firebaseUid) return null;
            return job;
        }

        public async Task<List<JobResult>> GetJobsByUserAsync(string firebaseUid)
        {
            var jobIds = await _db.SortedSetRangeByScoreAsync(
        $"user_jobs:{firebaseUid}",
        start: double.NegativeInfinity,
        stop: double.PositiveInfinity,   
        order: Order.Descending,
        take: 50);

            var jobs = new List<JobResult>();
            foreach (var id in jobIds)
            {
                var job = await GetJobAsync(id!);
                if (job != null) jobs.Add(job);
            }
            return jobs;
        }

        public async Task SetSessionAsync(string sessionId, List<ChatMessage> messages)
        {
            var key = $"session:{sessionId}";
            var json = JsonSerializer.Serialize(messages);
            await _db.StringSetAsync(key, json, _sessionTtl);
        }

        public async Task<List<ChatMessage>> GetSessionAsync(string sessionId)
        {
            var val = await _db.StringGetAsync($"session:{sessionId}");
            return val.IsNull
                ? new List<ChatMessage>()
                : JsonSerializer.Deserialize<List<ChatMessage>>(val!) ?? new();
        }

        public async Task AppendToSessionAsync(string sessionId, ChatMessage message)
        {
            var messages = await GetSessionAsync(sessionId);
            messages.Add(message);
            await SetSessionAsync(sessionId, messages);
        }
    }
}
