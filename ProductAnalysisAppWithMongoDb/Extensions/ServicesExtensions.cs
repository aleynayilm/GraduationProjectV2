using Microsoft.AspNetCore.Builder.Extensions;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Repositories.EFCore;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisApp.Services.ProductAnalysisApp.Services;

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
        //public static void ConfigureFirebaseAuthentication(this IServiceCollection services, IConfiguration configuration)
        //{
        //    var firebaseProjectId = configuration["Firebase:ProjectId"];
        //    var authority = $"https://securetoken.google.com/{firebaseProjectId}";

        //    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        //        .AddJwtBearer(options =>
        //        {
        //            options.Authority = authority;
        //            options.TokenValidationParameters = new TokenValidationParameters
        //            {
        //                ValidateIssuer = true,
        //                ValidIssuer = authority,
        //                ValidateAudience = true,
        //                ValidAudience = firebaseProjectId,
        //                ValidateLifetime = true
        //            };
        //            options.RequireHttpsMetadata = false;
        //        });

        //    services.AddAuthorization();
        //}
        //public static void ConfigureFirebaseApp(this IServiceCollection services, IConfiguration configuration)
        //{
        //    FirebaseApp.Create(new AppOptions
        //    {
        //        Credential = GoogleCredential.FromFile(configuration["Firebase:ServiceAccountJsonPath"])
        //    });
        //}
    }
}
