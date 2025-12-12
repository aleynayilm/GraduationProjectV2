using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.DataTransferObjects;
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
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        [HttpPost("compare")]
        public async Task<IActionResult> CompareProducts([FromBody] UrlRequest request)
        {
            var pythonApi = "http://localhost:8000/compare";
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, request);
            var resultObj = await response.Content.ReadFromJsonAsync<object>();
            var resultJson = JsonSerializer.Serialize(resultObj);
            sw.Stop();

            var sessionId = Guid.NewGuid().ToString();

            ChatSessionStore.Sessions[sessionId] = new List<ChatMessage>
    {
        new ChatMessage
        {
            Role = "assistant",
            Content = resultJson
        }
    };

            return Ok(new
            {
                sessionId,
                response = resultObj,
                responseTimeMs = sw.ElapsedMilliseconds
            });
        }
        [HttpPost("localllmcompare")]
        public async Task<IActionResult> CompareLocalLlm([FromBody] LocalLlmCompareRequest request)
        {

            var pythonApi = "http://localhost:8000/api/localllmcompare";
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, request);
            var resultObj = await response.Content.ReadFromJsonAsync<object>();
            var resultJson = JsonSerializer.Serialize(resultObj);
            sw.Stop();

            var sessionId = Guid.NewGuid().ToString();

            ChatSessionStore.Sessions[sessionId] = new List<ChatMessage>
    {
        new ChatMessage
        {
            Role = "assistant",
            Content = resultJson
        }
    };

            return Ok(new
            {
                sessionId,
                response = resultObj,
                responseTimeMs = sw.ElapsedMilliseconds
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
                sessionId = request.SessionId,
                message = request.Message,
                history = ChatSessionStore.Sessions[request.SessionId]
            };

            var response = await _httpClient.PostAsJsonAsync(pythonApi, payload);
            var result = await response.Content.ReadAsStringAsync();

            ChatSessionStore.Sessions[request.SessionId].Add(
                new ChatMessage { Role = "user", Content = request.Message }
            );
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(result);
            var reply = json["reply"];
            ChatSessionStore.Sessions[request.SessionId].Add(
                new ChatMessage { Role = "assistant", Content = reply }
            );

            return Ok(new { response = result });
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
