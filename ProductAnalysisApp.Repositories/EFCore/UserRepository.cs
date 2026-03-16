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
    public class UserRepository : MongoRepositoryBase<User>, IUserRepository
    {
        public UserRepository(IMongoCollection<User> collection) : base(collection) { }

        public async Task CreateOneUserAsync(User user) => await CreateAsync(user);

        public async Task DeleteOneUserAsync(string id)
        {
            await DeleteAsync(u => u.Id == id);
        }

        public async Task<List<User>> GetAllUsersAsync() => await _collection.Find(_ => true).ToListAsync();

        public async Task<User?> GetOneUserAsync(string id)
        {
            return await _collection.Find(u => u.Id == id).FirstOrDefaultAsync();
        }

        public async Task<User?> GetOneUserByFirebaseUidAsync(string firebaseUid)
        {
            return await _collection.Find(u => u.FirebaseUid == firebaseUid).FirstOrDefaultAsync();
        }

        public async Task UpdateOneUserAsync(User user)
        {
            var existing = await GetOneUserAsync(user.Id);
            if (existing == null) return;

            existing.Email = user.Email;
            existing.FirstName = user.FirstName;
            existing.LastName = user.LastName;
            existing.PushToken = user.PushToken;             
            existing.PriceAlertEnabled = user.PriceAlertEnabled;  
            existing.PriceCheckIntervalHours = user.PriceCheckIntervalHours;

            await UpdateAsync(u => u.Id == user.Id, existing);
        }
    }
}
