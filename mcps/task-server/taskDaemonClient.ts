import * as grpc from "@grpc/grpc-js";
import { loadTaskProto } from "../task-shared/proto.js";
import {
  taskSchema,
  type CreateTaskInput,
  type ListTasksInput,
  type Task,
  type UpdateTaskInput,
} from "../task-shared/taskTypes.js";

type RpcClient = grpc.Client & Record<string, unknown>;

function asRpcClientMethod(candidate: unknown) {
  if (typeof candidate !== "function") {
    throw new Error("Task daemon client is missing the requested RPC method.");
  }

  return candidate as (
    request: object,
    callback: grpc.requestCallback<unknown>,
  ) => void;
}

export class TaskDaemonClient {
  private readonly client: RpcClient;

  constructor(address: string) {
    const grpcPackage = loadTaskProto();
    const Client = grpcPackage.tasks.TaskService;
    this.client = new Client(address, grpc.credentials.createInsecure()) as RpcClient;
  }

  async createTask(input: CreateTaskInput): Promise<Task> {
    return taskSchema.parse(
      await this.call("createTask", {
        title: input.title,
        description: input.description ?? "",
        status: input.status ?? "",
      }),
    );
  }

  async listTasks(input: ListTasksInput): Promise<Task[]> {
    const response = (await this.call("listTasks", {
      status: input.status ?? "",
      search: input.search ?? "",
      sortBy: input.sortBy ?? "",
      sortOrder: input.sortOrder ?? "",
    })) as { tasks?: unknown[] };

    return (response.tasks ?? []).map((task) => taskSchema.parse(task));
  }

  async getTask(id: string): Promise<Task> {
    return taskSchema.parse(await this.call("getTask", { id }));
  }

  async updateTask(input: UpdateTaskInput): Promise<Task> {
    return taskSchema.parse(
      await this.call("updateTask", {
        id: input.id,
        title: input.title ?? "",
        description: input.description ?? "",
        status: input.status ?? "",
        updateTitle: input.title !== undefined,
        updateDescription: input.description !== undefined,
        updateStatus: input.status !== undefined,
      }),
    );
  }

  async deleteTask(id: string): Promise<{ deleted: boolean; id: string }> {
    return (await this.call("deleteTask", { id })) as {
      deleted: boolean;
      id: string;
    };
  }

  close() {
    this.client.close();
  }

  private call(methodName: string, request: object): Promise<unknown> {
    return new Promise((resolve, reject) => {
      const method = asRpcClientMethod(this.client[methodName]);
      method.call(this.client, request, (error, response) => {
        if (error) {
          reject(error);
          return;
        }

        resolve(response);
      });
    });
  }
}