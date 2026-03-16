using AutoMapper;
using Microsoft.Extensions.Configuration;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    namespace ProductAnalysisApp.Services
    {
        public class ServiceManager : IServiceManager
        {
            private readonly Lazy<IUserService> _userService;
            private readonly Lazy<IProductService> _productService;
            private readonly Lazy<IProductPlatformService> _productPlatformService;
            private readonly Lazy<ISearchHistoryService> _searchHistoryService;
            private readonly Lazy<IFavoriteService> _favoriteService;
            public ServiceManager(IRepositoryManager repositoryManager, /*ILoggerService logger*/
                IMapper mapper, IHttpClientFactory httpClientFactory)
            {
                _userService = new Lazy<IUserService>(() => new UserManager(repositoryManager/*, mapper*/));
                _productService = new Lazy<IProductService>(() => new ProductManager(repositoryManager, mapper));
                _favoriteService = new Lazy<IFavoriteService>(() => new FavoriteManager(repositoryManager, mapper, httpClientFactory));
                _productPlatformService = new Lazy<IProductPlatformService>(() => new ProductPlatformManager(repositoryManager));
                _searchHistoryService = new Lazy<ISearchHistoryService>(() => new SearchHistoryManager(repositoryManager));
            }
            public IUserService UserService => _userService.Value;

            public IProductService ProductService => _productService.Value;

            public IProductPlatformService ProductPlatformService => _productPlatformService.Value;

            public ISearchHistoryService SearchHistoryService => _searchHistoryService.Value;
            public IFavoriteService FavoriteService => _favoriteService.Value;
        }
    }
}
