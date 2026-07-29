using System.Reflection;

namespace Balsm.CareDirectory.Domain;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
