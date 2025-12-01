using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
    public interface IProductRepository : IRepositoryBase<Product>
    {
        IQueryable<Product> GetAllProducts();
        Product GetOneProduct(string id);
        Task AddProductAsync(Product product);
        Task UpdateProductIfChangedAsync(Product product);
        Task DeleteProductAsync(string id);
    }
}
