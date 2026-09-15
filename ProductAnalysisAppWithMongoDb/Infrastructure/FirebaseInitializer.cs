using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace ProductAnalysisAppWithMongoDb.Infrastructure
{
    public static class FirebaseInitializer
    {
        public static void Initialize(IConfiguration configuration)
        {
            if (FirebaseApp.DefaultInstance != null) return;

            var credentialPath = configuration["Firebase:CredentialPath"];

            GoogleCredential credential;

            if (!string.IsNullOrEmpty(credentialPath) && File.Exists(credentialPath))
            {
                credential = GoogleCredential.FromFile(credentialPath);
            }
            else
            {
                var json = configuration["Firebase:CredentialJson"]
                    ?? throw new InvalidOperationException(
                        "Firebase credential bulunamadı. " +
                        "Firebase:CredentialPath veya Firebase:CredentialJson ayarlayın.");

                credential = GoogleCredential.FromJson(json);
            }

            FirebaseApp.Create(new AppOptions { Credential = credential });
            Console.WriteLine("[FIREBASE] Başarıyla başlatıldı.");
        }
    }
}
