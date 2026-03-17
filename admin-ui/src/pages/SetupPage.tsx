import { FormEvent, useState } from 'react'
import { api } from '../api'

export function SetupPage({ onComplete }: { onComplete: () => void }) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setError('')

    if (password !== confirm) {
      setError('Passwords do not match.')
      return
    }
    if (password.length < 8) {
      setError('Password must be at least 8 characters.')
      return
    }

    setSubmitting(true)
    const result = await api.setup(username.trim(), password)
    if (result.ok) {
      onComplete()
    } else {
      setError(result.data.message || 'Setup failed.')
      setSubmitting(false)
    }
  }

  return (
    <div className="auth-container">
      <div className="auth-card">
        <h1>Balsm Healthcare Platform</h1>
        <h2>Initial Setup</h2>
        <p>Create your admin credentials to secure the admin panel.</p>
        {error && <div className="error-message">{error}</div>}
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="username">Username</label>
            <input
              type="text"
              id="username"
              required
              autoComplete="username"
              autoFocus
              value={username}
              onChange={e => setUsername(e.target.value)}
            />
          </div>
          <div className="form-group">
            <label htmlFor="password">Password</label>
            <input
              type="password"
              id="password"
              required
              minLength={8}
              autoComplete="new-password"
              value={password}
              onChange={e => setPassword(e.target.value)}
            />
            <small>Minimum 8 characters</small>
          </div>
          <div className="form-group">
            <label htmlFor="confirmPassword">Confirm Password</label>
            <input
              type="password"
              id="confirmPassword"
              required
              minLength={8}
              autoComplete="new-password"
              value={confirm}
              onChange={e => setConfirm(e.target.value)}
            />
          </div>
          <button type="submit" className="btn btn-primary" disabled={submitting}>
            {submitting ? 'Creating...' : 'Create Account'}
          </button>
        </form>
      </div>
    </div>
  )
}
