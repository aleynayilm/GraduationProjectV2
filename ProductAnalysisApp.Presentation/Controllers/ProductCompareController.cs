using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.DataTransferObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
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
        }

        [HttpPost("compare")]
        public async Task<IActionResult> CompareProducts([FromBody] UrlRequest request)
        {
            var pythonApi = "http://localhost:8000/compare";
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, request);
            var result = await response.Content.ReadFromJsonAsync<object>();
            sw.Stop();

            return Ok(new
            {
                response = result,
                responseTimeMs = sw.ElapsedMilliseconds
            });
        }
        [HttpPost("localllmcompare")]
        public async Task<IActionResult> CompareLocalLlm([FromBody] LocalLlmCompareRequest request)
        {

            var pythonApi = "http://localhost:8000/api/localllmcompare";
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(pythonApi, request);
            var result = await response.Content.ReadFromJsonAsync<object>();
            sw.Stop();

            return Ok(new
            {
                response = result,
                responseTimeMs = sw.ElapsedMilliseconds
            });
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
