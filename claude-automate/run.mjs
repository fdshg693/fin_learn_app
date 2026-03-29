import { query } from "@anthropic-ai/claude-agent-sdk";
import { readFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));

// --- エージェント定義 ---
const agents = {
  plain: {
    name: "Plain Agent",
    description: "ツールなし。テキストのみで回答する。",
    options: {
      maxTurns: 1,
      tools: [],
    },
  },
  file: {
    name: "File Agent",
    description: "ファイルの読み書き・検索が可能。コードベースを調査・編集できる。",
    options: {
      maxTurns: 10,
      allowedTools: ["Read", "Edit", "Glob", "Grep"],
      permissionMode: "acceptEdits",
    },
  },
};

// --- 引数パース ---
const args = process.argv.slice(2);
let agentName = "plain";
let promptName = "default.md";

for (let i = 0; i < args.length; i++) {
  if (args[i] === "--agent" && args[i + 1]) {
    agentName = args[i + 1];
    i++;
  } else {
    promptName = args[i];
  }
}

const agent = agents[agentName];
if (!agent) {
  console.error(`未知のエージェント: "${agentName}" (利用可能: ${Object.keys(agents).join(", ")})`);
  process.exit(1);
}

// --- プロンプト読み込み ---
const promptPath = resolve(__dirname, "prompts", promptName);

let prompt;
try {
  prompt = (await readFile(promptPath, "utf-8")).trim();
} catch {
  console.error(`プロンプトファイルが見つかりません: ${promptPath}`);
  process.exit(1);
}

console.log(`--- agent: ${agent.name} ---`);
console.log(`--- prompt: ${promptName} ---`);
console.log(prompt);
console.log("--- response ---");

// --- 実行 ---
for await (const message of query({
  prompt,
  options: agent.options,
})) {
  if (message.type === "assistant" && message.message?.content) {
    for (const block of message.message.content) {
      if (block.type === "text") {
        process.stdout.write(block.text);
      } else if (block.name) {
        console.log(`\n[Tool: ${block.name}]`);
      }
    }
  } else if (message.type === "result") {
    console.log();
    console.log(`--- done (${message.duration_ms}ms, $${message.total_cost_usd?.toFixed(4)}) ---`);
  }
}
