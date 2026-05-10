import * as grpc from "@grpc/grpc-js";
import * as protoLoader from "@grpc/proto-loader";
import { fileURLToPath } from "url";

export const taskProtoPath = fileURLToPath(new URL("./tasks.proto", import.meta.url));

export function loadTaskProto() {
  const packageDefinition = protoLoader.loadSync(taskProtoPath, {
    keepCase: false,
    longs: String,
    enums: String,
    defaults: true,
    oneofs: true,
  });

  return grpc.loadPackageDefinition(packageDefinition) as {
    tasks: {
      TaskService: grpc.ServiceClientConstructor & {
        service: grpc.ServiceDefinition<grpc.UntypedServiceImplementation>;
      };
    };
  };
}