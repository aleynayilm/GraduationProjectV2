using ProductAnalysisApp.Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Contracts
{
    public interface IUserService
    {
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetOneUserAsync(string id);
        Task<User> CreateOneUserAsync(User user);
        Task UpdateOneUserAsync(string id, User user);
        Task DeleteOneUserAsync(string id);
        Task<User?> GetOneUserByFirebaseUidAsync(string firebaseUid);
    }
}
