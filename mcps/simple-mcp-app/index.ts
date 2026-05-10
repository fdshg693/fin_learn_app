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

let latestSelection: { answers: string[]; timestamp: string } | null = null;

// ── 保留中の選択待ち（interactive-select が resolve 待ち）─────────────

type PendingSelection = {
  resolve: (answers: string[]) => void;
  reject: (reason: Error) => void;
  timer: NodeJS.Timeout;
};
let pending: PendingSelection | null = null;

const DEFAULT_TIMEOUT_MS = 5 * 60 * 1000;

// ── interactive-select: LLMが呼ぶ → UIが表示される → 全回答待機 ──

registerAppTool(
  server,
  "interactive-select",
  {
    title: "Interactive Select",
    description:
      "1つ以上の質問をインタラクティブなUIで表示し、ユーザーが全ての質問に回答して送信するまで待機する。" +
      "各質問では提示された選択肢ボタンに加えてテキスト入力で自由回答もできる。" +
      "timeoutMs を超えるとタイムアウトエラーを返す。",
    inputSchema: {
      questions: z
        .array(
          z.object({
            choices: z
              .array(z.string())
              .min(1)
              .describe("提示する選択肢"),
            prompt: z
              .string()
              .optional()
              .describe("選択肢の上に表示する質問文"),
          }),
        )
        .min(1)
        .describe("ユーザーに提示する質問の配列。各質問は choices と任意の prompt を持つ"),
      timeoutMs: z
        .number()
        .int()
        .positive()
        .optional()
        .describe(
          `全質問を回答し終えるまでの待機タイムアウト（ミリ秒）。既定: ${DEFAULT_TIMEOUT_MS}`,
        ),
    },
    _meta: {
      ui: { resourceUri: "ui://simple-app/view.html" },
    },
  },
  async ({ questions, timeoutMs }) => {
    // 直前の待機があれば打ち切る（新しい質問群に置き換える）
    if (pending) {
      clearTimeout(pending.timer);
      pending.reject(new Error("新しい interactive-select に置き換えられました"));
      pending = null;
    }

    const limit = timeoutMs ?? DEFAULT_TIMEOUT_MS;
    const expectedCount = questions.length;

    try {
      const answers = await new Promise<string[]>((resolve, reject) => {
        const timer = setTimeout(() => {
          pending = null;
          reject(new Error(`タイムアウト: ${limit}ms 以内にユーザーの回答がありませんでした`));
        }, limit);
        pending = { resolve, reject, timer };
      });

      if (answers.length !== expectedCount) {
        return {
          isError: true,
          content: [
            {
              type: "text" as const,
              text: `回答数が一致しません: 期待 ${expectedCount}, 実際 ${answers.length}`,
            },
          ],
        };
      }

      const lines = answers.map((a, i) => `${i + 1}. ${a}`).join("\n");
      return {
        content: [
          {
            type: "text" as const,
            text: `ユーザーが回答した値:\n${lines}`,
          },
        ],
      };
    } catch (err) {
      return {
        isError: true,
        content: [
          {
            type: "text" as const,
            text: err instanceof Error ? err.message : String(err),
          },
        ],
      };
    }
  },
);

// ── process-choice: Appからのみ呼ばれる（ユーザー回答を処理）──────

registerAppTool(
  server,
  "process-choice",
  {
    title: "Process Choice",
    description: "ユーザーが入力した回答群を処理して結果を返す。",
    inputSchema: {
      answers: z
        .array(z.string())
        .min(1)
        .describe("各質問への回答（質問順、選択肢クリックでも自由入力でも文字列として渡す）"),
    },
    _meta: {
      ui: {
        resourceUri: "ui://simple-app/view.html",
        visibility: ["app"], // Appからのみ呼び出し可能
      },
    },
  },
  async ({ answers }) => {
    latestSelection = { answers, timestamp: new Date().toISOString() };

    // interactive-select の待機を解放
    if (pending) {
      clearTimeout(pending.timer);
      pending.resolve(answers);
      pending = null;
    }

    const lines = answers.map((a, i) => `${i + 1}. ${a}`).join("\n");
    return {
      content: [
        {
          type: "text" as const,
          text: `回答を受け付けました:\n${lines}`,
        },
      ],
    };
  },
);

// ── get-latest-selection: LLMが最新の回答群を取得 ────────────────────

server.tool(
  "get-latest-selection",
  "最新のユーザー回答群を取得する。interactive-select で表示された質問群にユーザーが回答した値の配列を返す。",
  {},
  async () => {
    if (!latestSelection) {
      return {
        content: [
          {
            type: "text" as const,
            text: "まだ回答が行われていません。",
          },
        ],
      };
    }
    const lines = latestSelection.answers
      .map((a, i) => `${i + 1}. ${a}`)
      .join("\n");
    return {
      content: [
        {
          type: "text" as const,
          text: `回答群 (時刻: ${latestSelection.timestamp}):\n${lines}`,
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
