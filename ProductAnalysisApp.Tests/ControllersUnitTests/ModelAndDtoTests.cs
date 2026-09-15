using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Services.Messaging;
using System.Text.Json;
using Xunit;

namespace ProductAnalysisApp.Tests.EntitiesAndDtos
{
    // ProductForComparisonDto
    public class ProductForComparisonDtoTests
    {
        [Fact]
        public void ProductForComparisonDto_PropertiesSetCorrectly()
        {
            var dto = new ProductForComparisonDto
            {
                Name        = "Gaming Laptop",
                Price       = "24.999,00 TL",
                Description = "16GB RAM, RTX 4060",
                Platform    = "Trendyol"
            };

            Assert.Equal("Gaming Laptop",      dto.Name);
            Assert.Equal("24.999,00 TL",       dto.Price);
            Assert.Equal("16GB RAM, RTX 4060", dto.Description);
            Assert.Equal("Trendyol",           dto.Platform);
        }

        [Fact]
        public void ProductForComparisonDto_DeserializesFromJson_WithJsonPropertyNames()
        {
            var json = """{"name":"Mouse","price":"299 TL","description":"Wireless","platform":"Amazon"}""";

            var dto = JsonSerializer.Deserialize<ProductForComparisonDto>(json);

            Assert.NotNull(dto);
            Assert.Equal("Mouse",    dto!.Name);
            Assert.Equal("299 TL",   dto.Price);
            Assert.Equal("Wireless", dto.Description);
            Assert.Equal("Amazon",   dto.Platform);
        }

        [Fact]
        public void ProductForComparisonDto_AllowsNullValues()
        {
            var dto = new ProductForComparisonDto
            {
                Name = null!, Price = null!, Description = null!, Platform = null!
            };

            Assert.Null(dto.Name);
            Assert.Null(dto.Price);
        }
    }

    // ProductPlatformResponseDto
    public class ProductPlatformResponseDtoTests
    {
        [Fact]
        public void ProductPlatformResponseDto_PropertiesSetCorrectly()
        {
            var dto = new ProductPlatformResponseDto
            {
                PlatformId   = "platform-001",
                PlatformName = "Hepsiburada",
                ProductUrl   = "https://hepsiburada.com/p1",
                Price        = 1299.99m,
                Currency     = "TRY"
            };

            Assert.Equal("platform-001",              dto.PlatformId);
            Assert.Equal("Hepsiburada",               dto.PlatformName);
            Assert.Equal("https://hepsiburada.com/p1", dto.ProductUrl);
            Assert.Equal(1299.99m,                    dto.Price);
            Assert.Equal("TRY",                       dto.Currency);
        }

        [Fact]
        public void ProductPlatformResponseDto_DefaultPrice_IsZero()
        {
            var dto = new ProductPlatformResponseDto();
            Assert.Equal(0m, dto.Price);
        }
    }

    // TokenDto
    public class TokenDtoTests
    {
        [Fact]
        public void TokenDto_PropertiesSetCorrectly()
        {
            var dto = new TokenDto
            {
                AccessToken  = "access-abc-123",
                RefreshToken = "refresh-xyz-456"
            };

            Assert.Equal("access-abc-123",  dto.AccessToken);
            Assert.Equal("refresh-xyz-456", dto.RefreshToken);
        }

        [Fact]
        public void TokenDto_IsRecord_AllowsValueEquality()
        {
            var a = new TokenDto { AccessToken = "t1", RefreshToken = "r1" };
            var b = new TokenDto { AccessToken = "t1", RefreshToken = "r1" };

            Assert.Equal(a, b);
        }

        [Fact]
        public void TokenDto_WithDifferentValues_AreNotEqual()
        {
            var a = new TokenDto { AccessToken = "t1", RefreshToken = "r1" };
            var b = new TokenDto { AccessToken = "t2", RefreshToken = "r2" };

            Assert.NotEqual(a, b);
        }
    }

    // ApiResponse<T>
    public class ApiResponseTests
    {
        [Fact]
        public void ApiResponse_StringType_PropertiesSetCorrectly()
        {
            var response = new ApiResponse<string>
            {
                SessionId  = "ses-001",
                DurationMs = 250L,
                Data       = "Sonuç verisi"
            };

            Assert.Equal("ses-001",       response.SessionId);
            Assert.Equal(250L,            response.DurationMs);
            Assert.Equal("Sonuç verisi",  response.Data);
        }

        [Fact]
        public void ApiResponse_ObjectType_PropertiesSetCorrectly()
        {
            var data = new { ProductName = "Laptop", Price = 15000m };
            var response = new ApiResponse<object>
            {
                SessionId  = "ses-002",
                DurationMs = 100L,
                Data       = data
            };

            Assert.NotNull(response.Data);
            Assert.Equal(100L, response.DurationMs);
        }

        [Fact]
        public void ApiResponse_DefaultValues_AreNull_OrZero()
        {
            var response = new ApiResponse<string>();

            Assert.Null(response.SessionId);
            Assert.Equal(0L, response.DurationMs);
            Assert.Null(response.Data);
        }

        [Fact]
        public void ApiResponse_ListType_WorksCorrectly()
        {
            var response = new ApiResponse<List<int>>
            {
                Data = [1, 2, 3]
            };

            Assert.Equal(3, response.Data!.Count);
        }
    }

    // Category
    public class CategoryTests
    {
        [Fact]
        public void Category_PropertiesSetCorrectly()
        {
            var category = new Category
            {
                CategoryId       = "cat-001",
                ParentCategory   = "Elektronik",
                CategoryName     = "Bilgisayar",
                RefreshInterval  = 24,
                ProductIds       = ["prod-1", "prod-2"]
            };

            Assert.Equal("cat-001",    category.CategoryId);
            Assert.Equal("Elektronik", category.ParentCategory);
            Assert.Equal("Bilgisayar", category.CategoryName);
            Assert.Equal(24,           category.RefreshInterval);
            Assert.Equal(2,            category.ProductIds.Count);
        }

        [Fact]
        public void Category_ProductIds_DefaultsToEmptyList()
        {
            var category = new Category();

            Assert.NotNull(category.ProductIds);
            Assert.Empty(category.ProductIds);
        }

        [Fact]
        public void Category_ProductIds_CanBeModified()
        {
            var category = new Category();
            category.ProductIds.Add("prod-new");

            Assert.Single(category.ProductIds);
            Assert.Equal("prod-new", category.ProductIds[0]);
        }
    }

    // Platform
    public class PlatformTests
    {
        [Fact]
        public void Platform_PropertiesSetCorrectly()
        {
            var platform = new Platform
            {
                PlatformId         = "plat-001",
                Name               = "Trendyol",
                BaseUrl            = "https://trendyol.com",
                ProductPlatformIds = ["pp-1", "pp-2", "pp-3"]
            };

            Assert.Equal("plat-001",            platform.PlatformId);
            Assert.Equal("Trendyol",            platform.Name);
            Assert.Equal("https://trendyol.com", platform.BaseUrl);
            Assert.Equal(3,                     platform.ProductPlatformIds.Count);
        }

        [Fact]
        public void Platform_ProductPlatformIds_DefaultsToEmptyList()
        {
            var platform = new Platform();

            Assert.NotNull(platform.ProductPlatformIds);
            Assert.Empty(platform.ProductPlatformIds);
        }

        [Fact]
        public void Platform_ProductPlatformIds_CanBeModified()
        {
            var platform = new Platform();
            platform.ProductPlatformIds.Add("pp-new");

            Assert.Single(platform.ProductPlatformIds);
        }
    }

    // JobResult & JobResultStore
    public class JobResultTests
    {
        [Fact]
        public void JobResult_DefaultStatus_IsPending()
        {
            var job = new JobResult();
            Assert.Equal("pending", job.Status);
        }

        [Fact]
        public void JobResult_DefaultJobType_IsEmpty()
        {
            var job = new JobResult();
            Assert.Equal("", job.JobType);
        }

        [Fact]
        public void JobResult_CreatedAt_DefaultsToUtcNow()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var job    = new JobResult();
            var after  = DateTime.UtcNow.AddSeconds(1);

            Assert.InRange(job.CreatedAt, before, after);
        }

        [Fact]
        public void JobResult_PropertiesSetCorrectly()
        {
            var job = new JobResult
            {
                JobId      = "job-test",
                Status     = "completed",
                UserId     = "uid-001",
                JobType    = "scrape",
                FinishedAt = DateTime.UtcNow
            };

            Assert.Equal("job-test",  job.JobId);
            Assert.Equal("completed", job.Status);
            Assert.Equal("uid-001",   job.UserId);
            Assert.Equal("scrape",    job.JobType);
            Assert.NotNull(job.FinishedAt);
        }
    }

    public class JobResultStoreTests
    {

        [Fact]
        public void Set_And_Get_ReturnsStoredJob()
        {
            var key = $"store-test-{Guid.NewGuid()}";
            var job = new JobResult { JobId = key, Status = "pending" };

            JobResultStore.Set(key, job);
            var result = JobResultStore.Get(key);

            Assert.NotNull(result);
            Assert.Equal(key, result!.JobId);
        }

        [Fact]
        public void Get_ReturnsNull_WhenKeyNotExists()
        {
            var result = JobResultStore.Get($"nonexistent-{Guid.NewGuid()}");
            Assert.Null(result);
        }

        [Fact]
        public void Set_Overwrites_ExistingKey()
        {
            var key = $"overwrite-{Guid.NewGuid()}";
            JobResultStore.Set(key, new JobResult { JobId = key, Status = "pending" });
            JobResultStore.Set(key, new JobResult { JobId = key, Status = "completed" });

            var result = JobResultStore.Get(key);
            Assert.Equal("completed", result!.Status);
        }

        [Fact]
        public void GetByUser_ReturnsOnlyUserJobs()
        {
            var uid  = $"uid-{Guid.NewGuid()}";
            var key1 = $"j-{Guid.NewGuid()}";
            var key2 = $"j-{Guid.NewGuid()}";
            var key3 = $"j-{Guid.NewGuid()}";

            JobResultStore.Set(key1, new JobResult { JobId = key1, UserId = uid,      Status = "completed" });
            JobResultStore.Set(key2, new JobResult { JobId = key2, UserId = uid,      Status = "pending"   });
            JobResultStore.Set(key3, new JobResult { JobId = key3, UserId = "other",  Status = "completed" });

            var results = JobResultStore.GetByUser(uid);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(uid, r.UserId));
        }

        [Fact]
        public void GetByUser_ReturnsDescendingOrder_ByCreatedAt()
        {
            var uid  = $"uid-order-{Guid.NewGuid()}";
            var key1 = $"j-{Guid.NewGuid()}";
            var key2 = $"j-{Guid.NewGuid()}";

            JobResultStore.Set(key1, new JobResult { JobId = key1, UserId = uid, CreatedAt = DateTime.UtcNow.AddMinutes(-5) });
            JobResultStore.Set(key2, new JobResult { JobId = key2, UserId = uid, CreatedAt = DateTime.UtcNow });

            var results = JobResultStore.GetByUser(uid);

            Assert.True(results[0].CreatedAt >= results[1].CreatedAt);
        }

        [Fact]
        public void GetForUser_ReturnsJob_WhenUserMatches()
        {
            var uid = $"uid-for-{Guid.NewGuid()}";
            var key = $"j-{Guid.NewGuid()}";
            JobResultStore.Set(key, new JobResult { JobId = key, UserId = uid });

            var result = JobResultStore.GetForUser(key, uid);

            Assert.NotNull(result);
        }

        [Fact]
        public void GetForUser_ReturnsNull_WhenUserMismatch()
        {
            var key = $"j-{Guid.NewGuid()}";
            JobResultStore.Set(key, new JobResult { JobId = key, UserId = "owner-uid" });

            var result = JobResultStore.GetForUser(key, "other-uid");

            Assert.Null(result);
        }

        [Fact]
        public void GetForUser_ReturnsJob_WhenUserIdIsNull()
        {
            var key = $"j-{Guid.NewGuid()}";
            JobResultStore.Set(key, new JobResult { JobId = key, UserId = null });

            var result = JobResultStore.GetForUser(key, "any-uid");

            Assert.NotNull(result);
        }

        [Fact]
        public void GetForUser_ReturnsNull_WhenJobNotFound()
        {
            var result = JobResultStore.GetForUser($"missing-{Guid.NewGuid()}", "uid");
            Assert.Null(result);
        }

        [Fact]
        public void Set_IsThreadSafe_WhenCalledConcurrently()
        {
            var uid  = $"thread-uid-{Guid.NewGuid()}";
            var keys = Enumerable.Range(0, 20).Select(_ => $"j-{Guid.NewGuid()}").ToList();

            Parallel.ForEach(keys, key =>
                JobResultStore.Set(key, new JobResult { JobId = key, UserId = uid }));

            var results = JobResultStore.GetByUser(uid);
            Assert.Equal(20, results.Count);
        }
    }

    // User model — ProfileImageUrl getter coverage
    public class UserModelTests
    {
        [Fact]
        public void User_ProfileImageUrl_DefaultsToNull()
        {
            var user = new User();
            Assert.Null(user.ProfileImageUrl);
        }

        [Fact]
        public void User_ProfileImageUrl_CanBeSetAndGet()
        {
            var user = new User { ProfileImageUrl = "https://example.com/avatar.jpg" };
            Assert.Equal("https://example.com/avatar.jpg", user.ProfileImageUrl);
        }
    }
}
