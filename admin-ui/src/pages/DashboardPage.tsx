import { useCallback, useEffect, useRef, useState } from 'react'
import { api, ApiStatus, NetworkInfo } from '../api'
import { StatusCard } from '../components/StatusCard'
import { NetworkCard } from '../components/NetworkCard'
import { TunnelCard } from '../components/TunnelCard'
import { FederationCard } from '../components/FederationCard'
import { ModeCard } from '../components/ModeCard'
import { UpdateCard } from '../components/UpdateCard'
import { SettingsCard } from '../components/SettingsCard'

export function DashboardPage({ onLogout }: { onLogout: () => void }) {
  const [status, setStatus] = useState<ApiStatus | null>(null)
  const [network, setNetwork] = useState<NetworkInfo | null>(null)
  const [statusError, setStatusError] = useState(false)
  const [networkError, setNetworkError] = useState(false)
  const [restarting, setRestarting] = useState<string | null>(null)
  const timerRef = useRef<ReturnType<typeof setInterval>>(null)

  const loadStatus = useCallback(async () => {
    try {
      const s = await api.getStatus()
      setStatus(s)
      setStatusError(false)
    } catch (err) {
      if (err instanceof Error && err.message === 'unauthorized') {
        onLogout()
        return
      }
      setStatusError(true)
    }
  }, [onLogout])

  const loadNetwork = useCallback(async () => {
    try {
      const n = await api.getNetwork()
      setNetwork(n)
      setNetworkError(false)
    } catch {
      setNetworkError(true)
    }
  }, [])

  const refresh = useCallback(async () => {
    await Promise.all([loadStatus(), loadNetwork()])
  }, [loadStatus, loadNetwork])

  useEffect(() => {
    refresh()
    timerRef.current = setInterval(loadStatus, 15000)
    return () => {
      if (timerRef.current) clearInterval(timerRef.current)
    }
  }, [refresh, loadStatus])

  async function handleLogout() {
    if (timerRef.current) clearInterval(timerRef.current)
    await api.logout()
    onLogout()
  }

  async function handleRestart() {
    if (!confirm('Restart the server?')) return
    const result = await api.restart()
    if (result.ok) {
      setRestarting('Server restarting...')
      setTimeout(() => window.location.reload(), 5000)
    }
  }

  async function handleSwitchMode(mode: string) {
    if (mode === status?.mode) return
    if (!confirm(`Switch to ${mode} mode? The server will restart.`)) return

    const port = parseInt(window.location.port) || 5050
    const result = await api.changeMode(mode, port)
    if (result.ok) {
      setRestarting(`Restarting in ${mode} mode...`)
      setTimeout(() => window.location.reload(), 5000)
    } else {
      alert('Failed to switch mode: ' + (result.data.message || 'Unknown error'))
    }
  }

  if (restarting) {
    return (
      <div className="loading-container">
        <div className="spinner" />
        <p>{restarting}</p>
      </div>
    )
  }

  return (
    <div className="dashboard">
      <div className="dashboard-header">
        <h1>Balsam Admin</h1>
        <div className="dashboard-header-actions">
          <button className="btn btn-outline btn-sm" onClick={refresh}>
            Refresh
          </button>
          <button className="btn btn-outline btn-sm" onClick={handleLogout}>
            Sign Out
          </button>
        </div>
      </div>

      <StatusCard
        status={status}
        error={statusError}
        onRestart={handleRestart}
      />
      <NetworkCard network={network} error={networkError} />
      <TunnelCard currentMode={status?.mode ?? null} />
      <FederationCard />
      <ModeCard
        currentMode={status?.mode ?? null}
        onSwitch={handleSwitchMode}
      />
      <UpdateCard />
      <SettingsCard />
    </div>
  )
}
