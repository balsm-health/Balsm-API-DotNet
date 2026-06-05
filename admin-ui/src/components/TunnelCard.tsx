import { useCallback, useEffect, useState } from 'react'
import { Icon } from './atoms'
import { api, TunnelStatus } from '../api'

interface Props {
  currentMode: string | null
}

export function TunnelCard({ currentMode }: Props) {
  const [tunnel, setTunnel] = useState<TunnelStatus | null>(null)
  const [loading, setLoading] = useState(false)
  const [copied, setCopied] = useState(false)
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const [manualToken, setManualToken] = useState('')
  const [showToken, setShowToken] = useState(false)

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

  // Poll while loading (waiting for tunnel to connect)
  useEffect(() => {
    if (!loading) return
    const id = setInterval(async () => {
      const s = await api.getTunnelStatus()
      setTunnel(s)
      if (s.isRunning || s.error) {
        setLoading(false)
      }
    }, 3000)
    return () => clearInterval(id)
  }, [loading])

  if (currentMode !== 'public') return null
  if (!tunnel) return null

  async function handleEnablePublicAccess() {
    setLoading(true)
    const result = await api.registerTunnel()
    if (result.ok) {
      setTunnel(result.data)
      if (!result.data.isRunning) {
        // Still starting, polling will pick it up
      } else {
        setLoading(false)
      }
    } else {
      const errData = result.data as unknown as { message?: string }
      setTunnel(prev => prev ? { ...prev, error: errData?.message || 'Registration failed' } : prev)
      setLoading(false)
    }
  }

  async function handleDisablePublicAccess() {
    if (!confirm('Disable public access? The public URL will stop working.')) return
    setLoading(true)
    await api.unregisterTunnel()
    await loadStatus()
    setLoading(false)
  }

  async function handleStop() {
    await api.stopTunnel()
    await loadStatus()
  }

  async function handleManualStart() {
    if (!manualToken.trim()) return
    setLoading(true)
    const result = await api.startTunnel('named', manualToken.trim())
    if (result.ok) {
      setTunnel(result.data)
    }
    setLoading(false)
  }

  function handleCopy() {
    const url = tunnel?.tunnelUrl || tunnel?.registeredUrl
    if (url) {
      navigator.clipboard.writeText(url)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  // cloudflared not installed
  if (!tunnel.cloudflaredInstalled) {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Public Access</h2>
          <span className="badge badge-warning">Setup Required</span>
        </div>
        <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
          Install <code>cloudflared</code> to enable public access without port forwarding.
        </p>
        <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginTop: 8 }}>
          Visit <strong>developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads</strong> for
          installation instructions.
        </p>
      </div>
    )
  }

  const displayUrl = tunnel.tunnelUrl || tunnel.registeredUrl
  const isRegistered = !!tunnel.registeredUrl

  return (
    <div className="card">
      <div className="card-header">
        <h2>Public Access</h2>
        {tunnel.isRunning
          ? <span className="badge badge-success">Active</span>
          : isRegistered
            ? <span className="badge badge-warning">Disconnected</span>
            : <span className="badge badge-info">Inactive</span>}
      </div>

      {/* Active tunnel with URL */}
      {tunnel.isRunning && displayUrl && (
        <div style={{ marginBottom: 12, padding: 12, background: '#f0fdf4', borderRadius: 'var(--radius)' }}>
          <div className="info-row" style={{ borderBottom: 'none' }}>
            <span className="info-label">Public URL</span>
            <span className="info-value" style={{ fontSize: '0.8rem' }}>{displayUrl}</span>
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
            <button className="btn btn-outline btn-sm" onClick={handleCopy}>
              {copied ? 'Copied!' : 'Copy URL'}
            </button>
            {isRegistered ? (
              <button className="btn btn-danger btn-sm" onClick={handleDisablePublicAccess}>
                Disable Public Access
              </button>
            ) : (
              <button className="btn btn-danger btn-sm" onClick={handleStop}>
                Stop Tunnel
              </button>
            )}
          </div>
        </div>
      )}

      {/* Registered but disconnected */}
      {!tunnel.isRunning && isRegistered && !loading && (
        <div style={{ marginBottom: 12, padding: 12, background: '#fef3c7', borderRadius: 'var(--radius)' }}>
          <p style={{ fontSize: '0.85rem', marginBottom: 8 }}>
            Tunnel is registered but not connected. It will auto-reconnect on server restart.
          </p>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-danger btn-sm" onClick={handleDisablePublicAccess}>
              Disable Public Access
            </button>
          </div>
        </div>
      )}

      {/* Error display */}
      {tunnel.error && (
        <div className="error-message">{tunnel.error}</div>
      )}

      {/* Loading spinner */}
      {loading && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
          <div className="spinner" style={{ width: 16, height: 16, borderWidth: 2, marginBottom: 0 }} />
          <span style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>Setting up public access...</span>
        </div>
      )}

      {/* Enable button (not registered, not running, not loading) */}
      {!isRegistered && !tunnel.isRunning && !loading && (
        <div style={{ marginTop: 4 }}>
          <button className="btn btn-primary" style={{ width: 'auto', marginBottom: 8 }} onClick={handleEnablePublicAccess}>
            Enable Public Access
          </button>
          <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: 16 }}>
            Get a permanent public URL for your server. Free, no account needed.
          </p>

          {/* Advanced: Manual token */}
          <button className="collapsible-trigger" onClick={() => setAdvancedOpen(!advancedOpen)}>
            Advanced: Use Your Own Tunnel
          </button>
          {advancedOpen && (
            <div style={{ marginTop: 8 }}>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginBottom: 8 }}>
                If you have your own Cloudflare account, paste your tunnel token to use a custom domain.
              </p>
              <div className="form-group">
                <span style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                  <input
                    type={showToken ? 'text' : 'password'}
                    placeholder="Tunnel token"
                    style={{ paddingRight: 36 }}
                    value={manualToken}
                    onChange={e => setManualToken(e.target.value)}
                  />
                  <button type="button" className="field-eye" tabIndex={-1} onMouseDown={e => e.preventDefault()} onClick={() => setShowToken(s => !s)} aria-label={showToken ? 'Hide' : 'Show'}>
                    <Icon name={showToken ? 'eye-off' : 'eye'} size={15} />
                  </button>
                </span>
              </div>
              <button className="btn btn-primary btn-sm" style={{ width: 'auto' }} onClick={handleManualStart}
                disabled={!manualToken.trim()}>
                Connect
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
