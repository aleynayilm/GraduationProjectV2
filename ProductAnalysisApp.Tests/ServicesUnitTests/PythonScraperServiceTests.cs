using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ProductAnalysisApp.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ProductAnalysisApp.Tests
{
    public class PythonScraperServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<PythonScraperService>> _loggerMock;

        public PythonScraperServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<PythonScraperService>>();

            _configMock
                .Setup(c => c["PythonScraperService:BaseUrl"])
                .Returns("http://localhost:8000");
        }

        private PythonScraperService CreateService(HttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
            return new PythonScraperService(httpClient, _configMock.Object, _loggerMock.Object);
        }

        private static Mock<HttpMessageHandler> SetupHandler(HttpStatusCode statusCode, object? body = null)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var json = body != null ? JsonSerializer.Serialize(body) : "{}";

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });

            return handlerMock;
        }

        // ScrapeProductAsync 

        [Fact]
        public async Task ScrapeProductAsync_ReturnsProduct_WhenResponseIsSuccessful()
        {
            var expected = new
            {
                ProductName = "Test Ürün",
                Price = "299.99",
                PlatformName = "Trendyol",
                Currency = "TRY"
            };

            var handler = SetupHandler(HttpStatusCode.OK, expected);
            var service = CreateService(handler.Object);

            var result = await service.ScrapeProductAsync("https://example.com/product/1");

            Assert.NotNull(result);
            Assert.Equal("Test Ürün", result.ProductName);
            Assert.Equal("299.99", result.Price);
        }

        [Fact]
        public async Task ScrapeProductAsync_ReturnsNull_WhenResponseIsNotSuccessful()
        {
            var handler = SetupHandler(HttpStatusCode.InternalServerError);
            var service = CreateService(handler.Object);

            var result = await service.ScrapeProductAsync("https://example.com/product/1");

            Assert.Null(result);
        }

        [Fact]
        public async Task ScrapeProductAsync_ReturnsNull_WhenHttpClientThrowsException()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Bağlantı hatası"));

            var service = CreateService(handlerMock.Object);

            var result = await service.ScrapeProductAsync("https://example.com/product/1");

            Assert.Null(result);
        }

        [Fact]
        public async Task ScrapeProductAsync_UsesDefaultBaseUrl_WhenConfigurationIsNull()
        {
            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["PythonScraperService:BaseUrl"]).Returns((string?)null);

            var handler = SetupHandler(HttpStatusCode.OK, new { ProductName = "Ürün", Price = "10.00" });
            var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:8000") };
            var service = new PythonScraperService(httpClient, configMock.Object, _loggerMock.Object);

            var result = await service.ScrapeProductAsync("https://example.com/product/1");

            Assert.NotNull(result);
        }

        //  CompareProductsAsync 

        [Fact]
        public async Task CompareProductsAsync_ReturnsList_WhenResponseIsSuccessful()
        {
            var expected = new[]
            {
                new { ProductName = "Ürün A", Price = "100.00" },
                new { ProductName = "Ürün B", Price = "200.00" }
            };

            var handler = SetupHandler(HttpStatusCode.OK, expected);
            var service = CreateService(handler.Object);

            var result = await service.CompareProductsAsync("laptop");

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("Ürün A", result[0].ProductName);
        }

        [Fact]
        public async Task CompareProductsAsync_ReturnsNull_WhenResponseIsNotSuccessful()
        {
            var handler = SetupHandler(HttpStatusCode.BadRequest);
            var service = CreateService(handler.Object);

            var result = await service.CompareProductsAsync("laptop");

            Assert.Null(result);
        }

        [Fact]
        public async Task CompareProductsAsync_ReturnsNull_WhenExceptionThrown()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Timeout"));

            var service = CreateService(handlerMock.Object);

            var result = await service.CompareProductsAsync("laptop");

            Assert.Null(result);
        }

        [Fact]
        public async Task CompareProductsAsync_ReturnsEmptyList_WhenApiReturnsEmptyArray()
        {
            var handler = SetupHandler(HttpStatusCode.OK, Array.Empty<object>());
            var service = CreateService(handler.Object);

            var result = await service.CompareProductsAsync("laptop");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ScrapeOneAsync 

        [Fact]
        public async Task ScrapeOneAsync_ReturnsDto_WhenResponseIsSuccessful()
        {
            var expected = new PythonScraperService.ScrapedProductDto
            {
                ProductName = "Gaming Mouse",
                PlatformName = "Hepsiburada",
                Price = 549.90m,
                Currency = "TRY",
                ProductUrl = "https://example.com/mouse"
            };

            var handler = SetupHandler(HttpStatusCode.OK, expected);
            var service = CreateService(handler.Object);

            var result = await service.ScrapeOneAsync("https://example.com/mouse");

            Assert.NotNull(result);
            Assert.Equal("Gaming Mouse", result.ProductName);
            Assert.Equal(549.90m, result.Price);
        }

        [Fact]
        public async Task ScrapeOneAsync_ReturnsNull_WhenResponseIsNotSuccessful()
        {
            var handler = SetupHandler(HttpStatusCode.NotFound);
            var service = CreateService(handler.Object);

            var result = await service.ScrapeOneAsync("https://example.com/missing");

            Assert.Null(result);
        }

        [Fact]
        public async Task ScrapeOneAsync_ReturnsNull_WhenExceptionThrown()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Servis çevrimdışı"));

            var service = CreateService(handlerMock.Object);

            var result = await service.ScrapeOneAsync("https://example.com/mouse");

            Assert.Null(result);
        }
    }
}
