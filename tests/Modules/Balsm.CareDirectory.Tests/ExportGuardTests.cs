using Xunit;
using Balsm.CareDirectory.Infrastructure.MapPacks;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// The nightly export job's one safety check: never let a truncated snapshot
/// overwrite a good one.
/// </summary>
public sealed class ExportGuardTests
{
    [Fact]
    public void AGovernorateWithNoPriorSnapshotAlwaysPublishes()
    {
        // Nothing to have dropped from — this is onboarding, not a regression.
        Assert.True(ExportGuard.ShouldPublish(previousCount: 0, newCount: 0));
        Assert.True(ExportGuard.ShouldPublish(previousCount: 0, newCount: 500));
    }

    [Fact]
    public void ADropBelowHalfIsRefused()
    {
        Assert.False(ExportGuard.ShouldPublish(previousCount: 1000, newCount: 499));
    }

    [Fact]
    public void ExactlyHalfIsAccepted()
    {
        Assert.True(ExportGuard.ShouldPublish(previousCount: 1000, newCount: 500));
    }

    [Fact]
    public void AnIncreaseIsAlwaysAccepted()
    {
        Assert.True(ExportGuard.ShouldPublish(previousCount: 1000, newCount: 1200));
    }

    [Fact]
    public void ASmallDropIsAccepted()
    {
        Assert.True(ExportGuard.ShouldPublish(previousCount: 1000, newCount: 950));
    }
}
