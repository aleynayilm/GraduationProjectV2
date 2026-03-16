using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Services
{
    public class FcmService
    {
        private readonly ILogger<FcmService> _logger;

        public FcmService(ILogger<FcmService> logger)
            => _logger = logger;

        public async Task SendJobCompletedAsync(
            string pushToken,
            string jobType,
            string jobId,
            bool success)
        {
            var (title, body) = BuildMessage(jobType, success);

            var message = new Message
            {
                Token = pushToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = new Dictionary<string, string>
                {
                    ["jobId"] = jobId,
                    ["jobType"] = jobType,
                    ["status"] = success ? "completed" : "failed"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default",
                        ContentAvailable = true
                    }
                }
            };

            try
            {
                var result = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("[FCM] Gönderildi: {Result} | Job: {JobId}", result, jobId);
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError("[FCM ERROR] {Code}: {Message}", ex.MessagingErrorCode, ex.Message);

                if (ex.MessagingErrorCode is MessagingErrorCode.Unregistered
                                          or MessagingErrorCode.InvalidArgument)
                {
                    _logger.LogWarning("[FCM] Geçersiz token, temizlenmeli: {Token}", pushToken);
                    throw new InvalidPushTokenException(pushToken);
                }
            }
        }

        public async Task SendPriceDropAsync(
            string pushToken,
            string productName,
            decimal oldPrice,
            decimal newPrice,
            string productUrl)
        {
            try
            {
                var dropPercent = Math.Round(((oldPrice - newPrice) / oldPrice) * 100, 1);

                var message = new Message
                {
                    Token = pushToken,
                    Notification = new Notification
                    {
                        Title = $"Fiyat Düştü! %{dropPercent} indirim",
                        Body = $"{productName}: {oldPrice:N0} TL → {newPrice:N0} TL"
                    },
                    Data = new Dictionary<string, string>
            {
                { "type",       "price_drop"         },
                { "productUrl", productUrl            },
                { "oldPrice",   oldPrice.ToString()   },
                { "newPrice",   newPrice.ToString()   },
                { "dropPercent",dropPercent.ToString()}
            }
                };

                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation(
                    "[FCM] Fiyat düşüşü bildirimi gönderildi: {Product}", productName);
            }
            catch (FirebaseMessagingException ex)
                when (ex.MessagingErrorCode is MessagingErrorCode.Unregistered
                                            or MessagingErrorCode.InvalidArgument)
            {
                throw new InvalidPushTokenException(pushToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FCM] Fiyat bildirimi gönderilemedi: {Product}", productName);
            }
        }

        private static (string title, string body) BuildMessage(string jobType, bool success)
            => (jobType, success) switch
            {
                ("scrape", true) => ("Ürün Karşılaştırması Hazır", "Fiyat karşılaştırmanız tamamlandı!"),
                ("scrape", false) => ("Karşılaştırma Başarısız", "Ürün bilgisi alınamadı, tekrar deneyin."),
                ("cloud_llm_compare", true) => ("AI Analizi Tamamlandı", "Ürün analiz raporunuz hazır!"),
                ("cloud_llm_compare", false) => ("AI Analizi Başarısız", "Analiz sırasında hata oluştu."),
                ("local_llm_compare", true) => ("Karşılaştırma Tamamlandı", "Ürün karşılaştırma sonuçlarınız hazır!"),
                ("chat", true) => ("Yeni Mesaj", "Asistanınızdan yanıt geldi."),
                ("chat_cloud", true) => ("Yeni Mesaj", "Asistanınızdan yanıt geldi."),
                _ => ("İşlem Tamamlandı", "Sonuçlarınız hazır."),
            };
    }

    public class InvalidPushTokenException : Exception
    {
        public string Token { get; }
        public InvalidPushTokenException(string token) : base($"Geçersiz FCM token: {token}")
            => Token = token;
    }
}
