import { query } from "@anthropic-ai/claude-agent-sdk";

/**
 * エージェントにプロンプトを投げ、ストリーミング出力する。
 */
export async function run(agent, prompt) {
  console.log(`--- agent: ${agent.name} ---`);
  console.log(`--- prompt ---`);
  console.log(prompt);
  console.log("--- response ---");

  for await (const message of query({
    prompt,
    options: agent.options,
  })) {
    if (message.type === "assistant" && message.message?.content) {
      for (const block of message.message.content) {
        if (block.type === "text") {
          process.stdout.write(block.text);
        } else if (block.name) {
          console.log(`\n[Tool: ${block.name}]`);
        }
      }
    } else if (message.type === "result") {
      console.log();
      console.log(`--- done (${message.duration_ms}ms, $${message.total_cost_usd?.toFixed(4)}) ---`);
    }
  }
}
