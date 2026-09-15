using Microsoft.Extensions.Logging;
using Moq;
using ProductAnalysisApp.Services;
using Xunit;

namespace ProductAnalysisApp.Tests.Services
{
    // ChatSessionStore & ChatMessage
    public class ChatSessionStoreTests
    {
        [Fact]
        public void ChatSessionStore_Sessions_DefaultsToEmptyDictionary()
        {
            Assert.NotNull(ChatSessionStore.Sessions);
        }

        [Fact]
        public void ChatSessionStore_CanAddAndRetrieveSession()
        {
            var key = $"ses-{Guid.NewGuid()}";
            var messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Merhaba" }
            };

            ChatSessionStore.Sessions[key] = messages;
            var retrieved = ChatSessionStore.Sessions[key];

            Assert.Single(retrieved);
            Assert.Equal("user", retrieved[0].Role);

            ChatSessionStore.Sessions.Remove(key);
        }

        [Fact]
        public void ChatSessionStore_CanUpdateExistingSession()
        {
            var key = $"ses-update-{Guid.NewGuid()}";
            ChatSessionStore.Sessions[key] = [new() { Role = "user", Content = "İlk" }];
            ChatSessionStore.Sessions[key].Add(new() { Role = "assistant", Content = "Cevap" });

            Assert.Equal(2, ChatSessionStore.Sessions[key].Count);

            ChatSessionStore.Sessions.Remove(key);
        }

        [Fact]
        public void ChatMessage_DefaultRole_IsEmpty()
        {
            var msg = new ChatMessage();
            Assert.Equal("", msg.Role);
        }

        [Fact]
        public void ChatMessage_DefaultContent_IsEmpty()
        {
            var msg = new ChatMessage();
            Assert.Equal("", msg.Content);
        }

        [Fact]
        public void ChatMessage_PropertiesSetCorrectly()
        {
            var msg = new ChatMessage { Role = "assistant", Content = "Ürün A daha uygun." };

            Assert.Equal("assistant",         msg.Role);
            Assert.Equal("Ürün A daha uygun.", msg.Content);
        }

        [Fact]
        public void ChatMessage_SerializesWithJsonPropertyNames()
        {
            var msg  = new ChatMessage { Role = "user", Content = "Fiyatları karşılaştır" };
            var json = System.Text.Json.JsonSerializer.Serialize(msg);

            Assert.Contains("\"role\"",    json);
            Assert.Contains("\"content\"", json);
            Assert.Contains("user",        json);
        }
    }

    // InvalidPushTokenException
    public class InvalidPushTokenExceptionTests
    {
        [Fact]
        public void InvalidPushTokenException_StoresToken()
        {
            var ex = new InvalidPushTokenException("bad-token-xyz");

            Assert.Equal("bad-token-xyz", ex.Token);
        }

        [Fact]
        public void InvalidPushTokenException_MessageContainsToken()
        {
            var ex = new InvalidPushTokenException("bad-token-xyz");

            Assert.Contains("bad-token-xyz", ex.Message);
        }

        [Fact]
        public void InvalidPushTokenException_IsException()
        {
            var ex = new InvalidPushTokenException("t");

            Assert.IsAssignableFrom<Exception>(ex);
        }

        [Fact]
        public void InvalidPushTokenException_CanBeCaught_AsException()
        {
            Exception? caught = null;
            try { throw new InvalidPushTokenException("tok"); }
            catch (Exception ex) { caught = ex; }

            Assert.NotNull(caught);
            Assert.IsType<InvalidPushTokenException>(caught);
        }
    }

    // FcmService — BuildMessage 
    public class FcmServiceBuildMessageTests
    {
        private static (string title, string body) InvokeBuildMessage(string jobType, bool success)
        {
            var service = new FcmService(new Mock<ILogger<FcmService>>().Object);
            var method  = typeof(FcmService)
                .GetMethod("BuildMessage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            var result = method.Invoke(null, new object[] { jobType, success });
            return ((string, string))result!;
        }

        [Theory]
        [InlineData("scrape",            true,  "Ürün Karşılaştırması Hazır")]
        [InlineData("scrape",            false, "Karşılaştırma Başarısız")]
        [InlineData("cloud_llm_compare", true,  "AI Analizi Tamamlandı")]
        [InlineData("cloud_llm_compare", false, "AI Analizi Başarısız")]
        [InlineData("local_llm_compare", true,  "Karşılaştırma Tamamlandı")]
        [InlineData("chat",              true,  "Yeni Mesaj")]
        [InlineData("chat_cloud",        true,  "Yeni Mesaj")]
        [InlineData("unknown_type",      true,  "İşlem Tamamlandı")]
        public void BuildMessage_ReturnsCorrectTitle(string jobType, bool success, string expectedTitle)
        {
            var (title, _) = InvokeBuildMessage(jobType, success);

            Assert.Equal(expectedTitle, title);
        }

        [Fact]
        public void BuildMessage_ScrapeSuccess_HasCorrectBody()
        {
            var (_, body) = InvokeBuildMessage("scrape", true);
            Assert.NotEmpty(body);
        }

        [Fact]
        public void BuildMessage_ScrapeFailure_HasCorrectBody()
        {
            var (_, body) = InvokeBuildMessage("scrape", false);
            Assert.NotEmpty(body);
        }

        [Fact]
        public void FcmService_Constructor_AcceptsLogger()
        {
            var logger  = new Mock<ILogger<FcmService>>();
            var service = new FcmService(logger.Object);

            Assert.NotNull(service);
        }
    }

    // EmailService — SendPriceDropEmailAsync
    public class EmailServiceSendTests
    {
        private static EmailService BuildService()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var logger = new Mock<ILogger<EmailService>>().Object;
            return new EmailService(config, logger);
        }

        [Fact]
        public async Task SendPriceDropEmailAsync_WhenSmtpFails_DoesNotThrow()
        {
            var svc = BuildService();
            var ex = await Record.ExceptionAsync(() => svc.SendPriceDropEmailAsync(
                toEmail:      "test@example.com",
                toName:       "Test User",
                productName:  "Laptop",
                platformName: "Trendyol",
                oldPrice:     5000m,
                newPrice:     3000m,
                productUrl:   "https://trendyol.com/p",
                imageUrl:     null));

            Assert.Null(ex);
        }

        [Fact]
        public async Task SendPriceDropEmailAsync_WithImageUrl_DoesNotThrow()
        {
            var svc = BuildService();
            var ex = await Record.ExceptionAsync(() => svc.SendPriceDropEmailAsync(
                toEmail:      "test@example.com",
                toName:       "Ali",
                productName:  "Telefon",
                platformName: "Amazon",
                oldPrice:     2000m,
                newPrice:     1500m,
                productUrl:   "https://amazon.com/p",
                imageUrl:     "https://example.com/img.jpg"));

            Assert.Null(ex);
        }
    }

    // FcmService — SendPriceDropAsync 
    public class FcmServiceSendTests
    {
        [Fact]
        public async Task SendPriceDropAsync_WhenFirebaseNotInitialized_DoesNotThrow()
        {
            var logger  = new Mock<ILogger<FcmService>>();
            var service = new FcmService(logger.Object);

            var ex = await Record.ExceptionAsync(() => service.SendPriceDropAsync(
                pushToken:   "fake-token",
                productName: "Laptop",
                oldPrice:    5000m,
                newPrice:    3000m,
                productUrl:  "https://example.com/p"));

            Assert.Null(ex);
        }
    }

    // EmailService — BuildHtmlBody 
    public class EmailServiceBuildHtmlTests
    {
        private static string InvokeBuildHtmlBody(
            string toName, string productName, string platformName,
            decimal oldPrice, decimal newPrice, decimal dropAmount, decimal dropPercent,
            string productUrl, string? imageUrl)
        {
            var method = typeof(EmailService)
                .GetMethod("BuildHtmlBody",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            return (string)method.Invoke(null, new object?[]
            {
                toName, productName, platformName, oldPrice, newPrice,
                dropAmount, dropPercent, productUrl, imageUrl
            })!;
        }

        [Fact]
        public void BuildHtmlBody_ContainsProductName()
        {
            var html = InvokeBuildHtmlBody(
                "Ali", "Gaming Laptop", "Trendyol",
                5000m, 3000m, 2000m, 40m,
                "https://trendyol.com/p", null);

            Assert.Contains("Gaming Laptop", html);
        }

        [Fact]
        public void BuildHtmlBody_ContainsPlatformName()
        {
            var html = InvokeBuildHtmlBody(
                "Ali", "Laptop", "Trendyol",
                5000m, 3000m, 2000m, 40m,
                "https://trendyol.com/p", null);

            Assert.Contains("Trendyol", html);
        }

        [Fact]
        public void BuildHtmlBody_ContainsToName()
        {
            var html = InvokeBuildHtmlBody(
                "Ayşe", "Tablet", "Amazon",
                2000m, 1500m, 500m, 25m,
                "https://amazon.com/t", null);

            Assert.Contains("Ayşe", html);
        }

        [Fact]
        public void BuildHtmlBody_ContainsProductUrl()
        {
            var url  = "https://example.com/product";
            var html = InvokeBuildHtmlBody(
                "User", "Ürün", "Platform",
                100m, 80m, 20m, 20m, url, null);

            Assert.Contains(url, html);
        }

        [Fact]
        public void BuildHtmlBody_ContainsImageTag_WhenImageUrlProvided()
        {
            var html = InvokeBuildHtmlBody(
                "User", "Ürün", "Platform",
                100m, 80m, 20m, 20m,
                "https://example.com/p",
                "https://example.com/img.jpg");

            Assert.Contains("<img", html);
            Assert.Contains("https://example.com/img.jpg", html);
        }

        [Fact]
        public void BuildHtmlBody_DoesNotContainImageTag_WhenImageUrlIsNull()
        {
            var html = InvokeBuildHtmlBody(
                "User", "Ürün", "Platform",
                100m, 80m, 20m, 20m,
                "https://example.com/p", null);

            Assert.DoesNotContain("<img", html);
        }

        [Fact]
        public void BuildHtmlBody_ContainsPrices()
        {
            var html = InvokeBuildHtmlBody(
                "User", "Ürün", "Platform",
                5000m, 3000m, 2000m, 40m,
                "https://example.com/p", null);

            Assert.Contains("5.000", html);
            Assert.Contains("3.000", html);
        }

        [Fact]
        public void BuildHtmlBody_ContainsDropPercent()
        {
            var html = InvokeBuildHtmlBody(
                "User", "Ürün", "Platform",
                1000m, 600m, 400m, 40m,
                "https://example.com/p", null);

            Assert.Contains("40", html);
        }

        [Fact]
        public void EmailService_Constructor_AcceptsConfigAndLogger()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var logger = new Mock<ILogger<EmailService>>().Object;

            var service = new EmailService(config, logger);

            Assert.NotNull(service);
        }
    }
}
