import { useState, useEffect } from 'react';
import { Icon, Btn, Pill, Card, CopyField } from '../components/atoms';
import type { Dir, ServerState, DeployMode, DashLayout } from '../data';
import { SERVER, AUDIT, MODULE_COLORS, uptimeFrom } from '../data';
import type { Screen } from '../components/shell';

interface UptimeInfo { d: number; h: number; m: number; s: number; label: string; full: string }

function useUptime(): UptimeInfo {
  const [u, setU] = useState<UptimeInfo>(() => uptimeFrom(SERVER.bootedAt));
  useEffect(() => {
    const id = setInterval(() => setU(uptimeFrom(SERVER.bootedAt)), 1000);
    return () => clearInterval(id);
  }, []);
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
        <div className="bn-progress"><span style={{ width: '64%' }} /></div>
      </div>
    </div>
  );
  if (state === 'restoring') return (
    <div className="state-banner info">
      <span className="ic spin"><Icon name="loader" size={20} /></span>
      <div className="bn-body">
        <b>{isAr ? 'جارٍ الاستعادة من نسخة احتياطية' : 'Restore in progress'}</b>
        <span className="mono" style={{ fontSize: 12 }}>balsm-2026-05-29_1612.bak · {isAr ? 'الخادم للقراءة فقط' : 'server is read-only'} · ready=false</span>
        <div className="bn-progress"><span style={{ width: '42%' }} /></div>
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
        <span>{isAr ? 'لم تكتمل نسخة 27/05 الساعة 02:00 — مساحة القرص ممتلئة في /var/balsm. آخر نسخة سليمة محفوظة.' : 'The 27/05 02:00 backup did not complete — disk full on /var/balsm. Your last known-good backup is preserved.'}</span>
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

function HeroFacts({ mode }: { mode: DeployMode }) {
  const facts = [
    { k: 'HTTPS', v: `:${SERVER.httpsPort}`, ic: 'lock' },
    { k: 'Host', v: SERVER.host, ic: 'server' },
    { k: 'Cert · SHA-256', v: SERVER.certSha.slice(0, 17) + '…', ic: 'shield-check' },
    { k: 'mDNS', v: mode === 'standalone' ? 'suppressed' : 'advertised', ic: 'radio' },
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

function StatCards({ uptime, mode, dir = 'ltr', onScreen }: { uptime: UptimeInfo; mode: DeployMode; dir?: Dir; onScreen: (s: Screen) => void }) {
  const isAr = dir === 'rtl';
  const modeLabels: Record<DeployMode, string> = { standalone: isAr ? 'مستقل' : 'Standalone', network: isAr ? 'شبكة' : 'Network', public: isAr ? 'عام' : 'Public' };
  const cards = [
    { lab: isAr ? 'مدة التشغيل' : 'Uptime', ic: 'timer', val: uptime.label, mono: true, foot: `${isAr ? 'منذ الإقلاع' : 'since boot'} · ${SERVER.os}` },
    { lab: isAr ? 'آخر نسخة احتياطية' : 'Last backup', ic: 'database-backup', val: isAr ? 'اليوم' : 'Today', sub: '02:00 · 184.2 MB', footPos: true },
    { lab: isAr ? 'حجم قاعدة البيانات' : 'Database size', ic: 'hard-drive', val: SERVER.dbSize.split(' ')[0], unit: SERVER.dbSize.split(' ')[1], sub: 'SQLite · WAL' },
    { lab: isAr ? 'وضع التشغيل' : 'Operating mode', ic: 'router', val: modeLabels[mode], small: true, foot: <a onClick={() => onScreen('mode')} style={{ cursor: 'pointer' }}>{isAr ? 'تغيير' : 'Change'}</a> },
  ];
  return (
    <div className="stat-grid c4">
      {cards.map((c, i) => (
        <div className="stat" key={i}>
          <span className="lab"><Icon name={c.ic} size={13} /> {c.lab}</span>
          <span className={`val ${c.mono ? 'mono' : ''}`} style={c.small ? { fontSize: 22 } : undefined}>
            {c.val}{c.unit && <small> {c.unit}</small>}
          </span>
          <span className={`foot ${c.footPos ? 'pos' : ''}`}>{c.sub ?? c.foot ?? ''}</span>
        </div>
      ))}
    </div>
  );
}

function RecentActivity({ dir = 'ltr', onScreen }: { dir?: Dir; onScreen: (s: Screen) => void }) {
  const isAr = dir === 'rtl';
  const rows = AUDIT.slice(0, 5);
  return (
    <Card title={isAr ? 'النشاط الأخير' : 'Recent activity'} icon="activity"
      actions={<Btn variant="ghost" size="sm" iconRight="arrow-right" onClick={() => onScreen('audit')}>{isAr ? 'الكل' : 'View all'}</Btn>}>
      <div className="list-rows" style={{ marginTop: -6 }}>
        {rows.map(r => (
          <div className="list-row" key={r.id}>
            <span className="lr-ic"><Icon name={r.tone === 'danger' ? 'alert-triangle' : r.verb === 'auth' ? 'log-in' : r.verb === 'delete' ? 'archive' : r.verb === 'create' ? 'plus' : 'pencil'} size={17} /></span>
            <div className="lr-text">
              <div className="t">{r.action}</div>
              <div className="s mono">{r.target}</div>
            </div>
            <Pill tone={(MODULE_COLORS[r.module as keyof typeof MODULE_COLORS] as 'aqua' | 'info' | 'success' | 'neutral' | 'violet') || 'neutral'} dot={false}>{r.module}</Pill>
            <span className="meta mono" style={{ fontSize: 11, color: 'var(--fg3)', minWidth: 64, textAlign: dir === 'rtl' ? 'left' : 'right' }}>{r.time.split(' ')[1]}</span>
          </div>
        ))}
      </div>
    </Card>
  );
}

function NetworkSummary({ mode, dir = 'ltr', onScreen }: { mode: DeployMode; dir?: Dir; onScreen: (s: Screen) => void }) {
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
          <span className="v"><CopyField value={SERVER.certSha} /></span></div>
        <div className="kv-row"><span className="k"><Icon name="key-round" size={15} /> {isAr ? 'انتهاء الجلسة' : 'Session idle'}</span>
          <span className="v mono">30 min</span></div>
        <div className="kv-row"><span className="k"><Icon name="users-round" size={15} /> {isAr ? 'المسؤول' : 'Admin users'}</span>
          <span className="v">1 / 1</span></div>
      </div>
    </Card>
  );
}

interface DashboardPageProps {
  layout?: DashLayout;
  state: ServerState;
  mode: DeployMode;
  dir?: Dir;
  onScreen: (s: Screen) => void;
  onBackupNow?: () => void;
}

export function DashboardPage({ layout = 'hero', state, mode, dir = 'ltr', onScreen, onBackupNow }: DashboardPageProps) {
  const uptime = useUptime();
  const isAr = dir === 'rtl';
  const modePillTone = mode === 'public' ? 'violet' : mode === 'network' ? 'info' : 'neutral';
  const modeLabels: Record<DeployMode, string> = { standalone: isAr ? 'مستقل' : 'Standalone', network: isAr ? 'شبكة' : 'Network', public: isAr ? 'عام' : 'Public' };

  return (
    <div className="page">
      <div className="page-head">
        <div>
          <span className="eyebrow">{isAr ? 'خادم بَلسَم المحلي' : 'Balsm local server'}</span>
          <h1>{isAr ? 'لوحة التحكم' : 'Dashboard'}</h1>
          <div className="sub">{isAr ? `مساحة العمل ${SERVER.workspace} · الإصدار ${SERVER.version}` : `${SERVER.workspace} workspace · running Balsm ${SERVER.version}`}</div>
        </div>
        <div className="actions">
          <Btn variant="secondary" icon="external-link" onClick={() => {}}>{isAr ? 'فتح اللوحة' : 'Open panel URL'}</Btn>
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
                  <Pill tone="neutral" dot={false}>v{SERVER.version}</Pill>
                </div>
                <div className="ws">{isAr ? <>الخادم يخدم <b>{SERVER.workspace}</b> منذ <b className="mono">{uptime.label}</b></> : <>Serving <b>{SERVER.workspace}</b> · up <b className="mono">{uptime.full}</b></>}</div>
              </div>
              <div className="hero-actions">
                <Btn variant="secondary" icon="rotate-cw">{isAr ? 'إعادة تشغيل' : 'Restart'}</Btn>
              </div>
            </div>
            <HeroFacts mode={mode} />
          </div>
          <StatCards uptime={uptime} mode={mode} dir={dir} onScreen={onScreen} />
          <div className="two-col">
            <RecentActivity dir={dir} onScreen={onScreen} />
            <NetworkSummary mode={mode} dir={dir} onScreen={onScreen} />
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
                  <Pill tone="neutral" dot={false}>v{SERVER.version}</Pill>
                </div>
                <div style={{ fontSize: 13, color: 'var(--fg2)', marginTop: 5 }}>{isAr ? <>{SERVER.workspace} · مدة التشغيل <span className="mono">{uptime.full}</span></> : <>{SERVER.workspace} · up <span className="mono">{uptime.full}</span></>}</div>
              </div>
              <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                {[{ k: 'Host', v: SERVER.host }, { k: 'mDNS', v: mode === 'standalone' ? 'suppressed' : 'advertised' }, { k: 'TLS', v: '1.3' }].map(f => (
                  <div key={f.k}>
                    <div style={{ fontSize: 10, letterSpacing: '.12em', textTransform: 'uppercase', color: 'var(--fg3)', fontWeight: 700 }}>{f.k}</div>
                    <div className="mono" style={{ fontSize: 13, fontWeight: 600, color: 'var(--balsm-ink-900)', marginTop: 3 }}>{f.v}</div>
                  </div>
                ))}
              </div>
              <Btn variant="secondary" icon="rotate-cw">{isAr ? 'إعادة تشغيل' : 'Restart'}</Btn>
            </div>
          </Card>
          <StatCards uptime={uptime} mode={mode} dir={dir} onScreen={onScreen} />
          <div className="two-col">
            <RecentActivity dir={dir} onScreen={onScreen} />
            <NetworkSummary mode={mode} dir={dir} onScreen={onScreen} />
          </div>
        </div>
      )}
    </div>
  );
}
