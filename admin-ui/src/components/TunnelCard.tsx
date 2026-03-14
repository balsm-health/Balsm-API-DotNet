import { useCallback, useEffect, useState } from 'react'
import { api, TunnelStatus } from '../api'

interface Props {
  currentMode: string | null
}

export function TunnelCard({ currentMode }: Props) {
  const [tunnel, setTunnel] = useState<TunnelStatus | null>(null)
  const [loading, setLoading] = useState(false)
  const [tokenOpen, setTokenOpen] = useState(false)
  const [token, setToken] = useState('')
  const [tokenMsg, setTokenMsg] = useState('')
  const [copied, setCopied] = useState(false)

  const loadStatus = useCallback(async () => {
    try {
      const s = await api.getTunnelStatus()
      setTunnel(s)
    } catch {
      // ignore
    }
  }, [])

  useEffect(() => {
    loadStatus()
  }, [loadStatus])

  // Poll while loading (waiting for URL)
  useEffect(() => {
    if (!loading) return
    const id = setInterval(async () => {
      const s = await api.getTunnelStatus()
      setTunnel(s)
      if (s.tunnelUrl || s.error || !s.isRunning) {
        setLoading(false)
      }
    }, 3000)
    return () => clearInterval(id)
  }, [loading])

  if (currentMode !== 'public') return null
  if (!tunnel) return null

  async function handleQuickStart() {
    setLoading(true)
    const result = await api.startTunnel('quick')
    if (result.ok) {
      setTunnel(result.data)
      if (!result.data.tunnelUrl) {
        // Still starting, polling will pick it up
      } else {
        setLoading(false)
      }
    } else {
      setLoading(false)
    }
  }

  async function handleNamedStart() {
    if (!token.trim()) return
    setLoading(true)
    const result = await api.startTunnel('named', token.trim())
    if (result.ok) {
      setTunnel(result.data)
      setLoading(false)
    } else {
      setLoading(false)
    }
  }

  async function handleStop() {
    await api.stopTunnel()
    await loadStatus()
  }

  async function handleSaveToken() {
    if (!token.trim()) return
    const result = await api.saveTunnelToken(token.trim())
    setTokenMsg(result.ok ? 'Token saved.' : 'Failed to save token.')
    setTimeout(() => setTokenMsg(''), 3000)
  }

  function handleCopy() {
    if (tunnel?.tunnelUrl) {
      navigator.clipboard.writeText(tunnel.tunnelUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  if (!tunnel.cloudflaredInstalled) {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Cloudflare Tunnel</h2>
          <span className="badge badge-warning">Not Installed</span>
        </div>
        <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
          Install <code>cloudflared</code> to enable public tunnels without port forwarding.
        </p>
        <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginTop: 8 }}>
          Visit <strong>developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads</strong> for
          installation instructions.
        </p>
      </div>
    )
  }

  return (
    <div className="card">
      <div className="card-header">
        <h2>Cloudflare Tunnel</h2>
        {tunnel.isRunning
          ? <span className="badge badge-success">Active</span>
          : <span className="badge badge-info">Inactive</span>}
      </div>

      {tunnel.isRunning && tunnel.tunnelUrl && (
        <div style={{ marginBottom: 12, padding: 12, background: '#f0fdf4', borderRadius: 'var(--radius)' }}>
          <div className="info-row" style={{ borderBottom: 'none' }}>
            <span className="info-label">Public URL</span>
            <span className="info-value" style={{ fontSize: '0.8rem' }}>{tunnel.tunnelUrl}</span>
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
            <button className="btn btn-outline btn-sm" onClick={handleCopy}>
              {copied ? 'Copied!' : 'Copy URL'}
            </button>
            <button className="btn btn-danger btn-sm" onClick={handleStop}>
              Stop Tunnel
            </button>
          </div>
          {tunnel.tunnelType === 'quick' && (
            <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: 8, marginBottom: 0 }}>
              Quick tunnel URL changes on each restart. Use a Named Tunnel for a permanent URL.
            </p>
          )}
        </div>
      )}

      {tunnel.error && (
        <div className="error-message">{tunnel.error}</div>
      )}

      {loading && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
          <div className="spinner" style={{ width: 16, height: 16, borderWidth: 2, marginBottom: 0 }} />
          <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>Starting tunnel...</span>
        </div>
      )}

      {!tunnel.isRunning && !loading && (
        <div style={{ marginTop: 4 }}>
          <button className="btn btn-primary" style={{ width: 'auto', marginBottom: 12 }} onClick={handleQuickStart}>
            Start Quick Tunnel
          </button>
          <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: 16 }}>
            Free, no account needed. Generates a temporary public URL.
          </p>

          <button className="collapsible-trigger" onClick={() => setTokenOpen(!tokenOpen)}>
            Named Tunnel (Permanent URL)
          </button>
          {tokenOpen && (
            <div style={{ marginTop: 8 }}>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 8 }}>
                Paste your Cloudflare Tunnel token for a permanent custom domain.
              </p>
              <div className="form-group">
                <input
                  type="password"
                  placeholder="Tunnel token"
                  value={token}
                  onChange={e => setToken(e.target.value)}
                />
              </div>
              {tokenMsg && <p style={{ fontSize: '0.8rem', color: 'var(--success)', marginBottom: 8 }}>{tokenMsg}</p>}
              <div style={{ display: 'flex', gap: 8 }}>
                <button className="btn btn-primary btn-sm" style={{ width: 'auto' }} onClick={handleNamedStart}
                  disabled={!token.trim()}>
                  Connect
                </button>
                <button className="btn btn-outline btn-sm" onClick={handleSaveToken}
                  disabled={!token.trim()}>
                  Save Token
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
