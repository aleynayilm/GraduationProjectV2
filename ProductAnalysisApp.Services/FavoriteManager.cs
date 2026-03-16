using AutoMapper;
using MongoDB.Bson;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services.Contracts;
using System.Net.Http;
using System.Net.Http.Json;
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
        private readonly IHttpClientFactory _httpClientFactory;

        public FavoriteManager(IRepositoryManager repositoryManager, IMapper mapper, IHttpClientFactory httpClientFactory)
        {
            _repositoryManager = repositoryManager;
            _mapper = mapper;
            _httpClientFactory = httpClientFactory;
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

        public async Task CategorizeFavoritesAsync(string firebaseUid)
        {
            var user = await _repositoryManager.User
                .GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null) return;

            var favorites = _repositoryManager.Favorite
                .GetFavoritesByUserId(user.Id).ToList();
            if (!favorites.Any()) return;

            var items = favorites.Select(fav =>
            {
                var platform = _repositoryManager.ProductPlatform
                    .GetOneProductPlatform(fav.ProductPlatformId);
                return new
                {
                    favoriteId = fav.FavoriteId,
                    url = platform?.ProductUrl ?? "",
                    price = platform?.Price ?? 0
                };
            }).ToList();

            var client = _httpClientFactory.CreateClient("PythonScraperService");
            var payload = new { favorites = items };

            var response = await client.PostAsJsonAsync("/categorize-favorites", payload);
            if (!response.IsSuccessStatusCode) return;

            var categorized = await response.Content
                .ReadFromJsonAsync<List<CategorizationResult>>();

            if (categorized == null) return;

            foreach (var item in categorized)
            {
                var fav = favorites.FirstOrDefault(f => f.FavoriteId == item.FavoriteId);
                if (fav == null) continue;

                fav.Category = item.Category;
                await _repositoryManager.Favorite.UpdateFavoriteAsync(fav);
            }
        }

        private class CategorizationResult
        {
            public string FavoriteId { get; set; } = "";
            public string Category { get; set; } = "";
        }

        public async Task DeleteFavoriteAsync(string id)
        {
            var entity = _repositoryManager.Favorite.GetOneFavorite(id);
            if (entity is null) throw new Exception($"Favorite with id: {id} not found.");
            await _repositoryManager.Favorite.DeleteFavoriteAsync(id);
        }

        public IEnumerable<Favorite> GetAllFavorites() => _repositoryManager.Favorite.GetAllFavorites();

        public Favorite GetOneFavorite(string id) => _repositoryManager.Favorite.GetOneFavorite(id);

        public async Task<List<FavoriteDetailDto>> GetUserFavoriteDetailsAsync(string firebaseUid)
        {
            var user = await _repositoryManager.User
                .GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null) return new List<FavoriteDetailDto>();

            var favorites = _repositoryManager.Favorite
                .GetFavoritesByUserId(user.Id).ToList();

            var result = new List<FavoriteDetailDto>();

            foreach (var fav in favorites)
            {
                var platform = _repositoryManager.ProductPlatform
                    .GetOneProductPlatform(fav.ProductPlatformId);

                result.Add(new FavoriteDetailDto
                {
                    FavoriteId = fav.FavoriteId,
                    ProductPlatformId = fav.ProductPlatformId,
                    ProductUrl = platform?.ProductUrl ?? "",
                    Price = platform?.Price ?? 0,
                    Currency = platform?.Currency ?? "TRY",
                    Category = fav.Category,
                    LastPriceCheckedAt = platform?.LastPriceCheckedAt,
                    CreatedDate = fav.CreatedDate
                });
            }

            return result;
        }

        public async Task<IEnumerable<Favorite>> GetUserFavoritesAsync(string firebaseUid)
        {
            if (string.IsNullOrWhiteSpace(firebaseUid)) throw new ArgumentNullException(nameof(firebaseUid));

            var user = await _repositoryManager.User.GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null) return Enumerable.Empty<Favorite>();

            return _repositoryManager.Favorite.GetFavoritesByUserId(user.Id).ToList();
        }
    }
}
