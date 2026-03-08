using System.Security.Claims;

namespace ProductAnalysisApp.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetFirebaseUid(this ClaimsPrincipal user)
            => user.FindFirst("firebase_uid")?.Value;

        public static string? GetEmail(this ClaimsPrincipal user)
            => user.FindFirst(ClaimTypes.Email)?.Value;

        public static bool IsAuthenticated(this ClaimsPrincipal user)
            => user.Identity?.IsAuthenticated == true;
    }
}
