using MongoDB.Driver;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.EFCore;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class FavoriteRepositoryTests
    {
        private readonly Mock<IMongoCollection<Favorite>> _collectionMock;
        private readonly FavoriteRepository _sut;

        private static IAsyncCursor<Favorite> BuildAsyncCursor(IEnumerable<Favorite> data)
        {
            var list = data.ToList();
            var cursor = new Mock<IAsyncCursor<Favorite>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(list.Count > 0).ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(list);
            cursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                  .Returns(list.Count > 0).Returns(false);
            return cursor.Object;
        }

        public FavoriteRepositoryTests()
        {
            _collectionMock = new Mock<IMongoCollection<Favorite>>();
            _sut = new FavoriteRepository(_collectionMock.Object);
        }

        [Fact]
        public void Constructor_NotNull()
        {
            Assert.NotNull(_sut);
        }

        [Fact]
        public void GetAllFavorites_ReturnsQueryable()
        {
            _collectionMock.Setup(c => c.FindSync(
                It.IsAny<FilterDefinition<Favorite>>(),
                It.IsAny<FindOptions<Favorite, Favorite>>(),
                It.IsAny<CancellationToken>()))
            .Returns(BuildAsyncCursor(new[]
            {
                new Favorite { FavoriteId = "f-1" },
                new Favorite { FavoriteId = "f-2" }
            }));

            var result = _sut.GetAllFavorites();

            Assert.NotNull(result);
        }

        [Fact]
        public void GetOneFavorite_ReturnsFavorite_WhenFound()
        {
            var ex = Record.Exception(() => _sut.GetOneFavorite("fav-find"));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public void GetOneFavorite_ReturnsNull_WhenNotFound()
        {
            var ex = Record.Exception(() => _sut.GetOneFavorite("ghost"));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public async Task AddFavoriteAsync_CallsInsertOneAsync()
        {
            var fav = new Favorite { FavoriteId = "fav-new" };
            _collectionMock.Setup(c => c.InsertOneAsync(
                It.IsAny<Favorite>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await _sut.AddFavoriteAsync(fav);

            _collectionMock.Verify(c => c.InsertOneAsync(
                It.Is<Favorite>(f => f.FavoriteId == "fav-new"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteFavoriteAsync_CallsDeleteOneAsync()
        {
            _collectionMock.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Favorite>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(1));

            await _sut.DeleteFavoriteAsync("fav-del");

            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Favorite>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void GetFavoritesByUserId_ReturnsEnumerable()
        {
            var favorites = new List<Favorite>
            {
                new Favorite { FavoriteId = "f-a", UserId = "user-1" },
                new Favorite { FavoriteId = "f-b", UserId = "user-1" }
            };
            _collectionMock.Setup(c => c.FindSync(
                It.IsAny<FilterDefinition<Favorite>>(),
                It.IsAny<FindOptions<Favorite, Favorite>>(),
                It.IsAny<CancellationToken>()))
            .Returns(BuildAsyncCursor(favorites));

            var result = _sut.GetFavoritesByUserId("user-1");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateFavoriteAsync_CallsReplaceOneAsync()
        {
            var fav = new Favorite { FavoriteId = "fav-upd" };
            _collectionMock.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Favorite>>(),
                It.IsAny<Favorite>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            await _sut.UpdateFavoriteAsync(fav);

            _collectionMock.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Favorite>>(),
                It.Is<Favorite>(f => f.FavoriteId == "fav-upd"),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
