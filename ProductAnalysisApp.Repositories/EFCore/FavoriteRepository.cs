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
    public class FavoriteRepository : MongoRepositoryBase<Favorite>, IFavoriteRepository
    {
        public FavoriteRepository(IMongoCollection<Favorite> collection) : base(collection) { }

        public IQueryable<Favorite> GetAllFavorites() => FindAll();

        public Favorite GetOneFavorite(string id) => FindByCondition(f => f.FavoriteId == id).FirstOrDefault();

        public async Task AddFavoriteAsync(Favorite favorite) => await CreateAsync(favorite);

        public async Task DeleteFavoriteAsync(string id) => await DeleteAsync(f => f.FavoriteId == id);

        public IQueryable<Favorite> GetFavoritesByUserId(string userId)
        => _collection.Find(f => f.UserId == userId).ToEnumerable().AsQueryable();

        public async Task UpdateFavoriteAsync(Favorite favorite)
            => await UpdateAsync(f => f.FavoriteId == favorite.FavoriteId, favorite);
    }
}
