using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using Quartz;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class UsersControllerAdditionalTests : ControllerTestBase
    {
        private readonly Mock<IServiceManager>   _managerMock;
        private readonly Mock<IUserService>      _userServiceMock;
        private readonly Mock<UserJobScheduler>  _schedulerMock;
        private readonly UsersController         _controller;

        public UsersControllerAdditionalTests()
        {
            _userServiceMock = new Mock<IUserService>();
            _managerMock     = new Mock<IServiceManager>();
            _schedulerMock   = new Mock<UserJobScheduler>(Mock.Of<ISchedulerFactory>(), Mock.Of<ILogger<UserJobScheduler>>());

            _managerMock.Setup(m => m.UserService).Returns(_userServiceMock.Object);

            _controller = new UsersController(_managerMock.Object);
            SetAuthenticatedUser(_controller);
        }

        // GetOneUser 

        [Fact]
        public async Task GetOneUser_WhenServiceThrows_ShouldReturn500()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(It.IsAny<string>()))
                            .ThrowsAsync(new Exception("DB bağlantı hatası"));

            var result = await _controller.GetOneUser("uid-throws");

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
        }

        // CreateOneUser — exception senaryo

        [Fact]
        public async Task CreateOneUser_WhenServiceThrows_ShouldReturn400()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(It.IsAny<string>()))
                            .ReturnsAsync((User?)null);
            _userServiceMock.Setup(s => s.CreateOneUserAsync(It.IsAny<User>()))
                            .ThrowsAsync(new Exception("Kayıt hatası"));

            var user = new User { FirebaseUid = "uid-exception", Email = "x@y.com" };

            var result = await _controller.CreateOneUser(user);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // UpdateOneUser — exception senaryo 

        [Fact]
        public async Task UpdateOneUser_WhenServiceThrows_ShouldReturn500()
        {
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .ThrowsAsync(new Exception("Güncelleme hatası"));

            var result = await _controller.UpdateOneUser("id-throws", new User());

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
        }

        // UpdatePushToken — unauthenticated 

        [Fact]
        public async Task UpdatePushToken_WhenNotAuthenticated_ShouldReturn401()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.UpdatePushToken(
                new UsersController.UpdatePushTokenRequest("token-abc"));

            Assert.IsType<UnauthorizedResult>(result);
        }

        // UpdatePriceAlert 

        [Fact]
        public async Task UpdatePriceAlert_WhenNotAuthenticated_ShouldReturn401()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(true, 24),
                _schedulerMock.Object);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task UpdatePriceAlert_WhenUserNotFound_ShouldReturn404()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                            .ReturnsAsync((User?)null);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(true, 12),
                _schedulerMock.Object);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdatePriceAlert_WhenEnabled_ShouldScheduleJob()
        {
            var user = new User { Id = "user-id", FirebaseUid = TestFirebaseUid };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                            .ReturnsAsync(user);
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .Returns(Task.CompletedTask);
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(TestFirebaseUid, 24))
                          .Returns(Task.CompletedTask);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(Enabled: true, IntervalHours: 24),
                _schedulerMock.Object);

            Assert.IsType<NoContentResult>(result);
            _schedulerMock.Verify(s => s.ScheduleOrUpdateAsync(TestFirebaseUid, 24), Times.Once);
        }

        [Fact]
        public async Task UpdatePriceAlert_WhenDisabled_ShouldRemoveJob()
        {
            var user = new User { Id = "user-id", FirebaseUid = TestFirebaseUid };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                            .ReturnsAsync(user);
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .Returns(Task.CompletedTask);
            _schedulerMock.Setup(s => s.RemoveAsync(TestFirebaseUid))
                          .Returns(Task.CompletedTask);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(Enabled: false, IntervalHours: 0),
                _schedulerMock.Object);

            Assert.IsType<NoContentResult>(result);
            _schedulerMock.Verify(s => s.RemoveAsync(TestFirebaseUid), Times.Once);
            _schedulerMock.Verify(s => s.ScheduleOrUpdateAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdatePriceAlert_WhenEnabled_UpdatesUserFields()
        {
            var user = new User { Id = "user-id-2", FirebaseUid = TestFirebaseUid };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                            .ReturnsAsync(user);
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .Returns(Task.CompletedTask);
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(It.IsAny<string>(), It.IsAny<int>()))
                          .Returns(Task.CompletedTask);

            await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(Enabled: true, IntervalHours: 6),
                _schedulerMock.Object);

            _userServiceMock.Verify(s => s.UpdateOneUserAsync(
                "user-id-2",
                It.Is<User>(u => u.PriceAlertEnabled == true && u.PriceCheckIntervalHours == 6)),
                Times.Once);
        }

        [Fact]
        public async Task UpdatePriceAlert_WhenDisabled_SetsEnabledFalse()
        {
            var user = new User { Id = "user-id-3", FirebaseUid = TestFirebaseUid, PriceAlertEnabled = true };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                            .ReturnsAsync(user);
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .Returns(Task.CompletedTask);
            _schedulerMock.Setup(s => s.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

            await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(Enabled: false, IntervalHours: 0),
                _schedulerMock.Object);

            _userServiceMock.Verify(s => s.UpdateOneUserAsync(
                It.IsAny<string>(),
                It.Is<User>(u => u.PriceAlertEnabled == false)),
                Times.Once);
        }

        // TestPriceCheck 

        [Fact]
        public async Task TestPriceCheck_WhenAuthenticated_ShouldReturn200()
        {
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(TestFirebaseUid, 1))
                          .Returns(Task.CompletedTask);

            var result = await _controller.TestPriceCheck(_schedulerMock.Object);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("tetiklendi", ok.Value!.ToString());
        }

        [Fact]
        public async Task TestPriceCheck_CallsSchedulerWithIntervalOne()
        {
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(TestFirebaseUid, 1))
                          .Returns(Task.CompletedTask);

            await _controller.TestPriceCheck(_schedulerMock.Object);

            _schedulerMock.Verify(s => s.ScheduleOrUpdateAsync(TestFirebaseUid, 1), Times.Once);
        }

        // RegisterUserFromFirebase — exception senaryo 

        [Fact]
        public async Task RegisterUserFromFirebase_WhenServiceThrows_ShouldReturn500()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                            .ThrowsAsync(new Exception("DB hatası"));

            var result = await _controller.RegisterUserFromFirebase(
                new UsersController.RegisterRequest("test@test.com", "Ad", "Soyad"));

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
        }

        // RegisterRequest record — Email getter coverage 

        [Fact]
        public void RegisterRequest_Email_IsAccessible()
        {
            var req = new UsersController.RegisterRequest("user@example.com", "Ad", "Soyad");
            Assert.Equal("user@example.com", req.Email);
        }
    }
}
