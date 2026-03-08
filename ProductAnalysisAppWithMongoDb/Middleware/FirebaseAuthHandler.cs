using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace ProductAnalysisAppWithMongoDb.Middleware
{
    public class FirebaseAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public FirebaseAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Context.User.Identity?.IsAuthenticated == true)
                return Task.FromResult(
                    AuthenticateResult.Success(
                        new AuthenticationTicket(Context.User, "Firebase")));

            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }
}
