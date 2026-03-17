using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ProductAnalysisApp.Tests.Controllers
{
    /// <summary>
    /// Tüm controller testleri için ortak base class.
    /// Authenticated kullanıcı simülasyonu sağlar.
    /// </summary>
    public abstract class ControllerTestBase
    {
        protected const string TestFirebaseUid = "firebase-uid-test-001";
        protected const string TestUserId      = "user-test-001";
        protected const string TestEmail       = "test@example.com";

        /// <summary>
        /// Controller'a authenticated kullanıcı atar.
        /// </summary>
        protected static void SetAuthenticatedUser(ControllerBase controller, string firebaseUid = TestFirebaseUid)
        {
            var claims = new List<Claim>
            {
                new("firebase_uid",          firebaseUid),
                new(System.Security.Claims.ClaimTypes.Email, TestEmail),
            };

            var identity  = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        /// <summary>
        /// Controller'a anonim (unauthenticated) kullanıcı atar.
        /// </summary>
        protected static void SetAnonymousUser(ControllerBase controller)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };
        }
    }
}
