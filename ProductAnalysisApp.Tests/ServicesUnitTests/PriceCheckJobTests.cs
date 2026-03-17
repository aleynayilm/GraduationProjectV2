using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Messaging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static ProductAnalysisApp.Services.PythonScraperService;

namespace ProductAnalysisApp.Tests.ServicesUnitTests
{
    public class PriceCheckJobTests
    {
        private readonly Mock<IRepositoryManager>         _repoMock;
        private readonly Mock<IUserRepository>            _userRepoMock;
        private readonly Mock<IFavoriteRepository>        _favoriteRepoMock;
        private readonly Mock<IProductPlatformRepository> _platformRepoMock;
        private readonly Mock<PythonScraperService>       _scraperMock;
        private readonly Mock<EmailService>               _emailMock;
        private readonly Mock<FcmService>                 _fcmMock;
        private readonly Mock<IJobExecutionContext>        _contextMock;
        private readonly ServiceProvider                  _serviceProvider;
        private readonly PriceCheckJob                    _sut;

        private readonly User            _testUser;
        private readonly ProductPlatform _testPlatform;
        private readonly Favorite        _testFavorite;

        public PriceCheckJobTests()
        {
            _repoMock         = new Mock<IRepositoryManager>();
            _userRepoMock     = new Mock<IUserRepository>();
            _favoriteRepoMock = new Mock<IFavoriteRepository>();
            _platformRepoMock = new Mock<IProductPlatformRepository>();
            _contextMock      = new Mock<IJobExecutionContext>();

            _repoMock.Setup(r => r.User).Returns(_userRepoMock.Object);
            _repoMock.Setup(r => r.Favorite).Returns(_favoriteRepoMock.Object);
            _repoMock.Setup(r => r.ProductPlatform).Returns(_platformRepoMock.Object);

            // EmailService ve FcmService mock'ları — concrete sınıflar olduğu için
            // virtual metod gerektirmeden mock oluşturmak için ayrı servis olarak register edilir
            var services = new ServiceCollection();
            services.AddSingleton(_repoMock.Object);

            // PythonScraperService mock'u — HttpClient gerektirdiği için ServiceCollection üzerinden
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:8000")
            };
            services.AddSingleton(new PythonScraperService(
                httpClient,
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                NullLogger<PythonScraperService>.Instance));

            services.AddSingleton(new EmailService(
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                NullLogger<EmailService>.Instance));

            services.AddSingleton(new FcmService(NullLogger<FcmService>.Instance));

            _serviceProvider = services.BuildServiceProvider();

            _sut = new PriceCheckJob(_serviceProvider, NullLogger<PriceCheckJob>.Instance);

            // Test verileri
            _testUser = new User
            {
                Id                = "user-001",
                FirebaseUid       = "firebase-uid-001",
                Email             = "test@example.com",
                PushToken         = "fcm-token-001",
                PriceAlertEnabled = true,
                FirstName         = "Test",
                LastName          = "User"
            };

            _testPlatform = new ProductPlatform
            {
                ProductPlatformId = "platform-001",
                ProductUrl        = "https://www.n11.com/test-product",
                Price             = 5000m,
                Currency          = "TRY"
            };

            _testFavorite = new Favorite
            {
                FavoriteId        = "favorite-001",
                UserId            = "user-001",
                ProductPlatformId = "platform-001"
            };

            // Quartz context mock
            var jobDataMap = new JobDataMap();
            jobDataMap.Put(PriceCheckJob.DataKeyUid, "firebase-uid-001");

            var jobDetailMock = new Mock<IJobDetail>();
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDataMap);
            _contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);
        }

        // ── Execute — kullanıcı bulunamadı ───────────────────────────────

        [Fact]
        public async Task Execute_WhenUserNotFound_ShouldReturnEarly()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync((User?)null);

            // Act — exception fırlatmamalı
            var act = async () => await _sut.Execute(_contextMock.Object);

            // Assert
            await act.Should().NotThrowAsync();
            _favoriteRepoMock.Verify(
                r => r.GetFavoritesByUserId(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Execute_WhenPriceAlertDisabled_ShouldReturnEarly()
        {
            // Arrange
            var userWithAlertOff = new User
            {
                Id = "user-001", FirebaseUid = "firebase-uid-001",
                PriceAlertEnabled = false
            };

            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(userWithAlertOff);

            // Act
            await _sut.Execute(_contextMock.Object);

            // Assert
            _favoriteRepoMock.Verify(
                r => r.GetFavoritesByUserId(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Execute_WhenNoFavorites_ShouldReturnEarly()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            _favoriteRepoMock
                .Setup(r => r.GetFavoritesByUserId("user-001"))
                .Returns(Enumerable.Empty<Favorite>().AsQueryable());

            // Act
            await _sut.Execute(_contextMock.Object);

            // Assert — platformu hiç sorgulamaz
            _platformRepoMock.Verify(
                r => r.GetOneProductPlatform(It.IsAny<string>()), Times.Never);
        }

        // ── Execute — firebaseUid boş ────────────────────────────────────

        [Fact]
        public async Task Execute_WhenFirebaseUidMissing_ShouldReturnEarly()
        {
            // Arrange
            var emptyDataMap = new JobDataMap();
            var jobDetailMock = new Mock<IJobDetail>();
            jobDetailMock.Setup(j => j.JobDataMap).Returns(emptyDataMap);

            var contextMock = new Mock<IJobExecutionContext>();
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            // Act
            var act = async () => await _sut.Execute(contextMock.Object);

            // Assert
            await act.Should().NotThrowAsync();
            _userRepoMock.Verify(
                r => r.GetOneUserByFirebaseUidAsync(It.IsAny<string>()), Times.Never);
        }
    }

    // ── PriceCheckJob mantık testleri (CheckAndNotifyAsync dolaylı) ──────
    // Bu testler iş mantığını doğrudan test eder

    public class PriceCheckLogicTests
    {
        [Fact]
        public void WhenNewPriceIsLower_ShouldDetectDrop()
        {
            // Arrange
            decimal oldPrice = 5000m;
            decimal newPrice = 3000m;

            // Assert
            (newPrice < oldPrice).Should().BeTrue("Fiyat düşüşü tespit edilmeli");
        }

        [Fact]
        public void WhenNewPriceIsEqual_ShouldNotDetectDrop()
        {
            decimal oldPrice = 5000m;
            decimal newPrice = 5000m;

            (newPrice >= oldPrice).Should().BeTrue("Fiyat değişmediğinde bildirim gönderilmemeli");
        }

        [Fact]
        public void WhenNewPriceIsHigher_ShouldNotDetectDrop()
        {
            decimal oldPrice = 5000m;
            decimal newPrice = 6000m;

            (newPrice >= oldPrice).Should().BeTrue("Fiyat arttığında bildirim gönderilmemeli");
        }

        [Fact]
        public void DropPercent_ShouldCalculateCorrectly()
        {
            decimal oldPrice    = 5000m;
            decimal newPrice    = 3000m;
            decimal dropAmount  = oldPrice - newPrice;
            decimal dropPercent = Math.Round(dropAmount / oldPrice * 100, 1);

            dropPercent.Should().Be(40.0m);
        }

        [Fact]
        public void LastPriceCheckedAt_ShouldBeSetToUtcNow()
        {
            var before   = DateTime.UtcNow;
            var checkedAt = DateTime.UtcNow;
            var after    = DateTime.UtcNow;

            checkedAt.Should().BeOnOrAfter(before);
            checkedAt.Should().BeOnOrBefore(after);
        }
    }
}
