# 設計判断

## Minimal API を採用

Controller ベースではなく Minimal API。エンドポイント数が少なく、シンプルな構造に適合。

## エラー応答の二系統

- **形式不正は 400 BadRequest**: `side` 未指定/不正、`quantity <= 0`、`price <= 0`、`stopPrice <= 0`。クライアントの不正リクエストに対する REST 慣習に沿った応答。`PlaceOrder` ハンドラ冒頭で弾く。
- **ゲーム状態依存の失敗は 200 OK + `warning`**: 残高不足・保有不足・約定ゼロなど。ドメインが例外ではなく `(Game, TradeResult?, string? Warning)` タプルで結果を返す設計に対応。`warning != null` の場合は `GameStore` が更新されない。

## 多重防御

API 層で形式不正を弾いた上で、`TurnProcessor.Buy/Sell` も同条件で `Rejected()` を返す safety net を持つ。Domain 層を直接呼ぶテストや将来の他経路に対する自律性を保つ。

## 取引履歴は API 側で保持

ドメインの `Game` は取引履歴を持たない。`GameStore` が直近の `TradeResult` を最大 `MaxRecentTrades`（=3）件キャッシュし、レスポンスに同梱する。

## DTO マッピング

`GameMapper.ToResponse` で `Game` + `IExchange` → `GameResponse` 変換。`IExchangeFactory` 経由で `game.Prices` から評価用の `IExchange` を生成。

## ゲームIDは GUID

`Guid.NewGuid().ToString("N")` で生成。URL フレンドリーな 32 文字 hex。

## CORS

環境変数 `CORS_ALLOWED_ORIGINS`（カンマ区切り）で設定可能。デフォルトは React 開発サーバー（`http://localhost:5173`）。

## Admin エンドポイント

板の状態確認用。本番用途ではなくデバッグ・テスト支援のため `/api/admin` 配下に配置。

## `public partial class Program { }`

`WebApplicationFactory<Program>` による統合テストを可能にする。

## 関連

- [STRUCTURE.md](STRUCTURE.md) — プロジェクト構成・DI構成
- [ENDPOINTS.md](ENDPOINTS.md) — エンドポイント詳細
