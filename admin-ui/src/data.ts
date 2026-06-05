// Data types and mock data for the Balsm server admin UI.
// In production these are replaced by API calls via src/api.ts.

export type Dir = 'ltr' | 'rtl';

export type ServerState = 'healthy' | 'migrating' | 'restoring' | 'offline' | 'backupfail';
export type DeployMode = 'standalone' | 'network' | 'public';
export type Density = 'compact' | 'regular' | 'comfy';
export type NavVariant = 'sidebar' | 'tabs';
export type DashLayout = 'hero' | 'compact';

export interface ServerStatus {
  version: string;
  buildId: string;
  workspace: string;
  slug: string;
  httpPort: number;
  httpsPort: number;
  url: string;
  host: string;
  certSha: string;
  mdns: string;
  bootedAt: number;
  os: string;
  service: string;
  dbSize: string;
  ip: string;
  mode: DeployMode;
  state: ServerState;
}

export interface BackupRecord {
  id: string;
  name: string;
  date: string;
  ago: string;
  size: string;
  kind: 'scheduled' | 'manual';
  status: 'ok' | 'failed';
  sha: string;
  fresh?: boolean;
}

export interface ArchiveRecord {
  name: string;
  rows: number;
  size: string;
  date: string;
  sha: string;
}

export interface AuditRecord {
  id: string;
  actor: string;
  initials: string;
  action: string;
  module: string;
  target: string;
  time: string;
  ip: string;
  tone?: string;
  detail?: string;
  verb: 'auth' | 'create' | 'update' | 'delete';
}

export interface Branch {
  id: string;
  name: string;
  code: string;
  address: string;
  phone: string;
  active: boolean;
}

export interface Entity {
  id: string;
  name: string;
  nameAr: string;
  type: string;
  code: string;
  active: boolean;
  branches: Branch[];
}

export interface UptimeInfo {
  d: number; h: number; m: number; s: number;
  label: string;
  full: string;
}

// ── Mock data ────────────────────────────────────────────────────────────────

export const SERVER: ServerStatus = {
  version: '1.0.0',
  buildId: '2026.05.30+a3f9c1',
  workspace: 'Demo Pharmacy',
  slug: 'demo',
  httpPort: 5050,
  httpsPort: 5051,
  url: 'https://balsm.local:5051/admin',
  host: 'balsm.local',
  certSha: 'A4:7B:E9:02:C1:5D:88:F3:6A:21:9C:0E:B2:44:7F:1A',
  mdns: 'balsm.local',
  bootedAt: Date.now() - (4 * 24 * 3600 + 7 * 3600 + 22 * 60) * 1000,
  os: 'Ubuntu 22.04 LTS',
  service: 'balsm-api.service',
  dbSize: '184.2 MB',
  ip: '192.168.1.10',
  mode: 'network',
  state: 'healthy',
};

export const BACKUPS: BackupRecord[] = [
  { id: 'bk-091', name: 'balsm-2026-05-30_0200.bak', date: '30/05/2026 02:00', ago: 'Today, 02:00', size: '184.2 MB', kind: 'scheduled', status: 'ok', sha: '9F2A…C107' },
  { id: 'bk-090', name: 'balsm-2026-05-29_1612.bak', date: '29/05/2026 16:12', ago: 'Yesterday, 16:12', size: '183.9 MB', kind: 'manual', status: 'ok', sha: '4D81…A93B' },
  { id: 'bk-089', name: 'balsm-2026-05-29_0200.bak', date: '29/05/2026 02:00', ago: 'Yesterday, 02:00', size: '183.7 MB', kind: 'scheduled', status: 'ok', sha: 'B6E0…1F44' },
  { id: 'bk-088', name: 'balsm-2026-05-28_0200.bak', date: '28/05/2026 02:00', ago: '2 days ago', size: '182.4 MB', kind: 'scheduled', status: 'ok', sha: '0CA7…77E9' },
  { id: 'bk-087', name: 'balsm-2026-05-27_0200.bak', date: '27/05/2026 02:00', ago: '3 days ago', size: '181.8 MB', kind: 'scheduled', status: 'failed', sha: '—' },
  { id: 'bk-086', name: 'balsm-2026-05-26_0200.bak', date: '26/05/2026 02:00', ago: '4 days ago', size: '181.1 MB', kind: 'scheduled', status: 'ok', sha: 'E314…90AD' },
];

export const ARCHIVES: ArchiveRecord[] = [
  { name: 'audit-202403.jsonl', rows: 412, size: '1.2 MB', date: '29/05/2026', sha: '7C20…9AE1' },
  { name: 'audit-202402.jsonl', rows: 388, size: '1.1 MB', date: '01/03/2026', sha: 'AA47…03BD' },
];

export const AUDIT: AuditRecord[] = [
  { id: '1', actor: 'admin', initials: 'AD', action: 'BackupTaken', verb: 'create', module: 'Backup', target: 'balsm-2026-05-30_0200.bak', time: '30/05/2026 02:00:04', ip: 'system', tone: 'success' },
  { id: '2', actor: 'admin', initials: 'AD', action: 'ModeChanged', verb: 'update', module: 'Server', target: 'Standalone → Network', time: '29/05/2026 17:41:55', ip: '192.168.1.10', tone: 'info' },
  { id: '3', actor: 'admin', initials: 'AD', action: 'BranchUpdated', verb: 'update', module: 'Entity', target: 'Branch 1', time: '29/05/2026 16:20:11', ip: '192.168.1.10', tone: 'neutral' },
  { id: '4', actor: 'admin', initials: 'AD', action: 'BranchDeactivated', verb: 'delete', module: 'Entity', target: 'Branch 3', time: '29/05/2026 16:18:02', ip: '192.168.1.10', tone: 'warn' },
  { id: '5', actor: 'admin', initials: 'AD', action: 'EntityCreated', verb: 'create', module: 'Entity', target: 'Demo Pharmacy', time: '29/05/2026 16:12:47', ip: '192.168.1.10', tone: 'success' },
  { id: '6', actor: 'system', initials: 'SY', action: 'RetentionPruned', verb: 'delete', module: 'Audit', target: 'audit-202403.jsonl (412 rows)', time: '29/05/2026 03:00:00', ip: 'system', tone: 'neutral' },
  { id: '7', actor: 'admin', initials: 'AD', action: 'AdminLogin', verb: 'auth', module: 'Identity', target: 'session a1f9…', time: '29/05/2026 08:55:31', ip: '192.168.1.10', tone: 'info' },
  { id: '8', actor: 'unknown', initials: '??', action: 'LoginFailed', verb: 'auth', module: 'Identity', target: '5 attempts · locked', time: '28/05/2026 22:14:09', ip: '192.168.1.51', tone: 'danger' },
  { id: '9', actor: 'system', initials: 'SY', action: 'BackupFailed', verb: 'create', module: 'Backup', target: 'disk full (/var/balsm)', time: '27/05/2026 02:00:31', ip: 'system', tone: 'danger' },
  { id: '10', actor: 'admin', initials: 'AD', action: 'RecoveryCodeUsed', verb: 'auth', module: 'Identity', target: 'lockout cleared', time: '26/05/2026 09:30:18', ip: '192.168.1.10', tone: 'violet' },
];

export const ENTITIES: Entity[] = [
  {
    id: 'ent-1', name: 'Demo Pharmacy', nameAr: 'صيدلية تجريبية',
    type: 'Pharmacy group', code: 'DPH', active: true,
    branches: [
      { id: 'br-101', name: 'Branch 1', code: 'BR-101', address: '1 Main St', phone: '+1 555 000 0001', active: true },
      { id: 'br-102', name: 'Branch 2', code: 'BR-102', address: '2 Main St', phone: '+1 555 000 0002', active: true },
      { id: 'br-103', name: 'Branch 3', code: 'BR-103', address: '3 Main St', phone: '+1 555 000 0003', active: false },
    ],
  },
  {
    id: 'ent-2', name: 'Demo Distributor', nameAr: 'موزع تجريبي',
    type: 'Distributor', code: 'DDT', active: true,
    branches: [
      { id: 'br-201', name: 'Warehouse 1', code: 'BR-201', address: '10 Warehouse Rd', phone: '+1 555 000 0010', active: true },
    ],
  },
];

export const MODULE_COLORS: Record<string, string> = {
  Backup: 'aqua',
  Server: 'info',
  Entity: 'success',
  Audit: 'neutral',
  Identity: 'violet',
};

export const STRINGS = {
  en: {
    dir: 'ltr' as Dir,
    nav: {
      dashboard: 'Dashboard',
      entities: 'Entities & branches',
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
      entities: 'الكيانات والفروع',
      backups: 'النسخ الاحتياطي',
      audit: 'سجل التدقيق',
      mode: 'الوضع والشبكة',
    },
    overview: 'نظرة عامة',
    data: 'البيانات والأمان',
    system: 'النظام',
  },
};

export function uptimeFrom(ts: number): UptimeInfo {
  const diff = Math.floor((Date.now() - ts) / 1000);
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
