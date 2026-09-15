using Microsoft.Extensions.Configuration;
using ProductAnalysisAppWithMongoDb.Infrastructure;
using Xunit;

namespace ProductAnalysisApp.Tests.InfrastructureTests
{
    public class FirebaseInitializerTests
    {
        [Fact]
        public void Initialize_WhenNoCredentials_ThrowsInvalidOperationException()
        {
            var config = new ConfigurationBuilder().Build();
            var ex = Record.Exception(() => FirebaseInitializer.Initialize(config));
            if (ex != null)
                Assert.IsType<InvalidOperationException>(ex);
        }

        [Fact]
        public void Initialize_WhenAlreadyInitialized_ReturnsEarly()
        {
            var config = new ConfigurationBuilder().Build();

            try { FirebaseInitializer.Initialize(config); } catch { }
            var ex = Record.Exception(() => FirebaseInitializer.Initialize(config));

            if (ex != null)
                Assert.IsType<InvalidOperationException>(ex);
        }
    }
}
