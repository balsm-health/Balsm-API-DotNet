import { useState } from 'react'
import { api, UpdateInfo } from '../api'

export function UpdateCard() {
  const [checking, setChecking] = useState(false)
  const [applying, setApplying] = useState(false)
  const [update, setUpdate] = useState<UpdateInfo | null>(null)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  async function checkUpdate() {
    setChecking(true)
    setError('')
    setUpdate(null)

    const result = await api.checkUpdate()
    setChecking(false)
    if (result.ok) {
      setUpdate(result.data)
    } else {
      setError((result.data as { message?: string }).message || 'Failed to check for updates.')
    }
  }

  async function applyUpdate() {
    setApplying(true)
    setError('')

    const result = await api.applyUpdate()
    if (result.ok) {
      setSuccess('Update applied. Server is restarting...')
      setTimeout(() => window.location.reload(), 8000)
    } else {
      setError(result.data.message || 'Update failed.')
      setApplying(false)
    }
  }

  return (
    <div className="card">
      <div className="card-header">
        <h2>Software Update</h2>
      </div>

      {error && <div className="error-message">{error}</div>}
      {success && <div className="success-message">{success}</div>}

      {checking && (
        <>
          <div className="spinner" style={{ margin: '8px 0' }} />
          <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>Checking...</p>
        </>
      )}

      {applying && (
        <>
          <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)' }}>
            Downloading and applying update...
          </p>
          <div className="update-progress">
            <div className="progress-bar">
              <div className="progress-bar-fill" style={{ width: '100%' }} />
            </div>
          </div>
        </>
      )}

      {!checking && !applying && !success && update && update.isUpdateAvailable && (
        <>
          <div className="info-table">
            <div className="info-row">
              <span className="info-label">Current</span>
              <span className="info-value">{update.currentVersion}</span>
            </div>
            <div className="info-row">
              <span className="info-label">Latest</span>
              <span className="info-value">{update.latestVersion}</span>
            </div>
          </div>
          {update.releaseNotes && (
            <p style={{
              fontSize: '0.8rem',
              color: 'var(--text-muted)',
              margin: '8px 0',
              whiteSpace: 'pre-wrap',
            }}>
              {update.releaseNotes.substring(0, 500)}
            </p>
          )}
          <button className="btn btn-primary btn-sm" onClick={applyUpdate}>
            Apply Update
          </button>
        </>
      )}

      {!checking && !applying && !success && update && !update.isUpdateAvailable && (
        <>
          <p style={{ fontSize: '0.85rem', color: 'var(--success)' }}>
            You are running the latest version ({update.currentVersion}).
          </p>
          <button
            className="btn btn-outline btn-sm"
            style={{ marginTop: 8 }}
            onClick={checkUpdate}
          >
            Check Again
          </button>
        </>
      )}

      {!checking && !applying && !success && !update && !error && (
        <button className="btn btn-outline btn-sm" onClick={checkUpdate}>
          Check for Updates
        </button>
      )}

      {!checking && !applying && !success && !update && error && (
        <button className="btn btn-outline btn-sm" style={{ marginTop: 8 }} onClick={checkUpdate}>
          Retry
        </button>
      )}
    </div>
  )
}
