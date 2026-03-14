interface Props {
  currentMode: string | null
  onSwitch: (mode: string) => void
}

export function ModeCard({ currentMode, onSwitch }: Props) {
  return (
    <div className="card">
      <div className="card-header">
        <h2>Server Mode</h2>
      </div>
      <p style={{ fontSize: '0.85rem', color: 'var(--text-muted)', marginBottom: 8 }}>
        Switching mode will restart the server.
      </p>
      <div className="mode-switcher">
        <button
          className={`mode-option${currentMode === 'local' ? ' active' : ''}`}
          onClick={() => onSwitch('local')}
        >
          <div className="mode-label">Local</div>
          <div className="mode-desc">This machine only</div>
        </button>
        <button
          className={`mode-option${currentMode === 'network' ? ' active' : ''}`}
          onClick={() => onSwitch('network')}
        >
          <div className="mode-label">Network</div>
          <div className="mode-desc">Devices on same LAN</div>
        </button>
        <button
          className={`mode-option${currentMode === 'public' ? ' active' : ''}`}
          onClick={() => onSwitch('public')}
        >
          <div className="mode-label">Public</div>
          <div className="mode-desc">Access from any network</div>
        </button>
      </div>
    </div>
  )
}
