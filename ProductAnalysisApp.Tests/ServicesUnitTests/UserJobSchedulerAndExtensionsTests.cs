using Microsoft.Extensions.Logging;
using Moq;
using ProductAnalysisApp.Extensions;
using ProductAnalysisApp.Services;
using Quartz;
using System.Security.Claims;
using Xunit;

namespace ProductAnalysisApp.Tests.Services
{
    // UserJobScheduler Tests

    public class UserJobSchedulerTests
    {
        private readonly Mock<ISchedulerFactory>              _factoryMock;
        private readonly Mock<IScheduler>                     _schedulerMock;
        private readonly Mock<ILogger<UserJobScheduler>>      _loggerMock;
        private readonly UserJobScheduler                     _jobScheduler;

        public UserJobSchedulerTests()
        {
            _schedulerMock = new Mock<IScheduler>();
            _factoryMock   = new Mock<ISchedulerFactory>();
            _loggerMock    = new Mock<ILogger<UserJobScheduler>>();

            _factoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(_schedulerMock.Object);

            _jobScheduler = new UserJobScheduler(_factoryMock.Object, _loggerMock.Object);
        }

        // ScheduleOrUpdateAsync 

        [Fact]
        public async Task ScheduleOrUpdateAsync_SchedulesNewJob_WhenJobDoesNotExist()
        {
            _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(false);
            _schedulerMock.Setup(s => s.ScheduleJob(
                    It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            await _jobScheduler.ScheduleOrUpdateAsync("uid-001", 24);

            _schedulerMock.Verify(s => s.ScheduleJob(
                It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ScheduleOrUpdateAsync_DeletesExistingJob_BeforeScheduling()
        {
            _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(true);
            _schedulerMock.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(true);
            _schedulerMock.Setup(s => s.ScheduleJob(
                    It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            await _jobScheduler.ScheduleOrUpdateAsync("uid-002", 12);

            _schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _schedulerMock.Verify(s => s.ScheduleJob(
                It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ScheduleOrUpdateAsync_DoesNotDeleteJob_WhenJobDoesNotExist()
        {
            _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(false);
            _schedulerMock.Setup(s => s.ScheduleJob(
                    It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            await _jobScheduler.ScheduleOrUpdateAsync("uid-003", 6);

            _schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(6)]
        [InlineData(12)]
        [InlineData(24)]
        [InlineData(48)]
        [InlineData(72)]
        public async Task ScheduleOrUpdateAsync_AcceptsAllValidIntervals(int hours)
        {
            _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(false);
            _schedulerMock.Setup(s => s.ScheduleJob(
                    It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            // Exception fırlatmamalı
            await _jobScheduler.ScheduleOrUpdateAsync("uid-interval", hours);

            _schedulerMock.Verify(s => s.ScheduleJob(
                It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // RemoveAsync 

        [Fact]
        public async Task RemoveAsync_DeletesJob_WhenJobExists()
        {
            _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(true);
            _schedulerMock.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(true);

            await _jobScheduler.RemoveAsync("uid-to-remove");

            _schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_DoesNothing_WhenJobDoesNotExist()
        {
            _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(false);

            await _jobScheduler.RemoveAsync("uid-nonexistent");

            _schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // RestoreAllJobsAsync 

        [Fact]
        public async Task RestoreAllJobsAsync_SchedulesJobForEachUser()
        {
            var users = new List<(string, int)>
            {
                ("uid-a", 24),
                ("uid-b", 12),
                ("uid-c", 6)
            };

            _schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(false);
            _schedulerMock.Setup(s => s.ScheduleJob(
                    It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            await _jobScheduler.RestoreAllJobsAsync(users);

            _schedulerMock.Verify(s => s.ScheduleJob(
                It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task RestoreAllJobsAsync_ContinuesOnException_ForFailingUser()
        {
            var users = new List<(string, int)>
            {
                ("uid-ok", 24),
                ("uid-fail", 12)
            };

            _schedulerMock.Setup(s => s.CheckExists(
                    It.Is<JobKey>(k => k.Name.Contains("uid-ok")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _schedulerMock.Setup(s => s.CheckExists(
                    It.Is<JobKey>(k => k.Name.Contains("uid-fail")), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Scheduler bağlantı hatası"));

            _schedulerMock.Setup(s => s.ScheduleJob(
                    It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            var ex = await Record.ExceptionAsync(
                () => _jobScheduler.RestoreAllJobsAsync(users));

            Assert.Null(ex);
        }

        [Fact]
        public async Task RestoreAllJobsAsync_DoesNothing_WhenListIsEmpty()
        {
            await _jobScheduler.RestoreAllJobsAsync([]);

            _schedulerMock.Verify(s => s.ScheduleJob(
                It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    // ClaimsPrincipalExtensions Tests

    public class ClaimsPrincipalExtensionsTests
    {
        private static ClaimsPrincipal BuildPrincipal(
            string? firebaseUid = null,
            string? email       = null,
            bool    authenticated = true)
        {
            var claims = new List<Claim>();
            if (firebaseUid != null) claims.Add(new Claim("firebase_uid", firebaseUid));
            if (email != null)       claims.Add(new Claim(ClaimTypes.Email, email));

            var identity  = new ClaimsIdentity(claims, authenticated ? "TestAuth" : null);
            return new ClaimsPrincipal(identity);
        }

        // GetFirebaseUid 

        [Fact]
        public void GetFirebaseUid_ReturnsUid_WhenClaimExists()
        {
            var principal = BuildPrincipal(firebaseUid: "uid-abc");

            var result = principal.GetFirebaseUid();

            Assert.Equal("uid-abc", result);
        }

        [Fact]
        public void GetFirebaseUid_ReturnsNull_WhenClaimMissing()
        {
            var principal = BuildPrincipal();

            var result = principal.GetFirebaseUid();

            Assert.Null(result);
        }

        // GetEmail 

        [Fact]
        public void GetEmail_ReturnsEmail_WhenClaimExists()
        {
            var principal = BuildPrincipal(email: "user@example.com");

            var result = principal.GetEmail();

            Assert.Equal("user@example.com", result);
        }

        [Fact]
        public void GetEmail_ReturnsNull_WhenClaimMissing()
        {
            var principal = BuildPrincipal();

            var result = principal.GetEmail();

            Assert.Null(result);
        }

        // IsAuthenticated 

        [Fact]
        public void IsAuthenticated_ReturnsTrue_WhenUserIsAuthenticated()
        {
            var principal = BuildPrincipal(firebaseUid: "uid", authenticated: true);

            Assert.True(principal.IsAuthenticated());
        }

        [Fact]
        public void IsAuthenticated_ReturnsFalse_WhenUserIsAnonymous()
        {
            var principal = BuildPrincipal(authenticated: false);

            Assert.False(principal.IsAuthenticated());
        }
    }
}
