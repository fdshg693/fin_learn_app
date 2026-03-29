import { readFile, readdir } from "node:fs/promises";
import { resolve, basename, extname } from "node:path";

/**
 * agents/ ディレクトリからエージェント定義を読み込む。
 * ファイル名（拡張子除く）をデフォルトのエージェント名とし、
 * JSON 内の name フィールドがあればそちらを表示名として使う。
 */
export async function loadAgents(agentsDir) {
  const files = await readdir(agentsDir);
  const agents = {};

  for (const file of files) {
    if (extname(file) !== ".json") continue;

    const key = basename(file, ".json");
    const raw = await readFile(resolve(agentsDir, file), "utf-8");
    const config = JSON.parse(raw);

    agents[key] = {
      name: config.name ?? key,
      description: config.description ?? "",
      defaultPrompt: config.defaultPrompt ?? null,
      options: config.options ?? {},
    };
  }

  return agents;
}
