using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Balsm.API.Tests.Diagnostics;

/// <summary>Collects formatted log lines so a test can assert what was logged.</summary>
internal sealed class ListLogger<T>(LogLevel minimum = LogLevel.Trace) : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries => [.. _entries];

    public IReadOnlyList<string> Messages => [.. _entries.Select(e => e.Message)];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel)) _entries.Enqueue((logLevel, formatter(state, exception), exception));
    }
}
