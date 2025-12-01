using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
    public interface IUserRepository
    {
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetOneUserAsync(string id);
        Task CreateOneUserAsync(User user);
        Task UpdateOneUserAsync(User user);
        Task DeleteOneUserAsync(string id);
        Task<User?> GetOneUserByFirebaseUidAsync(string firebaseUid);
    }
}
