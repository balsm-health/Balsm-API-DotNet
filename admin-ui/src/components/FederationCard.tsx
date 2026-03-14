import { useCallback, useEffect, useRef, useState } from 'react'
import { api, PairingSummary } from '../api'

export function FederationCard() {
  const [pairings, setPairings] = useState<PairingSummary[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Generate code state
  const [generatedCode, setGeneratedCode] = useState<string | null>(null)
  const [codeExpiry, setCodeExpiry] = useState(0)
  const codeTimerRef = useRef<ReturnType<typeof setInterval>>(null)

  // Connect form state
  const [connectOpen, setConnectOpen] = useState(false)
  const [connectUrl, setConnectUrl] = useState('')
  const [connectCode, setConnectCode] = useState('')
  const [connecting, setConnecting] = useState(false)

  const loadPairings = useCallback(async () => {
    try {
      const data = await api.getFederationPairings()
      setPairings(data.pairings)
    } catch {
      // ignore
    }
  }, [])

  useEffect(() => {
    loadPairings()
    const id = setInterval(loadPairings, 30000)
    return () => clearInterval(id)
  }, [loadPairings])

  // Code expiry countdown
  useEffect(() => {
    if (codeExpiry <= 0) {
      if (codeTimerRef.current) clearInterval(codeTimerRef.current)
      if (generatedCode) setGeneratedCode(null)
      return
    }
    codeTimerRef.current = setInterval(() => {
      setCodeExpiry(prev => {
        if (prev <= 1) {
          setGeneratedCode(null)
          return 0
        }
        return prev - 1
      })
    }, 1000)
    return () => {
      if (codeTimerRef.current) clearInterval(codeTimerRef.current)
    }
  }, [codeExpiry > 0]) // eslint-disable-line react-hooks/exhaustive-deps

  async function handleGenerateCode() {
    setLoading(true)
    setError(null)
    const result = await api.generatePairingCode()
    if (result.ok) {
      setGeneratedCode(result.data.code)
      setCodeExpiry(result.data.expiresInSeconds)
    } else {
      setError('Failed to generate code')
    }
    setLoading(false)
  }

  async function handleConnect() {
    if (!connectUrl.trim() || !connectCode.trim()) return
    setConnecting(true)
    setError(null)
    const result = await api.initiatePairing(connectUrl.trim(), connectCode.trim())
    if (result.ok) {
      setConnectUrl('')
      setConnectCode('')
      setConnectOpen(false)
      await loadPairings()
    } else {
      setError(result.data.message || 'Failed to connect')
    }
    setConnecting(false)
  }

  async function handleRemove(id: string) {
    if (!confirm('Remove this server pairing? The remote server will no longer sync.')) return
    await api.removePairing(id)
    await loadPairings()
  }

  async function handlePause(id: string) {
    await api.pausePairing(id)
    await loadPairings()
  }

  async function handleResume(id: string) {
    await api.resumePairing(id)
    await loadPairings()
  }

  function formatTime(iso: string | null) {
    if (!iso) return 'Never'
    const d = new Date(iso)
    return d.toLocaleString()
  }

  function statusBadge(status: string) {
    switch (status.toLowerCase()) {
      case 'active':
        return <span className="badge badge-success">Active</span>
      case 'paused':
        return <span className="badge badge-warning">Paused</span>
      case 'disconnected':
        return <span className="badge badge-danger">Disconnected</span>
      default:
        return <span className="badge badge-info">{status}</span>
    }
  }

  const activeCount = pairings.filter(p => p.status.toLowerCase() === 'active').length

  return (
    <div className="card">
      <div className="card-header">
        <h2>Connected Servers</h2>
        {pairings.length > 0 && (
          <span className="badge badge-info">{activeCount}/{pairings.length}</span>
        )}
      </div>

      {/* Pairing list */}
      {pairings.length > 0 && (
        <div style={{ marginBottom: 16 }}>
          {pairings.map(p => (
            <div key={p.id} style={{
              padding: 12,
              marginBottom: 8,
              background: 'var(--bg-secondary, #f9fafb)',
              borderRadius: 'var(--radius)',
              border: '1px solid var(--border, #e5e7eb)',
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
                <strong style={{ fontSize: '0.9rem' }}>{p.serverName}</strong>
                {statusBadge(p.status)}
              </div>
              <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 8 }}>
                <div>{p.serverUrl}</div>
                <div>Last heartbeat: {formatTime(p.lastHeartbeatAt)}</div>
                <div>Paired: {formatTime(p.pairedAt)}</div>
              </div>
              <div style={{ display: 'flex', gap: 6 }}>
                {p.status.toLowerCase() === 'active' || p.status.toLowerCase() === 'disconnected' ? (
                  <button className="btn btn-outline btn-sm" onClick={() => handlePause(p.id)}>
                    Pause
                  </button>
                ) : (
                  <button className="btn btn-outline btn-sm" onClick={() => handleResume(p.id)}>
                    Resume
                  </button>
                )}
                <button className="btn btn-danger btn-sm" onClick={() => handleRemove(p.id)}>
                  Remove
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {pairings.length === 0 && (
        <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)', marginBottom: 16 }}>
          No servers connected. Generate a pairing code or connect to another server.
        </p>
      )}

      {/* Error display */}
      {error && (
        <div className="error-message" style={{ marginBottom: 12 }}>{error}</div>
      )}

      {/* Generate Code section */}
      {generatedCode ? (
        <div style={{
          padding: 12,
          marginBottom: 12,
          background: '#eff6ff',
          borderRadius: 'var(--radius)',
          textAlign: 'center',
        }}>
          <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 8 }}>
            Share this code with the other server:
          </p>
          <div style={{
            fontSize: '1.8rem',
            fontWeight: 700,
            letterSpacing: '0.3em',
            fontFamily: 'monospace',
          }}>
            {generatedCode}
          </div>
          <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: 8 }}>
            Expires in {Math.floor(codeExpiry / 60)}:{(codeExpiry % 60).toString().padStart(2, '0')}
          </p>
        </div>
      ) : (
        <button
          className="btn btn-primary btn-sm"
          style={{ width: 'auto', marginBottom: 8 }}
          onClick={handleGenerateCode}
          disabled={loading}
        >
          Generate Pairing Code
        </button>
      )}

      {/* Connect to Server section */}
      <button
        className="collapsible-trigger"
        onClick={() => setConnectOpen(!connectOpen)}
        style={{ marginTop: 4 }}
      >
        Connect to Another Server
      </button>
      {connectOpen && (
        <div style={{ marginTop: 8 }}>
          <div className="form-group">
            <label style={{ fontSize: '0.8rem' }}>Server URL</label>
            <input
              type="url"
              placeholder="https://other-server.example.com"
              value={connectUrl}
              onChange={e => setConnectUrl(e.target.value)}
            />
          </div>
          <div className="form-group">
            <label style={{ fontSize: '0.8rem' }}>Pairing Code</label>
            <input
              type="text"
              placeholder="A7K3X9"
              value={connectCode}
              onChange={e => setConnectCode(e.target.value.toUpperCase())}
              maxLength={6}
              style={{ letterSpacing: '0.2em', fontFamily: 'monospace' }}
            />
          </div>
          <button
            className="btn btn-primary btn-sm"
            style={{ width: 'auto' }}
            onClick={handleConnect}
            disabled={connecting || !connectUrl.trim() || !connectCode.trim()}
          >
            {connecting ? 'Connecting...' : 'Connect'}
          </button>
        </div>
      )}
    </div>
  )
}
