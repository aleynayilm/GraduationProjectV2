using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Extensions;
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
    [Authorize]
    public class FavoriteController : ControllerBase
    {
        private readonly IServiceManager _serviceManager;

        public FavoriteController(IServiceManager serviceManager)
        {
            _serviceManager = serviceManager;
        }
        
        [HttpGet("{id}")]
        public IActionResult GetOneFavorite(string id)
        {
            var favorite = _serviceManager.FavoriteService.GetOneFavorite(id);
            if (favorite is null) return NotFound();
            return Ok(favorite);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyFavorites()
        {
            var firebaseUid = User.GetFirebaseUid();
            if (string.IsNullOrEmpty(firebaseUid)) return Unauthorized();

            var favorites = await _serviceManager.FavoriteService.GetUserFavoriteDetailsAsync(firebaseUid);
            return Ok(favorites);
        }

        [HttpPost]
        public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
        {
            var firebaseUid = User.GetFirebaseUid();
            if (string.IsNullOrEmpty(firebaseUid)) return Unauthorized();

            try
            {
                var favorite = await _serviceManager.FavoriteService.AddFavoriteAsync(
                    new FavoriteDtoForCreate
                    {
                        FirebaseUid = firebaseUid,
                        ProductPlatformId = request.ProductPlatformId
                    });

                return StatusCode(201, favorite);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{favoriteId}")]
        public async Task<IActionResult> RemoveFavorite(string favoriteId)
        {
            var firebaseUid = User.GetFirebaseUid();
            if (string.IsNullOrEmpty(firebaseUid)) return Unauthorized();

            var favorite = _serviceManager.FavoriteService.GetOneFavorite(favoriteId);
            if (favorite == null) return NotFound();

            var user = await _serviceManager.UserService.GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null || favorite.UserId != user.Id) return Forbid();

            await _serviceManager.FavoriteService.DeleteFavoriteAsync(favoriteId);
            return NoContent();
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleFavorite([FromBody] AddFavoriteRequest request)
        {
            var firebaseUid = User.GetFirebaseUid();
            if (string.IsNullOrEmpty(firebaseUid)) return Unauthorized();

            try
            {
                var user = await _serviceManager.UserService.GetOneUserByFirebaseUidAsync(firebaseUid);
                if (user == null) return Unauthorized();

                var existing = _serviceManager.FavoriteService
                    .GetAllFavorites()
                    .FirstOrDefault(f =>
                        f.ProductPlatformId == request.ProductPlatformId &&
                        f.UserId == user.Id);

                if (existing == null)
                {
                    var added = await _serviceManager.FavoriteService.AddFavoriteAsync(
                        new FavoriteDtoForCreate
                        {
                            FirebaseUid = firebaseUid,
                            ProductPlatformId = request.ProductPlatformId
                        });
                    return Ok(new { added = true, favoriteId = added.FavoriteId });
                }
                else
                {
                    await _serviceManager.FavoriteService.DeleteFavoriteAsync(existing.FavoriteId);
                    return Ok(new { added = false });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("categorize")]
        public IActionResult CategorizeFavorites()
        {
            var firebaseUid = User.GetFirebaseUid();
            if (string.IsNullOrEmpty(firebaseUid)) return Unauthorized();

            _ = Task.Run(() =>
                _serviceManager.FavoriteService.CategorizeFavoritesAsync(firebaseUid));

            return Accepted(new { message = "Kategorize işlemi başlatıldı." });
        }

        public record AddFavoriteRequest(string ProductPlatformId);
    }
}
