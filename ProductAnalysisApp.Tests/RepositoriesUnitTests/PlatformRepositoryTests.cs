using MongoDB.Driver;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.EFCore;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class PlatformRepositoryTests
    {
        private readonly Mock<IMongoCollection<Platform>> _collectionMock;
        private readonly PlatformRepository _sut;

        private static IAsyncCursor<Platform> BuildAsyncCursor(IEnumerable<Platform> data)
        {
            var list = data.ToList();
            var cursor = new Mock<IAsyncCursor<Platform>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(list.Count > 0).ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(list);
            return cursor.Object;
        }

        public PlatformRepositoryTests()
        {
            _collectionMock = new Mock<IMongoCollection<Platform>>();
            _sut = new PlatformRepository(_collectionMock.Object);
        }

        [Fact]
        public void Constructor_NotNull()
        {
            Assert.NotNull(_sut);
        }

        [Fact]
        public async Task AddPlatformAsync_CallsInsertOneAsync()
        {
            var platform = new Platform { PlatformId = "plat-1", Name = "Trendyol" };
            _collectionMock.Setup(c => c.InsertOneAsync(
                It.IsAny<Platform>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await _sut.AddPlatformAsync(platform);

            _collectionMock.Verify(c => c.InsertOneAsync(
                It.Is<Platform>(p => p.Name == "Trendyol"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPlatformByNameAsync_ReturnsPlatform_WhenFound()
        {
            var platform = new Platform { PlatformId = "plat-tz", Name = "Trendyol" };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Platform>>(),
                It.IsAny<FindOptions<Platform, Platform>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(new[] { platform }));

            var result = await _sut.GetPlatformByNameAsync("trendyol");

            Assert.NotNull(result);
            Assert.Equal("Trendyol", result!.Name);
        }

        [Fact]
        public async Task GetPlatformByNameAsync_ReturnsNull_WhenNotFound()
        {
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Platform>>(),
                It.IsAny<FindOptions<Platform, Platform>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(Enumerable.Empty<Platform>()));

            var result = await _sut.GetPlatformByNameAsync("nonexistent");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllPlatformsAsync_ReturnsList()
        {
            var platforms = new List<Platform>
            {
                new Platform { PlatformId = "p-1", Name = "Trendyol" },
                new Platform { PlatformId = "p-2", Name = "Amazon" }
            };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Platform>>(),
                It.IsAny<FindOptions<Platform, Platform>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(platforms));

            var result = await _sut.GetAllPlatformsAsync();

            Assert.Equal(2, result.Count);
        }
    }
}
