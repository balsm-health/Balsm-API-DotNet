namespace Balsm.Account.Domain.Entities;

public sealed class ReservedHandleBlocklist
{
    public string HandleNormalized { get; private set; } = string.Empty;
    public string AddedBy { get; private set; } = "system";
    public DateTime AddedAt { get; private set; } = DateTime.UtcNow;

    private ReservedHandleBlocklist() { }

    public static ReservedHandleBlocklist Create(string handle, string addedBy = "system") =>
        new() { HandleNormalized = handle.ToLowerInvariant(), AddedBy = addedBy };
}
