TypeScript で記述された、MCPサーバー・クライアント をまとめるフォルダ
`frontend` とは独立したプロジェクトとする
重複をさけるため、ルート直下でライブラリをインストールして共有する形にする

各サブフォルダ単体を動作させられるように、エントリポイントを各フォルダごに用意する形とする。

## MCPサーバー

- `task-server` : stateless な MCP サーバー。ツールを通してタスク CRUD を受け付け、常駐 daemon に gRPC で委譲する
- `simple-mcp-app` : UI リソース付きのサンプル MCP サーバー

## 常駐サーバー

- `task-daemon` : SQLite を唯一の永続化層として持ち、gRPC API と HTTP Web アプリを同じ Node プロセスで公開する

## MCPクライアント

- `notion-client` : リモートの Notion MCP サーバーに OAuth 2.0 (PKCE) で接続するクライアント