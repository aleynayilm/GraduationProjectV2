using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using Quartz;
using System.Security.Claims;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class SearchHistoryControllerFullTests
    {
        private readonly Mock<IServiceManager>       _managerMock;
        private readonly Mock<ISearchHistoryService> _searchHistoryMock;
        private readonly SearchHistoryController     _controller;
        private const string Uid = "user-id-search-001"; 

        public SearchHistoryControllerFullTests()
        {
            _searchHistoryMock = new Mock<ISearchHistoryService>();
            _managerMock       = new Mock<IServiceManager>();
            _managerMock.Setup(m => m.SearchHistoryService).Returns(_searchHistoryMock.Object);

            _controller = new SearchHistoryController(_managerMock.Object);
            SetUser(_controller, Uid);
        }

        private static void SetUser(ControllerBase ctrl, string userId)
        {
            var claims   = new[] { new Claim("user_id", userId) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private static void SetAnonymousUser(ControllerBase ctrl)
        {
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };
        }

        // GetRecentSearchesAsync  GET /recent

        [Fact]
        public async Task GetRecentSearches_ReturnsOk_WithHistoryList()
        {
            var history = new List<SearchHistory>
            {
                new() { SearchUrl = "https://a.com" },
                new() { SearchUrl = "https://b.com" }
            };
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync(history);

            var result = await _controller.GetRecentSearchesAsync();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Equal(2, list.Count());
        }

        [Fact]
        public async Task GetRecentSearches_ReturnsOk_WithEmptyList()
        {
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync([]);

            var result = await _controller.GetRecentSearchesAsync();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetRecentSearches_Returns40_WhenUidIsNull()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.GetRecentSearchesAsync();

            var status = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(40, status.StatusCode);
        }

        [Fact]
        public async Task GetRecentSearches_CallsServiceWithCorrectUid()
        {
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync([]);

            await _controller.GetRecentSearchesAsync();

            _searchHistoryMock.Verify(s => s.GetRecentSearchesAsync(Uid), Times.Once);
        }

        // AddSearchAsync  POST /add

        [Fact]
        public async Task AddSearch_ReturnsOk_WithSuccessMessage()
        {
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), Uid))
                              .Returns(Task.CompletedTask);

            var result = await _controller.AddSearchAsync("https://example.com/product");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("successfully", ok.Value!.ToString());
        }

        [Fact]
        public async Task AddSearch_ReturnsBadRequest_WhenUrlIsEmpty()
        {
            var result = await _controller.AddSearchAsync("");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AddSearch_ReturnsBadRequest_WhenUrlIsWhitespace()
        {
            var result = await _controller.AddSearchAsync("   ");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AddSearch_Returns40_WhenUidIsNull()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.AddSearchAsync("https://valid-url.com");

            var status = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(40, status.StatusCode);
        }

        [Fact]
        public async Task AddSearch_CallsService_WithCorrectArguments()
        {
            const string url = "https://example.com/my-product";
            _searchHistoryMock.Setup(s => s.AddSearchAsync(url, Uid)).Returns(Task.CompletedTask);

            await _controller.AddSearchAsync(url);

            _searchHistoryMock.Verify(s => s.AddSearchAsync(url, Uid), Times.Once);
        }

        [Fact]
        public async Task AddSearch_DoesNotCallService_WhenUrlIsInvalid()
        {
            await _controller.AddSearchAsync("");

            _searchHistoryMock.Verify(s => s.AddSearchAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
    }

    public class UsersControllerFullTests
    {
        private readonly Mock<IServiceManager> _managerMock;
        private readonly Mock<IUserService>    _userServiceMock;
        private readonly Mock<UserJobScheduler> _schedulerMock;
        private readonly UsersController       _controller;
        private const string Uid = "firebase-uid-users-001";

        public UsersControllerFullTests()
        {
            _userServiceMock = new Mock<IUserService>();
            _managerMock     = new Mock<IServiceManager>();
            _schedulerMock   = new Mock<UserJobScheduler>(Mock.Of<ISchedulerFactory>(), Mock.Of<ILogger<UserJobScheduler>>());
            _managerMock.Setup(m => m.UserService).Returns(_userServiceMock.Object);

            _controller = new UsersController(_managerMock.Object);
            SetUser(_controller, Uid);
        }

        private static void SetUser(ControllerBase ctrl, string uid)
        {
            var claims   = new[] { new Claim("firebase_uid", uid) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
        }

        private static void SetAnonymousUser(ControllerBase ctrl)
        {
            ctrl.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };
        }

        // GetOneUser — 500 branch

        [Fact]
        public async Task GetOneUser_Returns500_WhenServiceThrows()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(It.IsAny<string>()))
                            .ThrowsAsync(new Exception("DB hatası"));

            var result = await _controller.GetOneUser("uid-throws");

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
        }

        // CreateOneUser — exception branch

        [Fact]
        public async Task CreateOneUser_ReturnsBadRequest_WhenServiceThrows()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(It.IsAny<string>()))
                            .ReturnsAsync((User?)null);
            _userServiceMock.Setup(s => s.CreateOneUserAsync(It.IsAny<User>()))
                            .ThrowsAsync(new Exception("Kayıt hatası"));

            var result = await _controller.CreateOneUser(
                new User { FirebaseUid = "uid-ex", Email = "a@b.com" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // UpdateOneUser — 500 branch

        [Fact]
        public async Task UpdateOneUser_Returns500_WhenServiceThrows()
        {
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .ThrowsAsync(new Exception("Güncelleme hatası"));

            var result = await _controller.UpdateOneUser("id-throws", new User());

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
        }

        // UpdatePushToken — unauthenticated

        [Fact]
        public async Task UpdatePushToken_Returns401_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.UpdatePushToken(
                new UsersController.UpdatePushTokenRequest("token-abc"));

            Assert.IsType<UnauthorizedResult>(result);
        }

        // RegisterUserFromFirebase — exception + unauthenticated branches

        [Fact]
        public async Task RegisterUserFromFirebase_Returns401_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.RegisterUserFromFirebase(
                new UsersController.RegisterRequest("a@b.com", "Ad", "Soyad"));

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RegisterUserFromFirebase_Returns500_WhenServiceThrows()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid))
                            .ThrowsAsync(new Exception("DB hatası"));

            var result = await _controller.RegisterUserFromFirebase(
                new UsersController.RegisterRequest("a@b.com", "Ad", "Soyad"));

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, status.StatusCode);
        }

        // UpdatePriceAlert — tüm branch'ler

        [Fact]
        public async Task UpdatePriceAlert_Returns401_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(true, 24),
                _schedulerMock.Object);

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task UpdatePriceAlert_Returns404_WhenUserNotFound()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid))
                            .ReturnsAsync((User?)null);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(true, 12),
                _schedulerMock.Object);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdatePriceAlert_Returns204_WhenEnabled_AndSchedulesJob()
        {
            var user = new User { Id = "u-pa-1", FirebaseUid = Uid };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .Returns(Task.CompletedTask);
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(Uid, 24)).Returns(Task.CompletedTask);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(Enabled: true, IntervalHours: 24),
                _schedulerMock.Object);

            Assert.IsType<NoContentResult>(result);
            _schedulerMock.Verify(s => s.ScheduleOrUpdateAsync(Uid, 24), Times.Once);
        }

        [Fact]
        public async Task UpdatePriceAlert_Returns204_WhenDisabled_AndRemovesJob()
        {
            var user = new User { Id = "u-pa-2", FirebaseUid = Uid };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .Returns(Task.CompletedTask);
            _schedulerMock.Setup(s => s.RemoveAsync(Uid)).Returns(Task.CompletedTask);

            var result = await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(Enabled: false, IntervalHours: 0),
                _schedulerMock.Object);

            Assert.IsType<NoContentResult>(result);
            _schedulerMock.Verify(s => s.RemoveAsync(Uid), Times.Once);
            _schedulerMock.Verify(s => s.ScheduleOrUpdateAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdatePriceAlert_UpdatesUserFields_Correctly()
        {
            var user = new User { Id = "u-pa-3", FirebaseUid = Uid };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);
            _userServiceMock.Setup(s => s.UpdateOneUserAsync(It.IsAny<string>(), It.IsAny<User>()))
                            .Returns(Task.CompletedTask);
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(It.IsAny<string>(), It.IsAny<int>()))
                          .Returns(Task.CompletedTask);

            await _controller.UpdatePriceAlert(
                new UsersController.UpdatePriceAlertRequest(Enabled: true, IntervalHours: 6),
                _schedulerMock.Object);

            _userServiceMock.Verify(s => s.UpdateOneUserAsync(
                "u-pa-3",
                It.Is<User>(u => u.PriceAlertEnabled == true &&
                                 u.PriceCheckIntervalHours == 6)),
                Times.Once);
        }

        // TestPriceCheck

        [Fact]
        public async Task TestPriceCheck_Returns200_WithMessage()
        {
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(Uid, 1)).Returns(Task.CompletedTask);

            var result = await _controller.TestPriceCheck(_schedulerMock.Object);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("tetiklendi", ok.Value!.ToString());
        }

        [Fact]
        public async Task TestPriceCheck_CallsScheduler_WithIntervalOne()
        {
            _schedulerMock.Setup(s => s.ScheduleOrUpdateAsync(Uid, 1)).Returns(Task.CompletedTask);

            await _controller.TestPriceCheck(_schedulerMock.Object);

            _schedulerMock.Verify(s => s.ScheduleOrUpdateAsync(Uid, 1), Times.Once);
        }
    }
}
