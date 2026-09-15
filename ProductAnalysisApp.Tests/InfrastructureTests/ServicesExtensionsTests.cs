using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Repositories.EFCore;
using ProductAnalysisApp.Services.Contracts;
using ProductAnalysisAppWithMongoDb.Extensions;
using Xunit;

namespace ProductAnalysisApp.Tests.InfrastructureTests
{
    public class ServicesExtensionsTests
    {
        private static IConfiguration BuildConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MongoConnection"] = "mongodb://fake:27017",
                    ["ConnectionStrings:MongoDatabase"]   = "testdb"
                })
                .Build();

        [Fact]
        public void ConfigureMongoContext_RegistersMongoDbContext_AsSingleton()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(BuildConfig());
            services.ConfigureMongoContext(BuildConfig());

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(MongoDbContext));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
        }

        [Fact]
        public void ConfigureRepositoryManager_RegistersIRepositoryManager_AsScoped()
        {
            var services = new ServiceCollection();
            services.ConfigureRepositoryManager();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IRepositoryManager));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
            Assert.Equal(typeof(RepositoryManager), descriptor.ImplementationType);
        }

        [Fact]
        public void ConfigureServiceManager_RegistersIServiceManager_AsScoped()
        {
            var services = new ServiceCollection();
            services.ConfigureServiceManager();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IServiceManager));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
        }
    }
}
