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
    public class PlatformRepository : MongoRepositoryBase<Platform>, IPlatformRepository
    {
        public PlatformRepository(IMongoCollection<Platform> collection)
            : base(collection)
        {
        }

        public async Task AddPlatformAsync(Platform platform)
            => await CreateAsync(platform);

        public async Task<Platform?> GetPlatformByNameAsync(string name)
            => await _collection
                .Find(p => p.Name.ToLower() == name.ToLower())
                .FirstOrDefaultAsync();

        public async Task<List<Platform>> GetAllPlatformsAsync()
            => await _collection.Find(_ => true).ToListAsync();
    }
}
