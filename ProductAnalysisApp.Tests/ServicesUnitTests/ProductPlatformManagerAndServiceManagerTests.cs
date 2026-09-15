using AutoMapper;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using Xunit;

namespace ProductAnalysisApp.Tests.Services
{
    public class ProductPlatformManagerTests
    {
        private readonly Mock<IRepositoryManager>            _repoManagerMock;
        private readonly Mock<IProductPlatformRepository>    _platformRepoMock;
        private readonly ProductPlatformManager              _manager;

        public ProductPlatformManagerTests()
        {
            _platformRepoMock = new Mock<IProductPlatformRepository>();
            _repoManagerMock  = new Mock<IRepositoryManager>();
            _repoManagerMock.Setup(r => r.ProductPlatform).Returns(_platformRepoMock.Object);

            _manager = new ProductPlatformManager(_repoManagerMock.Object);
        }

        // GetAllProductPlatforms 

        [Fact]
        public void GetAllProductPlatforms_ReturnsList()
        {
            var platforms = new List<ProductPlatform>
            {
                new() { ProductPlatformId = "p1", ProductId = "prod-1" },
                new() { ProductPlatformId = "p2", ProductId = "prod-2" }
            };
            _platformRepoMock.Setup(r => r.GetAllProductPlatforms()).Returns(platforms);

            var result = _manager.GetAllProductPlatforms();

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void GetAllProductPlatforms_ReturnsEmptyList_WhenNoneExist()
        {
            _platformRepoMock.Setup(r => r.GetAllProductPlatforms())
                             .Returns(Enumerable.Empty<ProductPlatform>());

            var result = _manager.GetAllProductPlatforms();

            Assert.Empty(result);
        }

        // GetOneProductPlatform 

        [Fact]
        public void GetOneProductPlatform_ReturnsPlatform_WhenFound()
        {
            var platform = new ProductPlatform { ProductPlatformId = "pp-001", ProductId = "prod-001" };
            _platformRepoMock.Setup(r => r.GetOneProductPlatform("pp-001")).Returns(platform);

            var result = _manager.GetOneProductPlatform("pp-001");

            Assert.NotNull(result);
            Assert.Equal("pp-001", result.ProductPlatformId);
        }

        [Fact]
        public void GetOneProductPlatform_ReturnsNull_WhenNotFound()
        {
            _platformRepoMock.Setup(r => r.GetOneProductPlatform(It.IsAny<string>()))
                             .Returns((ProductPlatform?)null!);

            var result = _manager.GetOneProductPlatform("nonexistent");

            Assert.Null(result);
        }

        // AddProductPlatformAsync 

        [Fact]
        public async Task AddProductPlatformAsync_CallsRepository()
        {
            var platform = new ProductPlatform { ProductPlatformId = "pp-new", ProductId = "prod-001" };
            _platformRepoMock.Setup(r => r.AddProductPlatformAsync(platform))
                             .Returns(Task.CompletedTask);

            await _manager.AddProductPlatformAsync(platform);

            _platformRepoMock.Verify(r => r.AddProductPlatformAsync(platform), Times.Once);
        }

        // UpdateProductPlatformAsync 

        [Fact]
        public async Task UpdateProductPlatformAsync_CallsRepository()
        {
            var platform = new ProductPlatform { ProductPlatformId = "pp-upd", ProductId = "prod-001" };
            _platformRepoMock.Setup(r => r.UpdateProductPlatformAsync(platform))
                             .Returns(Task.CompletedTask);

            await _manager.UpdateProductPlatformAsync(platform);

            _platformRepoMock.Verify(r => r.UpdateProductPlatformAsync(platform), Times.Once);
        }

        // GetPlatformsByProductIdAsync 

        [Fact]
        public async Task GetPlatformsByProductIdAsync_ReturnsList()
        {
            var list = new List<ProductPlatform>
            {
                new() { ProductId = "prod-999", PlatformId = "trendyol" },
                new() { ProductId = "prod-999", PlatformId = "hepsiburada" }
            };
            _platformRepoMock.Setup(r => r.GetPlatformsByProductIdAsync("prod-999"))
                             .ReturnsAsync(list);

            var result = await _manager.GetPlatformsByProductIdAsync("prod-999");

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetPlatformsByProductIdAsync_ReturnsEmptyList_WhenNoneFound()
        {
            _platformRepoMock.Setup(r => r.GetPlatformsByProductIdAsync(It.IsAny<string>()))
                             .ReturnsAsync([]);

            var result = await _manager.GetPlatformsByProductIdAsync("prod-no-platforms");

            Assert.Empty(result);
        }
    }

    // ServiceManager Tests

    public class ServiceManagerTests
    {
        private readonly Mock<IRepositoryManager>  _repoManagerMock;
        private readonly Mock<IMapper>             _mapperMock;
        private readonly Mock<IHttpClientFactory>  _httpClientFactoryMock;

        public ServiceManagerTests()
        {
            _repoManagerMock       = new Mock<IRepositoryManager>();
            _mapperMock            = new Mock<IMapper>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();

            _repoManagerMock.Setup(r => r.User).Returns(new Mock<IUserRepository>().Object);
            _repoManagerMock.Setup(r => r.Product).Returns(new Mock<IProductRepository>().Object);
            _repoManagerMock.Setup(r => r.Favorite).Returns(new Mock<IFavoriteRepository>().Object);
            _repoManagerMock.Setup(r => r.ProductPlatform)
                            .Returns(new Mock<IProductPlatformRepository>().Object);
            _repoManagerMock.Setup(r => r.SearchHistory)
                            .Returns(new Mock<ISearchHistoryRepository>().Object);
        }

        private ProductAnalysisApp.Services.ProductAnalysisApp.Services.ServiceManager CreateManager()
            => new(_repoManagerMock.Object, _mapperMock.Object, _httpClientFactoryMock.Object);

        // Lazy service örnekleme

        [Fact]
        public void UserService_IsNotNull()
        {
            var manager = CreateManager();
            Assert.NotNull(manager.UserService);
        }

        [Fact]
        public void ProductService_IsNotNull()
        {
            var manager = CreateManager();
            Assert.NotNull(manager.ProductService);
        }

        [Fact]
        public void SearchHistoryService_IsNotNull()
        {
            var manager = CreateManager();
            Assert.NotNull(manager.SearchHistoryService);
        }

        [Fact]
        public void FavoriteService_IsNotNull()
        {
            var manager = CreateManager();
            Assert.NotNull(manager.FavoriteService);
        }

        [Fact]
        public void ProductPlatformService_IsNotNull()
        {
            var manager = CreateManager();
            Assert.NotNull(manager.ProductPlatformService);
        }

        [Fact]
        public void UserService_ReturnsSameInstance_OnMultipleAccess()
        {
            var manager = CreateManager();

            var first  = manager.UserService;
            var second = manager.UserService;

            Assert.Same(first, second);
        }

        [Fact]
        public void ProductService_ReturnsSameInstance_OnMultipleAccess()
        {
            var manager = CreateManager();

            Assert.Same(manager.ProductService, manager.ProductService);
        }

        [Fact]
        public void SearchHistoryService_ReturnsSameInstance_OnMultipleAccess()
        {
            var manager = CreateManager();

            Assert.Same(manager.SearchHistoryService, manager.SearchHistoryService);
        }

        [Fact]
        public void FavoriteService_ReturnsSameInstance_OnMultipleAccess()
        {
            var manager = CreateManager();

            Assert.Same(manager.FavoriteService, manager.FavoriteService);
        }

        [Fact]
        public void ProductPlatformService_ReturnsSameInstance_OnMultipleAccess()
        {
            var manager = CreateManager();

            Assert.Same(manager.ProductPlatformService, manager.ProductPlatformService);
        }
    }
}
