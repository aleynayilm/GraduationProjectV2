using FluentAssertions;
using Moq;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Xunit;

namespace ProductAnalysisApp.Tests.ServicesUnitTests
{
    public class FavoriteManagerTests
    {
        // Test fixtures
        private readonly Mock<IRepositoryManager>           _repoMock;
        private readonly Mock<IUserRepository>              _userRepoMock;
        private readonly Mock<IFavoriteRepository>          _favoriteRepoMock;
        private readonly Mock<IProductPlatformRepository>   _platformRepoMock;
        private readonly Mock<IMapper>                      _mapperMock;
        private readonly Mock<IHttpClientFactory>           _httpFactoryMock;
        private readonly FavoriteManager                    _sut;

        private readonly User            _testUser;
        private readonly ProductPlatform _testPlatform;
        private readonly Favorite        _testFavorite;

        public FavoriteManagerTests()
        {
            _repoMock        = new Mock<IRepositoryManager>();
            _userRepoMock    = new Mock<IUserRepository>();
            _favoriteRepoMock= new Mock<IFavoriteRepository>();
            _platformRepoMock= new Mock<IProductPlatformRepository>();
            _mapperMock      = new Mock<IMapper>();
            _httpFactoryMock = new Mock<IHttpClientFactory>();

            _repoMock.Setup(r => r.User).Returns(_userRepoMock.Object);
            _repoMock.Setup(r => r.Favorite).Returns(_favoriteRepoMock.Object);
            _repoMock.Setup(r => r.ProductPlatform).Returns(_platformRepoMock.Object);

            _sut = new FavoriteManager(_repoMock.Object, _mapperMock.Object, _httpFactoryMock.Object);

            // Ortak test verileri
            _testUser = new User
            {
                Id          = "user-001",
                FirebaseUid = "firebase-uid-001",
                Email       = "test@example.com",
                FirstName   = "Test",
                LastName    = "User"
            };

            _testPlatform = new ProductPlatform
            {
                ProductPlatformId = "platform-001",
                ProductUrl        = "https://www.trendyol.com/test-product",
                Price             = 1500m,
                Currency          = "TRY",
                LastPriceCheckedAt= DateTime.UtcNow
            };

            _testFavorite = new Favorite
            {
                FavoriteId        = "favorite-001",
                UserId            = "user-001",
                ProductPlatformId = "platform-001",
                Category          = "Elektronik",
                CreatedDate       = DateTime.UtcNow
            };
        }

        // AddFavoriteAsync 

        [Fact]
        public async Task AddFavoriteAsync_WhenProductAndUserExist_ShouldCreateFavorite()
        {
            // Arrange
            var dto = new FavoriteDtoForCreate
            {
                FirebaseUid       = "firebase-uid-001",
                ProductPlatformId = "platform-001"
            };

            _platformRepoMock
                .Setup(r => r.GetOneProductPlatform("platform-001"))
                .Returns(_testPlatform);

            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            _favoriteRepoMock
                .Setup(r => r.GetAllFavorites())
                .Returns(Enumerable.Empty<Favorite>().AsQueryable());

            _favoriteRepoMock
                .Setup(r => r.AddFavoriteAsync(It.IsAny<Favorite>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.AddFavoriteAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.UserId.Should().Be("user-001");
            result.ProductPlatformId.Should().Be("platform-001");

            _favoriteRepoMock.Verify(
                r => r.AddFavoriteAsync(It.IsAny<Favorite>()), Times.Once);
        }

        [Fact]
        public async Task AddFavoriteAsync_WhenAlreadyFavorited_ShouldReturnExisting()
        {
            // Arrange
            var dto = new FavoriteDtoForCreate
            {
                FirebaseUid       = "firebase-uid-001",
                ProductPlatformId = "platform-001"
            };

            _platformRepoMock
                .Setup(r => r.GetOneProductPlatform("platform-001"))
                .Returns(_testPlatform);

            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            _favoriteRepoMock
                .Setup(r => r.GetAllFavorites())
                .Returns(new List<Favorite> { _testFavorite }.AsQueryable());

            // Act
            var result = await _sut.AddFavoriteAsync(dto);

            // Assert
            result.FavoriteId.Should().Be("favorite-001");
            _favoriteRepoMock.Verify(
                r => r.AddFavoriteAsync(It.IsAny<Favorite>()), Times.Never);
        }

        [Fact]
        public async Task AddFavoriteAsync_WhenProductNotFound_ShouldThrowException()
        {
            // Arrange
            var dto = new FavoriteDtoForCreate
            {
                FirebaseUid       = "firebase-uid-001",
                ProductPlatformId = "nonexistent-platform"
            };

            _platformRepoMock
                .Setup(r => r.GetOneProductPlatform("nonexistent-platform"))
                .Returns((ProductPlatform?)null);

            // Act & Assert
            await _sut.Invoking(s => s.AddFavoriteAsync(dto))
                .Should().ThrowAsync<Exception>()
                .WithMessage("*nonexistent-platform*");
        }

        [Fact]
        public async Task AddFavoriteAsync_WhenUserNotFound_ShouldThrowException()
        {
            // Arrange
            var dto = new FavoriteDtoForCreate
            {
                FirebaseUid       = "nonexistent-uid",
                ProductPlatformId = "platform-001"
            };

            _platformRepoMock
                .Setup(r => r.GetOneProductPlatform("platform-001"))
                .Returns(_testPlatform);

            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("nonexistent-uid"))
                .ReturnsAsync((User?)null);

            // Act & Assert
            await _sut.Invoking(s => s.AddFavoriteAsync(dto))
                .Should().ThrowAsync<Exception>()
                .WithMessage("*nonexistent-uid*");
        }

        [Fact]
        public async Task AddFavoriteAsync_WhenFirebaseUidEmpty_ShouldThrowArgumentNullException()
        {
            // Arrange
            var dto = new FavoriteDtoForCreate
            {
                FirebaseUid       = "",
                ProductPlatformId = "platform-001"
            };

            // Act & Assert
            await _sut.Invoking(s => s.AddFavoriteAsync(dto))
                .Should().ThrowAsync<ArgumentNullException>();
        }

        // DeleteFavoriteAsync 

        [Fact]
        public async Task DeleteFavoriteAsync_WhenExists_ShouldDelete()
        {
            // Arrange
            _favoriteRepoMock
                .Setup(r => r.GetOneFavorite("favorite-001"))
                .Returns(_testFavorite);

            _favoriteRepoMock
                .Setup(r => r.DeleteFavoriteAsync("favorite-001"))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.DeleteFavoriteAsync("favorite-001");

            // Assert
            _favoriteRepoMock.Verify(
                r => r.DeleteFavoriteAsync("favorite-001"), Times.Once);
        }

        [Fact]
        public async Task DeleteFavoriteAsync_WhenNotFound_ShouldThrowException()
        {
            // Arrange
            _favoriteRepoMock
                .Setup(r => r.GetOneFavorite("nonexistent"))
                .Returns((Favorite?)null);

            // Act & Assert
            await _sut.Invoking(s => s.DeleteFavoriteAsync("nonexistent"))
                .Should().ThrowAsync<Exception>()
                .WithMessage("*nonexistent*");
        }

        // GetUserFavoriteDetailsAsync 

        [Fact]
        public async Task GetUserFavoriteDetailsAsync_ShouldReturnDetailsWithPlatformInfo()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            _favoriteRepoMock
                .Setup(r => r.GetFavoritesByUserId("user-001"))
                .Returns(new List<Favorite> { _testFavorite }.AsQueryable());

            _platformRepoMock
                .Setup(r => r.GetOneProductPlatform("platform-001"))
                .Returns(_testPlatform);

            // Act
            var result = await _sut.GetUserFavoriteDetailsAsync("firebase-uid-001");

            // Assert
            result.Should().HaveCount(1);
            result[0].FavoriteId.Should().Be("favorite-001");
            result[0].ProductUrl.Should().Be("https://www.trendyol.com/test-product");
            result[0].Price.Should().Be(1500m);
            result[0].Category.Should().Be("Elektronik");
            result[0].LastPriceCheckedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUserFavoriteDetailsAsync_WhenUserNotFound_ShouldReturnEmptyList()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("nonexistent"))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _sut.GetUserFavoriteDetailsAsync("nonexistent");

            // Assert
            result.Should().BeEmpty();
        }

        // CategorizeFavoritesAsync 

        [Fact]
        public async Task CategorizeFavoritesAsync_WhenApiReturnsCategories_ShouldUpdateFavorites()
        {
            // Arrange
            var categorized = new List<object>
            {
                new { favoriteId = "favorite-001", category = "Elektronik" }
            };
            var json = JsonSerializer.Serialize(categorized);

            var httpClient = new HttpClient(new FakeHttpMessageHandler(json, HttpStatusCode.OK))
            {
                BaseAddress = new Uri("http://localhost:8000")
            };

            _httpFactoryMock
                .Setup(f => f.CreateClient("PythonScraperService"))
                .Returns(httpClient);

            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            _favoriteRepoMock
                .Setup(r => r.GetFavoritesByUserId("user-001"))
                .Returns(new List<Favorite> { _testFavorite }.AsQueryable());

            _platformRepoMock
                .Setup(r => r.GetOneProductPlatform("platform-001"))
                .Returns(_testPlatform);

            _favoriteRepoMock
                .Setup(r => r.UpdateFavoriteAsync(It.IsAny<Favorite>()))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.CategorizeFavoritesAsync("firebase-uid-001");

            // Assert
            _favoriteRepoMock.Verify(
                r => r.UpdateFavoriteAsync(It.Is<Favorite>(f =>
                    f.FavoriteId == "favorite-001" &&
                    f.Category   == "Elektronik")),
                Times.Once);
        }

        [Fact]
        public async Task CategorizeFavoritesAsync_WhenUserHasNoFavorites_ShouldNotCallApi()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            _favoriteRepoMock
                .Setup(r => r.GetFavoritesByUserId("user-001"))
                .Returns(Enumerable.Empty<Favorite>().AsQueryable());

            // Act
            await _sut.CategorizeFavoritesAsync("firebase-uid-001");

            // Assert
            _httpFactoryMock.Verify(
                f => f.CreateClient(It.IsAny<string>()), Times.Never);
        }

        // GetUserFavoritesAsync 

        [Fact]
        public async Task GetUserFavoritesAsync_WhenUidEmpty_ThrowsArgumentNullException()
        {
            await _sut.Invoking(s => s.GetUserFavoritesAsync(""))
                .Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task GetUserFavoritesAsync_WhenUidWhitespace_ThrowsArgumentNullException()
        {
            await _sut.Invoking(s => s.GetUserFavoritesAsync("   "))
                .Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task GetUserFavoritesAsync_WhenUserNotFound_ReturnsEmpty()
        {
            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("unknown-uid"))
                .ReturnsAsync((User?)null);

            var result = await _sut.GetUserFavoritesAsync("unknown-uid");

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetUserFavoritesAsync_WhenUserFound_ReturnsFavorites()
        {
            _userRepoMock.Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            _favoriteRepoMock.Setup(r => r.GetFavoritesByUserId("user-001"))
                .Returns(new List<Favorite> { _testFavorite, new Favorite { FavoriteId = "fav-2", UserId = "user-001" } }.AsQueryable());

            var result = await _sut.GetUserFavoritesAsync("firebase-uid-001");

            result.Should().HaveCount(2);
        }
    }

    // Fake HTTP handler 
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string     _responseContent;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseContent = responseContent;
            _statusCode      = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent,
                    System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
