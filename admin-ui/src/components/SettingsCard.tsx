import { FormEvent, useState } from 'react'
import { Icon } from './atoms'
import { api } from '../api'

export function SettingsCard() {
  const [open, setOpen] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [showCurrent, setShowCurrent] = useState(false)
  const [showNew, setShowNew] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError('')
    setSuccess('')

    if (newPassword !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    if (newPassword.length < 8) {
      setError('Password must be at least 8 characters.')
      return
    }

    setSubmitting(true)
    const result = await api.changePassword(currentPassword, newPassword)
    if (result.ok) {
      setSuccess('Password updated. Please sign in again.')
      setTimeout(() => { window.location.href = '/login' }, 2000)
    } else {
      setError(result.data.message || 'Failed to update password.')
      setSubmitting(false)
    }
  }

  return (
    <div className="card">
      <div className="card-header">
        <h2>Settings</h2>
      </div>
      <button className="collapsible-trigger" onClick={() => setOpen(!open)}>
        Change Password
      </button>
      {open && (
        <div style={{ marginTop: 12 }}>
          {error && <div className="error-message">{error}</div>}
          {success && <div className="success-message">{success}</div>}
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="currentPassword">Current Password</label>
              <span style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                <input
                  type={showCurrent ? 'text' : 'password'}
                  id="currentPassword"
                  required
                  autoComplete="current-password"
                  style={{ paddingRight: 36 }}
                  value={currentPassword}
                  onChange={e => setCurrentPassword(e.target.value)}
                />
                <button type="button" className="field-eye" tabIndex={-1} onMouseDown={e => e.preventDefault()} onClick={() => setShowCurrent(s => !s)} aria-label={showCurrent ? 'Hide' : 'Show'}>
                  <Icon name={showCurrent ? 'eye-off' : 'eye'} size={15} />
                </button>
              </span>
            </div>
            <div className="form-group">
              <label htmlFor="newPassword">New Password</label>
              <span style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                <input
                  type={showNew ? 'text' : 'password'}
                  id="newPassword"
                  required
                  minLength={8}
                  autoComplete="new-password"
                  style={{ paddingRight: 36 }}
                  value={newPassword}
                  onChange={e => setNewPassword(e.target.value)}
                />
                <button type="button" className="field-eye" tabIndex={-1} onMouseDown={e => e.preventDefault()} onClick={() => setShowNew(s => !s)} aria-label={showNew ? 'Hide' : 'Show'}>
                  <Icon name={showNew ? 'eye-off' : 'eye'} size={15} />
                </button>
              </span>
              <small>Minimum 8 characters</small>
            </div>
            <div className="form-group">
              <label htmlFor="confirmNewPassword">Confirm New Password</label>
              <span style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
                <input
                  type={showConfirm ? 'text' : 'password'}
                  id="confirmNewPassword"
                  required
                  minLength={8}
                  autoComplete="new-password"
                  style={{ paddingRight: 36 }}
                  value={confirmPassword}
                  onChange={e => setConfirmPassword(e.target.value)}
                />
                <button type="button" className="field-eye" tabIndex={-1} onMouseDown={e => e.preventDefault()} onClick={() => setShowConfirm(s => !s)} aria-label={showConfirm ? 'Hide' : 'Show'}>
                  <Icon name={showConfirm ? 'eye-off' : 'eye'} size={15} />
                </button>
              </span>
            </div>
            <button
              type="submit"
              className="btn btn-primary btn-sm"
              disabled={submitting}
            >
              {submitting ? 'Updating...' : 'Update Password'}
            </button>
          </form>
        </div>
      )}
    </div>
  )
}
