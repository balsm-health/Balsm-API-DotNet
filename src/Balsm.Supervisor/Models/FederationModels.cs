namespace Balsm.Supervisor.Models;

// --- Enums ---

public enum PairingDirection { Bidirectional, PushOnly, PullOnly }
public enum PairingStatus { Pending, Active, Paused, Disconnected }

// --- Persisted Models ---

public sealed class FederationData
{
    public List<ServerPairing> Pairings { get; set; } = [];
}

public sealed class ServerPairing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ServerUrl { get; set; } = "";
    public string OurApiKey { get; set; } = "";
    public string TheirApiKey { get; set; } = "";
    public PairingDirection Direction { get; set; } = PairingDirection.Bidirectional;
    public List<string> SyncEntities { get; set; } = [];
    public PairingStatus Status { get; set; } = PairingStatus.Pending;
    public DateTime PairedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
}

// --- In-memory ---

public sealed class PairingCode
{
    public string Code { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string OurApiKey { get; set; } = "";
}

// --- Request/Response DTOs ---

public sealed class GenerateCodeResponse
{
    public string Code { get; set; } = "";
    public int ExpiresInSeconds { get; set; }
}

public sealed class InitiatePairingRequest
{
    public string ServerUrl { get; set; } = "";
    public string Code { get; set; } = "";
}

public sealed class PairRequest
{
    public string Code { get; set; } = "";
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ServerUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public sealed class PairResponse
{
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public sealed class PairingListResponse
{
    public List<PairingSummary> Pairings { get; set; } = [];
}

public sealed class PairingSummary
{
    public Guid Id { get; set; }
    public string ServerId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public string ServerUrl { get; set; } = "";
    public string Direction { get; set; } = "";
    public List<string> SyncEntities { get; set; } = [];
    public string Status { get; set; } = "";
    public DateTime PairedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
}

public sealed class SyncBatch
{
    public string SourceServerId { get; set; } = "";
    public long SequenceNumber { get; set; }
    public List<SyncRecord> Records { get; set; } = [];
    public DateTime SentAt { get; set; }
}

public sealed class SyncRecord
{
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Operation { get; set; } = "";
    public string JsonPayload { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public sealed class SyncAckRequest
{
    public long SequenceNumber { get; set; }
    public string ServerId { get; set; } = "";
}

public sealed class HeartbeatResponse
{
    public string ServerId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
