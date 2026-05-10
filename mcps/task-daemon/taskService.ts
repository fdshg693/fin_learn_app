import { ZodError } from "zod";
import {
  createTaskInputSchema,
  defaultSortOrder,
  defaultTaskSortField,
  defaultTaskStatus,
  listTasksInputSchema,
  taskIdSchema,
  type Task,
  updateTaskInputSchema,
} from "../task-shared/taskTypes.js";
import { TaskRepository } from "./taskRepository.js";

type TaskServiceErrorKind = "validation" | "not_found";

export class TaskServiceError extends Error {
  constructor(
    readonly kind: TaskServiceErrorKind,
    message: string,
  ) {
    super(message);
    this.name = "TaskServiceError";
  }
}

function normalizeTitle(title: string) {
  const value = title.trim();
  if (value.length === 0) {
    throw new TaskServiceError("validation", "Title is required.");
  }

  return value;
}

function normalizeDescription(description?: string) {
  return description?.trim() ?? "";
}

function normalizeSearch(search?: string) {
  const value = search?.trim();
  return value ? value : undefined;
}

function rethrowAsServiceError(error: unknown): never {
  if (error instanceof TaskServiceError) {
    throw error;
  }

  if (error instanceof ZodError) {
    throw new TaskServiceError(
      "validation",
      error.issues[0]?.message ?? "Task input is invalid.",
    );
  }

  throw error;
}

export class TaskService {
  constructor(private readonly repository: TaskRepository) {}

  getHealthSummary() {
    return {
      status: "ok",
      dbPath: this.repository.getDbPath(),
      taskCount: this.repository.countTasks(),
    };
  }

  createTask(input: unknown): Task {
    try {
      const parsed = createTaskInputSchema.parse(input);
      return this.repository.createTask({
        title: normalizeTitle(parsed.title),
        description: normalizeDescription(parsed.description),
        status: parsed.status ?? defaultTaskStatus,
      });
    } catch (error) {
      return rethrowAsServiceError(error);
    }
  }

  listTasks(input: unknown) {
    try {
      const parsed = listTasksInputSchema.parse(input ?? {});
      return {
        tasks: this.repository.listTasks({
          status: parsed.status,
          search: normalizeSearch(parsed.search),
          sortBy: parsed.sortBy ?? defaultTaskSortField,
          sortOrder: parsed.sortOrder ?? defaultSortOrder,
        }),
      };
    } catch (error) {
      return rethrowAsServiceError(error);
    }
  }

  getTask(input: unknown): Task {
    try {
      const parsed = taskIdSchema.parse(input);
      const task = this.repository.getTask(parsed.id);
      if (!task) {
        throw new TaskServiceError("not_found", `Task ${parsed.id} was not found.`);
      }

      return task;
    } catch (error) {
      return rethrowAsServiceError(error);
    }
  }

  updateTask(input: unknown): Task {
    try {
      const parsed = updateTaskInputSchema.parse(input);
      const task = this.repository.updateTask({
        id: parsed.id,
        title: parsed.title !== undefined ? normalizeTitle(parsed.title) : undefined,
        description:
          parsed.description !== undefined ? normalizeDescription(parsed.description) : undefined,
        status: parsed.status,
      });

      if (!task) {
        throw new TaskServiceError("not_found", `Task ${parsed.id} was not found.`);
      }

      return task;
    } catch (error) {
      return rethrowAsServiceError(error);
    }
  }

  deleteTask(input: unknown) {
    try {
      const parsed = taskIdSchema.parse(input);
      const deleted = this.repository.deleteTask(parsed.id);
      if (!deleted) {
        throw new TaskServiceError("not_found", `Task ${parsed.id} was not found.`);
      }

      return {
        deleted: true,
        id: parsed.id,
      };
    } catch (error) {
      return rethrowAsServiceError(error);
    }
  }
}