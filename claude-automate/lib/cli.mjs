import { Command } from "commander";

/**
 * CLI 引数をパースする。
 * 戻り値: { workflowName, agentName, promptName, promptRaw }
 *
 * すべてフラグ指定。位置引数は受け付けない。
 *
 * - `--workflow <name>` 指定時、agentName / promptName / promptRaw は呼び出し側で無視される
 * - `--prompt-raw <text>` はプロンプトファイル (`--prompt`) より優先される（呼び出し側で解決）
 * - 未指定時の promptName は null。agentName のデフォルトは "plain"。
 */
export function parseArgs(argv) {
  const program = new Command();
  program
    .name("run.mjs")
    .description("Claude Agent SDK 自動化ランナー")
    .option("--workflow <name>", "ワークフロー名 (workflows/<name>.json)")
    .option("--agent <name>", "エージェント名 (agents/<name>.json)", "plain")
    .option("--prompt <file>", "プロンプトファイル名 (prompts/<file>)")
    .option("--prompt-raw <text>", "生のプロンプト文字列 (--prompt より優先)")
    .allowExcessArguments(false)
    .parse(argv);

  const opts = program.opts();
  return {
    workflowName: opts.workflow ?? null,
    agentName: opts.agent ?? "plain",
    promptName: opts.prompt ?? null,
    promptRaw: opts.promptRaw ?? null,
  };
}
