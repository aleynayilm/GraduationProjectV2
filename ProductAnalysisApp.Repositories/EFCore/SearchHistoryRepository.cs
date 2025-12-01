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
    public class SearchHistoryRepository : MongoRepositoryBase<SearchHistory>, ISearchHistoryRepository
    {
        private readonly IUserRepository _userRepository;

        public SearchHistoryRepository(IMongoCollection<SearchHistory> collection, IUserRepository userRepository)
            : base(collection)
        {
            _userRepository = userRepository;
        }

        public async Task AddSearchHistoryAsync(SearchHistory history)
            => await CreateAsync(history);

        public async Task<List<SearchHistory>> GetAllSearchHistoriesAsync()
            => await _collection.Find(_ => true).SortByDescending(s => s.SearchDate).ToListAsync();

        public async Task<List<SearchHistory>> GetUserSearchHistoriesAsync(string userId)
            => await _collection
                .Find(s => s.FirebaseUid == userId)
                .SortByDescending(s => s.SearchDate)
                .Limit(10)
                .ToListAsync();
    }
}
