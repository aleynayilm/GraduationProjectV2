using Microsoft.Extensions.Logging;
using Quartz;

namespace ProductAnalysisApp.Services
{
    /// <summary>
    /// Kullanıcı başına Quartz job oluşturur, günceller veya siler.
    /// Program.cs'te singleton olarak kayıt edilmeli.
    /// </summary>
    public class UserJobScheduler
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<UserJobScheduler> _logger;

        public UserJobScheduler(
            ISchedulerFactory schedulerFactory,
            ILogger<UserJobScheduler> logger)
        {
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        /// <summary>
        /// Kullanıcı için fiyat kontrol job'ını oluşturur veya günceller.
        /// intervalHours: 1, 6, 12, 24, 48, 72 gibi değerler
        /// </summary>
        public virtual async Task ScheduleOrUpdateAsync(string firebaseUid, int intervalHours)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = PriceCheckJob.KeyFor(firebaseUid);

            // Mevcut job varsa sil
            if (await scheduler.CheckExists(jobKey))
                await scheduler.DeleteJob(jobKey);

            var job = JobBuilder.Create<PriceCheckJob>()
                .WithIdentity(jobKey)
                .UsingJobData(PriceCheckJob.DataKeyUid, firebaseUid)
                .StoreDurably()
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"trigger-{firebaseUid}", "price-check")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(intervalHours)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            _logger.LogInformation(
                "[SCHEDULER] Job oluşturuldu/güncellendi: {Uid} — her {Hours} saatte bir",
                firebaseUid, intervalHours);
        }

        /// <summary>
        /// Kullanıcının job'ını durdurur ve siler.
        /// Fiyat bildirimi kapatıldığında veya kullanıcı silindiğinde çağrılır.
        /// </summary>
        public virtual async Task RemoveAsync(string firebaseUid)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = PriceCheckJob.KeyFor(firebaseUid);

            if (await scheduler.CheckExists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
                _logger.LogInformation("[SCHEDULER] Job silindi: {Uid}", firebaseUid);
            }
        }

        /// <summary>
        /// Uygulama başlangıcında tüm aktif kullanıcılar için job'ları yükler.
        /// </summary>
        public async Task RestoreAllJobsAsync(IEnumerable<(string firebaseUid, int intervalHours)> activeUsers)
        {
            foreach (var (uid, hours) in activeUsers)
            {
                try { await ScheduleOrUpdateAsync(uid, hours); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SCHEDULER] Restore hatası: {Uid}", uid);
                }
            }

            _logger.LogInformation("[SCHEDULER] {Count} kullanıcı job'ı restore edildi.", activeUsers.Count());
        }
    }
}