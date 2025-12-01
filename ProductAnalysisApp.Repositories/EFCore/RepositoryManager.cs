using ProductAnalysisApp.Repositories.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.EFCore
{
    public class RepositoryManager : IRepositoryManager
    {
        private readonly MongoDbContext _context;
        private readonly Lazy<IUserRepository> _userRepository;
        private readonly Lazy<IProductRepository> _productRepository;
        private readonly Lazy<IPlatformRepository> _platformRepository;
        private readonly Lazy<IProductPlatformRepository> _productPlatformRepository;
        private readonly Lazy<ISearchHistoryRepository> _searchHistoryRepository;
        private readonly Lazy<IFavoriteRepository> _favoriteRepository;

        public RepositoryManager(MongoDbContext context)
        {
            _context = context;
            _userRepository = new Lazy<IUserRepository>(() => new UserRepository(_context.Users));
            _productRepository = new Lazy<IProductRepository>(() => new ProductRepository(_context.Products));
            _platformRepository = new Lazy<IPlatformRepository>(() => new PlatformRepository(_context.Platforms));
            _productPlatformRepository = new Lazy<IProductPlatformRepository>(() => new ProductPlatformRepository(_context.ProductPlatforms));
            _searchHistoryRepository = new Lazy<ISearchHistoryRepository>(() => new SearchHistoryRepository(_context.SearchHistories, this.User));
            _favoriteRepository = new Lazy<IFavoriteRepository>(() => new FavoriteRepository(_context.Favorites));
        }

        public IUserRepository User => _userRepository.Value;
        public IProductRepository Product => _productRepository.Value;
        public IPlatformRepository Platform => _platformRepository.Value;
        public IProductPlatformRepository ProductPlatform => _productPlatformRepository.Value;
        public ISearchHistoryRepository SearchHistory => _searchHistoryRepository.Value;
        public IFavoriteRepository Favorite => _favoriteRepository.Value;
    }
}
