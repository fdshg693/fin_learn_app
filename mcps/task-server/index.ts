import * as grpc from "@grpc/grpc-js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  createTaskInputSchema,
  listTasksInputSchema,
  taskIdSchema,
  type Task,
  updateTaskInputSchema,
} from "../task-shared/taskTypes.js";
import { TaskDaemonClient } from "./taskDaemonClient.js";

const daemonAddress = process.env.TASK_DAEMON_GRPC_ADDRESS ?? "127.0.0.1:50061";
const taskDaemonClient = new TaskDaemonClient(daemonAddress);

const server = new McpServer({
  name: "task-server",
  version: "1.0.0",
});

function formatStatus(status: Task["status"]) {
  if (status === "in_progress") {
    return "In progress";
  }
  if (status === "done") {
    return "Done";
  }
  return "Todo";
}

function formatTask(task: Task) {
  return [
    `Task ${task.id}`,
    `Title: ${task.title}`,
    `Status: ${formatStatus(task.status)}`,
    `Created: ${task.createdAt}`,
    `Updated: ${task.updatedAt}`,
    `Description: ${task.description || "(empty)"}`,
  ].join("\n");
}

function formatTaskList(tasks: Task[]) {
  if (tasks.length === 0) {
    return "No tasks matched the current filters.";
  }

  return tasks
    .map(
      (task, index) =>
        `${index + 1}. ${task.title} [${formatStatus(task.status)}]\n   id=${task.id}\n   updated=${task.updatedAt}`,
    )
    .join("\n\n");
}

function asToolError(message: string) {
  return {
    isError: true,
    content: [{ type: "text" as const, text: message }],
  };
}

function formatRpcError(error: unknown) {
  if (typeof error === "object" && error !== null && "code" in error) {
    const serviceError = error as grpc.ServiceError;
    if (serviceError.code === grpc.status.UNAVAILABLE) {
      return `Task daemon is unavailable at ${daemonAddress}. Start it with npm run task-daemon inside mcps.`;
    }

    if (serviceError.details) {
      return serviceError.details;
    }
  }

  return error instanceof Error ? error.message : "Task daemon call failed.";
}

server.registerTool(
  "task_create",
  {
    title: "Create task",
    description: "Create a persisted task through the task daemon.",
    inputSchema: createTaskInputSchema,
  },
  async (input) => {
    try {
      const task = await taskDaemonClient.createTask(input);
      return {
        content: [{ type: "text", text: `Created task.\n\n${formatTask(task)}` }],
      };
    } catch (error) {
      return asToolError(formatRpcError(error));
    }
  },
);

server.registerTool(
  "task_list",
  {
    title: "List tasks",
    description: "List persisted tasks with optional status, search, and sorting filters.",
    inputSchema: listTasksInputSchema,
  },
  async (input) => {
    try {
      const tasks = await taskDaemonClient.listTasks(input);
      return {
        content: [{ type: "text", text: formatTaskList(tasks) }],
      };
    } catch (error) {
      return asToolError(formatRpcError(error));
    }
  },
);

server.registerTool(
  "task_get",
  {
    title: "Get task",
    description: "Fetch a persisted task by its id.",
    inputSchema: taskIdSchema,
  },
  async ({ id }) => {
    try {
      const task = await taskDaemonClient.getTask(id);
      return {
        content: [{ type: "text", text: formatTask(task) }],
      };
    } catch (error) {
      return asToolError(formatRpcError(error));
    }
  },
);

server.registerTool(
  "task_update",
  {
    title: "Update task",
    description: "Update one or more fields on an existing persisted task.",
    inputSchema: updateTaskInputSchema,
  },
  async (input) => {
    try {
      const task = await taskDaemonClient.updateTask(input);
      return {
        content: [{ type: "text", text: `Updated task.\n\n${formatTask(task)}` }],
      };
    } catch (error) {
      return asToolError(formatRpcError(error));
    }
  },
);

server.registerTool(
  "task_delete",
  {
    title: "Delete task",
    description: "Delete a persisted task by id.",
    inputSchema: taskIdSchema,
  },
  async ({ id }) => {
    try {
      const result = await taskDaemonClient.deleteTask(id);
      return {
        content: [
          {
            type: "text",
            text: result.deleted ? `Deleted task ${result.id}.` : `Task ${result.id} was not deleted.`,
          },
        ],
      };
    } catch (error) {
      return asToolError(formatRpcError(error));
    }
  },
);

const transport = new StdioServerTransport();
await server.connect(transport);

process.on("SIGINT", () => {
  taskDaemonClient.close();
  process.exit(0);
});

process.on("SIGTERM", () => {
  taskDaemonClient.close();
  process.exit(0);
});