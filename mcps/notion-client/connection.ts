import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";
import { SSEClientTransport } from "@modelcontextprotocol/sdk/client/sse.js";
import { randomBytes } from "crypto";
import { exec } from "child_process";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import {
  discoverOAuthMetadata,
  registerClient,
  refreshAccessToken,
  generateCodeVerifier,
  generateCodeChallenge,
  buildAuthorizationUrl,
  exchangeCodeForTokens,
  waitForCallback,
} from "./oauth.js";

// ── Constants ──────────────────────────────────────────────────────

export const NOTION_MCP_URL = "https://mcp.notion.com";
const CALLBACK_PORT = 8976;
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const TOKEN_FILE = path.join(__dirname, ".tokens.json");

// ── Token Persistence ──────────────────────────────────────────────

type StoredAuth = {
  clientId: string;
  clientSecret?: string;
  accessToken: string;
  refreshToken?: string;
};

function loadStoredAuth(): StoredAuth | null {
  try {
    if (fs.existsSync(TOKEN_FILE)) {
      return JSON.parse(fs.readFileSync(TOKEN_FILE, "utf-8")) as StoredAuth;
    }
  } catch {
    // ignore
  }
  return null;
}

function saveStoredAuth(auth: StoredAuth): void {
  fs.writeFileSync(TOKEN_FILE, JSON.stringify(auth, null, 2), "utf-8");
}

// ── MCP Client Connection ──────────────────────────────────────────

async function createMcpClient(
  serverUrl: string,
  accessToken: string,
  useSSE: boolean = false
): Promise<Client> {
  const client = new Client(
    { name: "fin-learn-notion-mcp-client", version: "1.0.0" },
    { capabilities: { roots: {}, sampling: {} } }
  );

  const headers = {
    Authorization: `Bearer ${accessToken}`,
    "User-Agent": "fin-learn-notion-mcp-client/1.0",
  };

  const transport = useSSE
    ? new SSEClientTransport(new URL(`${serverUrl}/sse`), {
        requestInit: { headers },
      })
    : new StreamableHTTPClientTransport(new URL(`${serverUrl}/mcp`), {
        requestInit: { headers },
      });

  await client.connect(transport);
  return client;
}

export async function connectWithFallback(
  accessToken: string
): Promise<Client> {
  try {
    return await createMcpClient(NOTION_MCP_URL, accessToken, false);
  } catch (err) {
    console.warn("Streamable HTTP 接続失敗、SSE にフォールバック:", err);
    return await createMcpClient(NOTION_MCP_URL, accessToken, true);
  }
}

// ── Authentication ─────────────────────────────────────────────────

export async function authenticate(): Promise<{
  accessToken: string;
  refreshToken?: string;
}> {
  const stored = loadStoredAuth();

  // 保存済みトークンがあればリフレッシュを試みる
  if (stored?.refreshToken) {
    console.log("保存済みトークンを検出、リフレッシュを試行...");
    try {
      const metadata = await discoverOAuthMetadata(NOTION_MCP_URL);
      const tokens = await refreshAccessToken(
        stored.refreshToken,
        metadata,
        stored.clientId,
        stored.clientSecret
      );
      const newAuth: StoredAuth = {
        clientId: stored.clientId,
        clientSecret: stored.clientSecret,
        accessToken: tokens.access_token,
        refreshToken: tokens.refresh_token ?? stored.refreshToken,
      };
      saveStoredAuth(newAuth);
      console.log("トークンリフレッシュ成功");
      return {
        accessToken: newAuth.accessToken,
        refreshToken: newAuth.refreshToken,
      };
    } catch (err) {
      console.warn("トークンリフレッシュ失敗、再認証します:", err);
    }
  }

  // 新規 OAuth フロー
  console.log("OAuth 認証を開始します...");
  const metadata = await discoverOAuthMetadata(NOTION_MCP_URL);
  console.log("OAuth メタデータ取得完了");

  const redirectUri = `http://localhost:${CALLBACK_PORT}/callback`;
  const credentials = await registerClient(metadata, redirectUri);
  console.log("クライアント登録完了:", credentials.client_id);

  const codeVerifier = generateCodeVerifier();
  const codeChallenge = generateCodeChallenge(codeVerifier);
  const state = randomBytes(32).toString("hex");

  const authUrl = buildAuthorizationUrl(
    metadata,
    credentials.client_id,
    redirectUri,
    codeChallenge,
    state
  );

  console.log("\n以下のURLをブラウザで開いてNotionを認可してください:\n");
  console.log(authUrl);
  console.log();

  // 可能ならブラウザを自動で開く
  try {
    const cmd =
      process.platform === "win32"
        ? `start "" "${authUrl}"`
        : process.platform === "darwin"
          ? `open "${authUrl}"`
          : `xdg-open "${authUrl}"`;
    exec(cmd);
  } catch {
    // ブラウザ起動失敗は無視（URLは表示済み）
  }

  // コールバック待ち
  const { code } = await waitForCallback(CALLBACK_PORT, state);

  const tokens = await exchangeCodeForTokens(
    code,
    codeVerifier,
    metadata,
    credentials.client_id,
    credentials.client_secret,
    redirectUri
  );
  console.log("トークン取得成功");

  const auth: StoredAuth = {
    clientId: credentials.client_id,
    clientSecret: credentials.client_secret,
    accessToken: tokens.access_token,
    refreshToken: tokens.refresh_token,
  };
  saveStoredAuth(auth);

  return {
    accessToken: auth.accessToken,
    refreshToken: auth.refreshToken,
  };
}
