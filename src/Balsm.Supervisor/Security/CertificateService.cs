using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Balsm.Supervisor.Security;

public static class CertificateService
{
    private const string CertFileName = "supervisor.pfx";
    private const string CertPassword = "balsm-self-signed";

    public static X509Certificate2? EnsureCertificate(ILogger logger)
    {
        var certPath = Path.Combine(AppContext.BaseDirectory, CertFileName);

        if (File.Exists(certPath))
        {
            logger.LogInformation(
                "Loading existing self-signed certificate from {Path}", certPath);

            return X509CertificateLoader.LoadPkcs12FromFile(
                certPath, CertPassword);
        }

        logger.LogInformation("Generating new self-signed certificate...");

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Balsm Healthcare Platform, O=Balsm",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("balsm.local");
        sanBuilder.AddDnsName("api.balsm.local");   // API served on the api.<host> subdomain
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5));

        var pfxBytes = cert.Export(X509ContentType.Pfx, CertPassword);
        File.WriteAllBytes(certPath, pfxBytes);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(certPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        logger.LogInformation(
            "Self-signed certificate saved to {Path} (valid 5 years)", certPath);

        return X509CertificateLoader.LoadPkcs12(
            pfxBytes, CertPassword);
    }

    public static string GetFingerprint(X509Certificate2 cert)
    {
        var sha256 = cert.GetCertHash(HashAlgorithmName.SHA256);
        return Convert.ToBase64String(sha256)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string? _cachedFingerprint;

    /// <summary>
    /// Base64url SHA-256 fingerprint of the on-disk certificate, or null if no
    /// certificate has been provisioned yet. Loads the existing PFX without a logger
    /// so unauthenticated callers (e.g. <c>GET /api/v1/server-info</c>) can expose the
    /// fingerprint for client trust-pinning in any deployment mode. Cached after first read.
    /// </summary>
    public static string? TryGetFingerprint()
    {
        if (_cachedFingerprint is not null)
            return _cachedFingerprint;

        var certPath = Path.Combine(AppContext.BaseDirectory, CertFileName);
        if (!File.Exists(certPath))
            return null;

        try
        {
            using var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, CertPassword);
            _cachedFingerprint = GetFingerprint(cert);
            return _cachedFingerprint;
        }
        catch
        {
            return null;
        }
    }
}
