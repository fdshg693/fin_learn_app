import * as grpc from "@grpc/grpc-js";
import { loadTaskProto } from "../task-shared/proto.js";
import { TaskService, TaskServiceError } from "./taskService.js";

type StartedGrpcServer = {
  close: () => Promise<void>;
};

function optionalString(value: unknown) {
  if (typeof value !== "string") {
    return undefined;
  }

  const normalized = value.trim();
  return normalized.length > 0 ? normalized : undefined;
}

function normalizeCreateRequest(request: Record<string, unknown>) {
  return {
    title: typeof request.title === "string" ? request.title : "",
    description: typeof request.description === "string" ? request.description : undefined,
    status: optionalString(request.status),
  };
}

function normalizeListRequest(request: Record<string, unknown>) {
  return {
    status: optionalString(request.status),
    search: optionalString(request.search),
    sortBy: optionalString(request.sortBy),
    sortOrder: optionalString(request.sortOrder),
  };
}

function normalizeUpdateRequest(request: Record<string, unknown>) {
  const normalized: Record<string, unknown> = {
    id: typeof request.id === "string" ? request.id : "",
  };

  if (request.updateTitle === true) {
    normalized.title = typeof request.title === "string" ? request.title : "";
  }

  if (request.updateDescription === true) {
    normalized.description =
      typeof request.description === "string" ? request.description : "";
  }

  if (request.updateStatus === true) {
    normalized.status = optionalString(request.status);
  }

  return normalized;
}

function toServiceError(error: unknown): grpc.ServiceError {
  const serviceError = new Error("Task daemon failed.") as grpc.ServiceError;
  serviceError.code = grpc.status.INTERNAL;
  serviceError.details = "Task daemon failed.";

  if (error instanceof TaskServiceError) {
    serviceError.message = error.message;
    serviceError.details = error.message;
    serviceError.code =
      error.kind === "not_found" ? grpc.status.NOT_FOUND : grpc.status.INVALID_ARGUMENT;
    return serviceError;
  }

  if (error instanceof Error) {
    serviceError.message = error.message;
    serviceError.details = error.message;
  }

  return serviceError;
}

function handleUnary<TResponse>(
  work: () => TResponse,
  callback: grpc.sendUnaryData<TResponse>,
) {
  try {
    callback(null, work());
  } catch (error) {
    callback(toServiceError(error), null);
  }
}

export async function startGrpcServer(
  taskService: TaskService,
  address: string,
): Promise<StartedGrpcServer> {
  const grpcPackage = loadTaskProto();
  const server = new grpc.Server();

  server.addService(grpcPackage.tasks.TaskService.service, {
    createTask(
      call: grpc.ServerUnaryCall<unknown, unknown>,
      callback: grpc.sendUnaryData<unknown>,
    ) {
      handleUnary(
        () => taskService.createTask(normalizeCreateRequest(call.request as Record<string, unknown>)),
        callback,
      );
    },
    listTasks(
      call: grpc.ServerUnaryCall<unknown, unknown>,
      callback: grpc.sendUnaryData<unknown>,
    ) {
      handleUnary(
        () => taskService.listTasks(normalizeListRequest(call.request as Record<string, unknown>)),
        callback,
      );
    },
    getTask(
      call: grpc.ServerUnaryCall<unknown, unknown>,
      callback: grpc.sendUnaryData<unknown>,
    ) {
      handleUnary(() => taskService.getTask(call.request), callback);
    },
    updateTask(
      call: grpc.ServerUnaryCall<unknown, unknown>,
      callback: grpc.sendUnaryData<unknown>,
    ) {
      handleUnary(
        () => taskService.updateTask(normalizeUpdateRequest(call.request as Record<string, unknown>)),
        callback,
      );
    },
    deleteTask(
      call: grpc.ServerUnaryCall<unknown, unknown>,
      callback: grpc.sendUnaryData<unknown>,
    ) {
      handleUnary(() => taskService.deleteTask(call.request), callback);
    },
    checkHealth(
      _call: grpc.ServerUnaryCall<unknown, unknown>,
      callback: grpc.sendUnaryData<unknown>,
    ) {
      handleUnary(() => taskService.getHealthSummary(), callback);
    },
  });

  await new Promise<void>((resolve, reject) => {
    server.bindAsync(address, grpc.ServerCredentials.createInsecure(), (error) => {
      if (error) {
        reject(error);
        return;
      }

      resolve();
    });
  });

  server.start();

  return {
    close: () =>
      new Promise<void>((resolve, reject) => {
        server.tryShutdown((error) => {
          if (error) {
            reject(error);
            return;
          }

          resolve();
        });
      }),
  };
}