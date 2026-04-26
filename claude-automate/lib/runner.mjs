import { query } from "@anthropic-ai/claude-agent-sdk";
import { mkdir, open } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const LOGS_DIR = resolve(__dirname, "..", "logs");

/**
 * 実行ごとに 1 ファイルのログを `claude-automate/logs/` に作成する。
 * ファイル名: `YYYY-MM-DD_HH-mm-ss-SSS_<agent>.log`
 */
async function openLogFile(agent) {
  await mkdir(LOGS_DIR, { recursive: true });
  const now = new Date();
  const pad = (n, w = 2) => String(n).padStart(w, "0");
  const stamp =
    `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}` +
    `_${pad(now.getHours())}-${pad(now.getMinutes())}-${pad(now.getSeconds())}` +
    `-${pad(now.getMilliseconds(), 3)}`;
  const safeAgent = (agent.name ?? "agent").replace(/[^A-Za-z0-9_.-]+/g, "_");
  const path = resolve(LOGS_DIR, `${stamp}_${safeAgent}.log`);
  const handle = await open(path, "a");
  return { path, handle };
}

/**
 * エージェントにプロンプトを投げ、ストリーミング出力する。
 * 標準出力と同じ内容を `logs/` 配下のログファイルにも書き出す。
 *
 * 戻り値: { logPath } — 結果を書き出したログファイルの絶対パス
 */
export async function run(agent, prompt) {
  const { path: logPath, handle: logHandle } = await openLogFile(agent);

  const write = (text) => {
    process.stdout.write(text);
    logHandle.write(text);
  };
  const writeLine = (text = "") => write(`${text}\n`);

  try {
    writeLine(`--- agent: ${agent.name} ---`);
    writeLine(`--- log: ${logPath} ---`);
    writeLine(`--- prompt ---`);
    writeLine(prompt);
    writeLine("--- response ---");

    for await (const message of query({
      prompt,
      options: agent.options,
    })) {
      if (message.type === "assistant" && message.message?.content) {
        for (const block of message.message.content) {
          if (block.type === "text") {
            write(block.text);
          } else if (block.name) {
            writeLine(`\n[Tool: ${block.name}]`);
          }
        }
      } else if (message.type === "result") {
        writeLine();
        writeLine(
          `--- done (${message.duration_ms}ms, $${message.total_cost_usd?.toFixed(4)}) ---`,
        );
      }
    }
  } finally {
    await logHandle.close();
  }

  return { logPath };
}
