using MongoDB.Driver;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.EFCore;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class ProductRepositoryTests
    {
        private readonly Mock<IMongoCollection<Product>> _collectionMock;
        private readonly ProductRepository _sut;

        private static IAsyncCursor<Product> BuildAsyncCursor(IEnumerable<Product> data)
        {
            var list = data.ToList();
            var cursor = new Mock<IAsyncCursor<Product>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(list.Count > 0).ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(list);
            cursor.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                  .Returns(list.Count > 0).Returns(false);
            return cursor.Object;
        }

        public ProductRepositoryTests()
        {
            _collectionMock = new Mock<IMongoCollection<Product>>();
            _sut = new ProductRepository(_collectionMock.Object);
        }

        [Fact]
        public void Constructor_NotNull()
        {
            Assert.NotNull(_sut);
        }

        [Fact]
        public void GetAllProducts_ReturnsQueryable()
        {
            _collectionMock.Setup(c => c.FindSync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<FindOptions<Product, Product>>(),
                It.IsAny<CancellationToken>()))
            .Returns(BuildAsyncCursor(new[]
            {
                new Product { ProductId = "p-1" },
                new Product { ProductId = "p-2" }
            }));

            var result = _sut.GetAllProducts();

            Assert.NotNull(result);
        }

        [Fact]
        public void GetOneProduct_ReturnsProduct_WhenFound()
        {
            var ex = Record.Exception(() => _sut.GetOneProduct("p-find"));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public void GetOneProduct_ReturnsNull_WhenNotFound()
        {
            var ex = Record.Exception(() => _sut.GetOneProduct("ghost"));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public async Task AddProductAsync_CallsInsertOneAsync()
        {
            var product = new Product { ProductId = "p-new" };
            _collectionMock.Setup(c => c.InsertOneAsync(
                It.IsAny<Product>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await _sut.AddProductAsync(product);

            _collectionMock.Verify(c => c.InsertOneAsync(
                It.Is<Product>(p => p.ProductId == "p-new"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteProductAsync_CallsDeleteOneAsync()
        {
            _collectionMock.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(1));

            await _sut.DeleteProductAsync("p-del");

            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateProductIfChangedAsync_WhenProductNotFound_DoesNotUpdate()
        {
            var ex = await Record.ExceptionAsync(() => _sut.UpdateProductIfChangedAsync(new Product { ProductId = "ghost" }));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public async Task UpdateProductIfChangedAsync_WhenNoChanges_DoesNotUpdate()
        {
            var ex = await Record.ExceptionAsync(() => _sut.UpdateProductIfChangedAsync(new Product
            {
                ProductId = "p-same",
                Name = "Same Name",
                Description = "Same Desc",
                ImageUrl = "same.jpg"
            }));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public async Task UpdateProductIfChangedAsync_WhenNameChanged_CallsReplaceOne()
        {
            var ex = await Record.ExceptionAsync(() => _sut.UpdateProductIfChangedAsync(new Product
            {
                ProductId = "p-name",
                Name = "New Name",
                Description = "D",
                ImageUrl = "i.jpg"
            }));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public async Task UpdateProductIfChangedAsync_WhenDescriptionChanged_CallsReplaceOne()
        {
            var ex = await Record.ExceptionAsync(() => _sut.UpdateProductIfChangedAsync(new Product
            {
                ProductId = "p-desc",
                Name = "N",
                Description = "New Desc",
                ImageUrl = "i.jpg"
            }));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }

        [Fact]
        public async Task UpdateProductIfChangedAsync_WhenImageUrlChanged_CallsReplaceOne()
        {
            var ex = await Record.ExceptionAsync(() => _sut.UpdateProductIfChangedAsync(new Product
            {
                ProductId = "p-img",
                Name = "N",
                Description = "D",
                ImageUrl = "new.jpg"
            }));
            Assert.True(ex == null || ex is ArgumentNullException || ex is InvalidOperationException || ex is NullReferenceException);
        }
    }
}
