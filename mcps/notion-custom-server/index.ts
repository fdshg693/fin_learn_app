import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { z } from "zod";
import { readFile } from "fs/promises";
import { fileURLToPath } from "url";
import { dirname, join } from "path";
import {
  authenticate,
  connectWithFallback,
} from "../notion-client/connection.js";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// ── ページ設定読み込み ────────────────────────────────────────────────

interface PageEntry {
  ID: number;
  URL: string;
  title: string;
}

async function loadPages(): Promise<PageEntry[]> {
  const raw = await readFile(
    join(__dirname, "config", "pages.json"),
    "utf-8"
  );
  return JSON.parse(raw) as PageEntry[];
}

// ── Notion MCP クライアント接続 ────────────────────────────────────

let notionClient: Client;

async function getNotionClient(): Promise<Client> {
  if (notionClient) return notionClient;
  const { accessToken } = await authenticate();
  notionClient = await connectWithFallback(accessToken);
  return notionClient;
}

// ── MCP サーバー定義 ───────────────────────────────────────────────

const server = new McpServer({
  name: "notion-custom-server",
  version: "1.0.0",
});

// ── list-pages ───────────────────────────────────────────────────────

server.registerTool(
  "list-pages",
  {
    title: "List Custom Pages",
    description:
      "Returns the list of pre-configured Notion pages (ID and title) from config/pages.json.",
    inputSchema: z.object({}),
  },
  async () => {
    const pages = await loadPages();
    const lines = pages.map((p) => `ID: ${p.ID}  Title: ${p.title}`);
    return { content: [{ type: "text" as const, text: lines.join("\n") }] };
  }
);

// ── get-page ─────────────────────────────────────────────────────────

server.registerTool(
  "get-page",
  {
    title: "Get Custom Page",
    description:
      "Fetches the content of a pre-configured Notion page by its ID (from config/pages.json) using notion-fetch.",
    inputSchema: z.object({
      id: z.number().int().describe("The page ID from list-pages."),
    }),
  },
  async ({ id }) => {
    const pages = await loadPages();
    const page = pages.find((p) => p.ID === id);
    if (!page) {
      return {
        content: [
          {
            type: "text" as const,
            text: `Page ID ${id} not found. Use list-pages to see available pages.`,
          },
        ],
        isError: true,
      };
    }
    const client = await getNotionClient();
    const result = await client.callTool({
      name: "notion-fetch",
      arguments: { id: page.URL },
    });
    return result as { content: Array<{ type: "text"; text: string }> };
  }
);

// ── notion-get-users ─────────────────────────────────────────────────

server.registerTool(
  "notion-get-users",
  {
    title: "Get Notion Users",
    description:
      "Retrieves a list of users in the current workspace. Shows workspace members and guests with their IDs, names, emails (if available), and types (person or bot). Supports cursor-based pagination. Pass user_id to fetch a specific user, or user_id='self' for the current user.",
    inputSchema: z.object({
      query: z
        .string()
        .optional()
        .describe(
          "Optional search query to filter users by name or email (case-insensitive)."
        ),
      start_cursor: z
        .string()
        .optional()
        .describe(
          "Cursor for pagination. Use the next_cursor value from the previous response to get the next page."
        ),
      page_size: z
        .number()
        .int()
        .optional()
        .describe(
          "Number of users to return per page (default: 100, max: 100)."
        ),
      user_id: z
        .string()
        .optional()
        .describe(
          'Return only the user matching this ID. Pass "self" to fetch the current user.'
        ),
    }),
  },
  async (args) => {
    const client = await getNotionClient();

    // 引数から undefined を除いた object を渡す
    const filteredArgs: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(args)) {
      if (value !== undefined) {
        filteredArgs[key] = value;
      }
    }

    const result = await client.callTool({
      name: "notion-get-users",
      arguments: filteredArgs,
    });

    return result as { content: Array<{ type: "text"; text: string }> };
  }
);

// ── 起動 ──────────────────────────────────────────────────────────

const transport = new StdioServerTransport();
await server.connect(transport);
