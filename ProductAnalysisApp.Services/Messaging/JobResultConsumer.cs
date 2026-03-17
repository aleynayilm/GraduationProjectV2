using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductAnalysisApp.Entities.DataTransferObjects;
using ProductAnalysisApp.Services.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services.Messaging
{
    public class JobResult
    {
        public string JobId { get; set; } = "";
        public string Status { get; set; } = "pending"; // pending | completed | failed
        public object? Data { get; set; }
        public string? UserId { get; set; }              // Firebase UID
        public string JobType { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
    }

    public static class JobResultStore
    {
        private static readonly Dictionary<string, JobResult> _store = new();
        private static readonly object _lock = new();

        public static void Set(string jobId, JobResult result)
        {
            lock (_lock) { _store[jobId] = result; }
        }

        public static JobResult? Get(string jobId)
        {
            lock (_lock) { return _store.GetValueOrDefault(jobId); }
        }

        public static List<JobResult> GetByUser(string firebaseUid)
        {
            lock (_lock)
            {
                return _store.Values
                    .Where(j => j.UserId == firebaseUid)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToList();
            }
        }

        public static JobResult? GetForUser(string jobId, string firebaseUid)
        {
            lock (_lock)
            {
                var result = _store.GetValueOrDefault(jobId);
                if (result == null) return null;
                if (result.UserId != null && result.UserId != firebaseUid) return null;
                return result;
            }
        }
    }

    public class JobResultConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<JobResultConsumer> _logger;
        private IConnection? _connection;
        private IChannel? _channel;

        public JobResultConsumer(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            ILogger<JobResultConsumer> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[CONSUMER] Başlatılıyor...");

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _config["RabbitMQ:Host"] ?? "localhost",
                    Port = 5672,
                    UserName = _config["RabbitMQ:UserName"] ?? "guest",
                    Password = _config["RabbitMQ:Password"] ?? "guest"
                };

                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await _channel.QueueDeclareAsync(
                    queue: RabbitMqPublisher.QueueResult,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                // Aynı anda en fazla 1 mesaj işle
                await _channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += OnMessageReceivedAsync;

                await _channel.BasicConsumeAsync(
                    queue: RabbitMqPublisher.QueueResult,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("[CONSUMER] Dinleniyor: {Queue}", RabbitMqPublisher.QueueResult);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[CONSUMER] Durduruldu.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONSUMER] BAŞLATMA HATASI");
                throw;
            }
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            var rawBody = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogInformation("[CONSUMER] Mesaj alındı: {Body}", rawBody);

            try
            {
                var incoming = JsonSerializer.Deserialize<JobResult>(rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (incoming == null)
                {
                    _logger.LogWarning("[CONSUMER] Deserialize null döndü, mesaj atılıyor.");
                    await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                    return;
                }

                _logger.LogInformation("[CONSUMER] Deserialize OK — jobId={JobId} status={Status}",
                    incoming.JobId, incoming.Status);

                using var scope = _scopeFactory.CreateScope();

                JobResult finalJob;
                var redisAvailable = false;

                try
                {
                    var redis = scope.ServiceProvider.GetRequiredService<RedisService>();
                    var existing = await redis.GetJobAsync(incoming.JobId);
                    redisAvailable = true;

                    if (existing != null)
                    {
                        existing.Status = incoming.Status;
                        existing.Data = incoming.Data;
                        existing.FinishedAt = DateTime.UtcNow;
                        finalJob = existing;
                    }
                    else
                    {
                        incoming.FinishedAt = DateTime.UtcNow;
                        finalJob = incoming;
                    }

                    await redis.SetJobAsync(finalJob);
                    _logger.LogInformation("[CONSUMER] Redis'e yazıldı: {JobId} → {Status}",
                        finalJob.JobId, finalJob.Status);
                }
                catch (Exception redisEx)
                {
                    _logger.LogWarning(redisEx, "[CONSUMER] Redis erişilemedi, in-memory kullanılıyor.");
                    incoming.FinishedAt = DateTime.UtcNow;
                    finalJob = incoming;
                    JobResultStore.Set(finalJob.JobId, finalJob);
                }

                if (redisAvailable)
                {
                    try
                    {
                        var redis = scope.ServiceProvider.GetRequiredService<RedisService>();
                        await UpdateChatSessionIfNeededAsync(finalJob, redis);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[CONSUMER] Chat session güncellenemedi.");
                    }
                }

                try
                {
                    var fcm = scope.ServiceProvider.GetRequiredService<FcmService>();
                    var userService = scope.ServiceProvider.GetRequiredService<IServiceManager>();
                    await SendPushNotificationAsync(finalJob, userService, fcm);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CONSUMER] Push notification gönderilemedi.");
                }
                _logger.LogInformation("[CONSUMER] JobType={JobType} Status={Status}",
                finalJob.JobType, finalJob.Status);
                if (finalJob.Status == "completed" &&
                (finalJob.JobType == "scrape" || finalJob.JobType == "compare"))
                {
                    try
                    {
                        var serviceManager = scope.ServiceProvider.GetRequiredService<IServiceManager>();
                        await SaveScrapedProductAsync(finalJob, serviceManager);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[CONSUMER] Ürün MongoDB'ye kaydedilemedi: {JobId}", finalJob.JobId);
                    }
                }
                await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                _logger.LogInformation("[CONSUMER] ACK gönderildi: {JobId}", finalJob.JobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CONSUMER] Mesaj işlenirken kritik hata. Body: {Body}", rawBody);
                try
                {
                    await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "[CONSUMER] NACK gönderilemedi.");
                }
            }
        }

        private async Task SaveScrapedProductAsync(JobResult job, IServiceManager serviceManager)
        {
            if (job.Data is not JsonElement dataEl) return;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var scrapedList = JsonSerializer.Deserialize<List<ProductForScrapingDto>>(
                dataEl.GetRawText(), options);

            if (scrapedList == null || !scrapedList.Any())
            {
                _logger.LogWarning("[CONSUMER] Scrape verisi boş: {JobId}", job.JobId);
                return;
            }

            await serviceManager.ProductService.SaveScrapedProductAsync(scrapedList);

            _logger.LogInformation(
                "[CONSUMER] Ürün MongoDB'ye kaydedildi: {JobId} — {Count} platform",
                job.JobId, scrapedList.Count);
        }

        private static async Task UpdateChatSessionIfNeededAsync(JobResult job, RedisService redis)
        {
            if (job.Status != "completed") return;
            if (job.JobType is not ("chat" or "chat_cloud")) return;
            if (job.Data is not JsonElement el) return;

            if (el.TryGetProperty("sessionId", out var sidEl) &&
                el.TryGetProperty("reply", out var replyEl))
            {
                var sessionId = sidEl.GetString();
                var reply = replyEl.GetString();

                if (!string.IsNullOrEmpty(sessionId))
                    await redis.AppendToSessionAsync(sessionId,
                        new ChatMessage { Role = "assistant", Content = reply ?? "" });
            }
        }

        private async Task SendPushNotificationAsync(
            JobResult job,
            IServiceManager userService,
            FcmService fcm)
        {
            if (string.IsNullOrEmpty(job.UserId)) return;

            var user = await userService.UserService.GetOneUserByFirebaseUidAsync(job.UserId);
            if (user == null || string.IsNullOrEmpty(user.PushToken))
            {
                _logger.LogDebug("[CONSUMER] Push token bulunamadı: {Uid}", job.UserId);
                return;
            }

            try
            {
                await fcm.SendJobCompletedAsync(
                    pushToken: user.PushToken,
                    jobType: job.JobType,
                    jobId: job.JobId,
                    success: job.Status == "completed");

                _logger.LogInformation("[CONSUMER] Push notification gönderildi: {JobId}", job.JobId);
            }
            catch (InvalidPushTokenException)
            {
                _logger.LogWarning("[CONSUMER] Geçersiz push token temizleniyor: {Uid}", job.UserId);
                user.PushToken = null;
                await userService.UserService.UpdateOneUserAsync(user.Id, user);
            }
        }

        public override async Task StopAsync(CancellationToken ct)
        {
            _logger.LogInformation("[CONSUMER] Kapatılıyor...");
            try
            {
                if (_channel != null) await _channel.CloseAsync(ct);
                if (_connection != null) await _connection.CloseAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CONSUMER] Kapatma sırasında hata.");
            }
            await base.StopAsync(ct);
        }
    }
}