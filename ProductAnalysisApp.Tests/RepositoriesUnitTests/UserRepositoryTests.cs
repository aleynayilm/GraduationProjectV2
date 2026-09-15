using MongoDB.Driver;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.EFCore;
using Xunit;

namespace ProductAnalysisApp.Tests.RepositoriesUnitTests
{
    public class UserRepositoryTests
    {
        private readonly Mock<IMongoCollection<User>> _collectionMock;
        private readonly UserRepository _sut;

        private static IAsyncCursor<User> BuildAsyncCursor(IEnumerable<User> data)
        {
            var list = data.ToList();
            var cursor = new Mock<IAsyncCursor<User>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(list.Count > 0).ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(list);
            return cursor.Object;
        }

        public UserRepositoryTests()
        {
            _collectionMock = new Mock<IMongoCollection<User>>();
            _sut = new UserRepository(_collectionMock.Object);
        }

        [Fact]
        public void Constructor_NotNull()
        {
            Assert.NotNull(_sut);
        }

        [Fact]
        public async Task CreateOneUserAsync_CallsInsertOneAsync()
        {
            var user = new User { Id = "u-001" };
            _collectionMock.Setup(c => c.InsertOneAsync(
                It.IsAny<User>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            await _sut.CreateOneUserAsync(user);

            _collectionMock.Verify(c => c.InsertOneAsync(
                It.Is<User>(u => u.Id == "u-001"),
                It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteOneUserAsync_CallsDeleteOneAsync()
        {
            _collectionMock.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(1));

            await _sut.DeleteOneUserAsync("u-001");

            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAllUsersAsync_ReturnsList()
        {
            var users = new List<User>
            {
                new User { Id = "u-1" },
                new User { Id = "u-2" }
            };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(users));

            var result = await _sut.GetAllUsersAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetOneUserAsync_ReturnsUser_WhenFound()
        {
            var user = new User { Id = "u-find" };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(new[] { user }));

            var result = await _sut.GetOneUserAsync("u-find");

            Assert.NotNull(result);
            Assert.Equal("u-find", result!.Id);
        }

        [Fact]
        public async Task GetOneUserAsync_ReturnsNull_WhenNotFound()
        {
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(Enumerable.Empty<User>()));

            var result = await _sut.GetOneUserAsync("ghost");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetOneUserByFirebaseUidAsync_ReturnsUser_WhenFound()
        {
            var user = new User { Id = "u-fb", FirebaseUid = "fb-123" };
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(new[] { user }));

            var result = await _sut.GetOneUserByFirebaseUidAsync("fb-123");

            Assert.NotNull(result);
            Assert.Equal("fb-123", result!.FirebaseUid);
        }

        [Fact]
        public async Task GetOneUserByFirebaseUidAsync_ReturnsNull_WhenNotFound()
        {
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(Enumerable.Empty<User>()));

            var result = await _sut.GetOneUserByFirebaseUidAsync("no-uid");

            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateOneUserAsync_WhenUserExists_CallsReplaceOne()
        {
            var existing = new User { Id = "u-upd", Email = "old@test.com", FirstName = "Old" };
            var updated = new User { Id = "u-upd", Email = "new@test.com", FirstName = "New" };

            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(new[] { existing }));

            _collectionMock.Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));

            await _sut.UpdateOneUserAsync(updated);

            _collectionMock.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateOneUserAsync_WhenUserNotFound_DoesNotCallReplaceOne()
        {
            _collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<FindOptions<User, User>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAsyncCursor(Enumerable.Empty<User>()));

            await _sut.UpdateOneUserAsync(new User { Id = "ghost" });

            _collectionMock.Verify(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<User>>(),
                It.IsAny<User>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
