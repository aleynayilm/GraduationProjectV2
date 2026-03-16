using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProductAnalysisApp.Entities.Models;
using ProductAnalysisApp.Repositories.Contracts;
using ProductAnalysisApp.Services.Messaging;
using Quartz;

namespace ProductAnalysisApp.Services
{
    [DisallowConcurrentExecution]
    public class PriceCheckJob : IJob
    {
        public static JobKey KeyFor(string firebaseUid)
            => new($"price-check-{firebaseUid}", "price-check");

        public static readonly string DataKeyUid = "firebaseUid";

        private readonly IServiceProvider _services;
        private readonly ILogger<PriceCheckJob> _logger;

        public PriceCheckJob(IServiceProvider services, ILogger<PriceCheckJob> logger)
        {
            _services = services;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var firebaseUid = context.JobDetail.JobDataMap.GetString(DataKeyUid);
            if (string.IsNullOrEmpty(firebaseUid)) return;

            _logger.LogInformation("[PRICE-CHECK] Başladı: {Uid}", firebaseUid);

            using var scope = _services.CreateScope();
            var sp = scope.ServiceProvider;

            var repoManager = sp.GetRequiredService<IRepositoryManager>();
            var scraperService = sp.GetRequiredService<PythonScraperService>();
            var emailService = sp.GetRequiredService<EmailService>();
            var fcmService = sp.GetRequiredService<FcmService>();

            var user = await repoManager.User.GetOneUserByFirebaseUidAsync(firebaseUid);
            if (user == null || !user.PriceAlertEnabled)
            {
                _logger.LogInformation("[PRICE-CHECK] Kullanıcı bulunamadı veya bildirim kapalı: {Uid}", firebaseUid);
                return;
            }

            var favorites = repoManager.Favorite.GetFavoritesByUserId(user.Id).ToList();
            if (!favorites.Any())
            {
                _logger.LogInformation("[PRICE-CHECK] Favori yok: {Uid}", firebaseUid);
                return;
            }

            _logger.LogInformation("[PRICE-CHECK] {Count} favori kontrol ediliyor: {Uid}",
                favorites.Count, firebaseUid);

            foreach (var favorite in favorites)
            {
                try
                {
                    await CheckAndNotifyAsync(
                        user, favorite, repoManager, scraperService, emailService, fcmService);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PRICE-CHECK] Favori hata: {FavoriteId}", favorite.FavoriteId);
                }
            }

            _logger.LogInformation("[PRICE-CHECK] Tamamlandı: {Uid}", firebaseUid);
        }

        private async Task CheckAndNotifyAsync(
            User user,
            Favorite favorite,
            IRepositoryManager repoManager,
            PythonScraperService scraperService,
            EmailService emailService,
            FcmService fcmService)
        {
            var platform = repoManager.ProductPlatform.GetOneProductPlatform(favorite.ProductPlatformId);
            if (platform == null) return;

            var scraped = await scraperService.ScrapeOneAsync(platform.ProductUrl);
            if (scraped == null)
            {
                _logger.LogWarning("[PRICE-CHECK] Scrape başarısız: {Url}", platform.ProductUrl);
                return;
            }

            var oldPrice = platform.Price;
            var newPrice = scraped.Price;

            platform.LastPriceCheckedAt = DateTime.UtcNow;

            if (newPrice >= oldPrice) return;

            _logger.LogInformation("[PRICE-CHECK] Fiyat düşüşü: {Url} {Old}→{New}",
                platform.ProductUrl, oldPrice, newPrice);

            platform.Price = newPrice;
            await repoManager.ProductPlatform.UpdateProductPlatformAsync(platform);
            if (newPrice >= oldPrice) return;

            if (!string.IsNullOrEmpty(user.Email))
            {
                await emailService.SendPriceDropEmailAsync(
                    toEmail: user.Email,
                    toName: $"{user.FirstName} {user.LastName}".Trim(),
                    productName: scraped.ProductName,
                    platformName: scraped.PlatformName,
                    oldPrice: oldPrice,
                    newPrice: newPrice,
                    productUrl: platform.ProductUrl,
                    imageUrl: scraped.ImageUrl);
            }

            if (!string.IsNullOrEmpty(user.PushToken))
            {
                await fcmService.SendPriceDropAsync(
                    pushToken: user.PushToken,
                    productName: scraped.ProductName,
                    oldPrice: oldPrice,
                    newPrice: newPrice,
                    productUrl: platform.ProductUrl);
            }
        }
    }
}