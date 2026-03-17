using AutoMapper;
using FluentAssertions;
using Moq;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProductAnalysisApp.Tests.ServicesUnitTests
{
    public class ProductManagerTests
    {
        private readonly Mock<IRepositoryManager>         _repoMock;
        private readonly Mock<IProductRepository>         _productRepoMock;
        private readonly Mock<IProductPlatformRepository> _platformRepoMock;
        private readonly Mock<IMapper>                    _mapperMock;
        private readonly ProductManager                   _sut;

        public ProductManagerTests()
        {
            _repoMock         = new Mock<IRepositoryManager>();
            _productRepoMock  = new Mock<IProductRepository>();
            _platformRepoMock = new Mock<IProductPlatformRepository>();
            _mapperMock       = new Mock<IMapper>();

            _repoMock.Setup(r => r.Product).Returns(_productRepoMock.Object);
            _repoMock.Setup(r => r.ProductPlatform).Returns(_platformRepoMock.Object);

            _sut = new ProductManager(_repoMock.Object, _mapperMock.Object);
        }

        // ── SaveScrapedProductAsync ──────────────────────────────────────

        [Fact]
        public async Task SaveScrapedProductAsync_WithValidData_ShouldSaveProductAndPlatforms()
        {
            // Arrange
            var scrapedList = new List<ProductForScrapingDto>
            {
                new() { ProductName = "JBL Kulaklık", PlatformName = "trendyol",
                        ProductUrl = "https://trendyol.com/p", Price = "1.500,00",
                        Currency = "TRY", ImageUrl = "https://img.com/1.jpg",
                        Description = "JBL Kulaklık" },
                new() { ProductName = "JBL Kulaklık", PlatformName = "amazon",
                        ProductUrl = "https://amazon.com.tr/dp/123", Price = "1.600,00",
                        Currency = "TRY", ImageUrl = "https://img.com/2.jpg",
                        Description = "JBL Kulaklık" },
            };

            _productRepoMock
                .Setup(r => r.AddProductAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            _platformRepoMock
                .Setup(r => r.AddProductPlatformAsync(It.IsAny<ProductPlatform>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.SaveScrapedProductAsync(scrapedList);

            // Assert
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("JBL Kulaklık");

            _productRepoMock.Verify(r => r.AddProductAsync(It.IsAny<Product>()), Times.Once);
            _platformRepoMock.Verify(
                r => r.AddProductPlatformAsync(It.IsAny<ProductPlatform>()), Times.Exactly(2));
        }

        [Fact]
        public async Task SaveScrapedProductAsync_WithEmptyList_ShouldThrowArgumentException()
        {
            // Act & Assert
            await _sut.Invoking(s => s.SaveScrapedProductAsync(new List<ProductForScrapingDto>()))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*empty*");
        }

        [Fact]
        public async Task SaveScrapedProductAsync_WithNullList_ShouldThrowArgumentException()
        {
            // Act & Assert
            await _sut.Invoking(s => s.SaveScrapedProductAsync(null!))
                .Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task SaveScrapedProductAsync_WithUnknownPlatform_ShouldSkipPlatform()
        {
            // Arrange
            var scrapedList = new List<ProductForScrapingDto>
            {
                new() { ProductName = "Test Ürün", PlatformName = "bilinmeyen-platform",
                        ProductUrl = "https://bilinmeyen.com/p", Price = "100,00",
                        Currency = "TRY" }
            };

            _productRepoMock
                .Setup(r => r.AddProductAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.SaveScrapedProductAsync(scrapedList);

            // Assert — ürün kaydedilir ama platform kaydedilmez
            result.Should().HaveCount(1);
            _platformRepoMock.Verify(
                r => r.AddProductPlatformAsync(It.IsAny<ProductPlatform>()), Times.Never);
        }

        // ── ParsePriceStringToDecimal (dolaylı test) ─────────────────────

        [Theory]
        [InlineData("1.500,00", 1500.00)]
        [InlineData("1.500",    1500.00)]
        [InlineData("1500,00",  1500.00)]
        [InlineData("999",      999.00)]
        [InlineData("",         0.00)]
        [InlineData(null,       0.00)]
        public async Task SaveScrapedProductAsync_PriceParsing_ShouldParseCorrectly(
            string? priceString, decimal expectedPrice)
        {
            // Arrange
            ProductPlatform? captured = null;

            var scrapedList = new List<ProductForScrapingDto>
            {
                new() { ProductName = "Test", PlatformName = "trendyol",
                        ProductUrl = "https://trendyol.com/p",
                        Price = priceString!, Currency = "TRY" }
            };

            _productRepoMock
                .Setup(r => r.AddProductAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            _platformRepoMock
                .Setup(r => r.AddProductPlatformAsync(It.IsAny<ProductPlatform>()))
                .Callback<ProductPlatform>(p => captured = p)
                .Returns(Task.CompletedTask);

            // Act
            await _sut.SaveScrapedProductAsync(scrapedList);

            // Assert
            captured.Should().NotBeNull();
            captured!.Price.Should().Be(expectedPrice);
        }

        // ── GetPlatformNameById ──────────────────────────────────────────

        [Theory]
        [InlineData("692d65ec17d42f6575ed568d", "trendyol")]
        [InlineData("692d65fa17d42f6575ed568f", "amazon")]
        [InlineData("692d660417d42f6575ed5691", "n11")]
        [InlineData("nonexistent-id",            "Unknown")]
        public void GetPlatformNameById_ShouldReturnCorrectName(
            string platformId, string expectedName)
        {
            var result = _sut.GetPlatformNameById(platformId);
            result.Should().Be(expectedName);
        }
    }
}
