using System.Reflection;

namespace Balsm.Disclosure.Application;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
