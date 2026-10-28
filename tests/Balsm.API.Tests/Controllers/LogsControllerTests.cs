using Balsm.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public sealed class LogsControllerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("balsm-logs-").FullName;

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ListFiles_DirectoryMissing_ReturnsNoItems()
    {
        var controller = ControllerFor(Path.Combine(_dir, "missing"));

        var body = ActionResultReader.Ok(controller.ListFiles());

        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void ListFiles_ReturnsOnlyLogFilesNewestFirst()
    {
        WriteLog("balsm-20260101.log", "old", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteLog("balsm-20260102.log", "new", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        File.WriteAllText(Path.Combine(_dir, "notes.txt"), "ignored");

        var body = ActionResultReader.Ok(ControllerFor(_dir).ListFiles());

        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString())
            .Should().Equal("balsm-20260102.log", "balsm-20260101.log");
    }

    [Fact]
    public void Tail_NamedFile_ReturnsItsLastLines()
    {
        WriteLog("balsm-20260101.log", "one\ntwo\nthree");

        var result = ControllerFor(_dir).Tail("balsm-20260101.log", lines: 2);

        result.Should().BeOfType<ContentResult>().Which.Content.Should().Be("two\nthree");
    }

    [Fact]
    public void Tail_NoFileNamed_ReadsTheNewestLog()
    {
        WriteLog("balsm-20260101.log", "old", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteLog("balsm-20260102.log", "new", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var result = ControllerFor(_dir).Tail(lines: 0);

        result.Should().BeOfType<ContentResult>().Which.Content.Should().Be("new");
    }

    [Fact]
    public void Tail_NoLogsAtAll_Returns404()
    {
        var result = ControllerFor(_dir).Tail(lines: 9999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Tail_DirectoryMissing_Returns404()
    {
        var result = ControllerFor(Path.Combine(_dir, "missing")).Tail();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData("../balsm.log")]
    [InlineData("balsm.txt")]
    [InlineData("absent.log")]
    public void Tail_UnsafeOrUnknownFile_Returns404(string file)
    {
        WriteLog("balsm.txt", "not a log");

        var result = ControllerFor(_dir).Tail(file);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Download_ExistingLog_StreamsIt()
    {
        WriteLog("balsm-20260101.log", "content");

        var result = ControllerFor(_dir).Download("balsm-20260101.log");

        using var stream = result.Should().BeOfType<FileStreamResult>().Subject.FileStream;
        new StreamReader(stream).ReadToEnd().Should().Be("content");
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing.log")]
    [InlineData("../../etc/passwd")]
    public void Download_UnsafeOrUnknownFile_Returns404(string file)
    {
        var result = ControllerFor(_dir).Download(file);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Download_DirectoryConfiguredWithTrailingSeparator_StillResolves()
    {
        WriteLog("balsm.log", "x");

        var result = ControllerFor(_dir + Path.DirectorySeparatorChar).Download("balsm.log");

        result.Should().BeOfType<FileStreamResult>().Subject.FileStream.Dispose();
    }

    [Fact]
    public void Ctor_NoDirectoryConfigured_UsesTheDefaultUnderTheBinary()
    {
        var controller = new LogsController(new ConfigurationBuilder().Build());

        var body = ActionResultReader.Ok(controller.ListFiles());

        body.GetProperty("directory").GetString()
            .Should().Be(Path.GetFullPath("var/logs", AppContext.BaseDirectory));
    }

    private static LogsController ControllerFor(string directory) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logs:Directory"] = directory })
            .Build());

    private void WriteLog(string name, string content, DateTime? lastWrite = null)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        if (lastWrite is { } at) File.SetLastWriteTimeUtc(path, at);
    }
}
