import { dirname, join } from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

function parsePort(name: string, fallback: number) {
  const raw = process.env[name];
  if (!raw) {
    return fallback;
  }

  const value = Number(raw);
  if (!Number.isInteger(value) || value <= 0 || value > 65535) {
    throw new Error(`${name} must be a valid TCP port.`);
  }

  return value;
}

export type TaskDaemonConfig = {
  httpHost: string;
  httpPort: number;
  grpcHost: string;
  grpcPort: number;
  grpcAddress: string;
  dbPath: string;
  publicDir: string;
};

export function loadTaskDaemonConfig(): TaskDaemonConfig {
  const httpHost = process.env.TASK_DAEMON_HTTP_HOST ?? "127.0.0.1";
  const httpPort = parsePort("TASK_DAEMON_HTTP_PORT", 4310);
  const grpcHost = process.env.TASK_DAEMON_GRPC_HOST ?? "127.0.0.1";
  const grpcPort = parsePort("TASK_DAEMON_GRPC_PORT", 50061);
  const dbPath = process.env.TASK_DAEMON_DB_PATH ?? join(__dirname, "data", "tasks.db");
  const publicDir = join(__dirname, "public");

  return {
    httpHost,
    httpPort,
    grpcHost,
    grpcPort,
    grpcAddress: `${grpcHost}:${grpcPort}`,
    dbPath,
    publicDir,
  };
}