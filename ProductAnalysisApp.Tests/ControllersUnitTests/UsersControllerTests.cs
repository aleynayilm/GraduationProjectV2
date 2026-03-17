using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class UsersControllerTests : ControllerTestBase
    {
        private readonly Mock<IServiceManager> _serviceManagerMock;
        private readonly Mock<IUserService>    _userServiceMock;
        private readonly UsersController       _sut;
        private readonly User                  _testUser;

        public UsersControllerTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
            _userServiceMock    = new Mock<IUserService>();

            _serviceManagerMock.Setup(m => m.UserService).Returns(_userServiceMock.Object);

            _sut = new UsersController(_serviceManagerMock.Object);
            SetAuthenticatedUser(_sut);

            _testUser = new User
            {
                Id          = TestUserId,
                FirebaseUid = TestFirebaseUid,
                Email       = TestEmail,
                FirstName   = "Test",
                LastName    = "User"
            };
        }

        // ── GetAllUsers ──────────────────────────────────────────────────

        [Fact]
        public async Task GetAllUsers_ShouldReturn200WithUserList()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetAllUsersAsync())
                .ReturnsAsync(new List<User> { _testUser });

            // Act
            var result = await _sut.GetAllUsers();

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var users = ok.Value.Should().BeAssignableTo<List<User>>().Subject;
            users.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetAllUsers_WhenServiceThrows_ShouldReturn500()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetAllUsersAsync())
                .ThrowsAsync(new Exception("DB error"));

            // Act
            var result = await _sut.GetAllUsers();

            // Assert
            var status = result.Should().BeOfType<ObjectResult>().Subject;
            status.StatusCode.Should().Be(500);
        }

        // ── GetOneUser ───────────────────────────────────────────────────

        [Fact]
        public async Task GetOneUser_WhenExists_ShouldReturn200()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            // Act
            var result = await _sut.GetOneUser(TestFirebaseUid);

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(_testUser);
        }

        [Fact]
        public async Task GetOneUser_WhenNotFound_ShouldReturn404()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync("nonexistent"))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _sut.GetOneUser("nonexistent");

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task GetOneUser_WhenEmptyUid_ShouldReturn401()
        {
            // Act
            var result = await _sut.GetOneUser("");

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        // ── CreateOneUser ────────────────────────────────────────────────

        [Fact]
        public async Task CreateOneUser_WithNewUser_ShouldReturn201()
        {
            // Arrange
            var newUser = new User { FirebaseUid = "new-uid", Email = "new@example.com" };

            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync("new-uid"))
                .ReturnsAsync((User?)null);

            _userServiceMock
                .Setup(s => s.CreateOneUserAsync(It.IsAny<User>()))
                .ReturnsAsync(newUser);

            // Act
            var result = await _sut.CreateOneUser(newUser);

            // Assert
            var status = result.Should().BeOfType<ObjectResult>().Subject;
            status.StatusCode.Should().Be(201);
        }

        [Fact]
        public async Task CreateOneUser_WhenAlreadyExists_ShouldReturn200()
        {
            // Arrange
            var existingUser = new User { FirebaseUid = TestFirebaseUid };

            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            // Act
            var result = await _sut.CreateOneUser(existingUser);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CreateOneUser_WithNullBody_ShouldReturn400()
        {
            // Act
            var result = await _sut.CreateOneUser(null!);

            // Assert
            result.Should().BeOfType<BadRequestResult>();
        }

        [Fact]
        public async Task CreateOneUser_WithEmptyFirebaseUid_ShouldReturn400()
        {
            // Arrange
            var user = new User { FirebaseUid = "" };

            // Act
            var result = await _sut.CreateOneUser(user);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        // ── RegisterUserFromFirebase ─────────────────────────────────────

        [Fact]
        public async Task RegisterUserFromFirebase_WhenNewUser_ShouldReturn201()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync((User?)null);

            _userServiceMock
                .Setup(s => s.CreateOneUserAsync(It.IsAny<User>()))
                .ReturnsAsync(_testUser);

            var request = new UsersController.RegisterRequest("test@example.com", "Test", "User");

            // Act
            var result = await _sut.RegisterUserFromFirebase(request);

            // Assert
            var status = result.Should().BeOfType<ObjectResult>().Subject;
            status.StatusCode.Should().Be(201);
        }

        [Fact]
        public async Task RegisterUserFromFirebase_WhenAlreadyExists_ShouldReturn200()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            var request = new UsersController.RegisterRequest(null, null, null);

            // Act
            var result = await _sut.RegisterUserFromFirebase(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task RegisterUserFromFirebase_WhenNotAuthenticated_ShouldReturn401()
        {
            // Arrange
            SetAnonymousUser(_sut);
            var request = new UsersController.RegisterRequest(null, null, null);

            // Act
            var result = await _sut.RegisterUserFromFirebase(request);

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        // ── UpdatePushToken ──────────────────────────────────────────────

        [Fact]
        public async Task UpdatePushToken_WhenUserExists_ShouldReturn204()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            _userServiceMock
                .Setup(s => s.UpdateOneUserAsync(TestUserId, It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            var request = new UsersController.UpdatePushTokenRequest("new-fcm-token");

            // Act
            var result = await _sut.UpdatePushToken(request);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            _userServiceMock.Verify(s => s.UpdateOneUserAsync(
                TestUserId,
                It.Is<User>(u => u.PushToken == "new-fcm-token")),
                Times.Once);
        }

        [Fact]
        public async Task UpdatePushToken_WhenUserNotFound_ShouldReturn404()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync((User?)null);

            var request = new UsersController.UpdatePushTokenRequest("token");

            // Act
            var result = await _sut.UpdatePushToken(request);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        // ── UpdateOneUser ────────────────────────────────────────────────

        [Fact]
        public async Task UpdateOneUser_WhenValid_ShouldReturn204()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.UpdateOneUserAsync(TestUserId, It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.UpdateOneUser(TestUserId, _testUser);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateOneUser_WithNullBody_ShouldReturn400()
        {
            // Act
            var result = await _sut.UpdateOneUser(TestUserId, null!);

            // Assert
            result.Should().BeOfType<BadRequestResult>();
        }

        // ── DeleteOneUser ────────────────────────────────────────────────

        [Fact]
        public async Task DeleteOneUser_WhenExists_ShouldReturn204()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.DeleteOneUserAsync(TestUserId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.DeleteOneUser(TestUserId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task DeleteOneUser_WhenServiceThrows_ShouldReturn500()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.DeleteOneUserAsync("bad-id"))
                .ThrowsAsync(new Exception("User not found"));

            // Act
            var result = await _sut.DeleteOneUser("bad-id");

            // Assert
            var status = result.Should().BeOfType<ObjectResult>().Subject;
            status.StatusCode.Should().Be(500);
        }
    }
}
