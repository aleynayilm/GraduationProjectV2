using MongoDB.Driver;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.EFCore;
using System.Linq.Expressions;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class MongoRepositoryBaseTests
    {
        private readonly Mock<IMongoCollection<User>> _collectionMock;
        private readonly MongoRepositoryBase<User> _sut;

        private static IAsyncCursor<User> BuildCursor(IEnumerable<User> data)
        {
            var cursorMock = new Mock<IAsyncCursor<User>>();
            var list = data.ToList();
            cursorMock.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(list.Count > 0)
                      .ReturnsAsync(false);
            cursorMock.Setup(c => c.Current).Returns(list);
            cursorMock.SetupSequence(c => c.MoveNext(It.IsAny<CancellationToken>()))
                      .Returns(list.Count > 0)
                      .Returns(false);
            return cursorMock.Object;
        }

        public MongoRepositoryBaseTests()
        {
            _collectionMock = new Mock<IMongoCollection<User>>();
            _sut = new MongoRepositoryBase<User>(_collectionMock.Object);
        }

        [Fact]
        public void Constructor_StoresCollection()
        {
            var repo = new MongoRepositoryBase<User>(_collectionMock.Object);
            Assert.NotNull(repo);
        }

        [Fact]
        public void FindAll_ReturnsQueryable()
        {
            var users = new List<User> { new User { Id = "u1" }, new User { Id = "u2" } };
            _collectionMock.Setup(c => c.FindSync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .Returns(BuildCursor(users));

            var result = _sut.FindAll();

            Assert.NotNull(result);
        }

        [Fact]
        public void FindByCondition_ReturnsQueryable()
        {
            var users = new List<User> { new User { Id = "u1", FirebaseUid = "fb-1" } };
            _collectionMock.Setup(c => c.FindSync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .Returns(BuildCursor(users));

            var result = _sut.FindByCondition(u => u.FirebaseUid == "fb-1");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateAsync_CallsInsertOneAsync()
        {
            var user = new User { Id = "new-user" };

            _collectionMock.Setup(c => c.InsertOneAsync(
                It.IsAny<User>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await _sut.CreateAsync(user);

            _collectionMock.Verify(c => c.InsertOneAsync(
                It.Is<User>(u => u.Id == "new-user"),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_CallsReplaceOneAsync()
        {
            var user = new User { Id = "upd-user" };

            _collectionMock.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            await _sut.UpdateAsync(u => u.Id == "upd-user", user);

            _collectionMock.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.Is<User>(u => u.Id == "upd-user"),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_CallsDeleteOneAsync()
        {
            _collectionMock.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(1));

            await _sut.DeleteAsync(u => u.Id == "del-user");

            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
