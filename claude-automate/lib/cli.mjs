/**
 * CLI 引数をパースする。
 * 戻り値: { workflowName, agentName, promptName }
 *
 * - `--workflow <name>` が指定された場合、agentName / promptName は無視される
 *   （呼び出し側でワークフロー優先のロジックを実装する）
 * - promptName はプロンプトが明示指定されなかった場合 null を返す。
 */
export function parseArgs(argv) {
  const args = argv.slice(2);
  let workflowName = null;
  let agentName = "plain";
  let promptName = null;

  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--workflow" && args[i + 1]) {
      workflowName = args[i + 1];
      i++;
    } else if (args[i] === "--agent" && args[i + 1]) {
      agentName = args[i + 1];
      i++;
    } else {
      promptName = args[i];
    }
  }

  return { workflowName, agentName, promptName };
}
