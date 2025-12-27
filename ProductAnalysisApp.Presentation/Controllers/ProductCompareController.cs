using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Services;
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
        private readonly HttpClient _httpClient;

        public ProductCompareController(HttpClient httpClient)
        {
            _httpClient = httpClient;
            //_httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        [HttpPost("compare")]
        public async Task<IActionResult> CompareProducts([FromBody] UrlRequest request)
        {
            var pythonApi = "http://localhost:8000//api/cloudllmcompare";
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, request);
            var resultObj = await response.Content.ReadFromJsonAsync<object>();
            sw.Stop();

            var sessionId = Guid.NewGuid().ToString();
            var assistantContent = JsonSerializer.Serialize(resultObj);

            ChatSessionStore.Sessions[sessionId] = new List<ChatMessage>
    {
        new ChatMessage
        {
            Role = "assistant",
            Content = assistantContent
        }
    };

            return Ok(new ApiResponse<object>
            {
                SessionId = sessionId,
                Data = resultObj,
                DurationMs = sw.ElapsedMilliseconds
            });
        }
        [HttpPost("localllmcompare")]
        public async Task<IActionResult> CompareLocalLlm([FromBody] LocalLlmCompareRequest request)
        {

            var pythonApi = "http://localhost:8000/api/localllmcompare";
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, request);
            //var resultObj = await response.Content.ReadFromJsonAsync<object>();
            sw.Stop();
            if (!response.IsSuccessStatusCode)
                return StatusCode(500, "Python API error");

            var jobInfo = await response.Content.ReadFromJsonAsync<LlmJobCreateResponse>();

            return Ok(new ApiResponse<object>
            {
                SessionId = null,
                Data = jobInfo,
                DurationMs = sw.ElapsedMilliseconds
            });
        }

        [HttpGet("localllmcompare/result/{jobId}")]
        public async Task<IActionResult> GetLocalLlmResult(string jobId)
        {
            var pythonApi = $"http://localhost:8000/api/localllmcompare/result/{jobId}";

            var response = await _httpClient.GetAsync(pythonApi);

            if (!response.IsSuccessStatusCode)
                return StatusCode(500, "Python API error");

            var jobResult = await response.Content.ReadFromJsonAsync<LlmJobResultResponse>();

            if (jobResult.Status != "completed")
            {
                return Ok(new ApiResponse<object>
                {
                    Data = jobResult,
                    DurationMs = 0
                });
            }
            if (jobResult.Status == "failed")
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Data = jobResult.Error,
                    DurationMs = 0
                });
            }

            var sessionId = Guid.NewGuid().ToString();
            var assistantContent = JsonSerializer.Serialize(jobResult.Result);

            ChatSessionStore.Sessions[sessionId] = new List<ChatMessage>
    {
        new ChatMessage
        {
            Role = "assistant",
            Content = assistantContent
        }
    };

            return Ok(new ApiResponse<object>
            {
                SessionId = sessionId,
                Data = jobResult.Result,
                DurationMs = 0
            });
        }
        [HttpPost("chat")]
        public async Task<IActionResult> ContinueChat([FromBody] ContinueChatRequest request)
        {
            if (!ChatSessionStore.Sessions.ContainsKey(request.SessionId))
                return BadRequest("Session not found");

            var pythonApi = "http://localhost:8000/api/chat";

            var payload = new
            {
                message = request.Message,
                history = ChatSessionStore.Sessions[request.SessionId]
            };
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, payload);
            var resultObj = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            sw.Stop();

            var reply = resultObj?["reply"] ?? "";

            ChatSessionStore.Sessions[request.SessionId].Add(
                new ChatMessage { Role = "user", Content = request.Message }
            );
            ChatSessionStore.Sessions[request.SessionId].Add(
                new ChatMessage { Role = "assistant", Content = reply }
            );

            return Ok(new ApiResponse<string>
            {
                SessionId = request.SessionId,
                Data = reply,
                DurationMs = sw.ElapsedMilliseconds
            });
        }

        [HttpPost("chat-cloud")]
        public async Task<IActionResult> ContinueChatCloud([FromBody] ContinueChatRequest request)
        {
            if (!ChatSessionStore.Sessions.ContainsKey(request.SessionId))
                return BadRequest("Session not found");

            var pythonApi = "http://localhost:8000/api/chat-cloud";

            var payload = new
            {
                message = request.Message,
                history = ChatSessionStore.Sessions[request.SessionId]
            };

            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, payload);
            var resultObj = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            sw.Stop();

            var reply = resultObj?["reply"] ?? "";

            ChatSessionStore.Sessions[request.SessionId].Add(
                new ChatMessage { Role = "user", Content = request.Message }
            );

            ChatSessionStore.Sessions[request.SessionId].Add(
                new ChatMessage { Role = "assistant", Content = reply }
            );

            return Ok(new ApiResponse<string>
            {
                SessionId = request.SessionId,
                Data = reply,
                DurationMs = sw.ElapsedMilliseconds
            });
        }
        public class ContinueChatRequest
        {
            public string SessionId { get; set; }
            public string Message { get; set; }
        }
        public class UrlRequest
        {
            public List<string> Urls { get; set; }
        }
        public class LocalLlmCompareRequest
        {
            public List<ProductForComparisonDto> Products { get; set; }
        }
    }
}
