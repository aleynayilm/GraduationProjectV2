using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface IProductService
    {
        IEnumerable<Product> GetAllProducts();
        Product GetOneProduct(string id);
        Task<Product> AddOneProductAsync(ProductDtoForCreate dto);
        Task UpdateOneProductAsync(string id, Product product);
        Task DeleteOneProductAsync(string id);
        Task<List<Product>> SaveScrapedProductAsync(List<ProductForScrapingDto> scrapedList);
        string GetPlatformNameById(string platformId);
    }
}
