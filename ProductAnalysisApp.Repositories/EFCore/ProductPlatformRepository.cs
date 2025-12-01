using MongoDB.Driver;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.EFCore
{
    public class ProductPlatformRepository : MongoRepositoryBase<ProductPlatform>, IProductPlatformRepository
    {
        public ProductPlatformRepository(IMongoCollection<ProductPlatform> collection)
            : base(collection)
        {
        }
        public IEnumerable<ProductPlatform> GetAllProductPlatforms()
            => FindAll();

        public ProductPlatform GetOneProductPlatform(string id)
            => FindByCondition(pp => pp.ProductPlatformId == id).FirstOrDefault();

        public async Task AddProductPlatformAsync(ProductPlatform productPlatform)
            => await CreateAsync(productPlatform);

        public async Task UpdateProductPlatformAsync(ProductPlatform productPlatform)
        {
            await UpdateAsync(p => p.ProductPlatformId == productPlatform.ProductPlatformId, productPlatform);
        }

        public async Task<List<ProductPlatform>> GetPlatformsByProductIdAsync(string productId)
        {
            return await _collection
                .Find(pp => pp.ProductId == productId)
                .ToListAsync();
        }
    }
}
