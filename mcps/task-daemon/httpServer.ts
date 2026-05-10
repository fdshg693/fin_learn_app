import cors from "cors";
import express from "express";
import type { Server } from "http";
import { join } from "path";
import type { TaskDaemonConfig } from "./config.js";
import { TaskService, TaskServiceError } from "./taskService.js";

type StartedHttpServer = {
  close: () => Promise<void>;
};

function firstQueryValue(value: unknown) {
  if (typeof value !== "string") {
    return undefined;
  }

  const normalized = value.trim();
  return normalized.length > 0 ? normalized : undefined;
}

function sendError(response: express.Response, error: unknown) {
  if (error instanceof TaskServiceError) {
    response
      .status(error.kind === "not_found" ? 404 : 400)
      .json({ error: error.message });
    return;
  }

  const message = error instanceof Error ? error.message : "Unexpected daemon error.";
  console.error(error);
  response.status(500).json({ error: message });
}

export async function startHttpServer(
  taskService: TaskService,
  config: TaskDaemonConfig,
): Promise<StartedHttpServer> {
  const app = express();
  const publicIndexPath = join(config.publicDir, "index.html");

  app.use(cors());
  app.use(express.json());

  app.get("/health", (_request, response) => {
    response.json({
      ...taskService.getHealthSummary(),
      grpcAddress: config.grpcAddress,
      httpUrl: `http://${config.httpHost}:${config.httpPort}`,
    });
  });

  app.get("/api/tasks", (request, response) => {
    try {
      response.json(
        taskService.listTasks({
          status: firstQueryValue(request.query.status),
          search: firstQueryValue(request.query.search),
          sortBy: firstQueryValue(request.query.sortBy),
          sortOrder: firstQueryValue(request.query.sortOrder),
        }),
      );
    } catch (error) {
      sendError(response, error);
    }
  });

  app.get("/api/tasks/:id", (request, response) => {
    try {
      response.json({ task: taskService.getTask({ id: request.params.id }) });
    } catch (error) {
      sendError(response, error);
    }
  });

  app.post("/api/tasks", (request, response) => {
    try {
      const task = taskService.createTask(request.body);
      response.status(201).json({ task });
    } catch (error) {
      sendError(response, error);
    }
  });

  app.patch("/api/tasks/:id", (request, response) => {
    try {
      const task = taskService.updateTask({ id: request.params.id, ...request.body });
      response.json({ task });
    } catch (error) {
      sendError(response, error);
    }
  });

  app.delete("/api/tasks/:id", (request, response) => {
    try {
      response.json(taskService.deleteTask({ id: request.params.id }));
    } catch (error) {
      sendError(response, error);
    }
  });

  app.use(express.static(config.publicDir));

  app.get("/", (_request, response) => {
    response.sendFile(publicIndexPath);
  });

  const server = await new Promise<Server>((resolve, reject) => {
    const instance = app.listen(config.httpPort, config.httpHost, () => {
      resolve(instance);
    });

    instance.on("error", reject);
  });

  return {
    close: () =>
      new Promise<void>((resolve, reject) => {
        server.close((error) => {
          if (error) {
            reject(error);
            return;
          }

          resolve();
        });
      }),
  };
}