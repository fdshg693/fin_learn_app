# エンドポイント

## エンドポイント一覧

| メソッド | パス | 説明 |
|---|---|---|
| POST | `/api/games` | 新規ゲーム作成 |
| GET | `/api/games/{id}` | ゲーム状態取得 |
| POST | `/api/games/{id}/orders` | 注文（買い/売り） |
| POST | `/api/games/{id}/wait` | 待機（ターンスキップ） |
| GET | `/api/admin/games/{id}/orderbook` | 板（注文帳）状態取得（管理用） |

ゲーム系エンドポイントは [src/FinLearn.Api/Endpoints/GameEndpoints.cs](../../src/FinLearn.Api/Endpoints/GameEndpoints.cs) 、管理用エンドポイントは [src/FinLearn.Api/Endpoints/AdminEndpoints.cs](../../src/FinLearn.Api/Endpoints/AdminEndpoints.cs) に定義されている。

レスポンス DTO の詳細は [RESPONSE_DTO.md](RESPONSE_DTO.md) / [ORDERBOOK_DTO.md](ORDERBOOK_DTO.md) を参照。

---

## POST /api/games

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

---

## GET /api/games/{id}

現在のゲーム状態を取得する。

**レスポンス:** `200 OK`（[POST /api/games](#post-apigames) と同じ形式）

**エラー:** `404 Not Found`（ゲームが存在しない場合）

---

## POST /api/games/{id}/orders

買い注文または売り注文を実行する。`side` フィールドで売買区分を指定する。

リクエスト DTO の詳細は [REQUEST_DTO.md](REQUEST_DTO.md) を参照。

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

---

## POST /api/games/{id}/wait

ターンをスキップする。リクエストボディなし。

**レスポンス:** `200 OK`（ゲーム状態、`warning` は常に `null`。`trade` / `warning` は破棄される）

---

## GET /api/admin/games/{id}/orderbook

ゲームの注文帳（`OrderBook`）状態を取得する。デバッグ・管理用途。

レスポンス DTO の詳細は [ORDERBOOK_DTO.md](ORDERBOOK_DTO.md) を参照。

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
