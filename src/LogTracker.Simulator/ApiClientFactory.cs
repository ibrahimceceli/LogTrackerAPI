using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace LogTracker.Simulator;

/// <summary>Creates the single <see cref="HttpClient"/> that every virtual user shares.</summary>
internal static class ApiClientFactory
{
    // Certificate extension that "dotnet dev-certs" adds to the ASP.NET Core HTTPS development certificate.
    private const string AspNetCoreDevelopmentCertificateOid = "1.3.6.1.4.1.311.84.1.1";

    public static HttpClient Create(Uri baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            // Re-resolve DNS from time to time, e.g. when the API container is recreated.
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };

        if (baseUrl.IsLoopback)
        {
            // "dotnet run" serves https://localhost with the development certificate, which is
            // often not trusted yet (always on Linux). For localhost only, accept that one
            // certificate even when the OS does not trust it. Every other host - and any
            // other certificate - goes through the normal TLS validation.
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                errors == SslPolicyErrors.None || IsAspNetCoreDevelopmentCertificate(certificate);
        }

        // /api/test/slow can take up to 10 seconds, so leave room above that.
        return new HttpClient(handler) { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(30) };
    }

    private static bool IsAspNetCoreDevelopmentCertificate(X509Certificate? certificate) =>
        certificate is X509Certificate2 certificate2
        && certificate2.Extensions.Any(extension => extension.Oid?.Value == AspNetCoreDevelopmentCertificateOid);
}
