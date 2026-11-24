using Balsm.SharedKernel.Results;

namespace Balsm.Account.Domain;

public static class AccountErrors
{
    public static readonly Error NotFound = new("Account.NotFound", "Account not found.");
}
