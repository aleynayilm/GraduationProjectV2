using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Extensions;
using ProductAnalysisApp.Services;
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
    public class UsersController : ControllerBase
    {
        private readonly IServiceManager _manager;

        public UsersController(IServiceManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _manager.UserService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving users", error = ex.Message });
            }
        }

        [HttpGet("{firebaseUid}")]
        public async Task<IActionResult> GetOneUser([FromRoute] string firebaseUid)
        {
            if (string.IsNullOrEmpty(firebaseUid))
                return Unauthorized();

            try
            {
                var user = await _manager.UserService.GetOneUserByFirebaseUidAsync(firebaseUid);

                if (user == null)
                    return NotFound();

                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving user", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateOneUser([FromBody] User user)
        {
            if (user is null)
                return BadRequest();

            if (string.IsNullOrEmpty(user.FirebaseUid))
                return BadRequest("FirebaseUid is required.");

            try
            {
                var existingUser = await _manager.UserService.GetOneUserByFirebaseUidAsync(user.FirebaseUid);
                if (existingUser != null)
                    return Ok(existingUser);

                user.CreatedDate = DateTime.Now;
                var createdUser = await _manager.UserService.CreateOneUserAsync(user);

                return StatusCode(201, createdUser);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOneUser([FromRoute] string id, [FromBody] User user)
        {
            if (user is null)
                return BadRequest();

            try
            {
                await _manager.UserService.UpdateOneUserAsync(id, user);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOneUser([FromRoute] string id)
        {
            try
            {
                await _manager.UserService.DeleteOneUserAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        [HttpPost("register")]
        [Authorize]
        public async Task<IActionResult> RegisterUserFromFirebase([FromBody] RegisterRequest request)
        {
            var firebaseUid = User.GetFirebaseUid();
            var email = User.GetEmail();

            if (string.IsNullOrEmpty(firebaseUid))
                return Unauthorized();

            try
            {
                var existingUser = await _manager.UserService.GetOneUserByFirebaseUidAsync(firebaseUid);
                if (existingUser != null)
                    return Ok(existingUser);

                var user = new User
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    FirebaseUid = firebaseUid,
                    Email = email ?? request.Email ?? "",
                    FirstName = request.FirstName ?? "",
                    LastName = request.LastName ?? "",
                    PriceAlertEnabled = false,
                    PriceRange = 0,
                    CreatedDate = DateTime.UtcNow
                };

                var created = await _manager.UserService.CreateOneUserAsync(user);
                return StatusCode(201, created);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        public record RegisterRequest(
            string? Email,
            string? FirstName,
            string? LastName
        );

        [HttpPut("push-token")]
        [Authorize]
        public async Task<IActionResult> UpdatePushToken([FromBody] UpdatePushTokenRequest request)
        {
            var firebaseUid = User.GetFirebaseUid();
            if (string.IsNullOrEmpty(firebaseUid))
                return Unauthorized();

            var user = await _manager.UserService.GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null) return NotFound();

            user.PushToken = request.PushToken;
            await _manager.UserService.UpdateOneUserAsync(user.Id, user);

            return NoContent();
        }

        /// <summary>
        /// Fiyat bildirimi ve güncelleme sıklığını ayarlar.
        /// intervalHours: 1 | 6 | 12 | 24 | 48 | 72
        /// </summary>
        [HttpPut("price-alert")]
        [Authorize]
        public async Task<IActionResult> UpdatePriceAlert(
            [FromBody] UpdatePriceAlertRequest request,
            [FromServices] UserJobScheduler jobScheduler)
        {
            var firebaseUid = User.GetFirebaseUid();
            if (string.IsNullOrEmpty(firebaseUid)) return Unauthorized();

            var user = await _manager.UserService.GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null) return NotFound();

            user.PriceAlertEnabled = request.Enabled;
            user.PriceCheckIntervalHours = request.IntervalHours;
            await _manager.UserService.UpdateOneUserAsync(user.Id, user);

            if (request.Enabled)
                await jobScheduler.ScheduleOrUpdateAsync(firebaseUid, request.IntervalHours);
            else
                await jobScheduler.RemoveAsync(firebaseUid);

            return NoContent();
        }

        [HttpPost("test-price-check")]
        [Authorize]
        public async Task<IActionResult> TestPriceCheck(
        [FromServices] UserJobScheduler jobScheduler)
        {
            var firebaseUid = User.GetFirebaseUid()!;

            await jobScheduler.ScheduleOrUpdateAsync(firebaseUid, intervalHours: 1);

            return Ok(new { message = "Job tetiklendi, logları kontrol et." });
        }

        public record UpdatePriceAlertRequest(bool Enabled, int IntervalHours);

        public record UpdatePushTokenRequest(string PushToken);
    }
}
