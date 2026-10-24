using System.IO;
using Balsm.CareDirectory.Api.Controllers;
using Balsm.CareDirectory.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Balsm.CareDirectory.Tests;

public sealed class CareControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IHostEnvironment _env = Substitute.For<IHostEnvironment>();
    private readonly CareController _controller;

    public CareControllerTests()
    {
        _env.ContentRootPath.Returns(AppContext.BaseDirectory);
        _controller = new CareController(_mediator, _env)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        _controller.HttpContext.Request.Scheme = "http";
        _controller.HttpContext.Request.Host = new HostString("localhost", 5050);
    }

    [Fact]
    public async Task Packs_ReturnsUrlsAsIs_WithoutHostRewriting()
    {
        var basemap = new MapPackArtifactDto(
            "20260913", 1000, "sha256basemap", "https://cdn.balsm.health/packs/cairo-20260913.pmtiles", null);
        var places = new MapPackArtifactDto(
            "20260914", 500, "sha256places", "/care/packs/places/cairo-20260914.ndjson.gz", 100);
        var pack = new MapPackDto("cairo", "Cairo", [31, 29, 32, 30], basemap, places);

        _mediator.Send(Arg.Any<GetMapPacksQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<MapPackDto> { pack });

        var actionResult = await _controller.Packs("en", default);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dataProp = okResult.Value?.GetType().GetProperty("data");
        var resolvedPacks = Assert.IsAssignableFrom<IEnumerable<MapPackDto>>(dataProp?.GetValue(okResult.Value));
        var resolved = Assert.Single(resolvedPacks);

        // CDN URLs pass through unchanged.
        Assert.Equal("https://cdn.balsm.health/packs/cairo-20260913.pmtiles", resolved.Basemap.Url);
        // Relative URLs are returned as-is (client resolves against its baseUrl).
        Assert.Equal("/care/packs/places/cairo-20260914.ndjson.gz", resolved.Places.Url);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("dir/file.txt")]
    [InlineData("file.txt")]
    [InlineData("cairo.pmtiles")]
    [InlineData("CAIRO-20260914.ndjson.gz")]       // uppercase rejected
    [InlineData("-20260914.ndjson.gz")]              // starts with dash
    [InlineData("cairo-2026091.ndjson.gz")]          // date too short
    [InlineData("cairo-202609140.ndjson.gz")]        // date too long
    [InlineData("")]
    [InlineData(" ")]
    public void DownloadPlaces_RejectsInvalidFilenames(string file)
    {
        var result = _controller.DownloadPlaces(file);
        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public void DownloadPlaces_ReturnsNotFound_WhenFileDoesNotExist()
    {
        var result = _controller.DownloadPlaces("nonexistent-20260914.ndjson.gz");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DownloadPlaces_ReturnsPhysicalFile_WhenFileExists()
    {
        var testDir = Path.Combine(AppContext.BaseDirectory, "data", "map-packs", "places");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "testgov-20260914.ndjson.gz");
        File.WriteAllBytes(testFile, [1, 2, 3]);

        try
        {
            var result = _controller.DownloadPlaces("testgov-20260914.ndjson.gz");
            var physicalFile = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal("application/x-ndjson", physicalFile.ContentType);
            Assert.True(physicalFile.EnableRangeProcessing);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }
}
