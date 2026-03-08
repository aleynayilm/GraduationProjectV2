using Microsoft.AspNetCore.Builder.Extensions;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Repositories.EFCore;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.ProductAnalysisApp.Services;
using System.Security.Claims;

namespace ProductAnalysisAppWithMongoDb.Extensions
{
    public static class ServicesExtensions
    {
        public static void ConfigureMongoContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<MongoDbContext>();
        }
        public static void ConfigureRepositoryManager(this IServiceCollection services) =>
            services.AddScoped<IRepositoryManager, RepositoryManager>();
        public static void ConfigureServiceManager(this IServiceCollection services) =>
            services.AddScoped<IServiceManager, ServiceManager>();
    }
}
