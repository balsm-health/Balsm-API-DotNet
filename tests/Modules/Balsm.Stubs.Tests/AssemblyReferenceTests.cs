using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Balsm.Stubs.Tests;

/// <summary>
/// Each stub module's <c>AssemblyReference</c> marker must point at its own
/// Domain assembly — it is what scanning (handlers, configurations) keys on
/// once the module gains real types.
/// </summary>
public sealed class AssemblyReferenceTests
{
    public static TheoryData<Assembly, string> Markers() => new()
    {
        { Balsm.Customer.Domain.AssemblyReference.Assembly, "Balsm.Customer.Domain" },
        { Balsm.Inventory.Domain.AssemblyReference.Assembly, "Balsm.Inventory.Domain" },
        { Balsm.POS.Domain.AssemblyReference.Assembly, "Balsm.POS.Domain" },
        { Balsm.Prescription.Domain.AssemblyReference.Assembly, "Balsm.Prescription.Domain" },
    };

    [Theory]
    [MemberData(nameof(Markers))]
    public void Assembly_OfAStubModule_IsThatModulesDomainAssembly(Assembly marker, string expectedName)
    {
        var name = marker.GetName().Name;

        name.Should().Be(expectedName);
    }
}
