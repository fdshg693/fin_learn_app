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
  type OAuthMetadata,
} from "./oauth.js";

// ── Constants ──────────────────────────────────────────────────────

const NOTION_MCP_URL = "https://mcp.notion.com";
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

async function connectWithFallback(accessToken: string): Promise<Client> {
  try {
    return await createMcpClient(NOTION_MCP_URL, accessToken, false);
  } catch (err) {
    console.warn("Streamable HTTP 接続失敗、SSE にフォールバック:", err);
    return await createMcpClient(NOTION_MCP_URL, accessToken, true);
  }
}

// ── Authentication ─────────────────────────────────────────────────

async function authenticate(): Promise<{
  accessToken: string;
  metadata: OAuthMetadata;
  clientId: string;
  clientSecret?: string;
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
        metadata,
        clientId: newAuth.clientId,
        clientSecret: newAuth.clientSecret,
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
    metadata,
    clientId: auth.clientId,
    clientSecret: auth.clientSecret,
    refreshToken: auth.refreshToken,
  };
}

// ── Demo: ツール一覧表示 & 呼び出し ─────────────────────────────────

const TOOLS_DIR = path.join(__dirname, "tools");

type ToolDef = {
  name: string;
  description?: string;
  inputSchema?: Record<string, unknown>;
};

/** ツール名からファイル名を生成 (notion-search → notion-search.md) */
function toolFileName(toolName: string): string {
  return `${toolName}.md`;
}

/** 個別ツールのマークダウンを生成 */
function formatSingleToolMarkdown(tool: ToolDef): string {
  const lines: string[] = [];
  lines.push(`# \`${tool.name}\``);
  lines.push("");
  lines.push(tool.description ?? "(説明なし)");
  lines.push("");

  const schema = tool.inputSchema as Record<string, unknown> | undefined;
  const properties = schema?.properties as
    | Record<string, Record<string, unknown>>
    | undefined;
  const required = (schema?.required as string[]) ?? [];

  if (properties && Object.keys(properties).length > 0) {
    lines.push("## パラメータ");
    lines.push("");
    lines.push("| パラメータ | 型 | 必須 | 説明 |");
    lines.push("|------------|------|:----:|------|");
    for (const [name, prop] of Object.entries(properties)) {
      const type = String(prop.type ?? "unknown");
      const req = required.includes(name) ? "✓" : "";
      const desc = String(prop.description ?? "")
        .replace(/\n/g, " ")
        .replace(/\|/g, "\\|");
      lines.push(`| \`${name}\` | ${type} | ${req} | ${desc} |`);
    }
    lines.push("");
  }

  return lines.join("\n");
}

/** ツール一覧インデックスのマークダウンを生成 */
function formatToolsIndex(tools: ToolDef[]): string {
  const lines: string[] = [];
  lines.push("# 利用可能なツール一覧");
  lines.push("");
  lines.push(`> 合計 **${tools.length}** 個のツール`);
  lines.push("");
  lines.push("| # | ツール名 | 説明 | ファイル |");
  lines.push("|--:|----------|------|----------|");
  tools.forEach((tool, i) => {
    const desc = (tool.description ?? "(説明なし)")
      .replace(/\n/g, " ")
      .replace(/\|/g, "\\|");
    const file = toolFileName(tool.name);
    lines.push(`| ${i + 1} | \`${tool.name}\` | ${desc} | [${file}](${file}) |`);
  });
  lines.push("");
  return lines.join("\n");
}

async function demo(client: Client): Promise<void> {
  const { tools } = await client.listTools();

  if (tools.length === 0) {
    console.log("(ツールなし)");
    return;
  }

  // tools/ ディレクトリを確保
  if (!fs.existsSync(TOOLS_DIR)) {
    fs.mkdirSync(TOOLS_DIR, { recursive: true });
  }

  // インデックスファイルを出力
  const indexPath = path.join(TOOLS_DIR, "README.md");
  fs.writeFileSync(indexPath, formatToolsIndex(tools), "utf-8");
  console.log(`ツール一覧インデックスを出力: ${indexPath}`);

  // 各ツールを個別ファイルに出力
  for (const tool of tools) {
    const filePath = path.join(TOOLS_DIR, toolFileName(tool.name));
    fs.writeFileSync(filePath, formatSingleToolMarkdown(tool), "utf-8");
    console.log(`  ${tool.name} → ${filePath}`);
  }

  // notion-search ツールがあればデモ実行 → tools/ 配下に出力
  const searchTool = tools.find((t) => t.name === "notion-search");
  if (searchTool) {
    console.log("notion-search デモを実行中...");
    const result = await client.callTool({
      name: "notion-search",
      arguments: { query: "test", page_size: 3 },
    });

    const searchMd = [
      "# notion-search デモ結果",
      "",
      "## リクエスト",
      "",
      "```json",
      JSON.stringify({ query: "test", page_size: 3 }, null, 2),
      "```",
      "",
      "## レスポンス",
      "",
      "```json",
      JSON.stringify(result, null, 2),
      "```",
      "",
    ].join("\n");

    const searchPath = path.join(__dirname, "demo-search.md");
    fs.writeFileSync(searchPath, searchMd, "utf-8");
    console.log(`検索デモ結果を出力: ${searchPath}`);
  }
}

// ── Main ───────────────────────────────────────────────────────────

async function main(): Promise<void> {
  try {
    const { accessToken } = await authenticate();
    console.log("\nNotion MCP サーバーに接続中...");
    const client = await connectWithFallback(accessToken);
    console.log("接続成功！\n");

    await demo(client);

    await client.close();
    console.log("\n切断完了");
  } catch (err) {
    console.error("エラー:", err);
    process.exit(1);
  }
}

main();
