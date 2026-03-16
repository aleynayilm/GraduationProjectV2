using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    public class PythonScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PythonScraperService> _logger;

        public PythonScraperService(HttpClient httpClient, IConfiguration configuration, ILogger<PythonScraperService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ProductForScrapingDto?> ScrapeProductAsync(string productUrl)
        {
            try
            {
                var pythonServiceUrl = _configuration["PythonScraperService:BaseUrl"] ?? "http://localhost:8000";

                var request = new ScrapeRequest { Url = productUrl };
                var jsonContent = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation($"[PYTHON] İstek gönderiliyor: {productUrl}");

                var response = await _httpClient.PostAsync($"{pythonServiceUrl}/api/scrape", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[PYTHON ERROR] {response.StatusCode}: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var product = JsonSerializer.Deserialize<ProductForScrapingDto>(responseContent, options);

                _logger.LogInformation($"[PYTHON SUCCESS] Ürün: {product?.ProductName}, Fiyat: {product?.Price}");

                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PYTHON EXCEPTION] {ex.Message}");
                return null;
            }
        }

        public async Task<List<ProductForScrapingDto>?> CompareProductsAsync(string query)
        {
            try
            {
                var pythonServiceUrl = _configuration["PythonScraperService:BaseUrl"] ?? "http://localhost:8000";

                var request = new { url = query };
                var jsonContent = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation($"[PYTHON] Karşılaştırma isteği gönderiliyor: {query}");

                var response = await _httpClient.PostAsync($"{pythonServiceUrl}/api/compare", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"[PYTHON ERROR] {response.StatusCode}: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var products = JsonSerializer.Deserialize<List<ProductForScrapingDto>>(responseContent, options);

                _logger.LogInformation($"[PYTHON SUCCESS] {products?.Count} ürün bulundu.");

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PYTHON EXCEPTION] {ex.Message}");
                return null;
            }
        }

        public async Task<ScrapedProductDto?> ScrapeOneAsync(string url)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/scrape-one", new { url });
                if (!response.IsSuccessStatusCode) return null;

                return await response.Content.ReadFromJsonAsync<ScrapedProductDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SCRAPER] ScrapeOne hatası: {Url}", url);
                return null;
            }
        }

        public class ScrapedProductDto
        {
            public string ProductName { get; set; } = "";
            public string PlatformName { get; set; } = "";
            public decimal Price { get; set; }
            public string Currency { get; set; } = "TRY";
            public string? ImageUrl { get; set; }
            public string ProductUrl { get; set; } = "";
        }
    }
}
