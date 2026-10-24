import { useState, useEffect } from 'react';
import { Icon, Btn, Pill, Card, CopyField } from '../components/atoms';
import type { Dir, ServerState, DeployMode, DashLayout, UptimeInfo } from '../data';
import { MODULE_COLORS, uptimeFromIso, formatBytes, formatTimestamp } from '../data';
import type { Screen } from '../components/shell';
import { api } from '../api';
import type { ApiStatus, NetworkInfo, AuditLogDto, BackupFileDto } from '../api';

function useUptime(startedAt: string | null | undefined): UptimeInfo {
  const [u, setU] = useState<UptimeInfo>(() => uptimeFromIso(startedAt));
  useEffect(() => {
    setU(uptimeFromIso(startedAt));
    const id = setInterval(() => setU(uptimeFromIso(startedAt)), 1000);
    return () => clearInterval(id);
  }, [startedAt]);
  return u;
}

export function StateBanner({ state, dir = 'ltr', onAct }: { state: ServerState; dir?: Dir; onAct?: (s: string) => void }) {
  if (state === 'healthy') return null;
  const isAr = dir === 'rtl';
  if (state === 'migrating') return (
    <div className="state-banner violet">
      <span className="ic spin"><Icon name="loader" size={20} /></span>
      <div className="bn-body">
        <b>{isAr ? 'جارٍ تطبيق ترحيلات قاعدة البيانات' : 'Applying database migrations'}</b>
        <span>{isAr ? 'يرفض الخادم كل الطلبات عدا الصحة (HTTP 503) حتى تكتمل الترقية. لا حاجة لأي إجراء.' : 'The server is refusing all non-health requests (HTTP 503) until the schema is consistent. No action needed.'}</span>
      </div>
    </div>
  );
  if (state === 'restoring') return (
    <div className="state-banner info">
      <span className="ic spin"><Icon name="loader" size={20} /></span>
      <div className="bn-body">
        <b>{isAr ? 'جارٍ الاستعادة من نسخة احتياطية' : 'Restore in progress'}</b>
        <span>{isAr ? 'الخادم للقراءة فقط حتى تكتمل الاستعادة وإعادة التشغيل.' : 'The server is read-only until the restore completes and it restarts.'}</span>
      </div>
    </div>
  );
  if (state === 'offline') return (
    <div className="state-banner warn">
      <span className="ic"><Icon name="wifi-off" size={20} /></span>
      <div className="bn-body">
        <b>{isAr ? 'وضع مستقل — الاكتشاف معطّل' : 'Standalone mode — discovery suppressed'}</b>
        <span>{isAr ? 'يستجيب الخادم على هذا الجهاز فقط (localhost). لن تكتشفه الأجهزة الأخرى عبر mDNS.' : 'The server only answers on this machine (localhost). Other devices cannot discover it over mDNS.'}</span>
      </div>
      <div className="bn-actions"><Btn variant="secondary" size="sm" icon="router" onClick={() => onAct?.('mode')}>{isAr ? 'تغيير الوضع' : 'Change mode'}</Btn></div>
    </div>
  );
  if (state === 'backupfail') return (
    <div className="state-banner danger">
      <span className="ic"><Icon name="alert-triangle" size={20} /></span>
      <div className="bn-body">
        <b>{isAr ? 'فشلت النسخة الاحتياطية المجدولة' : 'Scheduled backup failed'}</b>
        <span>{isAr ? 'لم تكتمل آخر نسخة مجدولة. آخر نسخة سليمة محفوظة.' : 'The most recent scheduled backup did not complete. Your last known-good backup is preserved.'}</span>
      </div>
      <div className="bn-actions">
        <Btn variant="secondary" size="sm" onClick={() => onAct?.('audit')}>{isAr ? 'السجل' : 'View log'}</Btn>
        <Btn variant="danger" size="sm" icon="refresh-cw" onClick={() => onAct?.('backups')}>{isAr ? 'إعادة المحاولة' : 'Retry'}</Btn>
      </div>
    </div>
  );
  return null;
}

function StatusRing({ state }: { state: ServerState }) {
  const cls = state === 'offline' ? 'down' : (state === 'migrating' || state === 'restoring') ? 'degraded' : '';
  const icon = state === 'offline' ? 'wifi-off' : (state === 'migrating' || state === 'restoring') ? 'loader' : 'check';
  return (
    <div className={`ring ${cls}`}>
      <Icon name={icon} size={32} stroke={2.2} className={state === 'migrating' || state === 'restoring' ? 'spin' : ''} />
    </div>
  );
}

function HeroFacts({ mode, status, network }: { mode: DeployMode; status: ApiStatus | null; network: NetworkInfo | null }) {
  const host = network?.mdnsHostname ?? network?.hostname ?? '—';
  const certShort = status?.certSha256 ? status.certSha256.slice(0, 17) + '…' : '—';
  const facts = [
    { k: 'HTTPS', v: status ? `:${status.httpsPort}` : '—', ic: 'lock' },
    { k: 'Host', v: host, ic: 'server' },
    { k: 'Cert · SHA-256', v: certShort, ic: 'shield-check' },
    { k: 'mDNS', v: mode === 'standalone' ? 'suppressed' : (network?.mdnsRegistered ? 'advertised' : 'pending'), ic: 'radio' },
  ];
  return (
    <div className="hero-facts">
      {facts.map(f => (
        <div className="f" key={f.k}>
          <span className="k">{f.k}</span>
          <span className="v"><Icon name={f.ic} size={13} /> {f.v}</span>
        </div>
      ))}
    </div>
  );
}

function statusLabel(state: ServerState, dir: Dir = 'ltr') {
  const isAr = dir === 'rtl';
  if (state === 'offline') return isAr ? 'يعمل · محلي' : 'Running · local';
  if (state === 'migrating') return isAr ? 'ترحيل' : 'Migrating';
  if (state === 'restoring') return isAr ? 'استعادة' : 'Restoring';
  return isAr ? 'يعمل' : 'Running';
}

function StatCards({ uptime, mode, status, lastBackup, dir = 'ltr', onScreen }: { uptime: UptimeInfo; mode: DeployMode; status: ApiStatus | null; lastBackup: BackupFileDto | null; dir?: Dir; onScreen: (s: Screen) => void }) {
  const isAr = dir === 'rtl';
  const modeLabels: Record<DeployMode, string> = { standalone: isAr ? 'مستقل' : 'Standalone', network: isAr ? 'شبكة' : 'Network', public: isAr ? 'عام' : 'Public' };
  const db = formatBytes(status?.dbSizeBytes);
  const backupSize = lastBackup ? formatBytes(lastBackup.sizeBytes) : null;
  const cards = [
    { lab: isAr ? 'مدة التشغيل' : 'Uptime', ic: 'timer', val: uptime.label, mono: true, foot: `${isAr ? 'منذ الإقلاع' : 'since boot'}${status?.os ? ` · ${status.os}` : ''}` },
    lastBackup
      ? { lab: isAr ? 'آخر نسخة احتياطية' : 'Last backup', ic: 'database-backup', val: formatTimestamp(lastBackup.createdAt).split(' ')[1], sub: `${formatTimestamp(lastBackup.createdAt).split(' ')[0]}${backupSize ? ` · ${backupSize.value} ${backupSize.unit}` : ''}`, footPos: true }
      : { lab: isAr ? 'آخر نسخة احتياطية' : 'Last backup', ic: 'database-backup', val: isAr ? 'لا يوجد' : 'None', sub: isAr ? 'لم تُنشأ نسخة بعد' : 'no backups yet' },
    { lab: isAr ? 'حجم قاعدة البيانات' : 'Database size', ic: 'hard-drive', val: db.value, unit: db.unit, sub: 'SQLite · WAL' },
    { lab: isAr ? 'وضع التشغيل' : 'Operating mode', ic: 'router', val: modeLabels[mode], small: true, foot: <a onClick={() => onScreen('mode')} style={{ cursor: 'pointer' }}>{isAr ? 'تغيير' : 'Change'}</a> },
  ];
  return (
    <div className="stat-grid c4">
      {cards.map((c, i) => (
        <div className="stat" key={i}>
          <span className="lab"><Icon name={c.ic} size={13} /> {c.lab}</span>
          <span className={`val ${('mono' in c && c.mono) ? 'mono' : ''}`} style={('small' in c && c.small) ? { fontSize: 22 } : undefined}>
            {c.val}{('unit' in c && c.unit) && <small> {c.unit}</small>}
          </span>
          <span className={`foot ${('footPos' in c && c.footPos) ? 'pos' : ''}`}>{('sub' in c && c.sub) ? c.sub : ('foot' in c ? c.foot : '')}</span>
        </div>
      ))}
    </div>
  );
}

function RecentActivity({ rows, dir = 'ltr', onScreen }: { rows: AuditLogDto[]; dir?: Dir; onScreen: (s: Screen) => void }) {
  const isAr = dir === 'rtl';
  const verb = (a: AuditLogDto): string => {
    const action = String(a.action ?? '').toLowerCase();
    if (action.includes('login') || action.includes('auth') || action.includes('recovery')) return 'auth';
    if (action.includes('deactiv') || action.includes('delete') || action.includes('prune')) return 'delete';
    if (action.includes('creat') || action.includes('taken') || action.includes('backup')) return 'create';
    return 'update';
  };
  const tone = (a: AuditLogDto): string => String(a.action ?? '').toLowerCase().includes('fail') ? 'danger' : 'neutral';
  return (
    <Card title={isAr ? 'النشاط الأخير' : 'Recent activity'} icon="activity"
      actions={<Btn variant="ghost" size="sm" iconRight="arrow-right" onClick={() => onScreen('audit')}>{isAr ? 'الكل' : 'View all'}</Btn>}>
      <div className="list-rows" style={{ marginTop: -6 }}>
        {rows.length === 0 && <div className="empty-row" style={{ padding: '14px 4px', color: 'var(--fg3)', fontSize: 13 }}>{isAr ? 'لا يوجد نشاط بعد' : 'No activity yet'}</div>}
        {rows.map(r => {
          const v = verb(r);
          return (
            <div className="list-row" key={r.id}>
              <span className="lr-ic"><Icon name={tone(r) === 'danger' ? 'alert-triangle' : v === 'auth' ? 'log-in' : v === 'delete' ? 'archive' : v === 'create' ? 'plus' : 'pencil'} size={17} /></span>
              <div className="lr-text">
                <div className="t">{r.action}</div>
                <div className="s mono">{r.targetId ?? r.targetType ?? '—'}</div>
              </div>
              <Pill tone={(MODULE_COLORS[r.module] as 'aqua' | 'info' | 'success' | 'neutral' | 'violet') || 'neutral'} dot={false}>{r.module}</Pill>
              <span className="meta mono" style={{ fontSize: 11, color: 'var(--fg3)', minWidth: 64, textAlign: dir === 'rtl' ? 'left' : 'right' }}>{formatTimestamp(r.occurredAt).split(' ')[1]}</span>
            </div>
          );
        })}
      </div>
    </Card>
  );
}

function NetworkSummary({ mode, status, dir = 'ltr', onScreen }: { mode: DeployMode; status: ApiStatus | null; dir?: Dir; onScreen: (s: Screen) => void }) {
  const isAr = dir === 'rtl';
  const modeLabels: Record<DeployMode, string> = { standalone: isAr ? 'مستقل' : 'Standalone', network: isAr ? 'شبكة محلية' : 'Network (LAN)', public: isAr ? 'عام' : 'Public tunnel' };
  const modeTone = mode === 'public' ? 'violet' : mode === 'network' ? 'info' : 'neutral';
  return (
    <Card title={isAr ? 'الشبكة والأمان' : 'Network & security'} icon="shield"
      actions={<Btn variant="ghost" size="sm" iconRight="arrow-right" onClick={() => onScreen('mode')}>{isAr ? 'إدارة' : 'Manage'}</Btn>}>
      <div className="kv">
        <div className="kv-row"><span className="k"><Icon name="router" size={15} /> {isAr ? 'الوضع' : 'Mode'}</span>
          <span className="v"><Pill tone={modeTone} dot={false}>{modeLabels[mode]}</Pill></span></div>
        <div className="kv-row"><span className="k"><Icon name="lock" size={15} /> TLS</span>
          <span className="v">1.3 · {isAr ? 'موقّعة ذاتياً' : 'self-signed'}</span></div>
        <div className="kv-row"><span className="k"><Icon name="fingerprint" size={15} /> {isAr ? 'البصمة' : 'Cert fingerprint'}</span>
          <span className="v">{status?.certSha256 ? <CopyField value={status.certSha256} /> : '—'}</span></div>
        <div className="kv-row"><span className="k"><Icon name="server" size={15} /> {isAr ? 'الإصدار' : 'Version'}</span>
          <span className="v mono">{status?.version ?? '—'}</span></div>
      </div>
    </Card>
  );
}

interface DashboardPageProps {
  layout?: DashLayout;
  state: ServerState;
  mode: DeployMode;
  dir?: Dir;
  status: ApiStatus | null;
  network: NetworkInfo | null;
  workspace: string;
  onScreen: (s: Screen) => void;
  onBackupNow?: () => void;
}

export function DashboardPage({ layout = 'hero', state, mode, dir = 'ltr', status, network, workspace, onScreen, onBackupNow }: DashboardPageProps) {
  const uptime = useUptime(status?.startedAt);
  const isAr = dir === 'rtl';
  const modePillTone = mode === 'public' ? 'violet' : mode === 'network' ? 'info' : 'neutral';
  const modeLabels: Record<DeployMode, string> = { standalone: isAr ? 'مستقل' : 'Standalone', network: isAr ? 'شبكة' : 'Network', public: isAr ? 'عام' : 'Public' };

  const [recent, setRecent] = useState<AuditLogDto[]>([]);
  const [lastBackup, setLastBackup] = useState<BackupFileDto | null>(null);
  useEffect(() => {
    api.getAuditLogs({ pageSize: 5 }).then(r => setRecent(r.items)).catch(() => setRecent([]));
    api.getBackups(1, 1).then(r => setLastBackup(r.items[0] ?? null)).catch(() => setLastBackup(null));
  }, []);

  const ws = workspace || (isAr ? 'مساحة العمل' : 'workspace');

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'خادم بلسم المحلي' : 'Balsm local server'}</span>
          <h1>{isAr ? 'لوحة التحكم' : 'Dashboard'}</h1>
          <div className="sub">{isAr ? `مساحة العمل ${ws} · الإصدار ${status?.version ?? '—'}` : `${ws} workspace · running Balsm ${status?.version ?? '—'}`}</div>
        </div>
        <div className="actions">
          <Btn variant="primary" icon="database-backup" onClick={onBackupNow}>{isAr ? 'نسخ احتياطي الآن' : 'Backup now'}</Btn>
        </div>
      </div>

      <StateBanner state={state} dir={dir} onAct={s => onScreen(s as Screen)} />

      {layout === 'hero' ? (
        <div className="section-stack">
          <div className="hero">
            <div className="hero-top">
              <StatusRing state={state} />
              <div className="hero-headline">
                <div className="status-line">
                  <h2>{statusLabel(state, dir)}</h2>
                  <Pill tone={modePillTone} dot={false} icon="router">{modeLabels[mode]}</Pill>
                  {status?.version && <Pill tone="neutral" dot={false}>v{status.version}</Pill>}
                </div>
                <div className="ws">{isAr ? <>الخادم يخدم <b>{ws}</b> منذ <b className="mono">{uptime.label}</b></> : <>Serving <b>{ws}</b> · up <b className="mono">{uptime.full}</b></>}</div>
              </div>
            </div>
            <HeroFacts mode={mode} status={status} network={network} />
          </div>
          <StatCards uptime={uptime} mode={mode} status={status} lastBackup={lastBackup} dir={dir} onScreen={onScreen} />
          <div className="two-col">
            <RecentActivity rows={recent} dir={dir} onScreen={onScreen} />
            <NetworkSummary mode={mode} status={status} dir={dir} onScreen={onScreen} />
          </div>
        </div>
      ) : (
        <div className="section-stack">
          <Card>
            <div style={{ display: 'flex', alignItems: 'center', gap: 18, flexWrap: 'wrap' }}>
              <StatusRing state={state} />
              <div style={{ flex: 1, minWidth: 220 }}>
                <div className="status-line" style={{ display: 'flex', alignItems: 'baseline', gap: 10, flexWrap: 'wrap' }}>
                  <span style={{ fontFamily: 'var(--font-display)', fontSize: 22, fontWeight: 800, letterSpacing: '-0.02em', color: 'var(--balsm-ink-900)' }}>{statusLabel(state, dir)}</span>
                  <Pill tone={modePillTone} dot={false} icon="router">{modeLabels[mode]}</Pill>
                  {status?.version && <Pill tone="neutral" dot={false}>v{status.version}</Pill>}
                </div>
                <div style={{ fontSize: 13, color: 'var(--fg2)', marginTop: 5 }}>{isAr ? <>{ws} · مدة التشغيل <span className="mono">{uptime.full}</span></> : <>{ws} · up <span className="mono">{uptime.full}</span></>}</div>
              </div>
              <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                {[{ k: 'Host', v: network?.hostname ?? '—' }, { k: 'mDNS', v: mode === 'standalone' ? 'suppressed' : 'advertised' }, { k: 'TLS', v: '1.3' }].map(f => (
                  <div key={f.k}>
                    <div style={{ fontSize: 10, letterSpacing: '.12em', textTransform: 'uppercase', color: 'var(--fg3)', fontWeight: 700 }}>{f.k}</div>
                    <div className="mono" style={{ fontSize: 13, fontWeight: 600, color: 'var(--balsm-ink-900)', marginTop: 3 }}>{f.v}</div>
                  </div>
                ))}
              </div>
            </div>
          </Card>
          <StatCards uptime={uptime} mode={mode} status={status} lastBackup={lastBackup} dir={dir} onScreen={onScreen} />
          <div className="two-col">
            <RecentActivity rows={recent} dir={dir} onScreen={onScreen} />
            <NetworkSummary mode={mode} status={status} dir={dir} onScreen={onScreen} />
          </div>
        </div>
      )}
    </div>
  );
}
