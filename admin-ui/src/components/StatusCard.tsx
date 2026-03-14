import { ApiStatus } from '../api'

function formatUptime(timespan: string): string {
  const parts = timespan.split(':')
  if (parts.length < 3) return timespan

  let dayPart = ''
  let hours = parts[0]
  if (hours.includes('.')) {
    const dp = hours.split('.')
    dayPart = dp[0] + 'd '
    hours = dp[1]
  }
  return dayPart + parseInt(hours) + 'h ' + parseInt(parts[1]) + 'm ' + parseInt(parts[2]) + 's'
}

interface Props {
  status: ApiStatus | null
  error: boolean
  onRestart: () => void
}

export function StatusCard({ status, error, onRestart }: Props) {
  if (error) {
    return (
      <div className="card">
        <h2>Server Status</h2>
        <p style={{ color: 'var(--danger)' }}>Failed to load status.</p>
      </div>
    )
  }

  if (!status) {
    return (
      <div className="card">
        <div className="spinner" style={{ margin: '8px 0' }} />
      </div>
    )
  }

  const uptime = status.uptime ? formatUptime(status.uptime) : 'N/A'

  return (
    <div className="card">
      <div className="card-header">
        <h2>Server Status</h2>
        {status.isRunning
          ? <span className="badge badge-success">Running</span>
          : <span className="badge badge-danger">Stopped</span>}
      </div>
      <div className="info-table">
        <div className="info-row">
          <span className="info-label">Mode</span>
          {status.mode === 'public'
            ? <span className="badge badge-warning">Public</span>
            : <span className="badge badge-info">Local</span>}
        </div>
        <div className="info-row">
          <span className="info-label">PID</span>
          <span className="info-value">{status.pid ?? 'N/A'}</span>
        </div>
        <div className="info-row">
          <span className="info-label">Uptime</span>
          <span className="info-value">{uptime}</span>
        </div>
        <div className="info-row">
          <span className="info-label">Version</span>
          <span className="info-value">{status.version ?? 'N/A'}</span>
        </div>
      </div>
      <div style={{ marginTop: 16, display: 'flex', gap: 8 }}>
        <button className="btn btn-outline btn-sm" onClick={onRestart}>
          Restart Server
        </button>
      </div>
    </div>
  )
}
