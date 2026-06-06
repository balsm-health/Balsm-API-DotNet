import './styles.css';
import { useState, useEffect, useCallback } from 'react';
import { SetupPage } from './pages/SetupPage';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { BackupsPage } from './pages/BackupsPage';
import { AuditPage } from './pages/AuditPage';
import { ModePage } from './pages/ModePage';
import { Sidebar, TopBar, EN, AR } from './components/shell';
import type { Screen } from './components/shell';
import type { Dir, ServerState, DeployMode, DashLayout } from './data';
import { api, apiFetch } from './api';
import type { ApiStatus, NetworkInfo, WorkspaceDto } from './api';

type AppView = 'setup' | 'login' | 'panel';

/** Backend reports mode as local|network|public; the UI models it as standalone|network|public. */
function toDeployMode(mode: string | null | undefined): DeployMode {
  if (mode === 'network') return 'network';
  if (mode === 'public') return 'public';
  return 'standalone';
}

// ── URL routing ────────────────────────────────────────────────────────────────
// The SPA is served at the host root (balsm.local/). Each panel screen maps to a
// path so the address bar, back/forward, and refresh/deep-links all work.
const PANEL_SCREENS: Screen[] = ['dashboard', 'mode', 'backups', 'audit'];

function screenFromPath(): Screen {
  const seg = window.location.pathname.replace(/^\//, '').split('/')[0];
  return (PANEL_SCREENS as string[]).includes(seg) ? (seg as Screen) : 'dashboard';
}

function pathForScreen(s: Screen): string {
  return s === 'dashboard' ? '/' : `/${s}`;
}

export function App() {
  // --- App-level state ---
  const [view, setView] = useState<AppView | null>(null);

  // Determine initial view from server
  useEffect(() => {
    apiFetch('/api/v1/admin/auth/status')
      .then(r => r.json())
      .then((d: { setupComplete: boolean }) => setView(d.setupComplete ? 'login' : 'setup'))
      .catch(() => setView('login'));
  }, []);
  const [lang, setLang] = useState<'en' | 'ar'>('en');
  const [dir, setDir] = useState<Dir>('ltr');
  const [screen, setScreen] = useState<Screen>(() => screenFromPath());
  const [dashLayout] = useState<DashLayout>('hero');
  const [justBackedUp, setJustBackedUp] = useState(false);

  // Browser back/forward → sync the active screen from the URL.
  useEffect(() => {
    const onPop = () => setScreen(screenFromPath());
    window.addEventListener('popstate', onPop);
    return () => window.removeEventListener('popstate', onPop);
  }, []);

  // Reflect the auth view (setup/login) in the URL without polluting history.
  useEffect(() => {
    if (view === 'setup' && window.location.pathname !== '/setup')
      window.history.replaceState(null, '', '/setup');
    if (view === 'login' && window.location.pathname !== '/login')
      window.history.replaceState(null, '', '/login');
  }, [view]);

  // Panel screen → URL. pushState so back/forward navigates between pages.
  useEffect(() => {
    if (view !== 'panel') return;
    const target = pathForScreen(screen);
    if (window.location.pathname !== target)
      window.history.pushState(null, '', target);
  }, [view, screen]);

  // --- Live server data ---
  const [status, setStatus] = useState<ApiStatus | null>(null);
  const [network, setNetwork] = useState<NetworkInfo | null>(null);
  const [workspace, setWorkspace] = useState<WorkspaceDto | null>(null);
  const [serverState, setServerState] = useState<ServerState>('healthy');

  const mode: DeployMode = toDeployMode(status?.mode);
  const alerts: Partial<Record<Screen, boolean>> = {};

  const refreshServerData = useCallback(() => {
    api.getStatus().then(setStatus).catch(() => setStatus(null));
    api.getNetwork().then(setNetwork).catch(() => setNetwork(null));
    api.getWorkspace().then(setWorkspace).catch(() => setWorkspace(null));
  }, []);

  // Fetch live data once we reach the panel.
  useEffect(() => {
    if (view === 'panel') refreshServerData();
  }, [view, refreshServerData]);

  const t = lang === 'ar' ? AR : EN;

  const handleLang = (l: 'en' | 'ar') => {
    setLang(l);
    setDir(l === 'ar' ? 'rtl' : 'ltr');
    document.documentElement.dir = l === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = l;
  };

  const handleScreen = (s: Screen | '__logout') => {
    if (s === '__logout') { api.logout().finally(() => setView('login')); return; }
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
  const workspaceName = workspace?.name ?? '';
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
        workspace={workspaceName}
      />
      <main className="main">
        {screen === 'dashboard' && (
          <DashboardPage
            layout={dashLayout}
            state={serverState}
            mode={mode}
            dir={dir}
            status={status}
            network={network}
            workspace={workspaceName}
            onScreen={s => setScreen(s)}
            onBackupNow={() => { setJustBackedUp(true); setScreen('backups'); }}
          />
        )}
        {screen === 'backups' && (
          <BackupsPage
            dir={dir}
            justBackedUp={justBackedUp}
            onRestore={() => setServerState('restoring')}
            onBackupNow={() => setJustBackedUp(true)}
          />
        )}
        {screen === 'audit' && <AuditPage dir={dir} />}
        {screen === 'mode' && (
          <ModePage
            mode={mode}
            status={status}
            network={network}
            onMode={() => refreshServerData()}
            dir={dir}
          />
        )}
      </main>
    </div>
  );
}
