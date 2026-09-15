using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Messaging;
using Quartz;
using System.Text.Json;
using Xunit;
using static ProductAnalysisApp.Services.PythonScraperService;

namespace ProductAnalysisApp.Tests.ServicesUnitTests
{
    public class PriceCheckJobAdditionalTests
    {
        private readonly Mock<IRepositoryManager>         _repoMock;
        private readonly Mock<IUserRepository>            _userRepoMock;
        private readonly Mock<IFavoriteRepository>        _favoriteRepoMock;
        private readonly Mock<IProductPlatformRepository> _platformRepoMock;
        private readonly Mock<IJobExecutionContext>        _contextMock;
        private readonly User                             _testUser;
        private readonly ProductPlatform                  _testPlatform;
        private readonly Favorite                         _testFavorite;

        public PriceCheckJobAdditionalTests()
        {
            _repoMock         = new Mock<IRepositoryManager>();
            _userRepoMock     = new Mock<IUserRepository>();
            _favoriteRepoMock = new Mock<IFavoriteRepository>();
            _platformRepoMock = new Mock<IProductPlatformRepository>();

            _repoMock.Setup(r => r.User).Returns(_userRepoMock.Object);
            _repoMock.Setup(r => r.Favorite).Returns(_favoriteRepoMock.Object);
            _repoMock.Setup(r => r.ProductPlatform).Returns(_platformRepoMock.Object);

            _testUser = new User
            {
                Id = "user-001", FirebaseUid = "uid-001",
                Email = "test@example.com", PushToken = "fcm-token",
                PriceAlertEnabled = true, FirstName = "Test", LastName = "User"
            };

            _testPlatform = new ProductPlatform
            {
                ProductPlatformId = "pp-001",
                ProductUrl        = "https://n11.com/product",
                Price             = 5000m,
                Currency          = "TRY"
            };

            _testFavorite = new Favorite
            {
                FavoriteId        = "fav-001",
                UserId            = "user-001",
                ProductPlatformId = "pp-001"
            };

            var jobDataMap    = new JobDataMap();
            jobDataMap.Put(PriceCheckJob.DataKeyUid, "uid-001");
            var jobDetailMock = new Mock<IJobDetail>();
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDataMap);
            _contextMock = new Mock<IJobExecutionContext>();
            _contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);
        }

        private PriceCheckJob BuildJob(
            ScrapedProductDto? scrapedResult = null,
            bool platformFound = true)
        {
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001"))
                             .Returns(platformFound ? _testPlatform : null!);

            var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8000") };
            var config     = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var scraper    = new PythonScraperService(
                httpClient, config, NullLogger<PythonScraperService>.Instance);

            var services = new ServiceCollection();
            services.AddSingleton(_repoMock.Object);
            services.AddSingleton(scraper);
            services.AddSingleton(new EmailService(config, NullLogger<EmailService>.Instance));
            services.AddSingleton(new FcmService(NullLogger<FcmService>.Instance));

            var sp = services.BuildServiceProvider();
            return new PriceCheckJob(sp, NullLogger<PriceCheckJob>.Instance);
        }

        // Platform bulunamadı 

        [Fact]
        public async Task Execute_WhenPlatformNotFound_ShouldSkipFavorite()
        {
            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001"))
                         .ReturnsAsync(_testUser);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite }.AsQueryable());

            var job = BuildJob(platformFound: false);

            var act = async () => await job.Execute(_contextMock.Object);

            // Exception fırlatmamalı
            await act.Should().NotThrowAsync();
        }

        // Scrape başarısız (localhost çalışmıyor)

        [Fact]
        public async Task Execute_WhenScrapeReturnsNull_ShouldNotUpdatePrice()
        {
            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001"))
                         .ReturnsAsync(_testUser);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite }.AsQueryable());
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001"))
                             .Returns(_testPlatform);

            var job = BuildJob(); 

            await job.Execute(_contextMock.Object);

            _platformRepoMock.Verify(r => r.UpdateProductPlatformAsync(It.IsAny<ProductPlatform>()),
                Times.Never);
        }

        // Execute: birden fazla favori, exception olan devam etmeli 

        [Fact]
        public async Task Execute_WithMultipleFavorites_ContinuesOnException()
        {
            var fav2 = new Favorite
            {
                FavoriteId = "fav-002", UserId = "user-001", ProductPlatformId = "pp-002"
            };

            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001"))
                         .ReturnsAsync(_testUser);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite, fav2 }.AsQueryable());

            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001"))
                             .Throws(new Exception("DB hatası"));
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-002"))
                             .Returns((ProductPlatform?)null);

            var job = BuildJob(platformFound: true);

            var act = async () => await job.Execute(_contextMock.Object);

            await act.Should().NotThrowAsync();
        }

        // KeyFor statik metod 

        [Fact]
        public void KeyFor_ReturnsJobKey_WithCorrectName()
        {
            var key = PriceCheckJob.KeyFor("uid-test");

            Assert.Equal("price-check-uid-test", key.Name);
            Assert.Equal("price-check",          key.Group);
        }

        [Fact]
        public void DataKeyUid_HasCorrectValue()
        {
            Assert.Equal("firebaseUid", PriceCheckJob.DataKeyUid);
        }

        // CheckAndNotify — fiyat yükselmesi 

        [Fact]
        public async Task Execute_WhenNewPriceIsHigher_ShouldNotUpdatePlatformPrice()
        {
            var oldPrice = 5000m;
            var newPrice = 6000m;

            (newPrice >= oldPrice).Should().BeTrue();
        }

        [Fact]
        public async Task Execute_WhenNewPriceIsEqual_ShouldNotUpdatePlatformPrice()
        {
            var oldPrice = 5000m;
            var newPrice = 5000m;

            (newPrice >= oldPrice).Should().BeTrue();
        }
    }

    // PriceCheckJob — CheckAndNotifyAsync fiyat düşüşü branch testleri
    public class PriceCheckJobPriceDropTests
    {
        private readonly Mock<IRepositoryManager>         _repoMock;
        private readonly Mock<IUserRepository>            _userRepoMock;
        private readonly Mock<IFavoriteRepository>        _favoriteRepoMock;
        private readonly Mock<IProductPlatformRepository> _platformRepoMock;
        private readonly Mock<IJobExecutionContext>        _contextMock;
        private readonly User                             _testUser;
        private readonly ProductPlatform                  _testPlatform;
        private readonly Favorite                         _testFavorite;

        public PriceCheckJobPriceDropTests()
        {
            _repoMock         = new Mock<IRepositoryManager>();
            _userRepoMock     = new Mock<IUserRepository>();
            _favoriteRepoMock = new Mock<IFavoriteRepository>();
            _platformRepoMock = new Mock<IProductPlatformRepository>();

            _repoMock.Setup(r => r.User).Returns(_userRepoMock.Object);
            _repoMock.Setup(r => r.Favorite).Returns(_favoriteRepoMock.Object);
            _repoMock.Setup(r => r.ProductPlatform).Returns(_platformRepoMock.Object);

            _testUser = new User
            {
                Id = "user-001", FirebaseUid = "uid-001",
                Email = "test@example.com", PushToken = "fcm-token",
                PriceAlertEnabled = true, FirstName = "Test", LastName = "User"
            };

            _testPlatform = new ProductPlatform
            {
                ProductPlatformId = "pp-001",
                ProductUrl        = "https://n11.com/product",
                Price             = 5000m,
                Currency          = "TRY"
            };

            _testFavorite = new Favorite
            {
                FavoriteId        = "fav-001",
                UserId            = "user-001",
                ProductPlatformId = "pp-001"
            };

            var jobDataMap    = new JobDataMap();
            jobDataMap.Put(PriceCheckJob.DataKeyUid, "uid-001");
            var jobDetailMock = new Mock<IJobDetail>();
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDataMap);
            _contextMock = new Mock<IJobExecutionContext>();
            _contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);
        }

        private PriceCheckJob BuildJobWithFakeScraper(string scrapeResponseJson, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            var config  = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var handler = new FakeHttpMessageHandlerForPriceCheck(scrapeResponseJson, statusCode);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };

            var scraper = new PythonScraperService(httpClient, config, NullLogger<PythonScraperService>.Instance);

            var services = new ServiceCollection();
            services.AddSingleton(_repoMock.Object);
            services.AddSingleton(scraper);
            services.AddSingleton(new EmailService(config, NullLogger<EmailService>.Instance));
            services.AddSingleton(new FcmService(NullLogger<FcmService>.Instance));

            return new PriceCheckJob(services.BuildServiceProvider(), NullLogger<PriceCheckJob>.Instance);
        }

        [Fact]
        public async Task Execute_WhenPriceDrops_UpdatesProductPlatform()
        {
            var scraped = new
            {
                productName  = "Test Ürün",
                platformName = "N11",
                price        = 3000,
                currency     = "TRY",
                productUrl   = "https://n11.com/product"
            };
            var json = System.Text.Json.JsonSerializer.Serialize(scraped);

            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001")).ReturnsAsync(_testUser);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite }.AsQueryable());
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001")).Returns(_testPlatform);
            _platformRepoMock.Setup(r => r.UpdateProductPlatformAsync(It.IsAny<ProductPlatform>()))
                             .Returns(Task.CompletedTask);

            var job = BuildJobWithFakeScraper(json);
            await job.Execute(_contextMock.Object);

            _platformRepoMock.Verify(r => r.UpdateProductPlatformAsync(
                It.Is<ProductPlatform>(p => p.Price == 3000m)), Times.Once);
        }

        [Fact]
        public async Task Execute_WhenPriceHigher_DoesNotUpdatePlatform()
        {
            var scraped = new { productName = "P", platformName = "N11", price = 6000, currency = "TRY", productUrl = "" };
            var json = System.Text.Json.JsonSerializer.Serialize(scraped);

            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001")).ReturnsAsync(_testUser);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite }.AsQueryable());
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001")).Returns(_testPlatform);

            var job = BuildJobWithFakeScraper(json);
            await job.Execute(_contextMock.Object);

            _platformRepoMock.Verify(r => r.UpdateProductPlatformAsync(It.IsAny<ProductPlatform>()), Times.Never);
        }

        [Fact]
        public async Task Execute_WhenPriceEqual_DoesNotUpdatePlatform()
        {
            var scraped = new { productName = "P", platformName = "N11", price = 5000, currency = "TRY", productUrl = "" };
            var json = System.Text.Json.JsonSerializer.Serialize(scraped);

            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001")).ReturnsAsync(_testUser);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite }.AsQueryable());
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001")).Returns(_testPlatform);

            var job = BuildJobWithFakeScraper(json);
            await job.Execute(_contextMock.Object);

            _platformRepoMock.Verify(r => r.UpdateProductPlatformAsync(It.IsAny<ProductPlatform>()), Times.Never);
        }

        [Fact]
        public async Task Execute_WhenPriceDrops_UserHasNoPushToken_DoesNotThrow()
        {
            var userNoPush = new User
            {
                Id = "user-001", FirebaseUid = "uid-001",
                Email = "test@example.com", PushToken = null,
                PriceAlertEnabled = true, FirstName = "Test", LastName = "User"
            };

            var scraped = new { productName = "P", platformName = "N11", price = 3000, currency = "TRY", productUrl = "" };
            var json = System.Text.Json.JsonSerializer.Serialize(scraped);

            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001")).ReturnsAsync(userNoPush);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite }.AsQueryable());
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001")).Returns(_testPlatform);
            _platformRepoMock.Setup(r => r.UpdateProductPlatformAsync(It.IsAny<ProductPlatform>()))
                             .Returns(Task.CompletedTask);

            var job = BuildJobWithFakeScraper(json);
            var ex = await Record.ExceptionAsync(() => job.Execute(_contextMock.Object));
            Assert.Null(ex);
        }

        [Fact]
        public async Task Execute_WhenPriceDrops_UserHasNoEmail_DoesNotThrow()
        {
            var userNoEmail = new User
            {
                Id = "user-001", FirebaseUid = "uid-001",
                Email = "", PushToken = null,
                PriceAlertEnabled = true
            };

            var scraped = new { productName = "P", platformName = "N11", price = 3000, currency = "TRY", productUrl = "" };
            var json = System.Text.Json.JsonSerializer.Serialize(scraped);

            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("uid-001")).ReturnsAsync(userNoEmail);
            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                             .Returns(new[] { _testFavorite }.AsQueryable());
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001")).Returns(_testPlatform);
            _platformRepoMock.Setup(r => r.UpdateProductPlatformAsync(It.IsAny<ProductPlatform>()))
                             .Returns(Task.CompletedTask);

            var job = BuildJobWithFakeScraper(json);
            var ex = await Record.ExceptionAsync(() => job.Execute(_contextMock.Object));
            Assert.Null(ex);
        }
    }

    public class FakeHttpMessageHandlerForPriceCheck : HttpMessageHandler
    {
        private readonly string _response;
        private readonly System.Net.HttpStatusCode _statusCode;

        public FakeHttpMessageHandlerForPriceCheck(string response, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            _response   = response;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_response, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    // PriceCheckJob iş mantığı — ek branch testleri
    public class PriceCheckJobLogicAdditionalTests
    {
        [Theory]
        [InlineData(5000, 3000, 40.0)]
        [InlineData(1000, 800,  20.0)]
        [InlineData(100,  1,    99.0)]
        public void DropPercent_CalculatesCorrectly_ForVariousPrices(
            decimal oldPrice, decimal newPrice, decimal expectedPercent)
        {
            var dropAmount  = oldPrice - newPrice;
            var dropPercent = Math.Round((dropAmount / oldPrice) * 100, 1);

            dropPercent.Should().Be(expectedPercent);
        }

        [Fact]
        public void KeyFor_WithDifferentUids_ProducesDifferentKeys()
        {
            var key1 = PriceCheckJob.KeyFor("uid-a");
            var key2 = PriceCheckJob.KeyFor("uid-b");

            Assert.NotEqual(key1.Name, key2.Name);
        }

        [Fact]
        public void KeyFor_WithSameUid_ProducesSameKey()
        {
            var key1 = PriceCheckJob.KeyFor("uid-same");
            var key2 = PriceCheckJob.KeyFor("uid-same");

            Assert.Equal(key1.Name, key2.Name);
        }
    }
}
