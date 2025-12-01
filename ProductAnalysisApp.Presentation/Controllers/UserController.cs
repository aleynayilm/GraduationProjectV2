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
    }
}
