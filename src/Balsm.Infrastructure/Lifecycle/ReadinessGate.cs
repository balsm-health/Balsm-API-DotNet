namespace Balsm.Infrastructure.Lifecycle;

public sealed class ReadinessGate
{
    private volatile bool _isReady = true;
    private volatile string? _reason;
    private readonly object _lock = new();

    public bool IsReady => _isReady;
    public string? Reason => _reason;

    public void SetNotReady(string reason)
    {
        lock (_lock)
        {
            _reason = reason;
            _isReady = false;
        }
    }

    public void SetReady()
    {
        lock (_lock)
        {
            _reason = null;
            _isReady = true;
        }
    }
}
