using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.EFCore
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var mongoUri = configuration.GetConnectionString("MongoConnection");
            var dbName = configuration.GetValue<string>("ConnectionStrings:MongoDatabase");
            var client = new MongoClient(mongoUri);
            _database = client.GetDatabase(dbName);
        }

        public IMongoCollection<Category> Categories => _database.GetCollection<Category>("Categories");
        public IMongoCollection<Favorite> Favorites => _database.GetCollection<Favorite>("Favorites");
        public IMongoCollection<Platform> Platforms => _database.GetCollection<Platform>("Platforms");
        public IMongoCollection<Product> Products => _database.GetCollection<Product>("Products");
        public IMongoCollection<ProductPlatform> ProductPlatforms => _database.GetCollection<ProductPlatform>("ProductPlatforms");
        public IMongoCollection<SearchHistory> SearchHistories => _database.GetCollection<SearchHistory>("SearchHistories");
        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
    }
}
