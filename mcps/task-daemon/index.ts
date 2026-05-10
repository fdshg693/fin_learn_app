import { loadTaskDaemonConfig } from "./config.js";
import { startGrpcServer } from "./grpcServer.js";
import { startHttpServer } from "./httpServer.js";
import { TaskRepository } from "./taskRepository.js";
import { TaskService } from "./taskService.js";

const config = loadTaskDaemonConfig();
const repository = new TaskRepository(config.dbPath);
const taskService = new TaskService(repository);

const [grpcServer, httpServer] = await Promise.all([
  startGrpcServer(taskService, config.grpcAddress),
  startHttpServer(taskService, config),
]);

console.log(`Task daemon gRPC listening on ${config.grpcAddress}`);
console.log(`Task daemon HTTP listening on http://${config.httpHost}:${config.httpPort}`);
console.log(`Task daemon SQLite path ${config.dbPath}`);

let isShuttingDown = false;

async function shutdown(signal: string) {
  if (isShuttingDown) {
    return;
  }

  isShuttingDown = true;
  console.log(`Received ${signal}. Shutting down task daemon.`);

  await Promise.allSettled([grpcServer.close(), httpServer.close()]);
  repository.close();
  process.exit(0);
}

process.on("SIGINT", () => {
  void shutdown("SIGINT");
});

process.on("SIGTERM", () => {
  void shutdown("SIGTERM");
});