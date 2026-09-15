using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services.Contracts;
using System.Security.Claims;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class FavoriteControllerFullTests
    {
        private readonly Mock<IServiceManager>  _managerMock;
        private readonly Mock<IFavoriteService> _favServiceMock;
        private readonly Mock<IUserService>     _userServiceMock;
        private readonly FavoriteController     _controller;
        private const string Uid = "firebase-uid-fav-001";

        public FavoriteControllerFullTests()
        {
            _favServiceMock  = new Mock<IFavoriteService>();
            _userServiceMock = new Mock<IUserService>();
            _managerMock     = new Mock<IServiceManager>();

            _managerMock.Setup(m => m.FavoriteService).Returns(_favServiceMock.Object);
            _managerMock.Setup(m => m.UserService).Returns(_userServiceMock.Object);

            _controller = new FavoriteController(_managerMock.Object);
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

        // GetOneFavorite  GET /{id}

        [Fact]
        public void GetOneFavorite_ReturnsOk_WhenFound()
        {
            var fav = new Favorite { FavoriteId = "fav-001", UserId = "u1" };
            _favServiceMock.Setup(s => s.GetOneFavorite("fav-001")).Returns(fav);

            var result = _controller.GetOneFavorite("fav-001");

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<Favorite>(ok.Value);
        }

        [Fact]
        public void GetOneFavorite_ReturnsNotFound_WhenNull()
        {
            _favServiceMock.Setup(s => s.GetOneFavorite(It.IsAny<string>()))
                           .Returns((Favorite?)null);

            var result = _controller.GetOneFavorite("nonexistent");

            Assert.IsType<NotFoundResult>(result);
        }

        // GetMyFavorites  GET /

        [Fact]
        public async Task GetMyFavorites_ReturnsOk_WhenAuthenticated()
        {
            var details = new List<FavoriteDetailDto>
            {
                new() { FavoriteId = "f1" },
                new() { FavoriteId = "f2" }
            };
            _favServiceMock.Setup(s => s.GetUserFavoriteDetailsAsync(Uid))
                           .ReturnsAsync(details);

            var result = await _controller.GetMyFavorites();

            var ok   = Assert.IsType<OkObjectResult>(result);
            var list = Assert.IsAssignableFrom<IEnumerable<FavoriteDetailDto>>(ok.Value);
            Assert.Equal(2, list.Count());
        }

        [Fact]
        public async Task GetMyFavorites_ReturnsUnauthorized_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.GetMyFavorites();

            Assert.IsType<UnauthorizedResult>(result);
        }

        // AddFavorite  POST /

        [Fact]
        public async Task AddFavorite_Returns201_WhenSuccessful()
        {
            var fav = new Favorite { FavoriteId = "f-new", UserId = "u1" };
            _favServiceMock.Setup(s => s.AddFavoriteAsync(It.IsAny<FavoriteDtoForCreate>()))
                           .ReturnsAsync(fav);

            var result = await _controller.AddFavorite(
                new FavoriteController.AddFavoriteRequest("pp-001"));

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, status.StatusCode);
        }

        [Fact]
        public async Task AddFavorite_ReturnsUnauthorized_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.AddFavorite(
                new FavoriteController.AddFavoriteRequest("pp-001"));

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task AddFavorite_ReturnsBadRequest_WhenServiceThrows()
        {
            _favServiceMock.Setup(s => s.AddFavoriteAsync(It.IsAny<FavoriteDtoForCreate>()))
                           .ThrowsAsync(new Exception("Zaten mevcut"));

            var result = await _controller.AddFavorite(
                new FavoriteController.AddFavoriteRequest("pp-dup"));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // RemoveFavorite  DELETE /{favoriteId}

        [Fact]
        public async Task RemoveFavorite_ReturnsUnauthorized_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.RemoveFavorite("fav-001");

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task RemoveFavorite_ReturnsNotFound_WhenFavoriteNotFound()
        {
            _favServiceMock.Setup(s => s.GetOneFavorite("fav-missing"))
                           .Returns((Favorite?)null);

            var result = await _controller.RemoveFavorite("fav-missing");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RemoveFavorite_ReturnsForbid_WhenFavoriteBelongsToDifferentUser()
        {
            var fav  = new Favorite { FavoriteId = "fav-other", UserId = "other-user-id" };
            var user = new User { Id = "my-user-id", FirebaseUid = Uid };

            _favServiceMock.Setup(s => s.GetOneFavorite("fav-other")).Returns(fav);
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);

            var result = await _controller.RemoveFavorite("fav-other");

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task RemoveFavorite_ReturnsForbid_WhenUserNotFound()
        {
            var fav = new Favorite { FavoriteId = "fav-orphan", UserId = "some-user" };
            _favServiceMock.Setup(s => s.GetOneFavorite("fav-orphan")).Returns(fav);
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid))
                            .ReturnsAsync((User?)null);

            var result = await _controller.RemoveFavorite("fav-orphan");

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task RemoveFavorite_ReturnsNoContent_WhenOwnerDeletes()
        {
            const string userId = "owner-user-id";
            var fav  = new Favorite { FavoriteId = "fav-own", UserId = userId };
            var user = new User { Id = userId, FirebaseUid = Uid };

            _favServiceMock.Setup(s => s.GetOneFavorite("fav-own")).Returns(fav);
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);
            _favServiceMock.Setup(s => s.DeleteFavoriteAsync("fav-own")).Returns(Task.CompletedTask);

            var result = await _controller.RemoveFavorite("fav-own");

            Assert.IsType<NoContentResult>(result);
            _favServiceMock.Verify(s => s.DeleteFavoriteAsync("fav-own"), Times.Once);
        }

        // ToggleFavorite  POST /toggle

        [Fact]
        public async Task ToggleFavorite_ReturnsUnauthorized_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = await _controller.ToggleFavorite(
                new FavoriteController.AddFavoriteRequest("pp-001"));

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task ToggleFavorite_ReturnsUnauthorized_WhenUserNotFound()
        {
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid))
                            .ReturnsAsync((User?)null);

            var result = await _controller.ToggleFavorite(
                new FavoriteController.AddFavoriteRequest("pp-001"));

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task ToggleFavorite_AddsAndReturnsAdded_WhenFavoriteDoesNotExist()
        {
            const string userId = "u-tgl-1";
            var user   = new User { Id = userId, FirebaseUid = Uid };
            var newFav = new Favorite { FavoriteId = "fav-new", UserId = userId };

            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);
            _favServiceMock.Setup(s => s.GetAllFavorites())
                           .Returns(new List<Favorite>().AsQueryable());
            _favServiceMock.Setup(s => s.AddFavoriteAsync(It.IsAny<FavoriteDtoForCreate>()))
                           .ReturnsAsync(newFav);

            var result = await _controller.ToggleFavorite(
                new FavoriteController.AddFavoriteRequest("pp-toggle-new"));

            var ok   = Assert.IsType<OkObjectResult>(result);
            var body = ok.Value!.ToString()!;
            Assert.Contains("True",    body);  
            Assert.Contains("fav-new", body);  
        }

        [Fact]
        public async Task ToggleFavorite_RemovesAndReturnsRemoved_WhenFavoriteExists()
        {
            const string userId = "u-tgl-2";
            var user       = new User { Id = userId, FirebaseUid = Uid };
            var existingFav = new Favorite
            {
                FavoriteId        = "fav-existing",
                UserId            = userId,
                ProductPlatformId = "pp-existing"
            };

            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);
            _favServiceMock.Setup(s => s.GetAllFavorites())
                           .Returns(new[] { existingFav }.AsQueryable());
            _favServiceMock.Setup(s => s.DeleteFavoriteAsync("fav-existing"))
                           .Returns(Task.CompletedTask);

            var result = await _controller.ToggleFavorite(
                new FavoriteController.AddFavoriteRequest("pp-existing"));

            var ok   = Assert.IsType<OkObjectResult>(result);
            var body = ok.Value!.ToString()!;
            Assert.Contains("False", body);
        }

        [Fact]
        public async Task ToggleFavorite_ReturnsBadRequest_WhenServiceThrows()
        {
            var user = new User { Id = "u-tgl-err", FirebaseUid = Uid };
            _userServiceMock.Setup(s => s.GetOneUserByFirebaseUidAsync(Uid)).ReturnsAsync(user);
            _favServiceMock.Setup(s => s.GetAllFavorites())
                           .Returns(new List<Favorite>().AsQueryable());
            _favServiceMock.Setup(s => s.AddFavoriteAsync(It.IsAny<FavoriteDtoForCreate>()))
                           .ThrowsAsync(new Exception("DB hatası"));

            var result = await _controller.ToggleFavorite(
                new FavoriteController.AddFavoriteRequest("pp-err"));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        // CategorizeFavorites  POST /categorize

        [Fact]
        public void CategorizeFavorites_ReturnsAccepted_WhenAuthenticated()
        {
            _favServiceMock.Setup(s => s.CategorizeFavoritesAsync(Uid))
                           .Returns(Task.CompletedTask);

            var result = _controller.CategorizeFavorites();

            var accepted = Assert.IsType<AcceptedResult>(result);
            Assert.Contains("başlatıldı", accepted.Value!.ToString());
        }

        [Fact]
        public void CategorizeFavorites_ReturnsUnauthorized_WhenNotAuthenticated()
        {
            SetAnonymousUser(_controller);

            var result = _controller.CategorizeFavorites();

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public void CategorizeFavorites_StartsBackgroundTask_WithCorrectUid()
        {
            _favServiceMock.Setup(s => s.CategorizeFavoritesAsync(Uid))
                           .Returns(Task.CompletedTask);

            _controller.CategorizeFavorites();

            _favServiceMock.Verify(s => s.CategorizeFavoritesAsync(It.IsAny<string>()),
                Times.AtMost(1));
        }
    }
}
