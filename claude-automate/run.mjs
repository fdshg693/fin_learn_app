import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "./lib/cli.mjs";
import { loadAgents } from "./lib/agents.mjs";
import { loadPrompt, resolvePrompt } from "./lib/prompts.mjs";
import { loadWorkflow } from "./lib/workflows.mjs";
import { run } from "./lib/runner.mjs";

const __dirname = dirname(fileURLToPath(import.meta.url));
const AGENTS_DIR = resolve(__dirname, "agents");
const PROMPTS_DIR = resolve(__dirname, "prompts");
const WORKFLOWS_DIR = resolve(__dirname, "workflows");

const { workflowName, agentName, promptName, promptRaw } = parseArgs(process.argv);

// --- エージェント読み込み ---
const agents = await loadAgents(AGENTS_DIR);

function resolveAgent(name) {
  const agent = agents[name];
  if (!agent) {
    console.error(
      `未知のエージェント: "${name}" (利用可能: ${Object.keys(agents).join(", ")})`,
    );
    process.exit(1);
  }
  return agent;
}

// --- 実行ステップを構築 ---
// 単発実行とワークフロー実行を「ステップ列の直列実行」に統一する。
let workflow = null;
let steps;
if (workflowName) {
  workflow = await loadWorkflow(WORKFLOWS_DIR, workflowName);
  steps = workflow.steps;
} else {
  steps = [{ agentName, promptName, promptRaw }];
}

// --- ステップを直列実行 ---
const results = [];
for (let i = 0; i < steps.length; i++) {
  const step = steps[i];
  const agent = resolveAgent(step.agentName);
  const resolved = resolvePrompt(step.promptName, step.promptRaw, agent);
  const prompt =
    resolved.kind === "raw"
      ? resolved.text
      : await loadPrompt(PROMPTS_DIR, resolved.name);
  const promptLabel = resolved.kind === "raw" ? "<raw>" : resolved.name;

  if (workflow) {
    console.log(
      `\n=== Step ${i + 1}/${steps.length}: ${agent.name} (${promptLabel}) ===`,
    );
  }

  const { logPath } = await run(agent, prompt);
  results.push({
    step: i + 1,
    agent: agent.name,
    prompt: promptLabel,
    logPath,
  });
}

// --- 結果サマリー ---
console.log("\n=== 結果ファイル ===");
if (workflow) console.log(`workflow: ${workflow.name}`);
for (const r of results) {
  const prefix = workflow ? `[${r.step}] ` : "";
  console.log(`${prefix}${r.agent} (${r.prompt}): ${r.logPath}`);
}
