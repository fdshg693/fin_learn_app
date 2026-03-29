/**
 * CLI 引数をパースする。
 * 戻り値: { agentName, promptName }
 * promptName はプロンプトが明示指定されなかった場合 null を返す。
 */
export function parseArgs(argv) {
  const args = argv.slice(2);
  let agentName = "plain";
  let promptName = null;

  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--agent" && args[i + 1]) {
      agentName = args[i + 1];
      i++;
    } else {
      promptName = args[i];
    }
  }

  return { agentName, promptName };
}
