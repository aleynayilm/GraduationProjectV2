using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
    public interface IPlatformRepository : IRepositoryBase<Platform>
    {
        Task<Platform?> GetPlatformByNameAsync(string name);
        Task AddPlatformAsync(Platform platform);
        Task<List<Platform>> GetAllPlatformsAsync();
    }
}
