Stateless MCP task server.

- Exposes `task_create`, `task_list`, `task_get`, `task_update`, and `task_delete`.
- Stores no durable state locally.
- Delegates all persistence to the task daemon over gRPC.

Run with `npm run task-server` inside `mcps/` after the daemon is up.