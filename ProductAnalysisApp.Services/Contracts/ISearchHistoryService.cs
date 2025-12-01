using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface ISearchHistoryService
    {
        Task AddSearchAsync(string url, string userId);
        Task<List<SearchHistory>> GetRecentSearchesAsync(string userId);
    }
}
