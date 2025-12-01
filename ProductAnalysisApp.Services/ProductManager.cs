using AutoMapper;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    public class ProductManager : IProductService
    {
        private readonly IRepositoryManager _manager;
        private readonly IMapper _mapper;

        public ProductManager(IRepositoryManager manager, IMapper mapper)
        {
            _manager = manager;
            _mapper = mapper;
        }

        public IEnumerable<Product> GetAllProducts()
            => _manager.Product.GetAllProducts();

        public Product GetOneProduct(string id)
            => _manager.Product.GetOneProduct(id);

        public async Task<Product> AddOneProductAsync(ProductDtoForCreate dto)
        {
            var product = _mapper.Map<Product>(dto);

            await _manager.Product.AddProductAsync(product);
            return product;
        }

        public async Task DeleteOneProductAsync(string id)
        {
            var product = GetOneProduct(id);
            if (product == null)
                throw new Exception("Product not found");

            await _manager.Product.DeleteProductAsync(id);
        }

        public async Task UpdateOneProductAsync(string id, Product product)
        {
            var existing = GetOneProduct(id);
            if (existing == null)
                throw new Exception("Product not found");

            product.ProductId = id;
            await _manager.Product.UpdateProductIfChangedAsync(product);
        }

        public async Task<List<Product>> SaveScrapedProductAsync(List<ProductForScrapingDto> scrapedList)
        {
            if (scrapedList == null || scrapedList.Count == 0)
                throw new ArgumentException("Scraped list is empty");

            var firstItem = scrapedList.First();

            var product = new Product
            {
                CategoryId = "692d61297cfe91ca4f77a0a5",
                Name = firstItem.ProductName,
                Description = firstItem.Description,
                ImageUrl = firstItem.ImageUrl,
                ScrapedDate = DateTime.Now
            };

            await _manager.Product.AddProductAsync(product);

            foreach (var item in scrapedList)
            {
                var normalized = item.PlatformName.Trim().ToLower();

                if (!PlatformMap.TryGetValue(normalized, out string platformId))
                    continue;

                var price = ParsePriceStringToDecimal(item.Price);

                var productPlatform = new ProductPlatform
                {
                    ProductId = product.ProductId,
                    PlatformId = platformId,
                    ProductUrl = item.ProductUrl,
                    Price = price,
                    Currency = item.Currency
                };

                await _manager.ProductPlatform.AddProductPlatformAsync(productPlatform);
            }

            return new List<Product> { product };
        }

        private readonly Dictionary<string, string> PlatformMap = new()
    {
        { "trendyol", "692d65ec17d42f6575ed568d" },
        { "amazon", "692d65fa17d42f6575ed568f" },
        { "n11", "692d660417d42f6575ed5691" }
    };

        private decimal ParsePriceStringToDecimal(string? priceString)
        {
            if (string.IsNullOrWhiteSpace(priceString))
                return 0;

            string cleaned = priceString.Replace(".", "");

            cleaned = cleaned.Replace(",", ".");

            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
        }

        public string GetPlatformNameById(string platformId)
            => PlatformMap.FirstOrDefault(x => x.Value == platformId).Key ?? "Unknown";
    }
}
