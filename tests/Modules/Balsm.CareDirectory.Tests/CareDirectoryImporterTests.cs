using Balsm.CareDirectory.Domain;
using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.Import;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// Import behaviour against a six-row fixture covering the shapes that actually
/// occur: a high-confidence row, one below the global floor, one that only passes
/// under a per-type override, an Arabic-only name, a bilingual name needing a
/// split, and a row with no phone.
/// </summary>
public sealed class CareDirectoryImporterTests : IDisposable
{
    private readonly CareDirectoryDbContext _db;
    private readonly SqliteConnection _connection;

    public CareDirectoryImporterTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CareDirectoryDbContext(
            new DbContextOptionsBuilder<CareDirectoryDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IDomainEventDispatcher>());
        _db.Database.EnsureCreated();
    }

    private static Stream Fixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "care_places.sample.ndjson"));

    private Task<ImportResult> ImportAsync(CareDirectoryOptions? options = null) =>
        new CareDirectoryImporter(_db, options ?? new CareDirectoryOptions()).ImportAsync(Fixture(), default);

    [Fact]
    public async Task Import_SkipsRowsBelowTheConfidenceFloor()
    {
        var result = await ImportAsync();

        result.Skipped.Should().Be(2, "one row is at 0.42 and one at 0.55, both under the 0.65 default");
        _db.CarePlaces.Should().OnlyContain(p => p.Confidence >= 0.65);
        _db.CarePlaces.Should().NotContain(p => p.ExternalId == "fixture-below-floor");
    }

    [Fact]
    public async Task Import_AppliesPerTypeFloor()
    {
        // scan is starved at the global floor — 177 rows nationally — so it gets
        // its own threshold rather than dragging the floor down for every type.
        await ImportAsync(new CareDirectoryOptions
        {
            MinConfidence = 0.65,
            MinConfidenceByType = new Dictionary<string, double> { ["scan"] = 0.50 }
        });

        var scan = await _db.CarePlaces.SingleOrDefaultAsync(p => p.ExternalId == "fixture-scan-midconfidence");
        scan.Should().NotBeNull();
        scan!.Confidence.Should().BeLessThan(0.65);
    }

    [Fact]
    public async Task Import_IsIdempotent()
    {
        var first = await ImportAsync();
        var countAfterFirst = await _db.CarePlaces.CountAsync();

        var second = await ImportAsync();

        (await _db.CarePlaces.CountAsync()).Should().Be(countAfterFirst, "re-import upserts, never duplicates");
        second.Inserted.Should().Be(0);
        second.Updated.Should().Be(first.Inserted);
    }

    [Fact]
    public async Task Import_PreservesRowIdentityAcrossReimport()
    {
        await ImportAsync();
        var before = await _db.CarePlaces.AsNoTracking().SingleAsync(p => p.ExternalId == "fixture-high-confidence");

        await ImportAsync();
        var after = await _db.CarePlaces.AsNoTracking().SingleAsync(p => p.ExternalId == "fixture-high-confidence");

        // Identity and age survive a refresh — that is what anything keyed to this
        // row (curated overrides, user reports) depends on.
        after.Id.Should().Be(before.Id);
        after.CreatedAt.Should().Be(before.CreatedAt);
    }

    [Fact]
    public async Task Import_SplitsABilingualName()
    {
        await ImportAsync();

        var row = await _db.CarePlaces.SingleAsync(p => p.ExternalId == "fixture-bilingual");
        row.NameEn.Should().Be("Fixture Dental Clinic");
        row.NameAr.Should().Be("عيادة الاختبار للاسنان");
    }

    [Fact]
    public async Task Import_FilesASingleScriptArabicNameUnderArabicOnly()
    {
        await ImportAsync();

        var row = await _db.CarePlaces.SingleAsync(p => p.ExternalId == "fixture-arabic");
        row.NameAr.Should().Be("معمل الاختبار للاشعة");
        row.NameEn.Should().BeNull("no transliteration — a mangled name is worse than an absent one");
        row.AddressAr.Should().Be("شارع الاختبار، القاهرة");
    }

    [Fact]
    public async Task Import_PopulatesNormalisedArabicColumns()
    {
        await ImportAsync();

        var row = await _db.CarePlaces.SingleAsync(p => p.ExternalId == "fixture-arabic");
        row.NameArNorm.Should().Be(ArabicText.Normalize(row.NameAr));
    }

    [Fact]
    public async Task Import_KeepsNullPhoneRatherThanInventingOne()
    {
        await ImportAsync();

        var row = await _db.CarePlaces.SingleAsync(p => p.ExternalId == "fixture-no-phone");
        row.Phone.Should().BeNull();
        row.Hours.Should().BeNull("Overture has no opening-hours field");
        row.Rating.Should().BeNull("no lawful free source supplies ratings");
    }

    [Fact]
    public async Task Import_RecordsProvenance()
    {
        await ImportAsync();

        _db.CarePlaces.Should().OnlyContain(p => p.Source == "overture" && p.ExternalId != null);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        _connection.Dispose();
    }
}
