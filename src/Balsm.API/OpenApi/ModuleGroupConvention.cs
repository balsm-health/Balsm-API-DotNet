using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Balsm.API.OpenApi;

/// <summary>
/// Stamps every controller's <see cref="ControllerModel.ApiExplorer"/> GroupName with the
/// owning module's lowercase name, inferred from the assembly name pattern
/// <c>Balsm.{Module}.Api</c>. Used by per-module OpenAPI documents to filter operations.
/// </summary>
internal sealed partial class ModuleGroupConvention : IControllerModelConvention
{
    private static readonly Regex ModuleAssemblyPattern =
        BuildModulePattern();

    public void Apply(ControllerModel controller)
    {
        var assemblyName = controller.ControllerType.Assembly.GetName().Name ?? string.Empty;
        var match = ModuleAssemblyPattern.Match(assemblyName);

        var group = match.Success
            ? match.Groups["module"].Value.ToLowerInvariant()
            : assemblyName.ToLowerInvariant();

        controller.ApiExplorer.GroupName ??= group;
    }

    [GeneratedRegex(@"^Balsm\.(?<module>[A-Za-z0-9]+)\.(Api|Supervisor)$", RegexOptions.Compiled)]
    private static partial Regex BuildModulePattern();
}
