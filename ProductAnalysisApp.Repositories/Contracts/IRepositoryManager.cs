using ProductAnalysisApp.Repositories.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
    public interface IRepositoryManager
    {
        IUserRepository User { get; }
        IProductRepository Product { get; }
        IPlatformRepository Platform { get; }
        IProductPlatformRepository ProductPlatform { get; }
        ISearchHistoryRepository SearchHistory { get; }
        IFavoriteRepository Favorite { get; }
    }
}
