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
    public class SearchHistoryManagerTests
    {
        private readonly Mock<IRepositoryManager>         _repoMock;
        private readonly Mock<ISearchHistoryRepository>   _historyRepoMock;
        private readonly SearchHistoryManager             _sut;

        public SearchHistoryManagerTests()
        {
            _repoMock        = new Mock<IRepositoryManager>();
            _historyRepoMock = new Mock<ISearchHistoryRepository>();

            _repoMock.Setup(r => r.SearchHistory).Returns(_historyRepoMock.Object);

            _sut = new SearchHistoryManager(_repoMock.Object);
        }

        // ── AddSearchAsync ───────────────────────────────────────────────

        [Fact]
        public async Task AddSearchAsync_WithValidUrl_ShouldSaveToRepository()
        {
            // Arrange
            _historyRepoMock
                .Setup(r => r.AddSearchHistoryAsync(It.IsAny<SearchHistory>()))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.AddSearchAsync("https://www.trendyol.com/product", "user-001");

            // Assert
            _historyRepoMock.Verify(r => r.AddSearchHistoryAsync(
                It.Is<SearchHistory>(h =>
                    h.SearchUrl   == "https://www.trendyol.com/product" &&
                    h.FirebaseUid == "user-001")),
                Times.Once);
        }

        [Fact]
        public async Task AddSearchAsync_WithLeadingTrailingSpaces_ShouldTrimUrl()
        {
            // Arrange
            _historyRepoMock
                .Setup(r => r.AddSearchHistoryAsync(It.IsAny<SearchHistory>()))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.AddSearchAsync("  https://www.trendyol.com/product  ", "user-001");

            // Assert
            _historyRepoMock.Verify(r => r.AddSearchHistoryAsync(
                It.Is<SearchHistory>(h =>
                    h.SearchUrl == "https://www.trendyol.com/product")),
                Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task AddSearchAsync_WithEmptyOrNullUrl_ShouldNotSave(string? url)
        {
            // Act
            await _sut.AddSearchAsync(url!, "user-001");

            // Assert
            _historyRepoMock.Verify(
                r => r.AddSearchHistoryAsync(It.IsAny<SearchHistory>()), Times.Never);
        }

        [Fact]
        public async Task AddSearchAsync_ShouldSetSearchDateToUtcNow()
        {
            // Arrange
            SearchHistory? captured = null;
            _historyRepoMock
                .Setup(r => r.AddSearchHistoryAsync(It.IsAny<SearchHistory>()))
                .Callback<SearchHistory>(h => captured = h)
                .Returns(Task.CompletedTask);

            var before = DateTime.UtcNow;

            // Act
            await _sut.AddSearchAsync("https://www.amazon.com.tr/dp/test", "user-001");

            var after = DateTime.UtcNow;

            // Assert
            captured.Should().NotBeNull();
            captured!.SearchDate.Should().BeOnOrAfter(before);
            captured.SearchDate.Should().BeOnOrBefore(after);
        }

        // ── GetRecentSearchesAsync ───────────────────────────────────────

        [Fact]
        public async Task GetRecentSearchesAsync_ShouldReturnUserHistories()
        {
            // Arrange
            var histories = new List<SearchHistory>
            {
                new() { Id = "h1", SearchUrl = "https://trendyol.com/p1", FirebaseUid = "user-001", SearchDate = DateTime.UtcNow },
                new() { Id = "h2", SearchUrl = "https://amazon.com.tr/p2", FirebaseUid = "user-001", SearchDate = DateTime.UtcNow.AddMinutes(-5) },
            };

            _historyRepoMock
                .Setup(r => r.GetUserSearchHistoriesAsync("user-001"))
                .ReturnsAsync(histories);

            // Act
            var result = await _sut.GetRecentSearchesAsync("user-001");

            // Assert
            result.Should().HaveCount(2);
            result[0].SearchUrl.Should().Be("https://trendyol.com/p1");
        }

        [Fact]
        public async Task GetRecentSearchesAsync_WhenNoHistory_ShouldReturnEmptyList()
        {
            // Arrange
            _historyRepoMock
                .Setup(r => r.GetUserSearchHistoriesAsync("user-001"))
                .ReturnsAsync(new List<SearchHistory>());

            // Act
            var result = await _sut.GetRecentSearchesAsync("user-001");

            // Assert
            result.Should().BeEmpty();
        }
    }
}
