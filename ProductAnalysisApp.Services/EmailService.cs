using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace ProductAnalysisApp.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendPriceDropEmailAsync(
            string toEmail,
            string toName,
            string productName,
            string platformName,
            decimal oldPrice,
            decimal newPrice,
            string productUrl,
            string? imageUrl = null)
        {
            try
            {
                var host = _config["Smtp:Host"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Smtp:Port"] ?? "587");
                var username = _config["Smtp:Username"] ?? "";
                var password = _config["Smtp:Password"] ?? "";
                var fromName = _config["Smtp:FromName"] ?? "Fiyat Takip";

                var dropAmount = oldPrice - newPrice;
                var dropPercent = Math.Round((dropAmount / oldPrice) * 100, 1);

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, username));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = $"💸 Fiyat Düştü! {productName} — {dropPercent}% indirim";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = BuildHtmlBody(
                        toName, productName, platformName,
                        oldPrice, newPrice, dropAmount, dropPercent,
                        productUrl, imageUrl)
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation(
                    "[EMAIL] Gönderildi: {Email} — {Product} {OldPrice}→{NewPrice}",
                    toEmail, productName, oldPrice, newPrice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[EMAIL] Gönderilemedi: {Email} — {Product}", toEmail, productName);
            }
        }

        private static string BuildHtmlBody(
            string toName,
            string productName,
            string platformName,
            decimal oldPrice,
            decimal newPrice,
            decimal dropAmount,
            decimal dropPercent,
            string productUrl,
            string? imageUrl)
        {
            var imageSection = imageUrl != null
                ? $@"<div style='text-align:center;margin-bottom:20px;'>
                       <img src='{imageUrl}' alt='{productName}'
                            style='max-width:200px;max-height:200px;border-radius:8px;'/>
                     </div>"
                : "";

            return $@"
<!DOCTYPE html>
<html lang='tr'>
<head>
  <meta charset='UTF-8'/>
  <meta name='viewport' content='width=device-width,initial-scale=1.0'/>
</head>
<body style='margin:0;padding:0;background:#f4f6f9;font-family:Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f6f9;padding:40px 0;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0'
             style='background:#ffffff;border-radius:12px;overflow:hidden;
                    box-shadow:0 2px 8px rgba(0,0,0,0.08);'>

        <!-- Başlık -->
        <tr>
          <td style='background:linear-gradient(135deg,#1F4E79,#2E75B6);
                     padding:32px 40px;text-align:center;'>
            <h1 style='color:#ffffff;margin:0;font-size:26px;'>💸 Fiyat Düşüşü!</h1>
            <p style='color:#cce3f5;margin:8px 0 0;font-size:15px;'>
              Takip ettiğiniz ürünün fiyatı düştü
            </p>
          </td>
        </tr>

        <!-- İçerik -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='color:#333;font-size:16px;margin:0 0 24px;'>
              Merhaba <strong>{toName}</strong>,
            </p>

            {imageSection}

            <!-- Ürün Kutusu -->
            <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;
                        padding:24px;margin-bottom:24px;'>
              <p style='margin:0 0 4px;color:#666;font-size:13px;text-transform:uppercase;
                        letter-spacing:0.5px;'>Ürün</p>
              <h2 style='margin:0 0 4px;color:#1a202c;font-size:20px;'>{productName}</h2>
              <p style='margin:0;color:#2E75B6;font-size:14px;'>{platformName}</p>
            </div>

            <!-- Fiyat Karşılaştırma -->
            <table width='100%' cellpadding='0' cellspacing='0' style='margin-bottom:24px;'>
              <tr>
                <td style='width:48%;text-align:center;background:#fff5f5;border-radius:10px;
                           padding:20px;border:1px solid #fed7d7;'>
                  <p style='margin:0 0 6px;color:#999;font-size:12px;'>ESKİ FİYAT</p>
                  <p style='margin:0;color:#e53e3e;font-size:22px;font-weight:bold;
                            text-decoration:line-through;'>{oldPrice:N2} TL</p>
                </td>
                <td style='width:4%;text-align:center;color:#666;font-size:24px;'>→</td>
                <td style='width:48%;text-align:center;background:#f0fff4;border-radius:10px;
                           padding:20px;border:1px solid #9ae6b4;'>
                  <p style='margin:0 0 6px;color:#999;font-size:12px;'>YENİ FİYAT</p>
                  <p style='margin:0;color:#276749;font-size:28px;font-weight:bold;'>{newPrice:N2} TL</p>
                </td>
              </tr>
            </table>

            <!-- İndirim Rozeti -->
            <div style='text-align:center;margin-bottom:28px;'>
              <span style='display:inline-block;background:#276749;color:#fff;
                           padding:10px 28px;border-radius:50px;font-size:16px;font-weight:bold;'>
                🎉 {dropAmount:N2} TL tasarruf — %{dropPercent} indirim
              </span>
            </div>

            <!-- CTA Butonu -->
            <div style='text-align:center;margin-bottom:28px;'>
              <a href='{productUrl}'
                 style='display:inline-block;background:linear-gradient(135deg,#1F4E79,#2E75B6);
                        color:#ffffff;text-decoration:none;padding:16px 40px;
                        border-radius:8px;font-size:16px;font-weight:bold;
                        box-shadow:0 4px 12px rgba(46,117,182,0.35);'>
                Ürüne Git →
              </a>
            </div>

            <p style='color:#999;font-size:13px;text-align:center;margin:0;'>
              Fiyat bildirimlerini
              <a href='#' style='color:#2E75B6;'>uygulama ayarlarından</a>
              yönetebilirsiniz.
            </p>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background:#f8fafc;padding:20px 40px;text-align:center;
                     border-top:1px solid #e2e8f0;'>
            <p style='margin:0;color:#aaa;font-size:12px;'>
              © 2026 Fiyat Takip Uygulaması · Bu e-posta otomatik olarak gönderilmiştir.
            </p>
          </td>
        </tr>

      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}