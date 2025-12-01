using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
        public interface IProductPlatformRepository : IRepositoryBase<ProductPlatform>
        {
            IEnumerable<ProductPlatform> GetAllProductPlatforms();
            ProductPlatform GetOneProductPlatform(string id);
            Task AddProductPlatformAsync(ProductPlatform productPlatform);
            Task UpdateProductPlatformAsync(ProductPlatform productPlatform);
            Task<List<ProductPlatform>> GetPlatformsByProductIdAsync(string productId);
        }
}
