using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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

    }
}
