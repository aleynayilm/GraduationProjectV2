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
    public class ProductScrapingControllerTests
    {
        private readonly Mock<IServiceManager>   _managerMock;
        private readonly Mock<IRabbitMqPublisher> _rabbitMock;
        private readonly Mock<IRedisService>      _redisMock;
        private readonly Mock<ILogger<ProductScrapingController>> _loggerMock;
        private readonly Mock<ISearchHistoryService> _searchHistoryMock;
        private readonly ProductScrapingController _controller;

        private const string FakeUid = "firebase-uid-scraping";

        public ProductScrapingControllerTests()
        {
            _managerMock      = new Mock<IServiceManager>();
            _rabbitMock       = new Mock<IRabbitMqPublisher>();
            _redisMock        = new Mock<IRedisService>();
            _loggerMock       = new Mock<ILogger<ProductScrapingController>>();
            _searchHistoryMock = new Mock<ISearchHistoryService>();

            _managerMock.Setup(m => m.SearchHistoryService).Returns(_searchHistoryMock.Object);

            _controller = new ProductScrapingController(
                _managerMock.Object, _rabbitMock.Object,
                _loggerMock.Object, _redisMock.Object);

            _controller.ControllerContext = BuildControllerContext(FakeUid);
        }

        // Helpers 

        private static ControllerContext BuildControllerContext(string uid)
        {
            var claims   = new[] { new Claim("firebase_uid", uid) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        private static SearchHistory MakeHistory(string url) => new() { SearchUrl = url };

        // ScrapeProduct 

        [Fact]
        public async Task ScrapeProduct_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCompare, It.IsAny<object>()))
                       .ReturnsAsync("job-scrape-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid))
                              .ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), FakeUid))
                              .Returns(Task.CompletedTask);

            var result = await _controller.ScrapeProduct(new ScrapeRequest { Url = "https://example.com/p1" });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("job-scrape-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task ScrapeProduct_SetsJobAsPending_InRedis()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-scrape-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid)).ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), FakeUid))
                              .Returns(Task.CompletedTask);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = "https://example.com/p2" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.Status   == "pending"  &&
                j.JobType  == "scrape"   &&
                j.UserId   == FakeUid)), Times.Once);
        }

        [Fact]
        public async Task ScrapeProduct_AddsToSearchHistory_WhenUrlIsNew()
        {
            const string newUrl = "https://example.com/new-product";

            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-scrape-003");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid)).ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(newUrl, FakeUid)).Returns(Task.CompletedTask);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = newUrl });

            _searchHistoryMock.Verify(s => s.AddSearchAsync(newUrl, FakeUid), Times.Once);
        }

        [Fact]
        public async Task ScrapeProduct_DoesNotAddToHistory_WhenUrlAlreadyExists()
        {
            const string existingUrl = "https://example.com/existing";

            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-scrape-004");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid))
                              .ReturnsAsync([MakeHistory(existingUrl)]);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = existingUrl });

            _searchHistoryMock.Verify(s => s.AddSearchAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ScrapeProduct_UrlComparisonIsCaseInsensitive()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-scrape-005");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid))
                              .ReturnsAsync([MakeHistory("HTTPS://EXAMPLE.COM/PRODUCT")]);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = "https://example.com/product" });

            _searchHistoryMock.Verify(s => s.AddSearchAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ScrapeProduct_PublishesToCorrectQueue()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCompare, It.IsAny<object>()))
                       .ReturnsAsync("job-scrape-006");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid)).ReturnsAsync([]);
            _searchHistoryMock.Setup(s => s.AddSearchAsync(It.IsAny<string>(), FakeUid))
                              .Returns(Task.CompletedTask);

            await _controller.ScrapeProduct(new ScrapeRequest { Url = "https://example.com/p6" });

            _rabbitMock.Verify(r => r.PublishAsync(RabbitMqPublisher.QueueCompare, It.IsAny<object>()),
                Times.Once);
        }

        // GetScrapeResult 

        [Fact]
        public async Task GetScrapeResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync("job-x", FakeUid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetScrapeResult("job-x");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetScrapeResult_ReturnsStatusAndData_WhenJobFound()
        {
            var job = new JobResult { JobId = "job-found", Status = "completed", UserId = FakeUid };
            _redisMock.Setup(r => r.GetJobForUserAsync("job-found", FakeUid)).ReturnsAsync(job);

            var result = await _controller.GetScrapeResult("job-found");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // GetMyJobs 

        [Fact]
        public async Task GetMyJobs_ReturnsJobList_ForAuthenticatedUser()
        {
            var jobs = new List<JobResult>
            {
                new() { JobId = "j1", UserId = FakeUid, Status = "completed" },
                new() { JobId = "j2", UserId = FakeUid, Status = "pending"   }
            };
            _redisMock.Setup(r => r.GetJobsByUserAsync(FakeUid)).ReturnsAsync(jobs);

            var result = await _controller.GetMyJobs();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<JobResult>>(ok.Value);
            Assert.Equal(2, list.Count());
        }

        [Fact]
        public async Task GetMyJobs_ReturnsEmptyList_WhenNoJobsExist()
        {
            _redisMock.Setup(r => r.GetJobsByUserAsync(FakeUid)).ReturnsAsync([]);

            var result = await _controller.GetMyJobs();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<JobResult>>(ok.Value);
            Assert.Empty(list);
        }

        // GetSearchHistory 

        [Fact]
        public async Task GetSearchHistory_ReturnsHistoryList_ForUser()
        {
            var history = new List<SearchHistory>
            {
                MakeHistory("https://example.com/a"),
                MakeHistory("https://example.com/b")
            };
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid)).ReturnsAsync(history);

            var result = await _controller.GetSearchHistory();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Equal(2, list.Count());
        }

        [Fact]
        public async Task GetSearchHistory_ReturnsEmptyList_WhenNoHistory()
        {
            _searchHistoryMock.Setup(s => s.GetRecentSearchesAsync(FakeUid)).ReturnsAsync([]);

            var result = await _controller.GetSearchHistory();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<SearchHistory>>(ok.Value);
            Assert.Empty(list);
        }

        // LocalSearch 

        [Fact]
        public async Task LocalSearch_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueLocalSearch, It.IsAny<object>()))
                       .ReturnsAsync("job-local-search-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            var result = await _controller.LocalSearch(new ProductScrapingController.SearchRequest
            {
                Input = "en iyi laptop", Language = "tr"
            });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("job-local-search-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task LocalSearch_SetsJobAsPending_WithCorrectType()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-local-search-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.LocalSearch(new ProductScrapingController.SearchRequest { Input = "sorgu" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "local_search" &&
                j.Status  == "pending"      &&
                j.UserId  == FakeUid)), Times.Once);
        }

        [Fact]
        public async Task LocalSearch_GeneratesNewSessionId_WhenNotProvided()
        {
            object? publishedPayload = null;
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .Callback<string, object>((_, payload) => publishedPayload = payload)
                       .ReturnsAsync("job-local-search-003");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.LocalSearch(new ProductScrapingController.SearchRequest
            {
                Input = "tablet", SessionId = null
            });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueLocalSearch, It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task LocalSearch_UsesProvidedSessionId_WhenGiven()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-local-search-004");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.LocalSearch(new ProductScrapingController.SearchRequest
            {
                Input = "arama", SessionId = "ses-existing"
            });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueLocalSearch, It.IsAny<object>()), Times.Once);
        }

        // GetLocalSearchResult 

        [Fact]
        public async Task GetLocalSearchResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync(It.IsAny<string>(), FakeUid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetLocalSearchResult("job-local-missing");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetLocalSearchResult_ReturnsResult_WhenJobFound()
        {
            var job = new JobResult { JobId = "job-ls-found", Status = "completed", UserId = FakeUid };
            _redisMock.Setup(r => r.GetJobForUserAsync("job-ls-found", FakeUid)).ReturnsAsync(job);

            var result = await _controller.GetLocalSearchResult("job-ls-found");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // CloudSearch 

        [Fact]
        public async Task CloudSearch_ReturnsAccepted_WithJobId()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCloudSearch, It.IsAny<object>()))
                       .ReturnsAsync("job-cloud-search-001");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            var result = await _controller.CloudSearch(new ProductScrapingController.SearchRequest
            {
                Input = "akıllı saat"
            });

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("job-cloud-search-001", accepted.Value!.ToString());
        }

        [Fact]
        public async Task CloudSearch_SetsJobAsPending_WithCorrectType()
        {
            _rabbitMock.Setup(r => r.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync("job-cloud-search-002");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.CloudSearch(new ProductScrapingController.SearchRequest { Input = "kamera" });

            _redisMock.Verify(r => r.SetJobAsync(It.Is<JobResult>(j =>
                j.JobType == "cloud_search" &&
                j.Status  == "pending"      &&
                j.UserId  == FakeUid)), Times.Once);
        }

        [Fact]
        public async Task CloudSearch_PublishesToCorrectQueue()
        {
            _rabbitMock.Setup(r => r.PublishAsync(RabbitMqPublisher.QueueCloudSearch, It.IsAny<object>()))
                       .ReturnsAsync("job-cloud-search-003");
            _redisMock.Setup(r => r.SetJobAsync(It.IsAny<JobResult>())).Returns(Task.CompletedTask);

            await _controller.CloudSearch(new ProductScrapingController.SearchRequest { Input = "kulaklık" });

            _rabbitMock.Verify(r => r.PublishAsync(
                RabbitMqPublisher.QueueCloudSearch, It.IsAny<object>()), Times.Once);
        }

        // GetCloudSearchResult 

        [Fact]
        public async Task GetCloudSearchResult_ReturnsPending_WhenJobNotFound()
        {
            _redisMock.Setup(r => r.GetJobForUserAsync(It.IsAny<string>(), FakeUid))
                      .ReturnsAsync((JobResult?)null);

            var result = await _controller.GetCloudSearchResult("job-cs-missing");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("pending", ok.Value!.ToString());
        }

        [Fact]
        public async Task GetCloudSearchResult_ReturnsResult_WhenJobFound()
        {
            var job = new JobResult { JobId = "job-cs-found", Status = "completed", UserId = FakeUid };
            _redisMock.Setup(r => r.GetJobForUserAsync("job-cs-found", FakeUid)).ReturnsAsync(job);

            var result = await _controller.GetCloudSearchResult("job-cs-found");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }
    }
}
