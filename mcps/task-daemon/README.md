Persistent task daemon for the MCP stack.

- Owns the SQLite database file.
- Exposes gRPC for the stateless MCP server.
- Exposes HTTP JSON plus a browser UI for manual inspection and CRUD.

Run with `npm run task-daemon` inside `mcps/`.