using MongoDB.Driver;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Repositories.EFCore;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class SearchHistoryRepositoryTests
    {
        private readonly Mock<IMongoCollection<SearchHistory>> _collectionMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly SearchHistoryRepository _sut;

        private static IAsyncCursor<SearchHistory> BuildAsyncCursor(IEnumerable<SearchHistory> data)
        {
            var list = data.ToList();
            var cursor = new Mock<IAsyncCursor<SearchHistory>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(list.Count > 0).ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(list);
            return cursor.Object;
        }

        public SearchHistoryRepositoryTests()
        {
            _collectionMock = new Mock<IMongoCollection<SearchHistory>>();
            _userRepoMock = new Mock<IUserRepository>();
            _sut = new SearchHistoryRepository(_collectionMock.Object, _userRepoMock.Object);
        }

        [Fact]
        public void Constructor_AcceptsCollectionAndUserRepository()
        {
            Assert.NotNull(_sut);
        }

        [Fact]
        public async Task AddSearchHistoryAsync_CallsInsertOneAsync()
        {
            var history = new SearchHistory { Id = "sh-1", SearchUrl = "https://test.com" };
            _collectionMock.Setup(c => c.InsertOneAsync(
                It.IsAny<SearchHistory>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await _sut.AddSearchHistoryAsync(history);

            _collectionMock.Verify(c => c.InsertOneAsync(
                It.Is<SearchHistory>(s => s.Id == "sh-1"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAllSearchHistoriesAsync_ReturnsList()
        {
            var histories = new List<SearchHistory>
            {
                new SearchHistory { Id = "sh-a", SearchUrl = "https://a.com" },
                new SearchHistory { Id = "sh-b", SearchUrl = "https://b.com" }
            };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<SearchHistory>>(),
                It.IsAny<FindOptions<SearchHistory, SearchHistory>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(histories));

            var result = await _sut.GetAllSearchHistoriesAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetUserSearchHistoriesAsync_ReturnsFilteredList()
        {
            var histories = new List<SearchHistory>
            {
                new SearchHistory { Id = "sh-u1", FirebaseUid = "fb-user", SearchUrl = "https://x.com" }
            };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<SearchHistory>>(),
                It.IsAny<FindOptions<SearchHistory, SearchHistory>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(histories));

            var result = await _sut.GetUserSearchHistoriesAsync("fb-user");

            Assert.Single(result);
            Assert.Equal("fb-user", result[0].FirebaseUid);
        }
    }
}
