import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "./lib/cli.mjs";
import { loadAgents } from "./lib/agents.mjs";
import { loadPrompt } from "./lib/prompts.mjs";
import { run } from "./lib/runner.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));

const { agentName, promptName } = parseArgs(process.argv);

// --- エージェント読み込み ---
const agents = await loadAgents(resolve(__dirname, "agents"));
const agent = agents[agentName];
if (!agent) {
  console.error(`未知のエージェント: "${agentName}" (利用可能: ${Object.keys(agents).join(", ")})`);
  process.exit(1);
}

// --- プロンプト読み込み & 実行 ---
const resolvedPromptName = promptName ?? agent.defaultPrompt ?? "default.md";
const prompt = await loadPrompt(resolve(__dirname, "prompts"), resolvedPromptName);
await run(agent, prompt);
