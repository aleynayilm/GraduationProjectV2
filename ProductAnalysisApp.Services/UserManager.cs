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
    public class UserManager : IUserService
    {
        private readonly IRepositoryManager _manager;

        public UserManager(IRepositoryManager manager)
        {
            _manager = manager;
        }

        public async Task<User> CreateOneUserAsync(User user)
        {
            if (user is null) throw new ArgumentNullException(nameof(user));

            await _manager.User.CreateOneUserAsync(user);
            return user;
        }

        public async Task DeleteOneUserAsync(string id)
        {
            var entity = await _manager.User.GetOneUserAsync(id);
            if (entity is null) throw new Exception($"User with id: {id} not found.");

            await _manager.User.DeleteOneUserAsync(id);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _manager.User.GetAllUsersAsync();
        }

        public async Task<User?> GetOneUserAsync(string id)
        {
            return await _manager.User.GetOneUserAsync(id);
        }

        public async Task<User?> GetOneUserByFirebaseUidAsync(string firebaseUid)
        {
            return await _manager.User.GetOneUserByFirebaseUidAsync(firebaseUid);
        }

        public async Task UpdateOneUserAsync(string id, User user)
        {
            var entity = await _manager.User.GetOneUserAsync(id);
            if (entity is null) throw new Exception($"User with id: {id} not found.");
            if (user is null) throw new ArgumentNullException(nameof(user));

            entity.Email = user.Email;
            entity.FirstName = user.FirstName;
            entity.LastName = user.LastName;
            await _manager.User.UpdateOneUserAsync(entity);
        }
    }
}
