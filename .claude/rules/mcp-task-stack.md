---
paths:
  - "mcps/task-daemon/**"
  - "mcps/task-server/**"
  - "mcps/task-shared/**"
---

## MCP Task Stack

`mcps/README.md` と各 README の前提は「永続化を持つ daemon」「stateless な MCP server」「共有契約」の3分割です。実装を触るときは、この責務境界を崩さないことを優先する。

## Architecture

- `mcps/task-daemon/index.ts` は `TaskRepository`・`TaskService`・gRPC server・HTTP server を同一プロセスで起動する
- 永続化は `mcps/task-daemon/taskRepository.ts` の SQLite のみ。SQL、DB パス、テーブル定義、検索ソート条件はここに閉じ込める
- `mcps/task-daemon/taskService.ts` は共有 Zod schema を使って入力を正規化し、default 値や trim を適用して `TaskServiceError` に寄せる
- `mcps/task-server/index.ts` は stdio MCP server。公開ツールは `task_create`・`task_list`・`task_get`・`task_update`・`task_delete` の5つで、状態は保持しない
- `mcps/task-server/taskDaemonClient.ts` は gRPC の thin client。MCP 側で独自の永続化や追加バリデーションを増やさず、daemon に委譲する
- `mcps/task-shared/tasks.proto` と `mcps/task-shared/taskTypes.ts` が RPC と型の共通契約。スキーマ変更時は daemon / server / shared を一緒に更新する

## Key Conventions

- 起動順は `mcps/` で `npm run task-daemon` を先、`npm run task-server` を後。server 側の既定接続先は `TASK_DAEMON_GRPC_ADDRESS=127.0.0.1:50061`
- daemon の既定 HTTP は `127.0.0.1:4310`、既定 DB は `mcps/task-daemon/data/tasks.db`。`/health` と `/api/tasks` 群に加えて `public/` の UI を配信する
- バリデーションの正本は `taskTypes.ts`。status は `todo|in_progress|done`、sort は `updatedAt|createdAt|title`、`title` は 1..200 文字、`description` は 0..4000 文字
- 部分更新は proto の `updateTitle` / `updateDescription` / `updateStatus` フラグで表現する。更新仕様を変えるときは daemon の normalize 処理と client の payload 生成を必ず揃える
- proto loader は `keepCase: false` なので、クライアント呼び出しは `createTask` などの camelCase 名を使う
- optional な文字列は gRPC payload 上では空文字で流し、daemon 側で `undefined` 相当に戻す実装がある。空文字と未指定を区別したい変更は両端の調整が必要