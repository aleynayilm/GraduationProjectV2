using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface IFavoriteService
    {
        IEnumerable<Favorite> GetAllFavorites();
        Favorite GetOneFavorite(string id);
        Task<Favorite> AddFavoriteAsync(FavoriteDtoForCreate favoriteDto);
        Task DeleteFavoriteAsync(string id);
        Task<IEnumerable<Favorite>> GetUserFavoritesAsync(string firebaseUid);
    }
}
