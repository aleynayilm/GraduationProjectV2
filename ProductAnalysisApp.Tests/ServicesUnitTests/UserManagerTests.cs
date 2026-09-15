using FluentAssertions;
using Moq;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProductAnalysisApp.Tests.ServicesUnitTests
{
    public class UserManagerTests
    {
        private readonly Mock<IRepositoryManager> _repoMock;
        private readonly Mock<IUserRepository>    _userRepoMock;
        private readonly UserManager              _sut;

        private readonly User _testUser;

        public UserManagerTests()
        {
            _repoMock     = new Mock<IRepositoryManager>();
            _userRepoMock = new Mock<IUserRepository>();

            _repoMock.Setup(r => r.User).Returns(_userRepoMock.Object);

            _sut = new UserManager(_repoMock.Object);

            _testUser = new User
            {
                Id                    = "user-001",
                FirebaseUid           = "firebase-uid-001",
                Email                 = "test@example.com",
                FirstName             = "Test",
                LastName              = "User",
                PriceAlertEnabled     = false,
                PriceCheckIntervalHours = 24,
                PushToken             = null
            };
        }

        // CreateOneUserAsync 

        [Fact]
        public async Task CreateOneUserAsync_WithValidUser_ShouldCreate()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.CreateOneUserAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.CreateOneUserAsync(_testUser);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be("user-001");
            _userRepoMock.Verify(r => r.CreateOneUserAsync(_testUser), Times.Once);
        }

        [Fact]
        public async Task CreateOneUserAsync_WithNullUser_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await _sut.Invoking(s => s.CreateOneUserAsync(null!))
                .Should().ThrowAsync<ArgumentNullException>();
        }

        // UpdateOneUserAsync 

        [Fact]
        public async Task UpdateOneUserAsync_ShouldUpdateAllFields()
        {
            // Arrange
            User? capturedUser = null;

            _userRepoMock
                .Setup(r => r.GetOneUserAsync("user-001"))
                .ReturnsAsync(_testUser);

            _userRepoMock
                .Setup(r => r.UpdateOneUserAsync(It.IsAny<User>()))
                .Callback<User>(u => capturedUser = u)
                .Returns(Task.CompletedTask);

            var updated = new User
            {
                Id                      = "user-001",
                Email                   = "new@example.com",
                FirstName               = "Yeni",
                LastName                = "İsim",
                PushToken               = "fcm-token-123",
                PriceAlertEnabled       = true,
                PriceCheckIntervalHours = 6
            };

            // Act
            await _sut.UpdateOneUserAsync("user-001", updated);

            // Assert
            capturedUser.Should().NotBeNull();
            capturedUser!.Email.Should().Be("new@example.com");
            capturedUser.FirstName.Should().Be("Yeni");
            capturedUser.LastName.Should().Be("İsim");
            capturedUser.PushToken.Should().Be("fcm-token-123");
            capturedUser.PriceAlertEnabled.Should().BeTrue();
            capturedUser.PriceCheckIntervalHours.Should().Be(6);
        }

        [Fact]
        public async Task UpdateOneUserAsync_WhenUserNotFound_ShouldThrowException()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserAsync("nonexistent"))
                .ReturnsAsync((User?)null);

            // Act & Assert
            await _sut.Invoking(s => s.UpdateOneUserAsync("nonexistent", _testUser))
                .Should().ThrowAsync<Exception>()
                .WithMessage("*nonexistent*");
        }

        // DeleteOneUserAsync 

        [Fact]
        public async Task DeleteOneUserAsync_WhenExists_ShouldDelete()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserAsync("user-001"))
                .ReturnsAsync(_testUser);

            _userRepoMock
                .Setup(r => r.DeleteOneUserAsync("user-001"))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.DeleteOneUserAsync("user-001");

            // Assert
            _userRepoMock.Verify(r => r.DeleteOneUserAsync("user-001"), Times.Once);
        }

        [Fact]
        public async Task DeleteOneUserAsync_WhenNotFound_ShouldThrowException()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserAsync("nonexistent"))
                .ReturnsAsync((User?)null);

            // Act & Assert
            await _sut.Invoking(s => s.DeleteOneUserAsync("nonexistent"))
                .Should().ThrowAsync<Exception>()
                .WithMessage("*nonexistent*");
        }

        // GetAllUsersAsync 

        [Fact]
        public async Task GetAllUsersAsync_ShouldReturnAllUsers()
        {
            // Arrange
            var users = new List<User> { _testUser, new User { Id = "user-002" } };

            _userRepoMock
                .Setup(r => r.GetAllUsersAsync())
                .ReturnsAsync(users);

            // Act
            var result = await _sut.GetAllUsersAsync();

            // Assert
            result.Should().HaveCount(2);
        }

        // GetOneUserByFirebaseUidAsync 

        [Fact]
        public async Task GetOneUserByFirebaseUidAsync_WhenExists_ShouldReturnUser()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("firebase-uid-001"))
                .ReturnsAsync(_testUser);

            // Act
            var result = await _sut.GetOneUserByFirebaseUidAsync("firebase-uid-001");

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("user-001");
        }

        [Fact]
        public async Task GetOneUserByFirebaseUidAsync_WhenNotExists_ShouldReturnNull()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetOneUserByFirebaseUidAsync("nonexistent"))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _sut.GetOneUserByFirebaseUidAsync("nonexistent");

            // Assert
            result.Should().BeNull();
        }
    }
}
