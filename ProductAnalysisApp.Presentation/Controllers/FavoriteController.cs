using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FavoriteController : ControllerBase
    {
        private readonly IServiceManager _serviceManager;

        public FavoriteController(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager;
        }
        [HttpGet]
        public IActionResult GetAllFavorites()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var favorites = _serviceManager.FavoriteService.GetAllFavorites();
                sw.Stop();
                return Ok(new ApiResponse<IEnumerable<Favorite>> { DurationMs = sw.ElapsedMilliseconds, Data = favorites });
            }
            catch (Exception ex) {
                sw.Stop();
                return BadRequest(new ApiResponse<string>
                {
                    DurationMs = sw.ElapsedMilliseconds,
                    Data = ex.Message
                });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetOneFavorite(string id)
        {
            var favorite = _serviceManager.FavoriteService.GetOneFavorite(id);
            if (favorite is null) return NotFound();
            return Ok(favorite);
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMyFavorites()
        {
            var sw = Stopwatch.StartNew();
            var firebaseUid = User.FindFirst("user_id")?.Value ?? User.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(firebaseUid))
            {
                sw.Stop();
                return BadRequest(new ApiResponse<string> { DurationMs = sw.ElapsedMilliseconds, Data = "User not authenticated." });
            }

            var favorites = await _serviceManager.FavoriteService.GetUserFavoritesAsync(firebaseUid);
            sw.Stop();
            return Ok(new ApiResponse<IEnumerable<Favorite>> { DurationMs = sw.ElapsedMilliseconds, Data = favorites });
        }

        [HttpPost]
        public async Task<IActionResult> CreateOneFavorite([FromBody] FavoriteDtoForCreate favoriteDto)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (favoriteDto is null)
                {
                    sw.Stop();
                    return BadRequest(new ApiResponse<string>
                    {
                        DurationMs = sw.ElapsedMilliseconds,
                        Data = "Favorite is null"
                    });
                }

                var firebaseUid = User.FindFirst("user_id")?.Value ?? User.FindFirst("uid")?.Value;
                if (string.IsNullOrEmpty(firebaseUid))
                {
                    sw.Stop();
                    return BadRequest(new ApiResponse<string>
                    {
                        DurationMs = sw.ElapsedMilliseconds,
                        Data = "User not authenticated."
                    });
                }

                var favorite = await _serviceManager.FavoriteService.AddFavoriteAsync(favoriteDto);

                sw.Stop();
                return StatusCode(201, new ApiResponse<Favorite>
                {
                    DurationMs = sw.ElapsedMilliseconds,
                    Data = favorite
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                return BadRequest(new ApiResponse<string>
                {
                    DurationMs = sw.ElapsedMilliseconds,
                    Data = ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOneFavorite(string id)
        {
            await _serviceManager.FavoriteService.DeleteFavoriteAsync(id);
            return NoContent();
        }
        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleFavorite([FromBody] FavoriteDtoForCreate favoriteDto)
        {
            try
            {
                var firebaseUid = User.FindFirst("user_id")?.Value ?? User.FindFirst("uid")?.Value;
                if (string.IsNullOrEmpty(firebaseUid)) return Unauthorized();

                var user = await _serviceManager.UserService.GetOneUserByFirebaseUidAsync(firebaseUid);
                if (user == null) return Unauthorized();
                var existing = _serviceManager.FavoriteService
                    .GetAllFavorites()
                    .FirstOrDefault(f => f.ProductPlatformId == favoriteDto.ProductPlatformId && f.UserId == user.Id);

                if (existing is null)
                {
                    var favorite = _serviceManager.FavoriteService.AddFavoriteAsync(favoriteDto);
                    return Ok(new { added = true, favorite });
                }
                else
                {
                    _serviceManager.FavoriteService.DeleteFavoriteAsync(existing.FavoriteId);
                    return Ok(new { added = false });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
