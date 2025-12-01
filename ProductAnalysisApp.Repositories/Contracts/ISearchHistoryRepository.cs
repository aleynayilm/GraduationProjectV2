using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
    public interface ISearchHistoryRepository
    {
        Task<List<SearchHistory>> GetAllSearchHistoriesAsync();
        Task AddSearchHistoryAsync(SearchHistory history);
        Task<List<SearchHistory>> GetUserSearchHistoriesAsync(string userId);
    }
}
