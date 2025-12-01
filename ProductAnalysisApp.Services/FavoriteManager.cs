using AutoMapper;
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

            var productPlatform = _repositoryManager.ProductPlatform.GetOneProductPlatform(favoriteDto.ProductPlatformId);
            if (productPlatform == null) throw new Exception($"Product '{favoriteDto.ProductPlatformId}' not found!");

            var user = _repositoryManager.User.GetOneUserByFirebaseUidAsync(favoriteDto.FirebaseUid);
            if (user == null) throw new Exception($"User with FirebaseUid '{favoriteDto.FirebaseUid}' not found!");

            var favorite = _mapper.Map<Favorite>(favoriteDto);
            favorite.ProductPlatformId = productPlatform.ProductId;
            favorite.UserId = user.Id.ToString();

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
    }
}
