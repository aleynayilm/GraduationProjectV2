using ProductAnalysisApp.Services.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface IRedisService
    {
        Task SetJobAsync(JobResult job);
        Task<JobResult?> GetJobForUserAsync(string jobId, string uid);
        Task<List<JobResult>> GetJobsByUserAsync(string uid);
        Task SetSessionAsync(string sessionId, List<ChatMessage> messages);
        Task<List<ChatMessage>> GetSessionAsync(string sessionId);
        Task AppendToSessionAsync(string sessionId, ChatMessage message);
    }
}
