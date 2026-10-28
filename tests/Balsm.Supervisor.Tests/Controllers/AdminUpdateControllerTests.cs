using System.Net;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Controllers;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using Balsm.Supervisor.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using static Balsm.Supervisor.Tests.Support.ActionResultReader;

namespace Balsm.Supervisor.Tests.Controllers;

public class AdminUpdateControllerTests
{
    private static readonly Uri GitHubBase = new("https://api.github.example/");

    private readonly FakeHostApplicationLifetime _lifetime = new();

    [Fact]
    public async Task Check_WhenReleaseFound_ReturnsUpdateInfo()
    {
        var sut = CreateSut(ReleaseHandler(downloadStatus: HttpStatusCode.OK));

        var result = await sut.Check();

        ((ObjectResult)result).Value.Should().BeOfType<UpdateInfoResponse>()
            .Which.Should().Match<UpdateInfoResponse>(u => u.LatestVersion == "999.0.0" && u.IsUpdateAvailable);
    }

    [Fact]
    public async Task Check_WhenGitHubUnreachable_Returns502()
    {
        var sut = CreateSut(new StubHttpMessageHandler(_ => throw new HttpRequestException("dns failure")));

        var result = await sut.Check();

        StatusOf(result).Should().Be(502);
    }

    [Fact]
    public async Task Apply_BeforeCheck_Returns400AndDoesNotRestart()
    {
        var sut = CreateSut(ReleaseHandler(downloadStatus: HttpStatusCode.OK));

        var result = await sut.Apply();

        StatusOf(result).Should().Be(400);
        _lifetime.StopRequests.Should().Be(0);
    }

    [Fact]
    public async Task Apply_WhenDownloadFails_Returns502AndDoesNotRestart()
    {
        var sut = CreateSut(ReleaseHandler(downloadStatus: HttpStatusCode.InternalServerError));
        await sut.Check();

        var result = await sut.Apply();

        StatusOf(result).Should().Be(502);
        _lifetime.StopRequests.Should().Be(0);
    }

    [Fact]
    public async Task Apply_WhenChecksumMismatches_Returns400AndDoesNotRestart()
    {
        var sut = CreateSut(ReleaseHandler(downloadStatus: HttpStatusCode.OK));
        await sut.Check();

        var result = await sut.Apply();

        StatusOf(result).Should().Be(400);
        BodyOf(result).GetProperty("Message").GetString().Should().Contain("SHA256 mismatch");
        _lifetime.StopRequests.Should().Be(0);
    }

    private static StubHttpMessageHandler ReleaseHandler(HttpStatusCode downloadStatus)
    {
        var rid = SelfUpdateService.GetCurrentRid();
        var release = $$"""
            {
              "tag_name": "v999.0.0",
              "html_url": "https://github.example/releases/v999.0.0",
              "body": "Release notes",
              "assets": [
                { "name": "balsm-{{rid}}.zip", "browser_download_url": "https://downloads.example.test/balsm-{{rid}}.zip" },
                { "name": "checksums-sha256.txt", "browser_download_url": "https://downloads.example.test/checksums-sha256.txt" }
              ]
            }
            """;

        return new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            var p when p.EndsWith("/releases/latest", StringComparison.Ordinal)
                => StubHttpMessageHandler.Json(HttpStatusCode.OK, release),
            var p when p.EndsWith(".zip", StringComparison.Ordinal)
                => new HttpResponseMessage(downloadStatus) { Content = new ByteArrayContent([1, 2, 3]) },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{new string('0', 64)}  balsm-{rid}.zip\n")
            }
        });
    }

    private AdminUpdateController CreateSut(StubHttpMessageHandler handler)
        => new(new SelfUpdateService(
            Options.Create(new SupervisorOptions { GitHubRepository = "example/balsm" }),
            _lifetime,
            new StubHttpClientFactory(handler, GitHubBase),
            NullLogger<SelfUpdateService>.Instance));
}
