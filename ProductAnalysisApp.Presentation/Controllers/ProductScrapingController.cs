using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Services;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductScrapingController : ControllerBase
    {
        private readonly IServiceManager _manager;
        private readonly PythonScraperService _scraperService;
        private readonly ILogger<ProductScrapingController> _logger;
        public ProductScrapingController(IServiceManager manager, PythonScraperService scraperService, ILogger<ProductScrapingController> logger)
        {
            _manager = manager;
            _scraperService = scraperService;
            _logger = logger;
        }
        [HttpPost("scrape")]
        public async Task<ActionResult> ScrapeProduct([FromBody] ScrapeRequest request)
        {
            //if (string.IsNullOrEmpty(request.Url))
            //    return BadRequest(new { message = "Product URL is required." });

            //var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            //if (string.IsNullOrEmpty(authHeader))
            //{
            //    _logger.LogWarning("[API] Token bulunamadı. İstek reddedildi.");
            //    return Unauthorized();
            //}

            //var firebaseUid = HttpContext.Items["FirebaseUid"]?.ToString();
            //var userId = HttpContext.Items["UserId"];

            //if (firebaseUid == null || userId == null)
            //{
            //    _logger.LogWarning("[API] Kullanıcı doğrulaması başarısız. UID veya UserId null.");
            //    return Unauthorized();
            //}

            //_logger.LogInformation($"[API] Scraping başlatıldı: {request.Url} | UID: {firebaseUid} | UserId: {userId}");
            var sw = Stopwatch.StartNew();
            try
            {
                var products = await _scraperService.CompareProductsAsync(request.Url);

                if (products == null || !products.Any())
                {
                    sw.Stop();
                    _logger.LogWarning($"[API] Ürün bilgisi alınamadı: {request.Url}");
                    return NotFound(new ApiResponse<string>
                    {
                        DurationMs = sw.ElapsedMilliseconds,
                        Data = "No product information could be scraped."
                    });
                }
                var savedProducts = await _manager.ProductService.SaveScrapedProductAsync(products);

                var response = new List<ProductResponseDto>();

                foreach (var p in savedProducts)
                {
                    var platforms = await _manager.ProductPlatformService.GetPlatformsByProductIdAsync(p.ProductId);

                    response.Add(new ProductResponseDto
                    {
                        ProductId = p.ProductId,
                        Name = p.Name,
                        Description = p.Description,
                        ImageUrl = p.ImageUrl,
                        Platforms = platforms.Select(pp => new ProductPlatformResponseDto
                        {
                            PlatformId = pp.PlatformId,
                            PlatformName = _manager.ProductService.GetPlatformNameById(pp.PlatformId),
                            ProductUrl = pp.ProductUrl,
                            Price = pp.Price,
                            Currency = pp.Currency
                        }).ToList()
                    });
                }
                sw.Stop();
                _logger.LogInformation($"[API] Scraping başarılı: {request.Url}");
                return StatusCode(201, new ApiResponse<List<ProductResponseDto>>
                {
                    DurationMs = sw.ElapsedMilliseconds,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[API] Scraping sırasında hata oluştu: {request.Url}");
                return StatusCode(500, new ApiResponse<string>
                {
                    DurationMs = sw.ElapsedMilliseconds,
                    Data = ex.Message
                });
            }
        }
    }
}
