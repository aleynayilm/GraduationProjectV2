using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SearchHistoryController : ControllerBase
    {
        private readonly ISearchHistoryService _searchHistoryService;

        public SearchHistoryController(IServiceManager serviceManager)
        {
            _searchHistoryService = serviceManager.SearchHistoryService;
        }

        // GET: api/SearchHistory/recent
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentSearchesAsync()
        {
            var firebaseUid = User.FindFirst("user_id")?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
                return StatusCode(40);

            var searches = await _searchHistoryService.GetRecentSearchesAsync(firebaseUid);
            return Ok(searches);
        }

        // POST: api/SearchHistory/add
        [HttpPost("add")]
        public async Task<IActionResult> AddSearchAsync([FromBody] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest(new { message = "URL cannot be empty." });

            var firebaseUid = User.FindFirst("user_id")?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
                return StatusCode(40);

            await _searchHistoryService.AddSearchAsync(url, firebaseUid);
            return Ok(new { message = "Search added successfully" });
        }
    }
}
