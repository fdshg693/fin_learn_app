import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { authenticate, connectWithFallback } from "./connection.js";

// ── Demo: ツール一覧表示 & 呼び出し ─────────────────────────────────

const __dirname = path.dirname(fileURLToPath(import.meta.url));
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
