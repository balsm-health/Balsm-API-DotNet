interface Env {
  CF_API_TOKEN: string;
  CF_ACCOUNT_ID: string;
  CF_ZONE_ID: string;
  REGISTRY_SECRET: string;
  TUNNEL_DOMAIN: string;
}

interface RegisterRequest {
  serverId: string;
}

interface TunnelCreateResponse {
  result: { id: string; name: string };
  success: boolean;
  errors: { message: string }[];
}

interface DnsRecordResponse {
  result: { id: string };
  success: boolean;
  errors: { message: string }[];
}

interface TunnelListResponse {
  result: { id: string; name: string }[];
  success: boolean;
}

interface DnsListResponse {
  result: { id: string; name: string; content: string }[];
  success: boolean;
}

const CF_API = "https://api.cloudflare.com/client/v4";

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function errorResponse(message: string, status: number): Response {
  return jsonResponse({ error: message }, status);
}

function authenticate(request: Request, env: Env): boolean {
  const auth = request.headers.get("Authorization");
  return auth === `Bearer ${env.REGISTRY_SECRET}`;
}

function cfHeaders(env: Env): HeadersInit {
  return {
    Authorization: `Bearer ${env.CF_API_TOKEN}`,
    "Content-Type": "application/json",
  };
}

async function handleRegister(
  request: Request,
  env: Env
): Promise<Response> {
  const body = (await request.json()) as RegisterRequest;
  const { serverId } = body;

  if (!serverId || !/^[a-z0-9]{6,24}$/.test(serverId)) {
    return errorResponse(
      "serverId must be 6-24 lowercase alphanumeric characters",
      400
    );
  }

  const hostname = `${serverId}.${env.TUNNEL_DOMAIN}`;
  const tunnelName = `balsam-${serverId}`;

  // 1. Generate tunnel secret
  const secretBytes = new Uint8Array(32);
  crypto.getRandomValues(secretBytes);
  const tunnelSecret = btoa(String.fromCharCode(...secretBytes));

  // 2. Create tunnel
  const createRes = await fetch(
    `${CF_API}/accounts/${env.CF_ACCOUNT_ID}/tunnels`,
    {
      method: "POST",
      headers: cfHeaders(env),
      body: JSON.stringify({
        name: tunnelName,
        tunnel_secret: tunnelSecret,
      }),
    }
  );

  const createData = (await createRes.json()) as TunnelCreateResponse;
  if (!createData.success) {
    return errorResponse(
      `Failed to create tunnel: ${createData.errors?.[0]?.message ?? "unknown"}`,
      502
    );
  }

  const tunnelId = createData.result.id;

  // 3. Configure ingress rules
  const configRes = await fetch(
    `${CF_API}/accounts/${env.CF_ACCOUNT_ID}/tunnels/${tunnelId}/configurations`,
    {
      method: "PUT",
      headers: cfHeaders(env),
      body: JSON.stringify({
        config: {
          ingress: [
            { hostname, service: "http://localhost:5050" },
            { service: "http_status:404" },
          ],
        },
      }),
    }
  );

  if (!configRes.ok) {
    return errorResponse("Failed to configure tunnel ingress", 502);
  }

  // 4. Create DNS CNAME record
  const dnsRes = await fetch(
    `${CF_API}/zones/${env.CF_ZONE_ID}/dns_records`,
    {
      method: "POST",
      headers: cfHeaders(env),
      body: JSON.stringify({
        type: "CNAME",
        name: hostname,
        content: `${tunnelId}.cfargotunnel.com`,
        proxied: true,
      }),
    }
  );

  const dnsData = (await dnsRes.json()) as DnsRecordResponse;
  if (!dnsData.success) {
    // Cleanup: delete the tunnel we just created
    await fetch(
      `${CF_API}/accounts/${env.CF_ACCOUNT_ID}/tunnels/${tunnelId}`,
      { method: "DELETE", headers: cfHeaders(env) }
    );
    return errorResponse("Failed to create DNS record", 502);
  }

  // 5. Build the tunnel token
  // Token format: base64(JSON({ a: accountTag, t: tunnelId, s: tunnelSecret }))
  const tokenPayload = JSON.stringify({
    a: env.CF_ACCOUNT_ID,
    t: tunnelId,
    s: tunnelSecret,
  });
  const tunnelToken = btoa(tokenPayload);

  return jsonResponse({
    tunnelToken,
    tunnelUrl: `https://${hostname}`,
    tunnelId,
    serverId,
  });
}

async function handleDelete(
  serverId: string,
  env: Env
): Promise<Response> {
  if (!serverId || !/^[a-z0-9]{6,24}$/.test(serverId)) {
    return errorResponse("Invalid serverId", 400);
  }

  const hostname = `${serverId}.${env.TUNNEL_DOMAIN}`;
  const tunnelName = `balsam-${serverId}`;

  // Find and delete DNS record
  const dnsListRes = await fetch(
    `${CF_API}/zones/${env.CF_ZONE_ID}/dns_records?name=${hostname}`,
    { headers: cfHeaders(env) }
  );
  const dnsListData = (await dnsListRes.json()) as DnsListResponse;
  for (const record of dnsListData.result ?? []) {
    await fetch(
      `${CF_API}/zones/${env.CF_ZONE_ID}/dns_records/${record.id}`,
      { method: "DELETE", headers: cfHeaders(env) }
    );
  }

  // Find and delete tunnel
  const tunnelListRes = await fetch(
    `${CF_API}/accounts/${env.CF_ACCOUNT_ID}/tunnels?name=${tunnelName}&is_deleted=false`,
    { headers: cfHeaders(env) }
  );
  const tunnelListData = (await tunnelListRes.json()) as TunnelListResponse;
  for (const tunnel of tunnelListData.result ?? []) {
    await fetch(
      `${CF_API}/accounts/${env.CF_ACCOUNT_ID}/tunnels/${tunnel.id}`,
      { method: "DELETE", headers: cfHeaders(env) }
    );
  }

  return jsonResponse({ message: "Tunnel and DNS record deleted", serverId });
}

async function handleGet(
  serverId: string,
  env: Env
): Promise<Response> {
  if (!serverId || !/^[a-z0-9]{6,24}$/.test(serverId)) {
    return errorResponse("Invalid serverId", 400);
  }

  const tunnelName = `balsam-${serverId}`;
  const hostname = `${serverId}.${env.TUNNEL_DOMAIN}`;

  const tunnelListRes = await fetch(
    `${CF_API}/accounts/${env.CF_ACCOUNT_ID}/tunnels?name=${tunnelName}&is_deleted=false`,
    { headers: cfHeaders(env) }
  );
  const tunnelListData = (await tunnelListRes.json()) as TunnelListResponse;
  const exists = (tunnelListData.result?.length ?? 0) > 0;

  return jsonResponse({
    exists,
    tunnelUrl: exists ? `https://${hostname}` : null,
    tunnelId: exists ? tunnelListData.result[0].id : null,
    serverId,
  });
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname;

    // CORS preflight
    if (request.method === "OPTIONS") {
      return new Response(null, {
        headers: {
          "Access-Control-Allow-Origin": "*",
          "Access-Control-Allow-Methods": "GET, POST, DELETE, OPTIONS",
          "Access-Control-Allow-Headers": "Content-Type, Authorization",
        },
      });
    }

    // Auth check
    if (!authenticate(request, env)) {
      return errorResponse("Unauthorized", 401);
    }

    // Routes
    if (path === "/api/tunnels/register" && request.method === "POST") {
      return handleRegister(request, env);
    }

    const matchGet = path.match(/^\/api\/tunnels\/([a-z0-9]+)$/);
    if (matchGet && request.method === "GET") {
      return handleGet(matchGet[1], env);
    }

    const matchDelete = path.match(/^\/api\/tunnels\/([a-z0-9]+)$/);
    if (matchDelete && request.method === "DELETE") {
      return handleDelete(matchDelete[1], env);
    }

    return errorResponse("Not Found", 404);
  },
} satisfies ExportedHandler<Env>;
