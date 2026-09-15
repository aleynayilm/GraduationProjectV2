using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services.Contracts;
using System.Security.Claims;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class SearchHistoryControllerTests : ControllerTestBase
    {
        private readonly Mock<IServiceManager>       _managerMock;
        private readonly Mock<ISearchHistoryService> _searchHistoryMock;
        private readonly SearchHistoryController     _controller;

        private new const string TestFirebaseUid = "firebase-uid-test-001";

        public SearchHistoryControllerTests()
        {
            _searchHistoryMock = new Mock<ISearchHistoryService>();
            _managerMock       = new Mock<IServiceManager>();
            _managerMock.Setup(m => m.SearchHistoryService).Returns(_searchHistoryMock.Object);

            _controller = new SearchHistoryController(_managerMock.Object);
            SetSearchHistoryUser(_controller, TestFirebaseUid);
        }

        private static void SetSearchHistoryUser(ControllerBase controller, string userId)
        {
            var claims = new List<Claim>
            {
                new("user_id", userId),
            };
            var identity  = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        // GetRecentSearchesAsync 

        [Fact]
        public async Task GetRecentSearches_ReturnsOk_WithHistoryList()
        {
            var history = new List<SearchHistory>
            {
                new() { SearchUrl = "https://example.com/a" },
                new() { SearchUrl = "https://example.com/b" }
            };
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(TestFirebaseUid))
                              .ReturnsAsync(history);

            var result = await _controller.GetRecentSearchesAsync();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Equal(2, list.Count());
        }

        [Fact]
        public async Task GetRecentSearches_ReturnsOk_WithEmptyList()
        {
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(TestFirebaseUid))
                              .ReturnsAsync([]);

            var result = await _controller.GetRecentSearchesAsync();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetRecentSearches_Returns40_WhenFirebaseUidIsNull()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.GetRecentSearchesAsync();

            var status = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(40, status.StatusCode);
        }

        [Fact]
        public async Task GetRecentSearches_CallsServiceWithCorrectUid()
        {
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(TestFirebaseUid))
                              .ReturnsAsync([]);

            await _controller.GetRecentSearchesAsync();

            _searchHistoryMock.Verify(s => s.GetRecentSearchesAsync(TestFirebaseUid), Times.Once);
        }

        // AddSearchAsync

        [Fact]
        public async Task AddSearch_ReturnsOk_WithSuccessMessage()
        {
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), TestFirebaseUid))
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
        public async Task AddSearch_Returns40_WhenFirebaseUidIsNull()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.AddSearchAsync("https://example.com/product");

            var status = Assert.IsType<StatusCodeResult>(result);
            Assert.Equal(40, status.StatusCode);
        }

        [Fact]
        public async Task AddSearch_CallsServiceWithCorrectArguments()
        {
            const string url = "https://example.com/my-product";
            _searchHistoryMock.Setup(s => s.AddSearchAsync(url, TestFirebaseUid))
                              .Returns(Task.CompletedTask);

            await _controller.AddSearchAsync(url);

            _searchHistoryMock.Verify(s => s.AddSearchAsync(url, TestFirebaseUid), Times.Once);
        }

        [Fact]
        public async Task AddSearch_DoesNotCallService_WhenUrlIsInvalid()
        {
            await _controller.AddSearchAsync("");

            _searchHistoryMock.Verify(s => s.AddSearchAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
    }
}
