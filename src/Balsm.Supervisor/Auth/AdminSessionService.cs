using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Balsm.Supervisor.Auth;

public sealed class AdminSessionService
{
    private readonly ConcurrentDictionary<string, AdminSession> _sessions = new();
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromHours(8);

    public string CreateSession(string username)
    {
        CleanExpiredSessions();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = new AdminSession
        {
            Username = username,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };
        return token;
    }

    public bool ValidateSession(string token)
    {
        if (!_sessions.TryGetValue(token, out var session)) return false;

        if (DateTime.UtcNow - session.LastActivity > SessionTimeout)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        session.LastActivity = DateTime.UtcNow;
        return true;
    }

    public void InvalidateSession(string token)
        => _sessions.TryRemove(token, out _);

    public void InvalidateAllSessions()
        => _sessions.Clear();

    private void CleanExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - SessionTimeout;
        foreach (var (key, session) in _sessions)
        {
            if (session.LastActivity < cutoff)
                _sessions.TryRemove(key, out _);
        }
    }
}

public sealed class AdminSession
{
    public string Username { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
}
