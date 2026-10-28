using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Tests.Auth;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

public class AdminAuthControllerTests
{
    private const string Username = "admin";
    private const string Password = "correct-horse-1";
    private const string CookieName = "balsm_admin_session";

    private readonly InMemoryCredentialStore _store = new();
    private readonly AdminAuthService _authService;
    private readonly AdminSessionService _sessionService = new();
    private readonly RecoveryCodeService _recoveryService;
    private readonly FakeFirstRunOrchestrator _firstRun = new();

    public AdminAuthControllerTests()
    {
        _authService = SupervisorFactory.CreateAuthService(_store);
        _recoveryService = new RecoveryCodeService(_store, NullLogger<RecoveryCodeService>.Instance);
    }

    [Fact]
    public async Task GetStatus_WhenSetupNotDone_ReportsSetupIncomplete()
    {
        var sut = CreateSut();

        var result = await sut.GetStatus();

        BodyOf(result).GetProperty("setupComplete").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Setup_WhenAlreadyComplete_Returns403()
    {
        await _authService.SetupAsync(Username, Password);
        var sut = CreateSut();

        var result = await sut.Setup(new SetupRequest { Username = "other", Password = "another-pass-1" });

        StatusOf(result).Should().Be(403);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData(Username, " ")]
    public async Task Setup_WhenUsernameOrPasswordBlank_Returns400(string username, string password)
    {
        var sut = CreateSut();

        var result = await sut.Setup(new SetupRequest { Username = username, Password = password });

        StatusOf(result).Should().Be(400);
    }

    [Fact]
    public async Task Setup_WhenPasswordTooShort_Returns400()
    {
        var sut = CreateSut();

        var result = await sut.Setup(new SetupRequest { Username = Username, Password = "short" });

        StatusOf(result).Should().Be(400);
        (await _authService.IsSetupCompleteAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Setup_WithDefaults_SeedsDefaultWorkspaceAndReturnsRecoveryCode()
    {
        var sut = CreateSut();

        var result = await sut.Setup(new SetupRequest { Username = Username, Password = Password });

        StatusOf(result).Should().Be(200);
        BodyOf(result).GetProperty("recoveryCode").GetString().Should().NotBeNullOrEmpty();
        _firstRun.Seeds.Should().ContainSingle().Which.Should().Be(("My Workspace", "my-workspace", "en"));
    }

    [Fact]
    public async Task Setup_WithWorkspaceAndLocale_PassesThemThrough()
    {
        var sut = CreateSut();

        await sut.Setup(new SetupRequest
        {
            Username = Username,
            Password = Password,
            WorkspaceName = "Clinic Example",
            WorkspaceSlug = "clinic-example",
            Locale = "ar"
        });

        _firstRun.Seeds.Should().ContainSingle().Which.Should().Be(("Clinic Example", "clinic-example", "ar"));
        (await _authService.GetLocaleAsync()).Should().Be("ar");
    }

    [Fact]
    public async Task Setup_OnLocalHost_SetsHostOnlyHttpOnlyStrictCookie()
    {
        var sut = CreateSut("localhost");

        await sut.Setup(new SetupRequest { Username = Username, Password = Password });

        var cookie = SessionCookie(sut);
        cookie.HttpOnly.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Strict);
        cookie.Domain.HasValue.Should().BeFalse();
        cookie.Path.Value.Should().Be("/");
        _sessionService.ValidateSession(Uri.UnescapeDataString(cookie.Value.Value!)).Should().BeTrue();
    }

    [Theory]
    [InlineData("api.balsm.local", "balsm.local")]
    [InlineData("balsm.local", "balsm.local")]
    [InlineData("api.clinic.example.health", "clinic.example.health")]
    public async Task Login_OnParentableHost_ScopesCookieToParentDomain(string host, string expectedDomain)
    {
        await _authService.SetupAsync(Username, Password);
        var sut = CreateSut(host);

        await sut.Login(new LoginRequest { Username = Username, Password = Password });

        SessionCookie(sut).Domain.Value.Should().Be(expectedDomain);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsLocale()
    {
        await _authService.SetupAsync(Username, Password, "ar");
        var sut = CreateSut();

        var result = await sut.Login(new LoginRequest { Username = Username, Password = Password });

        StatusOf(result).Should().Be(200);
        BodyOf(result).GetProperty("locale").GetString().Should().Be("ar");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithoutCookie()
    {
        await _authService.SetupAsync(Username, Password);
        var sut = CreateSut();

        var result = await sut.Login(new LoginRequest { Username = Username, Password = "wrong-password" });

        StatusOf(result).Should().Be(401);
        sut.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_WhenLockedOut_Returns423WithRemainingSeconds()
    {
        await _authService.SetupAsync(Username, Password);
        (await _store.LoadCredentialsAsync())!.LockoutEnd = DateTime.UtcNow.AddMinutes(10);
        var sut = CreateSut();

        var result = await sut.Login(new LoginRequest { Username = Username, Password = Password });

        StatusOf(result).Should().Be(423);
        BodyOf(result).GetProperty("lockoutRemaining").GetDouble().Should().BePositive();
        sut.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task Me_ReturnsStoredLocale()
    {
        await _authService.SetupAsync(Username, Password, "ar");
        var sut = CreateSut();

        var result = await sut.Me();

        BodyOf(result).GetProperty("locale").GetString().Should().Be("ar");
    }

    [Fact]
    public async Task UpdateLocale_WithUnsupportedLocale_Returns400()
    {
        await _authService.SetupAsync(Username, Password);
        var sut = CreateSut();

        var result = await sut.UpdateLocale(new UpdateLocaleRequest { Locale = "fr" });

        StatusOf(result).Should().Be(400);
        (await _authService.GetLocaleAsync()).Should().Be("en");
    }

    [Fact]
    public async Task UpdateLocale_WithSupportedLocale_PersistsIt()
    {
        await _authService.SetupAsync(Username, Password);
        var sut = CreateSut();

        var result = await sut.UpdateLocale(new UpdateLocaleRequest { Locale = "ar" });

        StatusOf(result).Should().Be(200);
        (await _authService.GetLocaleAsync()).Should().Be("ar");
    }

    [Fact]
    public void Logout_WithSessionCookie_InvalidatesSessionAndDeletesCookie()
    {
        var token = _sessionService.CreateSession(Username);
        var sut = CreateSut("balsm.local");
        sut.Request.Headers.Cookie = $"{CookieName}={token}";

        var result = sut.Logout();

        StatusOf(result).Should().Be(200);
        _sessionService.ValidateSession(token).Should().BeFalse();
        var deleted = SessionCookie(sut);
        deleted.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
        deleted.Domain.Value.Should().Be("balsm.local");
    }

    [Fact]
    public void Logout_WithoutCookie_StillSucceeds()
    {
        var other = _sessionService.CreateSession(Username);
        var sut = CreateSut();

        var result = sut.Logout();

        StatusOf(result).Should().Be(200);
        _sessionService.ValidateSession(other).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_RotatesAllSessions()
    {
        await _authService.SetupAsync(Username, Password);
        var oldToken = _sessionService.CreateSession(Username);
        var sut = CreateSut();

        var result = await sut.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = Password,
            NewPassword = "new-password-2"
        });

        StatusOf(result).Should().Be(200);
        _sessionService.ValidateSession(oldToken).Should().BeFalse();
        _sessionService.ValidateSession(Uri.UnescapeDataString(SessionCookie(sut).Value.Value!)).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401AndKeepsSessions()
    {
        await _authService.SetupAsync(Username, Password);
        var existing = _sessionService.CreateSession(Username);
        var sut = CreateSut();

        var result = await sut.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "not-the-password",
            NewPassword = "new-password-2"
        });

        StatusOf(result).Should().Be(401);
        _sessionService.ValidateSession(existing).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_WithShortNewPassword_Returns400()
    {
        await _authService.SetupAsync(Username, Password);
        var sut = CreateSut();

        var result = await sut.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = Password,
            NewPassword = "short"
        });

        StatusOf(result).Should().Be(400);
    }

    [Theory]
    [InlineData("", "new-password-2")]
    [InlineData("some-code", "")]
    public async Task UseRecoveryCode_WhenFieldMissing_Returns400(string code, string newPassword)
    {
        var sut = CreateSut();

        var result = await sut.UseRecoveryCode(new UseRecoveryCodeRequest { RecoveryCode = code, NewPassword = newPassword });

        StatusOf(result).Should().Be(400);
    }

    [Fact]
    public async Task UseRecoveryCode_WithShortPassword_Returns400WithoutConsumingCode()
    {
        await _authService.SetupAsync(Username, Password);
        var code = await _recoveryService.GenerateAsync();
        var sut = CreateSut();

        var result = await sut.UseRecoveryCode(new UseRecoveryCodeRequest { RecoveryCode = code, NewPassword = "short" });

        StatusOf(result).Should().Be(400);
        (await _store.LoadCredentialsAsync())!.RecoveryCodeUsedAt.Should().BeNull();
    }

    [Fact]
    public async Task UseRecoveryCode_WithWrongCode_Returns401AndKeepsPassword()
    {
        await _authService.SetupAsync(Username, Password);
        await _recoveryService.GenerateAsync();
        var sut = CreateSut();

        var result = await sut.UseRecoveryCode(new UseRecoveryCodeRequest
        {
            RecoveryCode = "not-a-valid-recovery-code",
            NewPassword = "new-password-2"
        });

        StatusOf(result).Should().Be(401);
        (await _authService.LoginAsync(Username, Password)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UseRecoveryCode_WithValidCode_ResetsPasswordAndRotatesSessions()
    {
        await _authService.SetupAsync(Username, Password);
        var code = await _recoveryService.GenerateAsync();
        var oldToken = _sessionService.CreateSession(Username);
        var sut = CreateSut();

        var result = await sut.UseRecoveryCode(new UseRecoveryCodeRequest
        {
            RecoveryCode = code,
            NewPassword = "new-password-2"
        });

        StatusOf(result).Should().Be(200);
        _sessionService.ValidateSession(oldToken).Should().BeFalse();
        (await _authService.LoginAsync(Username, "new-password-2")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UseRecoveryCode_WhenCodeAlreadyUsed_Returns401()
    {
        await _authService.SetupAsync(Username, Password);
        var code = await _recoveryService.GenerateAsync();
        var sut = CreateSut();
        await sut.UseRecoveryCode(new UseRecoveryCodeRequest { RecoveryCode = code, NewPassword = "new-password-2" });

        var result = await sut.UseRecoveryCode(new UseRecoveryCodeRequest { RecoveryCode = code, NewPassword = "new-password-3" });

        StatusOf(result).Should().Be(401);
    }

    [Fact]
    public async Task RegenerateRecoveryCode_ReturnsNewCodeAndRetiresOldOne()
    {
        await _authService.SetupAsync(Username, Password);
        var oldCode = await _recoveryService.GenerateAsync();
        var sut = CreateSut();

        var result = await sut.RegenerateRecoveryCode();

        var newCode = BodyOf(result).GetProperty("recoveryCode").GetString();
        newCode.Should().NotBe(oldCode);
        (await _recoveryService.ConsumeAsync(oldCode)).Should().BeFalse();
        (await _recoveryService.ConsumeAsync(newCode!)).Should().BeTrue();
    }

    private AdminAuthController CreateSut(string host = "localhost")
        => new(_authService, _sessionService, _recoveryService, _firstRun)
        {
            ControllerContext = SupervisorFactory.ControllerContextFor(host)
        };

    private static SetCookieHeaderValue SessionCookie(AdminAuthController sut)
        => SetCookieHeaderValue.ParseList(sut.Response.Headers.SetCookie.Select(v => v!).ToList())
            .Single(c => c.Name.Value == CookieName);
}
