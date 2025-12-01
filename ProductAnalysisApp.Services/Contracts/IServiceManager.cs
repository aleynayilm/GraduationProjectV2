using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface IServiceManager
    {
        IFavoriteService FavoriteService { get; }
        IProductService ProductService { get; }
        IProductPlatformService ProductPlatformService { get; }
        ISearchHistoryService SearchHistoryService { get; }
        IUserService UserService { get; }
    }
}
