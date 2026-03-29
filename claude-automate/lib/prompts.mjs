import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

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
