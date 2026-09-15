using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.Messaging;
using System.Security.Claims;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class ProductScrapingControllerFullTests
    {
        private readonly Mock<IServiceManager>       _managerMock;
        private readonly Mock<IRabbitMqPublisher> _rabbitMock;
        private readonly Mock<IRedisService> _redisMock;
        private readonly Mock<ILogger<ProductScrapingController>> _loggerMock;
        private readonly Mock<ISearchHistoryService> _searchHistoryMock;
        private readonly ProductScrapingController   _controller;
        private const string Uid = "firebase-uid-scraping-001";

        public ProductScrapingControllerFullTests()
        {
            _searchHistoryMock = new Mock<ISearchHistoryService>();
            _managerMock       = new Mock<IServiceManager>();
            _rabbitMock = new Mock<IRabbitMqPublisher>();
            _redisMock = new Mock<IRedisService>();
            _loggerMock        = new Mock<ILogger<ProductScrapingController>>();

            _managerMock.Setup(m => m.SearchHistoryService).Returns(_searchHistoryMock.Object);

            _controller = new ProductScrapingController(
                _managerMock.Object, _rabbitMock.Object, _loggerMock.Object, _redisMock.Object);

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

        private static SearchHistory History(string url) => new() { SearchUrl = url };

        // ScrapeProduct  POST /scrape

        [Fact]
        public async Task ScrapeProduct_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCompare, It.IsAny<object>()))
                       .ReturnsAsync("sc-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), Uid))
                              .Returns(Task.CompletedTask);

            var result = await _controller.ScrapeProduct(
                new ScrapeRequest { Url = "https://trendyol.com/p1" });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("sc-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task ScrapeProduct_SetsJobPending_WithCorrectFields()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("sc-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), Uid))
                              .Returns(Task.CompletedTask);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = "https://x.com" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.Status  == "pending" &&
                j.JobType == "scrape"  &&
                j.UserId  == Uid)), Times.Once);
        }

        [Fact]
        public async Task ScrapeProduct_AddsToHistory_WhenUrlIsNew()
        {
            const string url = "https://new-product.com";
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("sc-003");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(url, Uid)).Returns(Task.CompletedTask);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = url });

            _searchHistoryMock.Verify(s => s.AddSearchAsync(url, Uid), Times.Once);
        }

        [Fact]
        public async Task ScrapeProduct_DoesNotAddToHistory_WhenUrlAlreadyExists()
        {
            const string url = "https://existing.com/product";
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("sc-004");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid))
                              .ReturnsAsync([History(url)]);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = url });

            _searchHistoryMock.Verify(s => s.AddSearchAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ScrapeProduct_UrlComparison_IsCaseInsensitive()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("sc-005");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid))
                              .ReturnsAsync([History("HTTPS://EXAMPLE.COM/PRODUCT")]);

            await _controller.ScrapeProduct(
                new ScrapeRequest { Url = "https://example.com/product" });

            _searchHistoryMock.Verify(s => s.AddSearchAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ScrapeProduct_PublishesToCorrectQueue()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCompare, It.IsAny<object>()))
                       .ReturnsAsync("sc-006");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), Uid))
                              .Returns(Task.CompletedTask);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = "https://q.com" });

            _rabbitMock.Verify(r => r.PublishAsync(RabbitMqPublisher.QueueCompare, It.IsAny<object>()),
                Times.Once);
        }

        // GetScrapeResult  GET /scrape/{jobId}

        [Fact]
        public async Task GetScrapeResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("sr-x", Uid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetScrapeResult("sr-x");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetScrapeResult_ReturnsStatusAndData_WhenJobFound()
        {
            var job = new JobResult { JobId = "sr-ok", Status = "completed", UserId = Uid };
            _redisMock.Setup(r => r.GetJobForUserAsync("sr-ok", Uid)).ReturnsAsync(job);

            var result = await _controller.GetScrapeResult("sr-ok");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("completed", ok.Value!.ToString());
        }

        // GetMyJobs  GET /jobs

        [Fact]
        public async Task GetMyJobs_ReturnsJobList()
        {
            var jobs = new List<JobResult>
            {
                new() { JobId = "j1", UserId = Uid, Status = "completed" },
                new() { JobId = "j2", UserId = Uid, Status = "pending"   }
            };
            _redisMock.Setup(r => r.GetJobsByUserAsync(Uid)).ReturnsAsync(jobs);

            var result = await _controller.GetMyJobs();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<JobResult>>(ok.Value);
            Assert.Equal(2, list.Count());
        }

        [Fact]
        public async Task GetMyJobs_ReturnsEmptyList_WhenNoJobs()
        {
            _redisMock.Setup(r => r.GetJobsByUserAsync(Uid)).ReturnsAsync([]);

            var result = await _controller.GetMyJobs();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<JobResult>>(ok.Value);
            Assert.Empty(list);
        }

        // GetSearchHistory  GET /search-history

        [Fact]
        public async Task GetSearchHistory_ReturnsHistoryList()
        {
            var history = new List<SearchHistory>
            {
                History("https://a.com"), History("https://b.com")
            };
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync(history);

            var result = await _controller.GetSearchHistory();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Equal(2, list.Count());
        }

        [Fact]
        public async Task GetSearchHistory_ReturnsEmptyList_WhenNoHistory()
        {
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(Uid)).ReturnsAsync([]);

            var result = await _controller.GetSearchHistory();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Empty(list);
        }

        // LocalSearch  POST /local-search

        [Fact]
        public async Task LocalSearch_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueLocalSearch, It.IsAny<object>()))
                       .ReturnsAsync("ls-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            var result = await _controller.LocalSearch(
                new ProductScrapingController.SearchRequest { Input = "en iyi laptop" });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("ls-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task LocalSearch_SetsJobPending_WithLocalSearchType()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("ls-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.LocalSearch(
                new ProductScrapingController.SearchRequest { Input = "sorgu" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "local_search" &&
                j.Status  == "pending" &&
                j.UserId  == Uid)), Times.Once);
        }

        [Fact]
        public async Task LocalSearch_GeneratesSessionId_WhenNotProvided()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("ls-003");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            var result = await _controller.LocalSearch(
                new ProductScrapingController.SearchRequest { Input = "t", SessionId = null });

            Assert.IsType<AcceptedResult>(result);
            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueLocalSearch, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task LocalSearch_UsesProvidedSessionId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("ls-004");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.LocalSearch(new ProductScrapingController.SearchRequest
            { Input = "x", SessionId = "ses-existing" });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueLocalSearch, It.IsAny<object>()), Times.Once);
        }

        // GetLocalSearchResult  GET /local-search/{jobId}

        [Fact]
        public async Task GetLocalSearchResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("lsr-x", Uid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetLocalSearchResult("lsr-x");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetLocalSearchResult_ReturnsResult_WhenJobFound()
        {
            var job = new JobResult { JobId = "lsr-ok", Status = "completed", UserId = Uid };
            _redisMock.Setup(r => r.GetJobForUserAsync("lsr-ok", Uid)).ReturnsAsync(job);

            var result = await _controller.GetLocalSearchResult("lsr-ok");

            Assert.IsType<OkObjectResult>(result);
        }

        // CloudSearch  POST /cloud-search

        [Fact]
        public async Task CloudSearch_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCloudSearch, It.IsAny<object>()))
                       .ReturnsAsync("cs-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            var result = await _controller.CloudSearch(
                new ProductScrapingController.SearchRequest { Input = "akıllı saat" });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("cs-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task CloudSearch_SetsJobPending_WithCloudSearchType()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("cs-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.CloudSearch(
                new ProductScrapingController.SearchRequest { Input = "tablet" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "cloud_search" &&
                j.UserId  == Uid)), Times.Once);
        }

        [Fact]
        public async Task CloudSearch_PublishesToCorrectQueue()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCloudSearch, It.IsAny<object>()))
                       .ReturnsAsync("cs-003");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.CloudSearch(
                new ProductScrapingController.SearchRequest { Input = "kamera" });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueCloudSearch, It.IsAny<object>()), Times.Once);
        }

        // GetCloudSearchResult  GET /cloud-search/{jobId}

        [Fact]
        public async Task GetCloudSearchResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("csr-x", Uid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetCloudSearchResult("csr-x");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetCloudSearchResult_ReturnsResult_WhenJobFound()
        {
            var job = new JobResult { JobId = "csr-ok", Status = "completed", UserId = Uid };
            _redisMock.Setup(r => r.GetJobForUserAsync("csr-ok", Uid)).ReturnsAsync(job);

            var result = await _controller.GetCloudSearchResult("csr-ok");

            Assert.IsType<OkObjectResult>(result);
        }
    }
}
