using Balsm.SharedKernel.Domain;

namespace Balsm.Entity.Domain;

public sealed class Workspace : AggregateRoot
{
    private Workspace() { }

    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public WorkspaceStatus Status { get; private set; } = WorkspaceStatus.Active;
    public string LocaleDefault { get; private set; } = "en";

    public static Workspace Create(string name, string slug, string locale = "en")
    {
        return new Workspace
        {
            Name = name.Trim(),
            Slug = slug.ToLowerInvariant().Trim(),
            LocaleDefault = locale,
            Status = WorkspaceStatus.Active,
        };
    }

    public void Rename(string newName)
    {
        Name = newName.Trim();
    }

    public void UpdateLocale(string locale)
    {
        LocaleDefault = locale;
    }
}
