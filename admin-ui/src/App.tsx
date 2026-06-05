import './styles.css';
import { useState, useEffect } from 'react';
import { SetupPage } from './pages/SetupPage';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { EntityManagementPage } from './pages/EntityManagementPage';
import { BackupsPage } from './pages/BackupsPage';
import { AuditPage } from './pages/AuditPage';
import { ModePage } from './pages/ModePage';
import { Sidebar, TopBar, EN, AR } from './components/shell';
import type { Screen } from './components/shell';
import type { Dir, ServerState, DeployMode, DashLayout } from './data';

type AppView = 'setup' | 'login' | 'panel';

export function App() {
  // --- App-level state ---
  const [view, setView] = useState<AppView | null>(null);

  // Determine initial view from server
  useEffect(() => {
    fetch('/api/v1/admin/auth/status', { credentials: 'include' })
      .then(r => r.json())
      .then((d: { setupComplete: boolean }) => setView(d.setupComplete ? 'login' : 'setup'))
      .catch(() => setView('login'));
  }, []);
  const [lang, setLang] = useState<'en' | 'ar'>('en');
  const [dir, setDir] = useState<Dir>('ltr');
  const [screen, setScreen] = useState<Screen>('dashboard');
  const [serverState, setServerState] = useState<ServerState>('healthy');
  const [mode, setMode] = useState<DeployMode>('standalone');
  const [dashLayout] = useState<DashLayout>('hero');
  const [justBackedUp, setJustBackedUp] = useState(false);
  const [alerts] = useState<Partial<Record<Screen, boolean>>>({ backups: false });

  const t = lang === 'ar' ? AR : EN;

  const handleLang = (l: 'en' | 'ar') => {
    setLang(l);
    setDir(l === 'ar' ? 'rtl' : 'ltr');
    document.documentElement.dir = l === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = l;
  };

  const handleScreen = (s: Screen | '__logout') => {
    if (s === '__logout') { setView('login'); return; }
    if (s === '__backupnow' as unknown as Screen) {
      setJustBackedUp(true);
      return;
    }
    setScreen(s as Screen);
  };

  // --- Setup flow ---
  if (view === null) return null;

  if (view === 'setup') {
    return <SetupPage dir={dir} onLocale={handleLang} onFinish={(locale) => { handleLang(locale); setView('login'); }} />;
  }

  // --- Login flow ---
  if (view === 'login') {
    return <LoginPage dir={dir} onLogin={() => setView('panel')} />;
  }

  // --- Panel ---
  return (
    <div className="admin" dir={dir}>
      <Sidebar screen={screen} onScreen={handleScreen} t={t} alerts={alerts} />
      <TopBar
        screen={screen}
        t={t}
        dir={dir}
        lang={lang}
        onLang={handleLang}
        state={serverState}
        mode={mode}
        navTabs={false}
        onScreen={s => setScreen(s)}
        alerts={alerts}
      />
      <main className="main">
        {screen === 'dashboard' && (
          <DashboardPage
            layout={dashLayout}
            state={serverState}
            mode={mode}
            dir={dir}
            onScreen={s => setScreen(s)}
            onBackupNow={() => { setJustBackedUp(true); setScreen('backups'); }}
          />
        )}
        {screen === 'entities' && <EntityManagementPage dir={dir} />}
        {screen === 'backups' && (
          <BackupsPage
            dir={dir}
            justBackedUp={justBackedUp}
            onRestore={() => setServerState('restoring')}
            onBackupNow={() => setJustBackedUp(true)}
          />
        )}
        {screen === 'audit' && <AuditPage dir={dir} />}
        {screen === 'mode' && <ModePage mode={mode} onMode={m => setMode(m)} dir={dir} />}
      </main>
    </div>
  );
}
