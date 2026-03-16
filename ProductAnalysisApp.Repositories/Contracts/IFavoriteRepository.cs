using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
    public interface IFavoriteRepository: IRepositoryBase<Favorite>
    {
        IQueryable<Favorite> GetAllFavorites();
        Favorite GetOneFavorite(string id);
        Task AddFavoriteAsync(Favorite favorite);
        Task DeleteFavoriteAsync(string id);
        IQueryable<Favorite> GetFavoritesByUserId(string userId);
        Task UpdateFavoriteAsync(Favorite favorite);
    }
}
