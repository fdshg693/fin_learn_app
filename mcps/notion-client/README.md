# notion-client

リモートの Notion MCP サーバーに OAuth 2.0 (PKCE) で接続する MCP クライアント。

参考: https://developers.notion.com/guides/mcp/build-mcp-client

## 実行

```bash
cd mcps
npm run notion-client
```

初回実行時はブラウザが自動で開き、Notion の認可画面が表示される。
認可後、ローカルコールバックサーバー (`localhost:8976`) がトークンを受け取り、`.tokens.json` に保存する。
2回目以降はリフレッシュトークンで自動更新される。

## 構成

| ファイル | 役割 |
|----------|------|
| `index.ts` | エントリポイント: 認証 → MCP接続 → ツール一覧表示 |
| `oauth.ts` | OAuth 2.0 + PKCE ヘルパー (RFC 9470/8414/7636/7591) |

## 認証フロー

1. **OAuth Discovery** — `/.well-known/oauth-protected-resource` → `/.well-known/oauth-authorization-server`
2. **Dynamic Client Registration** (RFC 7591) — クライアントを自動登録
3. **PKCE (S256)** — code_verifier / code_challenge 生成
4. **ブラウザ認可** — Notion の認可画面へリダイレクト
5. **コールバック受信** — ローカル HTTP サーバーで authorization code を受け取る
6. **トークン交換** — code → access_token + refresh_token

## 接続方式

Streamable HTTP (`/mcp`) を優先し、失敗時は SSE (`/sse`) にフォールバックする。

## トークン管理

- `.tokens.json` にトークンを永続化（**gitignore 対象**）
- 起動時に refresh_token があればリフレッシュを試行
- `invalid_grant` 時は再認証フローを実行
