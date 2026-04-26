#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { spawn } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const serverName = process.argv[2];
if (!serverName) {
  console.error("usage: passthrough.mjs <server-name>");
  process.exit(2);
}

const mcpsDir = dirname(fileURLToPath(import.meta.url));
const configPath = resolve(mcpsDir, ".mcp.json");

const config = JSON.parse(readFileSync(configPath, "utf8"));
const server = config.mcpServers?.[serverName];
if (!server) {
  console.error(`server "${serverName}" not found in ${configPath}`);
  process.exit(2);
}
if (server.type && server.type !== "stdio") {
  console.error(`passthrough only supports stdio servers (got "${server.type}")`);
  process.exit(2);
}

const child = spawn(server.command, server.args ?? [], {
  cwd: mcpsDir,
  env: { ...process.env, ...(server.env ?? {}) },
  stdio: "inherit",
  shell: process.platform === "win32",
});

const forward = (sig) => {
  if (!child.killed) child.kill(sig);
};
process.on("SIGINT", () => forward("SIGINT"));
process.on("SIGTERM", () => forward("SIGTERM"));

child.on("exit", (code, signal) => {
  if (signal) process.kill(process.pid, signal);
  else process.exit(code ?? 0);
});
child.on("error", (err) => {
  console.error(`failed to spawn "${serverName}": ${err.message}`);
  process.exit(1);
});
