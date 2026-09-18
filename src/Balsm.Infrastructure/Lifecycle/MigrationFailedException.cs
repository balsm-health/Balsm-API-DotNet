namespace Balsm.Infrastructure.Lifecycle;

/// Thrown from <see cref="MigrationRunner"/> when migrations cannot be applied.
///
/// Startup must abort rather than continue: a server that stays up with an
/// unmigrated database answers 503 to every route forever while still reporting
/// "Healthy", which is indistinguishable from a migration still in progress.
/// Throwing from <c>IHostedService.StartAsync</c> stops the host before Kestrel
/// binds, so the failure is loud and the real cause is the exception you see.
///
/// <see cref="DatabaseConnectionFailedException"/> narrows this to the common
/// case where the database simply was not reachable.
public class MigrationFailedException : Exception
{
    public MigrationFailedException(Exception inner)
        : base("Database migrations failed — the server cannot start. See the inner exception.", inner)
    {
    }

    protected MigrationFailedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
