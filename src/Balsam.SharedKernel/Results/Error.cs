namespace Balsam.SharedKernel.Results;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NotFound = new("NotFound", "The requested resource was not found.");
    public static readonly Error Unauthorized = new("Unauthorized", "Unauthorized access.");
    public static readonly Error Forbidden = new("Forbidden", "Access is forbidden.");
    public static readonly Error Conflict = new("Conflict", "A conflict occurred.");
    public static readonly Error ValidationFailed = new("ValidationFailed", "One or more validation errors occurred.");
}
