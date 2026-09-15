using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class FavoriteControllerTests : ControllerTestBase
    {
        private readonly Mock<IServiceManager>  _serviceManagerMock;
        private readonly Mock<IFavoriteService> _favoriteServiceMock;
        private readonly Mock<IUserService>     _userServiceMock;
        private readonly FavoriteController     _sut;

        private readonly User     _testUser;
        private readonly Favorite _testFavorite;

        public FavoriteControllerTests()
        {
            _serviceManagerMock  = new Mock<IServiceManager>();
            _favoriteServiceMock = new Mock<IFavoriteService>();
            _userServiceMock     = new Mock<IUserService>();

            _serviceManagerMock.Setup(m => m.FavoriteService).Returns(_favoriteServiceMock.Object);
            _serviceManagerMock.Setup(m => m.UserService).Returns(_userServiceMock.Object);

            _sut = new FavoriteController(_serviceManagerMock.Object);
            SetAuthenticatedUser(_sut);

            _testUser = new User { Id = TestUserId, FirebaseUid = TestFirebaseUid };
            _testFavorite = new Favorite
            {
                FavoriteId        = "fav-001",
                UserId            = TestUserId,
                ProductPlatformId = "platform-001"
            };
        }

        // GetMyFavorites

        [Fact]
        public async Task GetMyFavorites_ShouldReturn200WithFavoriteDetails()
        {
            // Arrange
            var details = new List<FavoriteDetailDto>
            {
                new() { FavoriteId = "fav-001", ProductUrl = "https://trendyol.com/p", Price = 1500m, Category = "Elektronik" }
            };

            _favoriteServiceMock
                .Setup(s => s.GetUserFavoriteDetailsAsync(TestFirebaseUid))
                .ReturnsAsync(details);

            // Act
            var result = await _sut.GetMyFavorites();

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var data = ok.Value.Should().BeAssignableTo<List<FavoriteDetailDto>>().Subject;
            data.Should().HaveCount(1);
            data[0].Category.Should().Be("Elektronik");
        }

        [Fact]
        public async Task GetMyFavorites_WhenNotAuthenticated_ShouldReturn401()
        {
            // Arrange
            SetAnonymousUser(_sut);

            // Act
            var result = await _sut.GetMyFavorites();

            // Assert
            result.Should().BeOfType<UnauthorizedResult>();
        }

        // AddFavorite 

        [Fact]
        public async Task AddFavorite_WithValidRequest_ShouldReturn201()
        {
            // Arrange
            _favoriteServiceMock
                .Setup(s => s.AddFavoriteAsync(It.IsAny<FavoriteDtoForCreate>()))
                .ReturnsAsync(_testFavorite);

            var request = new FavoriteController.AddFavoriteRequest("platform-001");

            // Act
            var result = await _sut.AddFavorite(request);

            // Assert
            var status = result.Should().BeOfType<ObjectResult>().Subject;
            status.StatusCode.Should().Be(201);
        }

        [Fact]
        public async Task AddFavorite_WhenServiceThrows_ShouldReturn400()
        {
            // Arrange
            _favoriteServiceMock
                .Setup(s => s.AddFavoriteAsync(It.IsAny<FavoriteDtoForCreate>()))
                .ThrowsAsync(new Exception("Product not found!"));

            var request = new FavoriteController.AddFavoriteRequest("nonexistent-platform");

            // Act
            var result = await _sut.AddFavorite(request);

            // Assert
            var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            bad.StatusCode.Should().Be(400);
        }

        // RemoveFavorite 

        [Fact]
        public async Task RemoveFavorite_WhenOwner_ShouldReturn204()
        {
            // Arrange
            _favoriteServiceMock
                .Setup(s => s.GetOneFavorite("fav-001"))
                .Returns(_testFavorite);

            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            _favoriteServiceMock
                .Setup(s => s.DeleteFavoriteAsync("fav-001"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.RemoveFavorite("fav-001");

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task RemoveFavorite_WhenNotOwner_ShouldReturn403()
        {
            // Arrange
            var otherUserFavorite = new Favorite
            {
                FavoriteId = "fav-002",
                UserId     = "other-user-id",
                ProductPlatformId = "platform-001"
            };

            _favoriteServiceMock
                .Setup(s => s.GetOneFavorite("fav-002"))
                .Returns(otherUserFavorite);

            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            // Act
            var result = await _sut.RemoveFavorite("fav-002");

            // Assert
            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task RemoveFavorite_WhenNotFound_ShouldReturn404()
        {
            // Arrange
            _favoriteServiceMock
                .Setup(s => s.GetOneFavorite("nonexistent"))
                .Returns((Favorite?)null);

            // Act
            var result = await _sut.RemoveFavorite("nonexistent");

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        // ToggleFavorite 

        [Fact]
        public async Task ToggleFavorite_WhenNotFavorited_ShouldAddAndReturnAdded()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            _favoriteServiceMock
                .Setup(s => s.GetAllFavorites())
                .Returns(Enumerable.Empty<Favorite>());

            _favoriteServiceMock
                .Setup(s => s.AddFavoriteAsync(It.IsAny<FavoriteDtoForCreate>()))
                .ReturnsAsync(_testFavorite);

            var request = new FavoriteController.AddFavoriteRequest("platform-001");

            // Act
            var result = await _sut.ToggleFavorite(request);

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var body = ok.Value!.ToString();
            body.Should().Match(s => s.Contains("True") || s.Contains("added"));
        }

        [Fact]
        public async Task ToggleFavorite_WhenAlreadyFavorited_ShouldRemoveAndReturnNotAdded()
        {
            // Arrange
            _userServiceMock
                .Setup(s => s.GetOneUserByFirebaseUidAsync(TestFirebaseUid))
                .ReturnsAsync(_testUser);

            _favoriteServiceMock
                .Setup(s => s.GetAllFavorites())
                .Returns(new List<Favorite> { _testFavorite });

            _favoriteServiceMock
                .Setup(s => s.DeleteFavoriteAsync("fav-001"))
                .Returns(Task.CompletedTask);

            var request = new FavoriteController.AddFavoriteRequest("platform-001");

            // Act
            var result = await _sut.ToggleFavorite(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _favoriteServiceMock.Verify(s => s.DeleteFavoriteAsync("fav-001"), Times.Once);
        }

        // CategorizeFavorites 

        [Fact]
        public void CategorizeFavorites_ShouldReturn202Immediately()
        {
            // Act
            var result = _sut.CategorizeFavorites();

            // Assert
            result.Should().BeOfType<AcceptedResult>();
        }
    }
}
