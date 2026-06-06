import React from 'react';
import { Icon, IconBtn, Avatar, Brand } from './atoms';
import type { ServerState, DeployMode, Dir } from '../data';

// ── Navigation config ─────────────────────────────────────────────────────────

export type Screen = 'dashboard' | 'mode' | 'backups' | 'audit';

const NAV: { id: Screen; icon: string; group: 'overview' | 'data' | 'system' }[] = [
  { id: 'dashboard', icon: 'layout-dashboard', group: 'overview' },
  { id: 'mode',      icon: 'router',           group: 'system' },
  { id: 'backups',   icon: 'database-backup',  group: 'data' },
  { id: 'audit',     icon: 'scroll-text',      group: 'data' },
];

export interface Strings {
  overview: string;
  data: string;
  system: string;
  nav: Record<Screen, string>;
}

export const EN: Strings = {
  overview: 'Overview',
  data: 'Data & safety',
  system: 'System',
  nav: {
    dashboard: 'Dashboard',
    backups:   'Backups',
    audit:     'Audit log',
    mode:      'Mode & network',
  },
};

export const AR: Strings = {
  overview: 'نظرة عامة',
  data: 'البيانات والأمان',
  system: 'النظام',
  nav: {
    dashboard: 'لوحة التحكم',
    backups:   'النسخ الاحتياطي',
    audit:     'سجل التدقيق',
    mode:      'الوضع والشبكة',
  },
};

function navGroups(t: Strings) {
  return [
    { key: 'overview', label: t.overview, items: NAV.filter(n => n.group === 'overview') },
    { key: 'data',     label: t.data,     items: NAV.filter(n => n.group === 'data') },
    { key: 'system',   label: t.system,   items: NAV.filter(n => n.group === 'system') },
  ];
}

// ── Sidebar ───────────────────────────────────────────────────────────────────

interface SidebarProps {
  screen: Screen;
  onScreen: (s: Screen | '__logout') => void;
  t: Strings;
  alerts: Partial<Record<Screen, boolean>>;
}

export function Sidebar({ screen, onScreen, t, alerts }: SidebarProps) {
  return (
    <aside className="sidebar">
      <Brand />
      <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {navGroups(t).map(g => (
          <React.Fragment key={g.key}>
            <div className="nav-section">{g.label}</div>
            {g.items.map(item => (
              <div key={item.id} className={`nav-item ${screen === item.id ? 'active' : ''}`} onClick={() => onScreen(item.id)}>
                <Icon name={item.icon} size={18} />
                <span className="lbl">{t.nav[item.id]}</span>
                {alerts[item.id] && <span className="alert-dot" title="Needs attention" />}
              </div>
            ))}
          </React.Fragment>
        ))}
      </div>
      <div className="spring" />
      <div className="footer">
        <Avatar initials="AD" tone="violet" size="sm" />
        <div className="info">
          <span className="who">Admin</span>
          <span className="role">Administrator</span>
        </div>
        <IconBtn icon="log-out" title="Sign out" size={30} onClick={() => onScreen('__logout')} />
      </div>
    </aside>
  );
}

// ── Top-tabs variant ──────────────────────────────────────────────────────────

interface TopTabsProps {
  screen: Screen;
  onScreen: (s: Screen) => void;
  t: Strings;
  alerts: Partial<Record<Screen, boolean>>;
  size?: number;
}

export function TopTabs({ screen, onScreen, t, alerts, size = 34 }: TopTabsProps) {
  return (
    <div className="tabs-brand">
      <div className="flower-clip" style={{ width: size, height: size, borderRadius: 9, overflow: 'hidden', flexShrink: 0 }}>
        <svg viewBox="0 0 40 40" width={size} height={size} xmlns="http://www.w3.org/2000/svg">
          <circle cx="20" cy="20" r="7" fill="#02BBB5" />
          <ellipse cx="20" cy="9" rx="5" ry="9" fill="#01C4A2" />
          <ellipse cx="9" cy="15" rx="5" ry="9" transform="rotate(-60 9 15)" fill="#1283FF" />
          <ellipse cx="31" cy="15" rx="5" ry="9" transform="rotate(60 31 15)" fill="#724DD0" />
          <ellipse cx="12" cy="32" rx="5" ry="9" transform="rotate(30 12 32)" fill="#55D77F" />
          <ellipse cx="28" cy="32" rx="5" ry="9" transform="rotate(-30 28 32)" fill="#02BBB5" />
        </svg>
      </div>
      <div className="tabnav">
        {NAV.map(item => (
          <div key={item.id} className={`tab ${screen === item.id ? 'active' : ''}`} onClick={() => onScreen(item.id)}>
            <Icon name={item.icon} size={17} />
            <span className="lbl">{t.nav[item.id]}</span>
            {alerts[item.id] && <span className="alert-dot" />}
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Server status pill ────────────────────────────────────────────────────────

interface ServerStatusPillProps {
  state: ServerState;
  mode: DeployMode;
  dir: Dir;
}

export function ServerStatusPill({ state, mode, dir }: ServerStatusPillProps) {
  const isAr = dir === 'rtl';
  const map: Record<ServerState, { cls: string; label: string }> = {
    healthy:    { cls: mode === 'public' ? 'mode-public' : mode === 'network' ? 'mode-network' : '', label: isAr ? 'يعمل' : 'Running' },
    migrating:  { cls: 'degraded', label: isAr ? 'ترحيل قاعدة البيانات' : 'Migrating' },
    restoring:  { cls: 'degraded', label: isAr ? 'جارٍ الاستعادة' : 'Restoring' },
    offline:    { cls: 'down',     label: isAr ? 'محلي فقط' : 'Localhost only' },
    backupfail: { cls: '',         label: isAr ? 'يعمل' : 'Running' },
  };
  const m = map[state] ?? map.healthy;
  return (
    <span className={`server-pill ${m.cls}`}>
      <span className="dot" />
      {m.label}
    </span>
  );
}

// ── TopBar ────────────────────────────────────────────────────────────────────

interface TopBarProps {
  screen: Screen;
  t: Strings;
  dir: Dir;
  lang: 'en' | 'ar';
  onLang: (l: 'en' | 'ar') => void;
  state: ServerState;
  mode: DeployMode;
  navTabs: boolean;
  onScreen: (s: Screen) => void;
  alerts: Partial<Record<Screen, boolean>>;
  workspace?: string;
}

export function TopBar({ screen, t, dir, lang, onLang, state, mode, navTabs, onScreen, alerts, workspace = '' }: TopBarProps) {
  const titles = t.nav;
  const subs: Record<Screen, string> = {
    dashboard: dir === 'rtl' ? `مساحة العمل · ${workspace}` : `Workspace · ${workspace}`,
    backups:   dir === 'rtl' ? 'النسخ الاحتياطي والاستعادة' : 'Backup, schedule & restore',
    audit:     dir === 'rtl' ? 'سجل غير قابل للتعديل' : 'Append-only activity log',
    mode:      dir === 'rtl' ? 'وضع التشغيل واكتشاف الشبكة' : 'Operating mode & discovery',
  };
  return (
    <header className="topbar">
      {navTabs ? (
        <TopTabs screen={screen} onScreen={onScreen} t={t} alerts={alerts} />
      ) : (
        <div className="page-title">
          <span className="t">{titles[screen]}</span>
          <span className="meta">{subs[screen]}</span>
        </div>
      )}
      <div className="spacer" />
      <div className="search">
        <Icon name="search" size={16} className="ic" />
        <input placeholder={dir === 'rtl' ? 'بحث…' : 'Search settings, audit, backups…'} />
      </div>
      <ServerStatusPill state={state} mode={mode} dir={dir} />
      <div className="lang-toggle">
        <button className={lang === 'en' ? 'active' : ''} onClick={() => onLang('en')}>EN</button>
        <button className={lang === 'ar' ? 'active' : ''} onClick={() => onLang('ar')}>ع</button>
      </div>
    </header>
  );
}
