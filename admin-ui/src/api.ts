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
    const res = await fetch('/api/v1/admin/auth/status');
    return json<AuthStatus>(res);
  },

  async setup(username: string, password: string) {
    const res = await fetch('/api/v1/admin/auth/setup', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });
    return apiCall(res);
  },

  async login(username: string, password: string) {
    const res = await fetch('/api/v1/admin/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });
    return apiCall<{ message: string; lockoutRemaining?: number }>(res);
  },

  async logout() {
    await fetch('/api/v1/admin/auth/logout', { method: 'POST' });
  },

  async getStatus(): Promise<ApiStatus> {
    const res = await fetch('/api/v1/admin/status');
    if (res.status === 401) throw new Error('unauthorized');
    return json<ApiStatus>(res);
  },

  async getNetwork(): Promise<NetworkInfo> {
    const res = await fetch('/api/v1/admin/network');
    return json<NetworkInfo>(res);
  },

  async checkUpdate() {
    const res = await fetch('/api/v1/admin/update/check');
    return apiCall<UpdateInfo>(res);
  },

  async applyUpdate() {
    const res = await fetch('/api/v1/admin/update/apply', { method: 'POST' });
    return apiCall(res);
  },

  async changeMode(mode: string, port: number) {
    const res = await fetch('/api/v1/admin/control/mode', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ mode, port }),
    });
    return apiCall(res);
  },

  async restart() {
    const res = await fetch('/api/v1/admin/control/restart', {
      method: 'POST',
    });
    return apiCall(res);
  },

  async changePassword(currentPassword: string, newPassword: string) {
    const res = await fetch('/api/v1/admin/auth/change-password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword, newPassword }),
    });
    return apiCall(res);
  },

  async getTunnelStatus(): Promise<TunnelStatus> {
    const res = await fetch('/api/v1/admin/tunnel');
    return json<TunnelStatus>(res);
  },

  async registerTunnel() {
    const res = await fetch('/api/v1/admin/tunnel/register', { method: 'POST' });
    return apiCall<TunnelStatus>(res);
  },

  async unregisterTunnel() {
    const res = await fetch('/api/v1/admin/tunnel/unregister', { method: 'POST' });
    return apiCall(res);
  },

  async stopTunnel() {
    const res = await fetch('/api/v1/admin/tunnel/stop', { method: 'POST' });
    return apiCall(res);
  },

  async startTunnel(type: string, token?: string) {
    const res = await fetch('/api/v1/admin/tunnel/start', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ type, token }),
    });
    return apiCall<TunnelStatus>(res);
  },

  async getFederationPairings(): Promise<PairingListResponse> {
    const res = await fetch('/api/v1/admin/federation/pairings');
    return json<PairingListResponse>(res);
  },

  async generatePairingCode() {
    const res = await fetch('/api/v1/admin/federation/pairings/generate-code', {
      method: 'POST',
    });
    return apiCall<GenerateCodeResponse>(res);
  },

  async initiatePairing(serverUrl: string, code: string) {
    const res = await fetch('/api/v1/admin/federation/pairings/initiate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ serverUrl, code }),
    });
    return apiCall(res);
  },

  async removePairing(id: string) {
    const res = await fetch(`/api/v1/admin/federation/pairings/${id}`, {
      method: 'DELETE',
    });
    return apiCall(res);
  },

  async pausePairing(id: string) {
    const res = await fetch(`/api/v1/admin/federation/pairings/${id}/pause`, {
      method: 'PUT',
    });
    return apiCall(res);
  },

  async resumePairing(id: string) {
    const res = await fetch(`/api/v1/admin/federation/pairings/${id}/resume`, {
      method: 'PUT',
    });
    return apiCall(res);
  },
};
