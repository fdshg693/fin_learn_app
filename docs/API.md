# API設計

## 概要

株売買シミュレーターの REST API。フロントエンド（React）からの操作を受け付け、`TurnProcessor` を通じてドメインロジックを実行する。

## 技術スタック

- **ASP.NET Core Minimal API**（.NET 9）
- **プロジェクト名:** `FinLearn.Api`
- **状態管理:** インメモリ（`ConcurrentDictionary<string, Game>`）— 学習用アプリのため DB は不要

## ゲーム設定（`GameConfig`）

| パラメータ | デフォルト値 | 備考 |
|---|---|---|
| 銘柄数 | 3 | Instrument ID: 1, 2, 3 |
| 初期株価 | 各100 JPY | |
| 手数料 | 10 JPY | 固定 |
| プレイヤー初期資金 | 10,000 JPY | `Player` の定数 |

---

## エンドポイント一覧

| メソッド | パス | 説明 |
|---|---|---|
| POST | `/api/games` | 新規ゲーム作成 |
| GET | `/api/games/{id}` | ゲーム状態取得 |
| POST | `/api/games/{id}/orders` | 注文（買い/売り） |
| POST | `/api/games/{id}/wait` | 待機（ターンスキップ） |
| GET | `/api/admin/games/{id}/orderbook` | 板（注文帳）状態取得（管理用） |

ゲーム系エンドポイントは [src/FinLearn.Api/Endpoints/GameEndpoints.cs](../src/FinLearn.Api/Endpoints/GameEndpoints.cs) 、管理用エンドポイントは [src/FinLearn.Api/Endpoints/AdminEndpoints.cs](../src/FinLearn.Api/Endpoints/AdminEndpoints.cs) に定義されている。

---

## エンドポイント詳細

### POST /api/games

新規ゲームを作成し、初期状態を返す。

**レスポンス:** `201 Created`

```json
{
  "gameId": "abc123",
  "turn": 1,
  "player": {
    "name": "player",
    "cash": 10000,
    "positions": [],
    "totalAssets": 10000,
    "profitLoss": 0
  },
  "instruments": [
    { "id": 1, "price": 100 },
    { "id": 2, "price": 100 },
    { "id": 3, "price": 100 }
  ],
  "recentTrades": [],
  "warning": null
}
```

### GET /api/games/{id}

現在のゲーム状態を取得する。

**レスポンス:** `200 OK`（POST /api/games と同じ形式）

**エラー:** `404 Not Found`（ゲームが存在しない場合）

### POST /api/games/{id}/orders

買い注文または売り注文を実行する。`side` フィールドで売買区分を指定する。

**リクエスト:**

```json
{
  "side": "Buy",
  "instrumentId": 1,
  "quantity": 5,
  "price": null,
  "stopPrice": null,
  "expiresInTurns": 2
}
```

| フィールド | 型 | 必須 | 説明 |
|---|---|---|---|
| side | `"Buy"` \| `"Sell"` | YES | 売買区分。未指定または不正値は 400 |
| instrumentId | int | YES | 銘柄ID |
| quantity | int | YES | 数量（1 以上）。`<= 0` は 400 |
| price | int? | NO | 指値価格（null = 成行）。指定する場合 1 以上、`<= 0` は 400 |
| stopPrice | int? | NO | 逆指値価格（null = 通常注文）。指定する場合 1 以上、`<= 0` は 400 |
| expiresInTurns | int? | NO | 注文の有効ターン数。未指定時はデフォルト 2（生成ターンと次のターンまで有効）。`<= 0` は 400 |

**レスポンス:** `200 OK`

```json
{
  "gameId": "abc123",
  "turn": 2,
  "player": { ... },
  "instruments": [ ... ],
  "recentTrades": [
    { "instrumentId": 1, "side": "Buy", "filledQuantity": 5, "totalAmount": 500, "fee": 10 }
  ],
  "warning": null
}
```

**警告付きレスポンス:** `200 OK`

ゲーム状態に依存する失敗（残高不足、保有不足、成行で約定なし等）でドメインが `(Game, TradeResult?, string? Warning)` で `Warning != null` を返した場合、`warning` にメッセージが入り、`GameStore` は更新されない（取引履歴も追加されない）。

```json
{
  "gameId": "abc123",
  "turn": 1,
  "player": { ... },
  "instruments": [ ... ],
  "recentTrades": [ ... ],
  "warning": "現金が不足して購入できません"
}
```

**400 Bad Request:** 形式不正（`side` 未指定または `"Buy"` / `"Sell"` 以外、`quantity <= 0`、`price <= 0`、`stopPrice <= 0`、`expiresInTurns <= 0`）。

### POST /api/games/{id}/wait

ターンをスキップする。リクエストボディなし。

**レスポンス:** `200 OK`（ゲーム状態、`warning` は常に `null`。`trade` / `warning` は破棄される）

### GET /api/admin/games/{id}/orderbook

ゲームの注文帳（`OrderBook`）状態を取得する。デバッグ・管理用途。

**クエリパラメータ:**

| 名前 | 型 | デフォルト | 備考 |
|---|---|---|---|
| page | int | 1 | 1始まり。`<1` は 400 |
| pageSize | int | 50 | 1–200。範囲外は 400 |

**レスポンス:** `200 OK`

```json
{
  "orders": [
    {
      "id": 1,
      "traderId": "player",
      "instrumentId": 1,
      "side": "Buy",
      "type": "Limit",
      "quantity": 5,
      "price": 100,
      "stopPrice": null,
      "createdAtTurn": 1,
      "expiresAtTurn": 3
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
```

`page` がデータ範囲を超えた場合、`orders` は空配列、`totalCount` には全件数が入る。

**エラー:**
- `400 Bad Request`（`page < 1` / `pageSize < 1` / `pageSize > 200`）
- `404 Not Found`（ゲームが存在しない場合）

---

## レスポンスDTO

DTO 定義: [src/FinLearn.Api/Dtos/GameResponse.cs](../src/FinLearn.Api/Dtos/GameResponse.cs) / [src/FinLearn.Api/Dtos/OrderBookResponse.cs](../src/FinLearn.Api/Dtos/OrderBookResponse.cs)

### GameResponse（共通レスポンス）

```
gameId        : string
turn          : int
player        : PlayerDto
instruments   : InstrumentDto[]
recentTrades  : TradeResultDto[]  # 直近最大3件（GameStore.MaxRecentTrades）
warning       : string?           # アクション失敗時のみ
```

### PlayerDto

```
name          : string
cash          : int
positions     : PositionDto[]
totalAssets   : int             # cash + 全ポジション評価額
profitLoss    : int             # totalAssets - 初期資金
```

### PositionDto

```
instrumentId  : int
quantity      : int
currentPrice  : int
amount        : int             # quantity * currentPrice
```

### InstrumentDto

```
id            : int
price         : int             # 現在の市場価格
```

### TradeResultDto

```
instrumentId    : int
side            : string        # "Buy" | "Sell"
filledQuantity  : int
totalAmount     : int           # 約定金額（手数料抜き）
fee             : int
```

### OrderBookResponse / OrderDto

```
orders      : OrderDto[]
totalCount  : int   # 全注文件数（ページング前）
page        : int   # 現在のページ番号（1始まり）
pageSize    : int   # 1ページあたりの最大件数

OrderDto:
  id             : int
  traderId       : string
  instrumentId   : int
  side           : string       # "Buy" | "Sell"
  type           : string       # "Market" | "Limit" | "Stop" | "StopLimit"
  quantity       : int
  price          : int?
  stopPrice      : int?
  createdAtTurn  : int
  expiresAtTurn  : int             # 有効期限ターン番号（絶対値、currentTurn >= expiresAtTurn で除去）
```

---

## プロジェクト構成

```
src/
  FinLearn.Api/
    Program.cs                        # DI 登録 + CORS + エンドポイントマップ
    Endpoints/
      GameEndpoints.cs                # /api/games/*（Create/Get/Buy/Sell/Wait）
      AdminEndpoints.cs               # /api/admin/games/{id}/orderbook
    Dtos/
      GameResponse.cs                 # GameResponse, PlayerDto, PositionDto, InstrumentDto, TradeResultDto
      OrderRequest.cs                 # OrderRequest (instrumentId, quantity, price?, stopPrice?, expiresInTurns?)
      OrderBookResponse.cs            # OrderBookResponse, OrderDto
    Mappers/
      GameMapper.cs                   # Game → GameResponse 変換
      OrderBookMapper.cs              # OrderBook → OrderBookResponse 変換
    Services/
      GameConfig.cs                   # 銘柄数・初期株価・手数料
      GameStore.cs                    # ゲーム状態 + 直近取引履歴（最大3件）
```

## DI 構成（[Program.cs](../src/FinLearn.Api/Program.cs)）

```
GameConfig          → Singleton
GameStore           → Singleton（ConcurrentDictionary + 取引履歴）
IExchangeFactory    → Singleton（SimpleExchangeFactory）
TurnProcessor       → Transient（ComputerTrader + Market + RandomPriceFluctuator + IExchangeFactory）
Random              → Random.Shared を直接利用
```

## 設計判断

- **Minimal API を採用**: Controller ベースではなく Minimal API。エンドポイント数が少なく、シンプルな構造に適合
- **エラー応答の二系統**:
  - **形式不正は 400 BadRequest**: `side` 未指定/不正、`quantity <= 0`、`price <= 0`、`stopPrice <= 0`。クライアントの不正リクエストに対する REST 慣習に沿った応答。`PlaceOrder` ハンドラ冒頭で弾く。
  - **ゲーム状態依存の失敗は 200 OK + `warning`**: 残高不足・保有不足・約定ゼロなど。ドメインが例外ではなく `(Game, TradeResult?, string? Warning)` タプルで結果を返す設計に対応。`warning != null` の場合は `GameStore` が更新されない。
- **多重防御**: API 層で形式不正を弾いた上で、`TurnProcessor.Buy/Sell` も同条件で `Rejected()` を返す safety net を持つ。Domain 層を直接呼ぶテストや将来の他経路に対する自律性を保つ
- **取引履歴は API 側で保持**: ドメインの `Game` は取引履歴を持たない。`GameStore` が直近の `TradeResult` を最大 `MaxRecentTrades`（=3）件キャッシュし、レスポンスに同梱する
- **DTO マッピング**: `GameMapper.ToResponse` で `Game` + `IExchange` → `GameResponse` 変換。`IExchangeFactory` 経由で `game.Prices` から評価用の `IExchange` を生成
- **ゲームIDは GUID**: `Guid.NewGuid().ToString("N")` で生成。URL フレンドリーな 32 文字 hex
- **CORS**: 環境変数 `CORS_ALLOWED_ORIGINS`（カンマ区切り）で設定可能。デフォルトは React 開発サーバー（`http://localhost:5173`）
- **Admin エンドポイント**: 板の状態確認用。本番用途ではなくデバッグ・テスト支援のため `/api/admin` 配下に配置
- **`public partial class Program { }`**: `WebApplicationFactory<Program>` による統合テストを可能にする
