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
    public class SearchHistoryManager : ISearchHistoryService
    {
        private readonly IRepositoryManager _manager;

        public SearchHistoryManager(IRepositoryManager manager)
        {
            _manager = manager;
        }

        public async Task AddSearchAsync(string url, string userId)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            var history = new SearchHistory
            {
                SearchUrl = url.Trim(),
                FirebaseUid = userId,
                SearchDate = DateTime.UtcNow
            };

            await _manager.SearchHistory.AddSearchHistoryAsync(history);
        }

        public async Task<List<SearchHistory>> GetRecentSearchesAsync(string userId)
        {
            return await _manager.SearchHistory.GetUserSearchHistoriesAsync(userId);
        }
    }
}
