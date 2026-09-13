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
    public async Task Packs_ResolvesRelativePlacesUrl_AgainstRequestHost()
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

        Assert.Equal("https://cdn.balsm.health/packs/cairo-20260913.pmtiles", resolved.Basemap.Url);
        Assert.Equal("http://localhost:5050/care/packs/places/cairo-20260914.ndjson.gz", resolved.Places.Url);
    }

    [Fact]
    public void DownloadPlaces_RejectsPathTraversal()
    {
        var result = _controller.DownloadPlaces("../secret.txt");
        Assert.IsType<BadRequestResult>(result);

        var result2 = _controller.DownloadPlaces("dir/file.txt");
        Assert.IsType<BadRequestResult>(result2);
    }

    [Fact]
    public void DownloadPlaces_ReturnsNotFound_WhenFileDoesNotExist()
    {
        var result = _controller.DownloadPlaces("nonexistent-file.ndjson.gz");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DownloadPlaces_ReturnsPhysicalFile_WhenFileExists()
    {
        var testDir = Path.Combine(AppContext.BaseDirectory, "data", "map-packs", "places");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "test-gov-20260914.ndjson.gz");
        File.WriteAllBytes(testFile, [1, 2, 3]);

        try
        {
            var result = _controller.DownloadPlaces("test-gov-20260914.ndjson.gz");
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
