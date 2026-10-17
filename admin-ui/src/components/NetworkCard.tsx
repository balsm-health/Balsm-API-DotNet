import { NetworkInfo } from '../api'

interface Props {
  network: NetworkInfo | null
  error: boolean
}

export function NetworkCard({ network, error }: Props) {
  if (error) {
    return (
      <div className="card">
        <h2>Network</h2>
        <p style={{ color: 'var(--danger)' }}>Failed to load network info.</p>
      </div>
    )
  }

  if (!network) {
    return (
      <div className="card">
        <div className="spinner" style={{ margin: '8px 0' }} />
      </div>
    )
  }

  const port = window.location.port || '5050'

  return (
    <div className="card">
      <div className="card-header">
        <h2>Network &amp; IP Discovery</h2>
        {network.mdnsRegistered
          ? <span className="badge badge-success">mDNS Active</span>
          : <span className="badge badge-warning">mDNS Inactive</span>}
      </div>
      <div className="info-row">
        <span className="info-label">Hostname</span>
        <span className="info-value">{network.hostname}</span>
      </div>
      <ul className="address-list">
        {network.mdnsHostname && (
          <li>
            <span className="addr-url">
              http://{network.mdnsHostname}:{port}
            </span>
            <span className="addr-type">mDNS</span>
          </li>
        )}
        {network.lanAddresses.map((a, i) => (
          <li key={i}>
            <span className="addr-url">{a.apiUrl}</span>
            <span className="addr-type">
              {a.interfaceType} ({a.interfaceName})
            </span>
          </li>
        ))}
        {network.lanAddresses.length === 0 && (
          <li style={{ color: 'var(--text-muted)' }}>
            No LAN addresses detected.
          </li>
        )}
      </ul>
      {network.publicIp && (
        <div style={{ marginTop: 12, padding: 12, background: '#f5f5f5', borderRadius: 'var(--radius)' }}>
          <div className="info-row">
            <span className="info-label">Public IP</span>
            <span className="info-value">{network.publicIp}</span>
          </div>
          <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: 8, marginBottom: 0 }}>
            To allow access from outside your network, configure your router to forward
            port {port} to this machine's LAN IP address.
          </p>
        </div>
      )}
    </div>
  )
}
