using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Balsm.Supervisor.Security;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.Supervisor.Tests.Security;

[Collection(ProcessStateCollection.Name)]
public sealed class CertificateServiceTests : IDisposable
{
    private readonly BaseDirectoryFileScope _certFile = new("supervisor.pfx");

    public CertificateServiceTests()
    {
        ResetFingerprintCache();
    }

    [Fact]
    public void EnsureCertificate_WhenMissing_CreatesOwnerOnlySelfSignedCertificate()
    {
        using var cert = CertificateService.EnsureCertificate(NullLogger.Instance)!;

        cert.Subject.Should().Contain("CN=Balsm Healthcare Platform");
        cert.HasPrivateKey.Should().BeTrue();
        File.Exists(_certFile.FilePath).Should().BeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(_certFile.FilePath)
                .Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void EnsureCertificate_WhenMissing_CoversLocalHostnames()
    {
        using var cert = CertificateService.EnsureCertificate(NullLogger.Instance)!;

        var san = cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
        san.EnumerateDnsNames().Should().BeEquivalentTo("localhost", "balsm.local", "api.balsm.local");
    }

    [Fact]
    public void EnsureCertificate_WhenPresent_LoadsSameCertificate()
    {
        using var created = CertificateService.EnsureCertificate(NullLogger.Instance)!;

        using var loaded = CertificateService.EnsureCertificate(NullLogger.Instance)!;

        loaded.Thumbprint.Should().Be(created.Thumbprint);
    }

    [Fact]
    public void GetFingerprint_ReturnsBase64UrlSha256()
    {
        using var cert = CertificateService.EnsureCertificate(NullLogger.Instance)!;

        var fingerprint = CertificateService.GetFingerprint(cert);

        fingerprint.Should().HaveLength(43).And.MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void TryGetFingerprint_WhenNoCertificate_ReturnsNull()
    {
        var fingerprint = CertificateService.TryGetFingerprint();

        fingerprint.Should().BeNull();
    }

    [Fact]
    public void TryGetFingerprint_WhenCertificatePresent_ReturnsAndCachesFingerprint()
    {
        using var cert = CertificateService.EnsureCertificate(NullLogger.Instance)!;
        var expected = CertificateService.GetFingerprint(cert);

        var first = CertificateService.TryGetFingerprint();
        File.Delete(_certFile.FilePath);
        var second = CertificateService.TryGetFingerprint();

        first.Should().Be(expected);
        second.Should().Be(expected);
    }

    [Fact]
    public void TryGetFingerprint_WhenCertificateUnreadable_ReturnsNull()
    {
        File.WriteAllText(_certFile.FilePath, "not a pfx");

        var fingerprint = CertificateService.TryGetFingerprint();

        fingerprint.Should().BeNull();
    }

    public void Dispose()
    {
        ResetFingerprintCache();
        _certFile.Dispose();
    }

    private static void ResetFingerprintCache()
        => typeof(CertificateService)
            .GetField("_cachedFingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);
}
