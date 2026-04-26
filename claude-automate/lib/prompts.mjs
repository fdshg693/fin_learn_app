import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

/**
 * プロンプトを解決する。
 *
 * 優先順位:
 *   1. promptRaw（明示指定） — CLI `--prompt-raw` または workflow step の `promptRaw`
 *   2. エージェントの defaultPromptRaw
 *   3. promptName（明示指定） — CLI 位置引数または workflow step の `prompt`
 *   4. エージェントの defaultPrompt
 *   5. "default.md"
 *
 * promptRaw 系がいずれかで解決されれば、ファイルは読まずそのままプロンプトとして渡す。
 *
 * 戻り値: { kind: "raw", text } | { kind: "file", name }
 */
export function resolvePrompt(promptName, promptRaw, agent) {
  const raw = promptRaw ?? agent.defaultPromptRaw ?? null;
  if (raw !== null) return { kind: "raw", text: raw };
  return { kind: "file", name: promptName ?? agent.defaultPrompt ?? "default.md" };
}

/**
 * prompts/ ディレクトリからプロンプトファイルを読み込む。
 */
export async function loadPrompt(promptsDir, promptName) {
  const promptPath = resolve(promptsDir, promptName);

  try {
    return (await readFile(promptPath, "utf-8")).trim();
  } catch {
    console.error(`プロンプトファイルが見つかりません: ${promptPath}`);
    process.exit(1);
  }
}
