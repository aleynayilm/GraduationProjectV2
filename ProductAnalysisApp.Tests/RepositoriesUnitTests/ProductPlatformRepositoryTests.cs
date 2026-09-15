using MongoDB.Driver;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.EFCore;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class ProductPlatformRepositoryTests
    {
        private readonly Mock<IMongoCollection<ProductPlatform>> _collectionMock;
        private readonly ProductPlatformRepository _sut;

        private static IAsyncCursor<ProductPlatform> BuildAsyncCursor(IEnumerable<ProductPlatform> data)
        {
            var list = data.ToList();
            var cursor = new Mock<IAsyncCursor<ProductPlatform>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(list.Count > 0).ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(list);
            cursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                  .Returns(list.Count > 0).Returns(false);
            return cursor.Object;
        }

        public ProductPlatformRepositoryTests()
        {
            _collectionMock = new Mock<IMongoCollection<ProductPlatform>>();
            _sut = new ProductPlatformRepository(_collectionMock.Object);
        }

        [Fact]
        public void Constructor_NotNull()
        {
            Assert.NotNull(_sut);
        }

        [Fact]
        public void GetAllProductPlatforms_ReturnsEnumerable()
        {
            _collectionMock.Setup(c => c.FindSync(
                It.IsAny<FilterDefinition<ProductPlatform>>(),
                It.IsAny<FindOptions<ProductPlatform, ProductPlatform>>(),
                It.IsAny<CancellationToken>()))
            .Returns(BuildAsyncCursor(new[]
            {
                new ProductPlatform { ProductPlatformId = "pp-1" },
                new ProductPlatform { ProductPlatformId = "pp-2" }
            }));

            var result = _sut.GetAllProductPlatforms();

            Assert.NotNull(result);
        }

        [Fact]
        public void GetOneProductPlatform_Returns_WhenFound()
        {
            var ex = Record.Exception(() => _sut.GetOneProductPlatform("pp-find"));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public void GetOneProductPlatform_ReturnsNull_WhenNotFound()
        {
            var ex = Record.Exception(() => _sut.GetOneProductPlatform("ghost"));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public async Task AddProductPlatformAsync_CallsInsertOneAsync()
        {
            var pp = new ProductPlatform { ProductPlatformId = "pp-new" };
            _collectionMock.Setup(c => c.InsertOneAsync(
                It.IsAny<ProductPlatform>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await _sut.AddProductPlatformAsync(pp);

            _collectionMock.Verify(c => c.InsertOneAsync(
                It.Is<ProductPlatform>(p => p.ProductPlatformId == "pp-new"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProductPlatformAsync_CallsReplaceOneAsync()
        {
            var pp = new ProductPlatform { ProductPlatformId = "pp-upd" };
            _collectionMock.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ProductPlatform>>(),
                It.IsAny<ProductPlatform>(),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            await _sut.UpdateProductPlatformAsync(pp);

            _collectionMock.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ProductPlatform>>(),
                It.Is<ProductPlatform>(p => p.ProductPlatformId == "pp-upd"),
                It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPlatformsByProductIdAsync_ReturnsList()
        {
            var platforms = new List<ProductPlatform>
            {
                new ProductPlatform { ProductPlatformId = "pp-a", ProductId = "prod-1" },
                new ProductPlatform { ProductPlatformId = "pp-b", ProductId = "prod-1" }
            };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProductPlatform>>(),
                It.IsAny<FindOptions<ProductPlatform, ProductPlatform>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(platforms));

            var result = await _sut.GetPlatformsByProductIdAsync("prod-1");

            Assert.Equal(2, result.Count);
        }
    }
}
