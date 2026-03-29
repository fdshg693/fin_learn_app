import { randomBytes, createHash } from "crypto";
import http from "http";

// ── Types ──────────────────────────────────────────────────────────

export type OAuthMetadata = {
  issuer: string;
  authorization_endpoint: string;
  token_endpoint: string;
  registration_endpoint?: string;
  code_challenge_methods_supported?: string[];
  grant_types_supported?: string[];
  response_types_supported?: string[];
  scopes_supported?: string[];
};

export type ClientCredentials = {
  client_id: string;
  client_secret?: string;
  client_id_issued_at?: number;
  client_secret_expires_at?: number;
};

export type TokenResponse = {
  access_token: string;
  token_type: string;
  expires_in?: number;
  refresh_token?: string;
  scope?: string;
};

// ── PKCE ───────────────────────────────────────────────────────────

function base64URLEncode(buf: Buffer): string {
  return buf
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=/g, "");
}

export function generateCodeVerifier(): string {
  return base64URLEncode(randomBytes(32));
}

export function generateCodeChallenge(verifier: string): string {
  return base64URLEncode(createHash("sha256").update(verifier).digest());
}

function generateState(): string {
  return randomBytes(32).toString("hex");
}

// ── OAuth Discovery (RFC 9470 + RFC 8414) ──────────────────────────

export async function discoverOAuthMetadata(
  mcpServerUrl: string
): Promise<OAuthMetadata> {
  const url = new URL(mcpServerUrl);

  // Step 1: RFC 9470 — Protected Resource Metadata
  const protectedResourceUrl = new URL(
    "/.well-known/oauth-protected-resource",
    url
  );
  const prResponse = await fetch(protectedResourceUrl.toString());
  if (!prResponse.ok) {
    throw new Error(
      `Failed to fetch protected resource metadata: ${prResponse.status}`
    );
  }
  const protectedResource = await prResponse.json();
  const authServers = protectedResource.authorization_servers;
  if (!Array.isArray(authServers) || authServers.length === 0) {
    throw new Error(
      "No authorization servers found in protected resource metadata"
    );
  }

  // Step 2: RFC 8414 — Authorization Server Metadata
  const metadataUrl = new URL(
    "/.well-known/oauth-authorization-server",
    authServers[0]
  );
  const metadataResponse = await fetch(metadataUrl.toString());
  if (!metadataResponse.ok) {
    throw new Error(
      `Failed to fetch authorization server metadata: ${metadataResponse.status}`
    );
  }

  const metadata = (await metadataResponse.json()) as OAuthMetadata;
  if (!metadata.authorization_endpoint || !metadata.token_endpoint) {
    throw new Error("Missing required OAuth endpoints in metadata");
  }
  return metadata;
}

// ── Dynamic Client Registration (RFC 7591) ─────────────────────────

export async function registerClient(
  metadata: OAuthMetadata,
  redirectUri: string
): Promise<ClientCredentials> {
  if (!metadata.registration_endpoint) {
    throw new Error("Server does not support dynamic client registration");
  }

  const response = await fetch(metadata.registration_endpoint, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
    },
    body: JSON.stringify({
      client_name: "fin-learn-notion-mcp-client",
      redirect_uris: [redirectUri],
      grant_types: ["authorization_code", "refresh_token"],
      response_types: ["code"],
      token_endpoint_auth_method: "none",
    }),
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Client registration failed: ${response.status} - ${body}`);
  }
  return (await response.json()) as ClientCredentials;
}

// ── Authorization URL ──────────────────────────────────────────────

export function buildAuthorizationUrl(
  metadata: OAuthMetadata,
  clientId: string,
  redirectUri: string,
  codeChallenge: string,
  state: string
): string {
  const params = new URLSearchParams({
    response_type: "code",
    client_id: clientId,
    redirect_uri: redirectUri,
    state,
    code_challenge: codeChallenge,
    code_challenge_method: "S256",
    prompt: "consent",
  });
  return `${metadata.authorization_endpoint}?${params.toString()}`;
}

// ── Token Exchange ─────────────────────────────────────────────────

export async function exchangeCodeForTokens(
  code: string,
  codeVerifier: string,
  metadata: OAuthMetadata,
  clientId: string,
  clientSecret: string | undefined,
  redirectUri: string
): Promise<TokenResponse> {
  const params = new URLSearchParams({
    grant_type: "authorization_code",
    code,
    client_id: clientId,
    redirect_uri: redirectUri,
    code_verifier: codeVerifier,
  });
  if (clientSecret) params.append("client_secret", clientSecret);

  const response = await fetch(metadata.token_endpoint, {
    method: "POST",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
      Accept: "application/json",
    },
    body: params.toString(),
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Token exchange failed: ${response.status} - ${body}`);
  }

  const tokens = (await response.json()) as TokenResponse;
  if (!tokens.access_token) {
    throw new Error("Missing access_token in response");
  }
  return tokens;
}

// ── Token Refresh ──────────────────────────────────────────────────

export async function refreshAccessToken(
  refreshToken: string,
  metadata: OAuthMetadata,
  clientId: string,
  clientSecret: string | undefined
): Promise<TokenResponse> {
  const params = new URLSearchParams({
    grant_type: "refresh_token",
    refresh_token: refreshToken,
    client_id: clientId,
  });
  if (clientSecret) params.append("client_secret", clientSecret);

  const response = await fetch(metadata.token_endpoint, {
    method: "POST",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
      Accept: "application/json",
    },
    body: params.toString(),
  });

  if (!response.ok) {
    const body = await response.text();
    try {
      const err = JSON.parse(body);
      if (err.error === "invalid_grant") throw new Error("REAUTH_REQUIRED");
    } catch (e) {
      if (e instanceof Error && e.message === "REAUTH_REQUIRED") throw e;
    }
    throw new Error(`Token refresh failed: ${response.status} - ${body}`);
  }
  return (await response.json()) as TokenResponse;
}

// ── CLI OAuth Flow (local callback server) ─────────────────────────

/**
 * ローカルHTTPサーバーを起動し、OAuthコールバックを1回だけ受け取る。
 * 受け取ったら即サーバーを閉じる。
 */
export function waitForCallback(
  port: number,
  expectedState: string
): Promise<{ code: string }> {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      const url = new URL(req.url ?? "/", `http://localhost:${port}`);
      if (url.pathname !== "/callback") {
        res.writeHead(404);
        res.end("Not Found");
        return;
      }

      const error = url.searchParams.get("error");
      if (error) {
        const desc = url.searchParams.get("error_description") ?? "Unknown";
        res.writeHead(400);
        res.end(`OAuth Error: ${error} - ${desc}`);
        server.close();
        reject(new Error(`OAuth error: ${error} - ${desc}`));
        return;
      }

      const stateParam = url.searchParams.get("state");
      if (stateParam !== expectedState) {
        res.writeHead(400);
        res.end("Invalid state parameter");
        server.close();
        reject(new Error("Invalid state parameter — possible CSRF attack"));
        return;
      }

      const code = url.searchParams.get("code");
      if (!code) {
        res.writeHead(400);
        res.end("Missing authorization code");
        server.close();
        reject(new Error("Missing authorization code"));
        return;
      }

      res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
      res.end(
        "<html><body><h1>認証成功！</h1><p>このタブを閉じてターミナルに戻ってください。</p></body></html>"
      );
      server.close();
      resolve({ code });
    });

    server.listen(port, () => {
      console.log(`コールバックサーバー起動: http://localhost:${port}/callback`);
    });

    // 5分でタイムアウト
    setTimeout(() => {
      server.close();
      reject(new Error("OAuth callback timed out (5 min)"));
    }, 5 * 60 * 1000);
  });
}
