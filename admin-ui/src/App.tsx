import { useEffect, useState } from 'react'
import { Routes, Route, Navigate, useNavigate } from 'react-router-dom'
import { api } from './api'
import { SetupPage } from './pages/SetupPage'
import { LoginPage } from './pages/LoginPage'
import { DashboardPage } from './pages/DashboardPage'

export function App() {
  const [loading, setLoading] = useState(true)
  const [setupComplete, setSetupComplete] = useState(false)
  const [authenticated, setAuthenticated] = useState(false)
  const navigate = useNavigate()

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const { setupComplete: done } = await api.getAuthStatus()
        if (cancelled) return
        setSetupComplete(done)

        if (done) {
          try {
            await api.getStatus()
            if (!cancelled) setAuthenticated(true)
          } catch {
            // not authenticated
          }
        }
      } catch {
        // server unavailable
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => { cancelled = true }
  }, [])

  if (loading) {
    return (
      <div className="loading-container">
        <div className="spinner" />
        <p>Loading...</p>
      </div>
    )
  }

  return (
    <Routes>
      <Route
        path="/setup"
        element={
          setupComplete
            ? <Navigate to="/" replace />
            : <SetupPage onComplete={() => {
                setSetupComplete(true)
                setAuthenticated(true)
                navigate('/')
              }} />
        }
      />
      <Route
        path="/login"
        element={
          !setupComplete
            ? <Navigate to="/setup" replace />
            : authenticated
              ? <Navigate to="/" replace />
              : <LoginPage onLogin={() => {
                  setAuthenticated(true)
                  navigate('/')
                }} />
        }
      />
      <Route
        path="/*"
        element={
          !setupComplete
            ? <Navigate to="/setup" replace />
            : !authenticated
              ? <Navigate to="/login" replace />
              : <DashboardPage onLogout={() => {
                  setAuthenticated(false)
                  navigate('/login')
                }} />
        }
      />
    </Routes>
  )
}
