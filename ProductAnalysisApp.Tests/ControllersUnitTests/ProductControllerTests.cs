using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Presentation.Controllers;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProductAnalysisApp.Tests.Controllers
{
    public class ProductControllerTests : ControllerTestBase
    {
        private readonly Mock<IServiceManager>  _serviceManagerMock;
        private readonly Mock<IProductService>  _productServiceMock;
        private readonly ProductController      _sut;
        private readonly Product                _testProduct;

        public ProductControllerTests()
        {
            _serviceManagerMock = new Mock<IServiceManager>();
            _productServiceMock = new Mock<IProductService>();

            _serviceManagerMock.Setup(m => m.ProductService).Returns(_productServiceMock.Object);

            _sut = new ProductController(_serviceManagerMock.Object);
            SetAuthenticatedUser(_sut);

            _testProduct = new Product
            {
                ProductId   = "product-001",
                Name        = "JBL Kulaklık",
                Description = "JBL Tune 510BT",
            };
        }

        // GetAllProducts 

        [Fact]
        public void GetAllProducts_ShouldReturn200WithList()
        {
            // Arrange
            _productServiceMock
                .Setup(s => s.GetAllProducts())
                .Returns(new List<Product> { _testProduct });

            // Act
            var result = _sut.GetAllProducts();

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().NotBeNull();
        }

        // GetOneProduct 

        [Fact]
        public void GetOneProduct_WhenExists_ShouldReturn200()
        {
            // Arrange
            _productServiceMock
                .Setup(s => s.GetOneProduct("product-001"))
                .Returns(_testProduct);

            // Act
            var result = _sut.GetOneProduct("product-001");

            // Assert
            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            ok.Value.Should().BeEquivalentTo(_testProduct);
        }

        [Fact]
        public void GetOneProduct_WhenNotFound_ShouldReturn404()
        {
            // Arrange
            _productServiceMock
                .Setup(s => s.GetOneProduct("nonexistent"))
                .Returns((Product?)null);

            // Act
            var result = _sut.GetOneProduct("nonexistent");

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        // AddOneProduct 

        [Fact]
        public async Task AddOneProduct_WithValidDto_ShouldReturn201()
        {
            // Arrange
            var dto = new ProductDtoForCreate { Name = "Yeni Ürün" };

            _productServiceMock
                .Setup(s => s.AddOneProductAsync(dto))
                .ReturnsAsync(_testProduct);

            // Act
            var result = await _sut.AddOneProduct(dto);

            // Assert
            var status = result.Should().BeOfType<ObjectResult>().Subject;
            status.StatusCode.Should().Be(201);
        }

        [Fact]
        public async Task AddOneProduct_WithNullDto_ShouldReturn400()
        {
            // Act
            var result = await _sut.AddOneProduct(null!);

            // Assert
            result.Should().BeOfType<BadRequestResult>();
        }

        // UpdateOneProduct 

        [Fact]
        public async Task UpdateOneProduct_ShouldReturn204()
        {
            // Arrange
            _productServiceMock
                .Setup(s => s.UpdateOneProductAsync("product-001", It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.UpdateOneProduct("product-001", _testProduct);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        // DeleteOneProduct 

        [Fact]
        public async Task DeleteOneProduct_ShouldReturn204()
        {
            // Arrange
            _productServiceMock
                .Setup(s => s.DeleteOneProductAsync("product-001"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.DeleteOneProduct("product-001");

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }
    }
}
