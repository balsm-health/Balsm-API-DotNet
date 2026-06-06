// Typed API client for Balsm Supervisor admin endpoints

export interface AuthStatus {
  setupComplete: boolean;
}

export interface ApiResponse<T = { message: string }> {
  ok: boolean;
  data: T;
  status: number;
}

export interface ApiStatus {
  isRunning: boolean;
  pid: number | null;
  startedAt: string | null;
  uptime: string | null;
  mode: string | null;
  apiUrl: string | null;
  version: string | null;
  os: string | null;
  httpPort: number;
  httpsPort: number;
  dbSizeBytes: number | null;
  certSha256: string | null;
}

export interface LanAddress {
  ipAddress: string;
  interfaceName: string;
  interfaceType: string;
  apiUrl: string;
  adminUrl: string;
}

export interface NetworkInfo {
  hostname: string;
  lanAddresses: LanAddress[];
  mdnsHostname: string | null;
  mdnsApiUrl: string | null;
  mdnsRegistered: boolean;
  publicIp: string | null;
}

export interface TunnelStatus {
  isRunning: boolean;
  tunnelUrl: string | null;
  tunnelType: string | null;
  error: string | null;
  cloudflaredInstalled: boolean;
  registeredUrl: string | null;
  serverId: string | null;
}

export interface UpdateInfo {
  currentVersion: string;
  latestVersion: string;
  isUpdateAvailable: boolean;
  releaseUrl: string | null;
  downloadUrl: string | null;
  assetFileName: string | null;
  checksumUrl: string | null;
  releaseNotes: string | null;
}

// --- Entity module types ---
export interface WorkspaceDto {
  id: string
  name: string
  slug: string
  status: string
  localeDefault: string
}

export interface EntityTypeDto {
  id: string
  code: string
  labelEn: string
  labelAr: string
}

export interface EntityDto {
  id: string
  workspaceId: string
  name: string
  typeCode: string
  registrationNumber: string | null
  isActive: boolean
}

export interface BranchDto {
  id: string
  entityRootId: string
  name: string
  addressLine1: string | null
  addressLine2: string | null
  city: string | null
  countryCode: string | null
  isActive: boolean
}

export interface CreateEntityRequest {
  name: string
  typeCode: string
  registrationNumber?: string
}

export interface UpdateEntityRequest {
  name: string
  registrationNumber?: string
}

export interface CreateBranchRequest {
  name: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  countryCode?: string
}

export interface UpdateBranchRequest {
  name: string
  addressLine1?: string
  addressLine2?: string
  city?: string
  countryCode?: string
}

export interface PairingSummary {
  id: string
  serverId: string
  serverName: string
  serverUrl: string
  status: string
  lastHeartbeatAt: string | null
  lastSyncAt: string | null
  pairedAt: string
}

export interface PairingListResponse {
  pairings: PairingSummary[]
}

export interface GenerateCodeResponse {
  code: string
  expiresInSeconds: number
}

// --- Backups ---
export interface BackupFileDto {
  id: string
  filename: string
  sizeBytes: number
  sha256: string
  trigger: string
  status: string
  createdAt: string
}

export interface BackupListResponse {
  total: number
  page: number
  pageSize: number
  items: BackupFileDto[]
}

export interface BackupSchedule {
  cron: string
  retention: number
}

// --- Audit ---
export interface AuditLogDto {
  id: string
  occurredAt: string
  actor: string
  sourceIp: string | null
  module: string
  action: string
  targetType: string | null
  targetId: string | null
  detailsJson: string | null
  correlationId: string | null
}

export interface AuditListResponse {
  total: number
  page: number
  pageSize: number
  items: AuditLogDto[]
}

export interface AuditRetention {
  cron: string
  retentionYears: number
}

export interface AuditArchiveDto {
  id: string
  filename: string
  sizeBytes: number
  sha256: string
  archivedAt: string
  periodStart: string | null
  periodEnd: string | null
  rowCount: number
}

export interface AuditArchiveListResponse {
  total: number
  page: number
  pageSize: number
  items: AuditArchiveDto[]
}

/**
 * Origin for API calls. The API is served at api.<host> (e.g. api.balsm.local),
 * while the admin panel is at <host> (balsm.local). On localhost dev — or when
 * already on api.* — use same-origin.
 */
export function apiBase(): string {
  if (typeof window === 'undefined') return '';
  const { protocol, hostname, port } = window.location;
  if ((hostname.endsWith('.local') || hostname.endsWith('.health')) && !hostname.startsWith('api.')) {
    // Preserve the panel's port (dev: 5050/5051) so api.<host> is reachable on
    // the same port. In production the panel runs on 80/443 → no port suffix.
    const p = port ? `:${port}` : '';
    return `${protocol}//api.${hostname}${p}`;
  }
  return '';
}

const API_ORIGIN = apiBase();

/** fetch wrapper: prefixes the API origin and always sends the session cookie (cross-origin). */
export function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  return fetch(`${API_ORIGIN}${path}`, { credentials: 'include', ...init });
}

async function json<T>(res: Response): Promise<T> {
  return res.json() as Promise<T>;
}

async function apiCall<T = { message: string }>(
  res: Response,
): Promise<ApiResponse<T>> {
  return { ok: res.ok, data: await json<T>(res), status: res.status };
}

export const api = {
  async getAuthStatus(): Promise<AuthStatus> {
    const res = await apiFetch('/api/v1/admin/auth/status');
    return json<AuthStatus>(res);
  },

  async setup(username: string, password: string) {
    const res = await apiFetch('/api/v1/admin/auth/setup', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });
    return apiCall(res);
  },

  async login(username: string, password: string) {
    const res = await apiFetch('/api/v1/admin/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });
    return apiCall<{ message: string; lockoutRemaining?: number }>(res);
  },

  async logout() {
    await apiFetch('/api/v1/admin/auth/logout', { method: 'POST' });
  },

  async getStatus(): Promise<ApiStatus> {
    const res = await apiFetch('/api/v1/admin/status');
    if (res.status === 401) throw new Error('unauthorized');
    return json<ApiStatus>(res);
  },

  async getNetwork(): Promise<NetworkInfo> {
    const res = await apiFetch('/api/v1/admin/network');
    return json<NetworkInfo>(res);
  },

  async checkUpdate() {
    const res = await apiFetch('/api/v1/admin/update/check');
    return apiCall<UpdateInfo>(res);
  },

  async applyUpdate() {
    const res = await apiFetch('/api/v1/admin/update/apply', { method: 'POST' });
    return apiCall(res);
  },

  async changeMode(mode: string, port: number) {
    const res = await apiFetch('/api/v1/admin/control/mode', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ mode, port }),
    });
    return apiCall(res);
  },

  async restart() {
    const res = await apiFetch('/api/v1/admin/control/restart', {
      method: 'POST',
    });
    return apiCall(res);
  },

  async changePassword(currentPassword: string, newPassword: string) {
    const res = await apiFetch('/api/v1/admin/auth/change-password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword, newPassword }),
    });
    return apiCall(res);
  },

  async getTunnelStatus(): Promise<TunnelStatus> {
    const res = await apiFetch('/api/v1/admin/tunnel');
    return json<TunnelStatus>(res);
  },

  async registerTunnel() {
    const res = await apiFetch('/api/v1/admin/tunnel/register', { method: 'POST' });
    return apiCall<TunnelStatus>(res);
  },

  async unregisterTunnel() {
    const res = await apiFetch('/api/v1/admin/tunnel/unregister', { method: 'POST' });
    return apiCall(res);
  },

  async stopTunnel() {
    const res = await apiFetch('/api/v1/admin/tunnel/stop', { method: 'POST' });
    return apiCall(res);
  },

  async startTunnel(type: string, token?: string) {
    const res = await apiFetch('/api/v1/admin/tunnel/start', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ type, token }),
    });
    return apiCall<TunnelStatus>(res);
  },

  async getFederationPairings(): Promise<PairingListResponse> {
    const res = await apiFetch('/api/v1/admin/federation/pairings');
    return json<PairingListResponse>(res);
  },

  async generatePairingCode() {
    const res = await apiFetch('/api/v1/admin/federation/pairings/generate-code', {
      method: 'POST',
    });
    return apiCall<GenerateCodeResponse>(res);
  },

  async initiatePairing(serverUrl: string, code: string) {
    const res = await apiFetch('/api/v1/admin/federation/pairings/initiate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ serverUrl, code }),
    });
    return apiCall(res);
  },

  async removePairing(id: string) {
    const res = await apiFetch(`/api/v1/admin/federation/pairings/${id}`, {
      method: 'DELETE',
    });
    return apiCall(res);
  },

  async pausePairing(id: string) {
    const res = await apiFetch(`/api/v1/admin/federation/pairings/${id}/pause`, {
      method: 'PUT',
    });
    return apiCall(res);
  },

  async resumePairing(id: string) {
    const res = await apiFetch(`/api/v1/admin/federation/pairings/${id}/resume`, {
      method: 'PUT',
    });
    return apiCall(res);
  },

  // --- Workspace ---
  async getWorkspace(): Promise<WorkspaceDto> {
    const res = await apiFetch('/api/v1/admin/workspace');
    return json<WorkspaceDto>(res);
  },

  // --- Backups ---
  async getBackups(page = 1, pageSize = 20): Promise<BackupListResponse> {
    const res = await apiFetch(`/api/v1/admin/backups?page=${page}&pageSize=${pageSize}`);
    return json<BackupListResponse>(res);
  },

  async triggerBackup() {
    const res = await apiFetch('/api/v1/admin/backups', { method: 'POST' });
    return apiCall<BackupFileDto>(res);
  },

  async getBackupSchedule(): Promise<BackupSchedule> {
    const res = await apiFetch('/api/v1/admin/backups/schedule');
    return json<BackupSchedule>(res);
  },

  async updateBackupSchedule(cron: string, retention: number) {
    const res = await apiFetch('/api/v1/admin/backups/schedule', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ cron, retention }),
    });
    return apiCall<BackupSchedule>(res);
  },

  async restoreBackup(id: string) {
    const res = await apiFetch(`/api/v1/admin/backups/${id}/restore`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ confirmPhrase: 'RESTORE' }),
    });
    return apiCall(res);
  },

  // --- Audit ---
  async getAuditLogs(params: { page?: number; pageSize?: number; module?: string } = {}): Promise<AuditListResponse> {
    const q = new URLSearchParams();
    q.set('page', String(params.page ?? 1));
    q.set('pageSize', String(params.pageSize ?? 50));
    if (params.module && params.module !== 'all') q.set('module', params.module);
    const res = await apiFetch(`/api/v1/admin/audit/logs?${q.toString()}`);
    return json<AuditListResponse>(res);
  },

  async getAuditRetention(): Promise<AuditRetention> {
    const res = await apiFetch('/api/v1/admin/audit/retention');
    return json<AuditRetention>(res);
  },

  async updateAuditRetention(cron: string, retentionYears: number) {
    const res = await apiFetch('/api/v1/admin/audit/retention', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ cron, retentionYears }),
    });
    return apiCall<AuditRetention>(res);
  },

  async getAuditArchives(page = 1, pageSize = 20): Promise<AuditArchiveListResponse> {
    const res = await apiFetch(`/api/v1/admin/audit/archives?page=${page}&pageSize=${pageSize}`);
    return json<AuditArchiveListResponse>(res);
  },

  // --- Entity Types ---
  async getEntityTypes(): Promise<EntityTypeDto[]> {
    const res = await apiFetch('/api/v1/entity-types');
    return json<EntityTypeDto[]>(res);
  },

  // --- Entities ---
  async getEntities(includeInactive = false): Promise<EntityDto[]> {
    const res = await apiFetch(`/api/v1/entities?includeInactive=${includeInactive}`);
    return json<EntityDto[]>(res);
  },

  async createEntity(data: CreateEntityRequest) {
    const res = await apiFetch('/api/v1/entities', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    return apiCall<EntityDto>(res);
  },

  async updateEntity(id: string, data: UpdateEntityRequest) {
    const res = await apiFetch(`/api/v1/entities/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    return apiCall<EntityDto>(res);
  },

  async deactivateEntity(id: string) {
    const res = await apiFetch(`/api/v1/entities/${id}/deactivate`, { method: 'PUT' });
    return apiCall(res);
  },

  async reactivateEntity(id: string) {
    const res = await apiFetch(`/api/v1/entities/${id}/reactivate`, { method: 'PUT' });
    return apiCall(res);
  },

  // --- Branches ---
  async getBranches(entityId: string, includeInactive = false): Promise<BranchDto[]> {
    const res = await apiFetch(`/api/v1/entities/${entityId}/branches?includeInactive=${includeInactive}`);
    return json<BranchDto[]>(res);
  },

  async createBranch(entityId: string, data: CreateBranchRequest) {
    const res = await apiFetch(`/api/v1/entities/${entityId}/branches`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    return apiCall<BranchDto>(res);
  },

  async updateBranch(entityId: string, branchId: string, data: UpdateBranchRequest) {
    const res = await apiFetch(`/api/v1/entities/${entityId}/branches/${branchId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    return apiCall<BranchDto>(res);
  },

  async deactivateBranch(entityId: string, branchId: string) {
    const res = await apiFetch(`/api/v1/entities/${entityId}/branches/${branchId}/deactivate`, { method: 'PUT' });
    return apiCall(res);
  },

  async reactivateBranch(entityId: string, branchId: string) {
    const res = await apiFetch(`/api/v1/entities/${entityId}/branches/${branchId}/reactivate`, { method: 'PUT' });
    return apiCall(res);
  },
};
