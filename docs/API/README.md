# API設計

株売買シミュレーターの REST API ドキュメント。フロントエンド（React）からの操作を受け付け、`TurnProcessor` を通じてドメインロジックを実行する。

## 目次

- [OVERVIEW.md](OVERVIEW.md) — 概要・技術スタック・ゲーム設定（`GameConfig`）
- [ENDPOINTS.md](ENDPOINTS.md) — エンドポイント一覧 + 各エンドポイントの詳細仕様
- [REQUEST_DTO.md](REQUEST_DTO.md) — リクエストDTO（`OrderRequest`）
- [RESPONSE_DTO.md](RESPONSE_DTO.md) — ゲーム状態レスポンスDTO（`GameResponse` + 子DTO）
- [ORDERBOOK_DTO.md](ORDERBOOK_DTO.md) — 板レスポンスDTO（`OrderBookResponse` / `OrderDto`）
- [STRUCTURE.md](STRUCTURE.md) — プロジェクト構成・DI構成
- [DESIGN.md](DESIGN.md) — 設計判断

## ファイル分割の方針

- **エンドポイント追加** → [ENDPOINTS.md](ENDPOINTS.md) にセクション追加。エンドポイント数が肥大化したら `endpoints/` サブフォルダに分割を検討
- **リクエスト/レスポンス型追加** → 用途別の DTO ファイルにセクション追加。新規系統（例: 統計系レスポンス）は別ファイルとして追加
- **設計判断の追記** → [DESIGN.md](DESIGN.md) に追記
