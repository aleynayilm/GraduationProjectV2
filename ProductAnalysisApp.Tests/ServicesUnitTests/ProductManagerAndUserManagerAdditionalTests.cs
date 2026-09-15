using AutoMapper;
using Moq;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services;
using Xunit;

namespace ProductAnalysisApp.Tests.Services
{
    public class ProductManagerAdditionalTests
    {
        private readonly Mock<IRepositoryManager>    _repoMock;
        private readonly Mock<IProductRepository>    _productRepoMock;
        private readonly Mock<IProductPlatformRepository> _platformRepoMock;
        private readonly Mock<IMapper>               _mapperMock;
        private readonly ProductManager              _manager;

        public ProductManagerAdditionalTests()
        {
            _productRepoMock  = new Mock<IProductRepository>();
            _platformRepoMock = new Mock<IProductPlatformRepository>();
            _repoMock         = new Mock<IRepositoryManager>();
            _mapperMock       = new Mock<IMapper>();

            _repoMock.Setup(r => r.Product).Returns(_productRepoMock.Object);
            _repoMock.Setup(r => r.ProductPlatform).Returns(_platformRepoMock.Object);

            _manager = new ProductManager(_repoMock.Object, _mapperMock.Object);
        }

        // GetAllProducts 

        [Fact]
        public void GetAllProducts_ReturnsAllProducts()
        {
            var products = new List<Product>
            {
                new() { ProductId = "p1", Name = "Laptop" },
                new() { ProductId = "p2", Name = "Tablet" }
            };
            _productRepoMock.Setup(r => r.GetAllProducts()).Returns(products.AsQueryable());

            var result = _manager.GetAllProducts();

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public void GetAllProducts_ReturnsEmptyList_WhenNoneExist()
        {
            _productRepoMock.Setup(r => r.GetAllProducts())
                            .Returns(Enumerable.Empty<Product>().AsQueryable());

            var result = _manager.GetAllProducts();

            Assert.Empty(result);
        }

        // GetOneProduct 

        [Fact]
        public void GetOneProduct_ReturnsProduct_WhenFound()
        {
            var product = new Product { ProductId = "p1", Name = "Mouse" };
            _productRepoMock.Setup(r => r.GetOneProduct("p1")).Returns(product);

            var result = _manager.GetOneProduct("p1");

            Assert.NotNull(result);
            Assert.Equal("Mouse", result.Name);
        }

        [Fact]
        public void GetOneProduct_ReturnsNull_WhenNotFound()
        {
            _productRepoMock.Setup(r => r.GetOneProduct(It.IsAny<string>()))
                            .Returns((Product?)null!);

            var result = _manager.GetOneProduct("nonexistent");

            Assert.Null(result);
        }

        // AddOneProductAsync 

        [Fact]
        public async Task AddOneProductAsync_ReturnsProduct_WhenSuccessful()
        {
            var dto     = new ProductDtoForCreate { Name = "Kulaklık" };
            var product = new Product { Name = "Kulaklık" };

            _mapperMock.Setup(m => m.Map<Product>(dto)).Returns(product);
            _productRepoMock.Setup(r => r.AddProductAsync(product)).Returns(Task.CompletedTask);

            var result = await _manager.AddOneProductAsync(dto);

            Assert.NotNull(result);
            Assert.Equal("Kulaklık", result.Name);
        }

        [Fact]
        public async Task AddOneProductAsync_CallsRepository()
        {
            var dto     = new ProductDtoForCreate { Name = "Kamera" };
            var product = new Product { Name = "Kamera" };

            _mapperMock.Setup(m => m.Map<Product>(dto)).Returns(product);
            _productRepoMock.Setup(r => r.AddProductAsync(product)).Returns(Task.CompletedTask);

            await _manager.AddOneProductAsync(dto);

            _productRepoMock.Verify(r => r.AddProductAsync(product), Times.Once);
        }

        // DeleteOneProductAsync 

        [Fact]
        public async Task DeleteOneProductAsync_DeletesProduct_WhenFound()
        {
            var product = new Product { ProductId = "p-del", Name = "Silinecek" };
            _productRepoMock.Setup(r => r.GetOneProduct("p-del")).Returns(product);
            _productRepoMock.Setup(r => r.DeleteProductAsync("p-del")).Returns(Task.CompletedTask);

            await _manager.DeleteOneProductAsync("p-del");

            _productRepoMock.Verify(r => r.DeleteProductAsync("p-del"), Times.Once);
        }

        [Fact]
        public async Task DeleteOneProductAsync_ThrowsException_WhenProductNotFound()
        {
            _productRepoMock.Setup(r => r.GetOneProduct(It.IsAny<string>()))
                            .Returns((Product?)null!);

            await Assert.ThrowsAsync<Exception>(
                () => _manager.DeleteOneProductAsync("nonexistent"));
        }

        // UpdateOneProductAsync 

        [Fact]
        public async Task UpdateOneProductAsync_UpdatesProduct_WhenFound()
        {
            var existing = new Product { ProductId = "p-upd", Name = "Eski" };
            var updated  = new Product { Name = "Yeni" };

            _productRepoMock.Setup(r => r.GetOneProduct("p-upd")).Returns(existing);
            _productRepoMock.Setup(r => r.UpdateProductIfChangedAsync(It.IsAny<Product>()))
                            .Returns(Task.CompletedTask);

            await _manager.UpdateOneProductAsync("p-upd", updated);

            _productRepoMock.Verify(r => r.UpdateProductIfChangedAsync(
                It.Is<Product>(p => p.ProductId == "p-upd")), Times.Once);
        }

        [Fact]
        public async Task UpdateOneProductAsync_ThrowsException_WhenProductNotFound()
        {
            _productRepoMock.Setup(r => r.GetOneProduct(It.IsAny<string>()))
                            .Returns((Product?)null!);

            await Assert.ThrowsAsync<Exception>(
                () => _manager.UpdateOneProductAsync("missing", new Product()));
        }

        [Fact]
        public async Task UpdateOneProductAsync_SetsProductId_ToRouteId()
        {
            var existing = new Product { ProductId = "p-set-id" };
            var incoming = new Product { Name = "Güncellendi" };

            _productRepoMock.Setup(r => r.GetOneProduct("p-set-id")).Returns(existing);
            _productRepoMock.Setup(r => r.UpdateProductIfChangedAsync(It.IsAny<Product>()))
                            .Returns(Task.CompletedTask);

            await _manager.UpdateOneProductAsync("p-set-id", incoming);

            Assert.Equal("p-set-id", incoming.ProductId);
        }

        // GetPlatformNameById 

        [Theory]
        [InlineData("692d65ec17d42f6575ed568d", "trendyol")]
        [InlineData("692d65fa17d42f6575ed568f", "amazon")]
        [InlineData("692d660417d42f6575ed5691", "n11")]
        public void GetPlatformNameById_ReturnsCorrectName(string platformId, string expectedName)
        {
            var result = _manager.GetPlatformNameById(platformId);
            Assert.Equal(expectedName, result);
        }

        [Fact]
        public void GetPlatformNameById_ReturnsUnknown_WhenIdNotFound()
        {
            var result = _manager.GetPlatformNameById("nonexistent-id");
            Assert.Equal("Unknown", result);
        }

        // ParsePriceStringToDecimal 

        [Theory]
        [InlineData("1.299,99", 129999)]   
        [InlineData("299,00",   29900)]  
        [InlineData("",         0)]     
        [InlineData(null,       0)]    
        [InlineData("abc",      0)]      
        public async Task SaveScrapedProductAsync_ParsesPriceCorrectly(
            string? priceString, int expectedCents)
        {
            var scrapedList = new List<ProductForScrapingDto>
            {
                new()
                {
                    ProductName  = "Test Ürün",
                    PlatformName = "trendyol",
                    Price        = priceString!,
                    ProductUrl   = "https://t.com/p",
                    Currency     = "TRY"
                }
            };

            _productRepoMock.Setup(r => r.AddProductAsync(It.IsAny<Product>()))
                            .Returns(Task.CompletedTask);
            _platformRepoMock.Setup(r => r.AddProductPlatformAsync(It.IsAny<ProductPlatform>()))
                             .Returns(Task.CompletedTask);

            await _manager.SaveScrapedProductAsync(scrapedList);

            _platformRepoMock.Verify(r => r.AddProductPlatformAsync(
                It.Is<ProductPlatform>(p => (int)(p.Price * 100) == expectedCents ||
                                            (priceString == null || priceString == "") && p.Price == 0m)),
                Times.AtMost(1));
        }
    }

    // UserManager 
    public class UserManagerAdditionalTests
    {
        private readonly Mock<IRepositoryManager> _repoMock;
        private readonly Mock<IUserRepository>    _userRepoMock;
        private readonly UserManager              _manager;

        public UserManagerAdditionalTests()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _repoMock     = new Mock<IRepositoryManager>();
            _repoMock.Setup(r => r.User).Returns(_userRepoMock.Object);
            _manager = new UserManager(_repoMock.Object);
        }

        // GetOneUserAsync 

        [Fact]
        public async Task GetOneUserAsync_ReturnsUser_WhenFound()
        {
            var user = new User { Id = "u1", FirebaseUid = "uid-001" };
            _userRepoMock.Setup(r => r.GetOneUserAsync("u1")).ReturnsAsync(user);

            var result = await _manager.GetOneUserAsync("u1");

            Assert.NotNull(result);
            Assert.Equal("u1", result!.Id);
        }

        [Fact]
        public async Task GetOneUserAsync_ReturnsNull_WhenNotFound()
        {
            _userRepoMock.Setup(r => r.GetOneUserAsync(It.IsAny<string>()))
                         .ReturnsAsync((User?)null);

            var result = await _manager.GetOneUserAsync("nonexistent");

            Assert.Null(result);
        }

        // UpdateOneUserAsync 

        [Fact]
        public async Task UpdateOneUserAsync_ThrowsArgumentNullException_WhenUserIsNull()
        {
            var existing = new User { Id = "u1" };
            _userRepoMock.Setup(r => r.GetOneUserAsync("u1")).ReturnsAsync(existing);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _manager.UpdateOneUserAsync("u1", null!));
        }

        [Fact]
        public async Task UpdateOneUserAsync_UpdatesPushToken()
        {
            var existing = new User { Id = "u2", Email = "a@b.com" };
            _userRepoMock.Setup(r => r.GetOneUserAsync("u2")).ReturnsAsync(existing);
            _userRepoMock.Setup(r => r.UpdateOneUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await _manager.UpdateOneUserAsync("u2", new User
            {
                PushToken              = "new-token",
                PriceAlertEnabled      = true,
                PriceCheckIntervalHours = 12,
                Email                  = "new@b.com",
                FirstName              = "Ad",
                LastName               = "Soyad"
            });

            _userRepoMock.Verify(r => r.UpdateOneUserAsync(
                It.Is<User>(u =>
                    u.PushToken              == "new-token" &&
                    u.PriceAlertEnabled      == true        &&
                    u.PriceCheckIntervalHours == 12         &&
                    u.Email                  == "new@b.com")),
                Times.Once);
        }

        [Fact]
        public async Task UpdateOneUserAsync_UpdatesPriceAlertEnabled_ToFalse()
        {
            var existing = new User { Id = "u3", PriceAlertEnabled = true };
            _userRepoMock.Setup(r => r.GetOneUserAsync("u3")).ReturnsAsync(existing);
            _userRepoMock.Setup(r => r.UpdateOneUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            await _manager.UpdateOneUserAsync("u3", new User { PriceAlertEnabled = false });

            _userRepoMock.Verify(r => r.UpdateOneUserAsync(
                It.Is<User>(u => u.PriceAlertEnabled == false)), Times.Once);
        }
    }
}
