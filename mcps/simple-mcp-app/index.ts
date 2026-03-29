import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  registerAppTool,
  registerAppResource,
  RESOURCE_MIME_TYPE,
} from "@modelcontextprotocol/ext-apps/server";
import { z } from "zod";
import { readFile } from "fs/promises";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// ── MCP サーバー定義 ───────────────────────────────────────────────

const server = new McpServer({
  name: "simple-mcp-app",
  version: "1.0.0",
});

// ── 最新の選択結果を保持 ─────────────────────────────────────────────

let latestSelection: { choice: string; timestamp: string } | null = null;

// ── interactive-select: LLMが呼ぶ → UIが表示される ─────────────────

registerAppTool(
  server,
  "interactive-select",
  {
    title: "Interactive Select",
    description:
      "選択肢をインタラクティブなUIで表示し、ユーザーに選ばせる。" +
      "choices に選択肢の配列、prompt に表示メッセージを渡す。",
    inputSchema: {
      choices: z
        .array(z.string())
        .min(1)
        .describe("ユーザーに提示する選択肢"),
      prompt: z
        .string()
        .optional()
        .describe("選択肢の上に表示するメッセージ"),
    },
    _meta: {
      ui: { resourceUri: "ui://simple-app/view.html" },
    },
  },
  async ({ choices, prompt }) => {
    return {
      content: [
        {
          type: "text" as const,
          text: `選択肢を表示しました: ${choices.join(", ")}${prompt ? ` (${prompt})` : ""}`,
        },
      ],
    };
  },
);

// ── process-choice: Appからのみ呼ばれる（ユーザー選択を処理）──────

registerAppTool(
  server,
  "process-choice",
  {
    title: "Process Choice",
    description: "ユーザーが選んだ選択肢を処理して結果を返す。",
    inputSchema: {
      choice: z.string().describe("ユーザーが選択した値"),
    },
    _meta: {
      ui: {
        resourceUri: "ui://simple-app/view.html",
        visibility: ["app"], // Appからのみ呼び出し可能
      },
    },
  },
  async ({ choice }) => {
    latestSelection = { choice, timestamp: new Date().toISOString() };
    return {
      content: [
        {
          type: "text" as const,
          text: `「${choice}」を選択しました。`,
        },
      ],
    };
  },
);

// ── get-latest-selection: LLMが最新の選択結果を取得 ──────────────────

server.tool(
  "get-latest-selection",
  "最新のユーザー選択結果を取得する。interactive-select で表示された選択肢からユーザーが選んだ値を返す。",
  {},
  async () => {
    if (!latestSelection) {
      return {
        content: [
          {
            type: "text" as const,
            text: "まだ選択が行われていません。",
          },
        ],
      };
    }
    return {
      content: [
        {
          type: "text" as const,
          text: `選択値: ${latestSelection.choice} (時刻: ${latestSelection.timestamp})`,
        },
      ],
    };
  },
);

// ── UI リソース ──────────────────────────────────────────────────────

registerAppResource(
  server,
  "Simple App View",
  "ui://simple-app/view.html",
  { description: "インタラクティブ選択UI" },
  async () => {
    const html = await readFile(
      join(__dirname, "dist", "index.html"),
      "utf-8",
    );
    return {
      contents: [
        {
          uri: "ui://simple-app/view.html",
          mimeType: RESOURCE_MIME_TYPE,
          text: html,
        },
      ],
    };
  },
);

// ── 起動 ──────────────────────────────────────────────────────────

const transport = new StdioServerTransport();
await server.connect(transport);
