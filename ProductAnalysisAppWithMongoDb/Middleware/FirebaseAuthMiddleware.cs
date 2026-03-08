using FirebaseAdmin.Auth;
using System.Security.Claims;

namespace ProductAnalysisAppWithMongoDb.Middleware
{
    public class FirebaseAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public FirebaseAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var idToken = authHeader["Bearer ".Length..].Trim();

                try
                {
                    var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);

                    var claims = new List<Claim>
                {
                    new Claim("firebase_uid", decodedToken.Uid),
                    new Claim(ClaimTypes.Email, decodedToken.Claims
                        .GetValueOrDefault("email")?.ToString() ?? ""),
                };

                    var identity = new ClaimsIdentity(claims, "Firebase");
                    context.User = new ClaimsPrincipal(identity);
                }
                catch (FirebaseAuthException ex)
                {
                    Console.WriteLine($"[FIREBASE] Token doğrulanamadı: {ex.Message}");
                }
            }

            await _next(context);
        }
    }
}
