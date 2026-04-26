import { readFile } from "node:fs/promises";
import { resolve, extname } from "node:path";

/**
 * workflows/ ディレクトリからワークフロー定義を読み込む。
 *
 * ワークフロー JSON の形式:
 * {
 *   "name": "表示名（省略可）",
 *   "description": "説明（省略可）",
 *   "steps": [
 *     { "agent": "file", "prompt": "step1.md" },
 *     { "agent": "plain", "prompt": "step2.md" }
 *   ]
 * }
 *
 * - 各 step の prompt は省略可（その場合エージェントの defaultPrompt にフォールバック）
 * - 現状は直列実行のみサポート
 */
export async function loadWorkflow(workflowsDir, workflowName) {
  const fileName = extname(workflowName) ? workflowName : `${workflowName}.json`;
  const path = resolve(workflowsDir, fileName);

  let raw;
  try {
    raw = await readFile(path, "utf-8");
  } catch {
    console.error(`ワークフローファイルが見つかりません: ${path}`);
    process.exit(1);
  }

  const config = JSON.parse(raw);
  if (!Array.isArray(config.steps) || config.steps.length === 0) {
    console.error(`ワークフロー "${workflowName}" に steps が定義されていません`);
    process.exit(1);
  }

  config.steps.forEach((step, i) => {
    if (!step || typeof step.agent !== "string") {
      console.error(`ワークフロー "${workflowName}" の step ${i + 1} に agent が指定されていません`);
      process.exit(1);
    }
  });

  return {
    name: config.name ?? workflowName,
    description: config.description ?? "",
    steps: config.steps.map((step) => ({
      agentName: step.agent,
      promptName: step.prompt ?? null,
    })),
  };
}
