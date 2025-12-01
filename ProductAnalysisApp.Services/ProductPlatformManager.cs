using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    public class ProductPlatformManager : IProductPlatformService
    {
        private readonly IRepositoryManager _manager;

        public ProductPlatformManager(IRepositoryManager manager)
        {
            _manager = manager;
        }

        public IEnumerable<ProductPlatform> GetAllProductPlatforms()
            => _manager.ProductPlatform.GetAllProductPlatforms();

        public ProductPlatform GetOneProductPlatform(string id)
            => _manager.ProductPlatform.GetOneProductPlatform(id);

        public async Task AddProductPlatformAsync(ProductPlatform productPlatform)
            => await _manager.ProductPlatform.AddProductPlatformAsync(productPlatform);

        public async Task UpdateProductPlatformAsync(ProductPlatform productPlatform)
            => await _manager.ProductPlatform.UpdateProductPlatformAsync(productPlatform);

        public async Task<List<ProductPlatform>> GetPlatformsByProductIdAsync(string productId)
            => await _manager.ProductPlatform.GetPlatformsByProductIdAsync(productId);
    }
}
