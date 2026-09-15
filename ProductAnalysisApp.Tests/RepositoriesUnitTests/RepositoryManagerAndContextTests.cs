using Microsoft.Extensions.Configuration;
using ProductAnalysisApp.Repositories.EFCore;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class MongoDbContextTests
    {
        private static IConfiguration BuildFakeConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MongoConnection"] = "mongodb://fake:27017",
                    ["ConnectionStrings:MongoDatabase"]   = "testdb"
                })
                .Build();

        [Fact]
        public void Constructor_WithFakeConfig_DoesNotThrow()
        {
            var ex = Record.Exception(() => new MongoDbContext(BuildFakeConfig()));
            Assert.Null(ex);
        }

        [Fact]
        public void AllCollections_ReturnNonNull()
        {
            var ctx = new MongoDbContext(BuildFakeConfig());

            Assert.NotNull(ctx.Categories);
            Assert.NotNull(ctx.Favorites);
            Assert.NotNull(ctx.Platforms);
            Assert.NotNull(ctx.Products);
            Assert.NotNull(ctx.ProductPlatforms);
            Assert.NotNull(ctx.SearchHistories);
            Assert.NotNull(ctx.Users);
        }
    }

    public class RepositoryManagerTests
    {
        private static MongoDbContext BuildContext() =>
            new MongoDbContext(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MongoConnection"] = "mongodb://fake:27017",
                        ["ConnectionStrings:MongoDatabase"]   = "testdb"
                    })
                    .Build());

        [Fact]
        public void Constructor_Succeeds()
        {
            var ctx = BuildContext();
            var manager = new RepositoryManager(ctx);
            Assert.NotNull(manager);
        }

        [Fact]
        public void AllProperties_ReturnNonNull()
        {
            var manager = new RepositoryManager(BuildContext());

            Assert.NotNull(manager.User);
            Assert.NotNull(manager.Product);
            Assert.NotNull(manager.Platform);
            Assert.NotNull(manager.ProductPlatform);
            Assert.NotNull(manager.SearchHistory);
            Assert.NotNull(manager.Favorite);
        }

        [Fact]
        public void User_IsLazy_ReturnsSameInstance()
        {
            var manager = new RepositoryManager(BuildContext());

            var first  = manager.User;
            var second = manager.User;

            Assert.Same(first, second);
        }

        [Fact]
        public void Product_IsLazy_ReturnsSameInstance()
        {
            var manager = new RepositoryManager(BuildContext());
            Assert.Same(manager.Product, manager.Product);
        }

        [Fact]
        public void Platform_IsLazy_ReturnsSameInstance()
        {
            var manager = new RepositoryManager(BuildContext());
            Assert.Same(manager.Platform, manager.Platform);
        }

        [Fact]
        public void Favorite_IsLazy_ReturnsSameInstance()
        {
            var manager = new RepositoryManager(BuildContext());
            Assert.Same(manager.Favorite, manager.Favorite);
        }
    }
}
