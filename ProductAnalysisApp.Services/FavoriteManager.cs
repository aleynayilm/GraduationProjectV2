using AutoMapper;
using MongoDB.Bson;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    public class FavoriteManager : IFavoriteService
    {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IMapper _mapper;

        public FavoriteManager(IRepositoryManager repositoryManager, IMapper mapper)
        {
            _repositoryManager = repositoryManager;
            _mapper = mapper;
        }

        public async Task<Favorite> AddFavoriteAsync(FavoriteDtoForCreate favoriteDto)
        {
            if (favoriteDto is null) throw new ArgumentNullException(nameof(favoriteDto));
            if (string.IsNullOrWhiteSpace(favoriteDto.FirebaseUid)) throw new ArgumentNullException(nameof(favoriteDto.FirebaseUid));

            var productPlatform = _repositoryManager.ProductPlatform.GetOneProductPlatform(favoriteDto.ProductPlatformId);
            if (productPlatform == null) throw new Exception($"Product '{favoriteDto.ProductPlatformId}' not found!");

            var user = await _repositoryManager.User.GetOneUserByFirebaseUidAsync(favoriteDto.FirebaseUid);
            if (user == null) throw new Exception($"User with FirebaseUid '{favoriteDto.FirebaseUid}' not found!");

            var existing = _repositoryManager.Favorite
            .GetAllFavorites()
            .FirstOrDefault(f => f.UserId == user.Id && f.ProductPlatformId == productPlatform.ProductPlatformId);

            if (existing != null)
                return existing;

            var favorite = new Favorite
            {
                FavoriteId = ObjectId.GenerateNewId().ToString(),
                UserId = user.Id,
                ProductPlatformId = productPlatform.ProductPlatformId,
                CreatedDate = DateTime.UtcNow
            };

            await _repositoryManager.Favorite.AddFavoriteAsync(favorite);

            return favorite;
        }

        public async Task DeleteFavoriteAsync(string id)
        {
            var entity = _repositoryManager.Favorite.GetOneFavorite(id);
            if (entity is null) throw new Exception($"Favorite with id: {id} not found.");
            await _repositoryManager.Favorite.DeleteFavoriteAsync(id);
        }

        public IEnumerable<Favorite> GetAllFavorites() => _repositoryManager.Favorite.GetAllFavorites();

        public Favorite GetOneFavorite(string id) => _repositoryManager.Favorite.GetOneFavorite(id);

        public async Task<IEnumerable<Favorite>> GetUserFavoritesAsync(string firebaseUid)
        {
            if (string.IsNullOrWhiteSpace(firebaseUid)) throw new ArgumentNullException(nameof(firebaseUid));

            var user = await _repositoryManager.User.GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null) return Enumerable.Empty<Favorite>();

            return _repositoryManager.Favorite.GetFavoritesByUserId(user.Id).ToList();
        }
    }
}
