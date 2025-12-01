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
    public class ProductRepository : MongoRepositoryBase<Product>, IProductRepository
    {
        public ProductRepository(IMongoCollection<Product> collection)
            : base(collection)
        {
        }

        public IQueryable<Product> GetAllProducts()
            => FindAll();

        public Product GetOneProduct(string id)
            => FindByCondition(p => p.ProductId == id).FirstOrDefault();

        public async Task AddProductAsync(Product product)
            => await CreateAsync(product);

        public async Task DeleteProductAsync(string id)
            => await DeleteAsync(p => p.ProductId == id);

        public async Task UpdateProductIfChangedAsync(Product product)
        {
            var existing = GetOneProduct(product.ProductId);
            if (existing == null) return;

            bool hasChanges =
                existing.Name != product.Name ||
                existing.Description != product.Description ||
                existing.ImageUrl != product.ImageUrl;

            if (hasChanges)
            {
                product.ScrapedDate = DateTime.Now;
                await UpdateAsync(p => p.ProductId == product.ProductId, product);
            }
        }
    }
}
