// Shared types, UI string tables, and formatting helpers for the Balsm server
// admin UI. All runtime data is fetched live from the API via src/api.ts —
// there is no mock/demo data here.

export type Dir = 'ltr' | 'rtl';

export type ServerState = 'healthy' | 'migrating' | 'restoring' | 'offline' | 'backupfail';
export type DeployMode = 'standalone' | 'network' | 'public';
export type Density = 'compact' | 'regular' | 'comfy';
export type NavVariant = 'sidebar' | 'tabs';
export type DashLayout = 'hero' | 'compact';

export interface UptimeInfo {
  d: number; h: number; m: number; s: number;
  label: string;
  full: string;
}

// Audit module → pill tone. Used to colour the module column in the audit log
// and the recent-activity feed. Modules are emitted by the backend.
export const MODULE_COLORS: Record<string, string> = {
  Backup: 'aqua',
  Server: 'info',
  Mode: 'info',
  Entity: 'success',
  Audit: 'neutral',
  Identity: 'violet',
};

export const STRINGS = {
  en: {
    dir: 'ltr' as Dir,
    nav: {
      dashboard: 'Dashboard',
      backups: 'Backups',
      audit: 'Audit log',
      mode: 'Mode & network',
    },
    overview: 'Overview',
    data: 'Data & safety',
    system: 'System',
  },
  ar: {
    dir: 'rtl' as Dir,
    nav: {
      dashboard: 'لوحة التحكم',
      backups: 'النسخ الاحتياطي',
      audit: 'سجل التدقيق',
      mode: 'الوضع والشبكة',
    },
    overview: 'نظرة عامة',
    data: 'البيانات والأمان',
    system: 'النظام',
  },
};

// ── Helpers ──────────────────────────────────────────────────────────────────

/** Derive a live uptime breakdown from a server-provided ISO start timestamp. */
export function uptimeFromIso(startedAt: string | null | undefined): UptimeInfo {
  const ts = startedAt ? new Date(startedAt).getTime() : Date.now();
  const diff = Math.max(0, Math.floor((Date.now() - ts) / 1000));
  const d = Math.floor(diff / 86400);
  const h = Math.floor((diff % 86400) / 3600);
  const m = Math.floor((diff % 3600) / 60);
  const s = diff % 60;
  return {
    d, h, m, s,
    label: `${d}d ${h}h ${m}m`,
    full: `${d}d ${String(h).padStart(2, '0')}h ${String(m).padStart(2, '0')}m ${String(s).padStart(2, '0')}s`,
  };
}

/** Human-readable byte size, e.g. 184.2 MB. Returns { value, unit } for separate styling. */
export function formatBytes(bytes: number | null | undefined): { value: string; unit: string } {
  if (bytes == null || bytes < 0) return { value: '—', unit: '' };
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let i = 0;
  let n = bytes;
  while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
  return { value: n.toFixed(n >= 100 || i === 0 ? 0 : 1), unit: units[i] };
}

/** "184.2 MB" single string form. */
export function formatBytesStr(bytes: number | null | undefined): string {
  const { value, unit } = formatBytes(bytes);
  return unit ? `${value} ${unit}` : value;
}

/** Short hex SHA fingerprint for table display, e.g. "9F2A…C107". */
export function shortSha(sha: string | null | undefined): string {
  if (!sha) return '—';
  const clean = sha.replace(/:/g, '');
  return clean.length > 8 ? `${clean.slice(0, 4)}…${clean.slice(-4)}` : clean;
}

/** Format an ISO timestamp as "DD/MM/YYYY HH:mm:ss" (UTC) for audit/backup rows. */
export function formatTimestamp(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '—';
  const p = (n: number) => String(n).padStart(2, '0');
  return `${p(d.getUTCDate())}/${p(d.getUTCMonth() + 1)}/${d.getUTCFullYear()} ${p(d.getUTCHours())}:${p(d.getUTCMinutes())}:${p(d.getUTCSeconds())}`;
}
