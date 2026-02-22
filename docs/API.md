# API設計

## 概要

株売買シミュレーターの REST API。フロントエンド（React）からの操作を受け付け、`TurnProcessor` を通じてドメインロジックを実行する。

## 技術スタック

- **ASP.NET Core Minimal API**（.NET 9）
- **プロジェクト名:** `FinLearn.Api`
- **状態管理:** インメモリ（`ConcurrentDictionary<string, Game>`）— 学習用アプリのため DB は不要

## ゲーム設定

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
| POST | `/api/games/{id}/buy` | 買い注文 |
| POST | `/api/games/{id}/sell` | 売り注文 |
| POST | `/api/games/{id}/wait` | 待機（ターンスキップ） |

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
  ]
}
```

### GET /api/games/{id}

現在のゲーム状態を取得する。

**レスポンス:** `200 OK`（POST /api/games と同じ形式）

**エラー:** `404 Not Found`（ゲームが存在しない場合）

### POST /api/games/{id}/buy

買い注文を実行する。

**リクエスト:**

```json
{
  "instrumentId": 1,
  "quantity": 5,
  "price": null
}
```

| フィールド | 型 | 必須 | 説明 |
|---|---|---|---|
| instrumentId | int | YES | 銘柄ID |
| quantity | int | YES | 数量（1以上） |
| price | int? | NO | 指値価格（null = 成行） |

**レスポンス:** `200 OK`

```json
{
  "gameId": "abc123",
  "turn": 2,
  "player": { ... },
  "instruments": [ ... ],
  "warning": null
}
```

**警告付きレスポンス（ターン不変）:** `200 OK`

約定失敗や残高不足の場合、`warning` にメッセージが入り、ゲーム状態は変化しない。

```json
{
  "gameId": "abc123",
  "turn": 1,
  "player": { ... },
  "instruments": [ ... ],
  "warning": "現金が不足して購入できません"
}
```

### POST /api/games/{id}/sell

売り注文を実行する。リクエスト・レスポンス形式は `/buy` と同一。

### POST /api/games/{id}/wait

ターンをスキップする。リクエストボディなし。

**レスポンス:** `200 OK`（ゲーム状態のみ、`warning` は常に `null`）

---

## レスポンスDTO

### GameResponse（共通レスポンス）

```
gameId        : string
turn          : int
player        : PlayerDto
instruments   : InstrumentDto[]
warning       : string?         # アクション失敗時のみ
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

---

## プロジェクト構成

```
src/
  FinLearn.Api/
    Program.cs              # Minimal API エンドポイント定義 + DI
    Dtos/
      GameResponse.cs       # レスポンスDTO
      OrderRequest.cs       # リクエストDTO
    Services/
      GameStore.cs          # ConcurrentDictionary によるゲーム状態管理
```

## DI 構成（Program.cs）

```
GameStore           → Singleton（インメモリ状態管理）
TurnProcessor       → Transient（ComputerTrader + RandomPriceFluctuator）
Random              → Singleton（シード固定可能）
```

## 設計判断

- **Minimal API を採用**: Controller ベースではなく Minimal API。エンドポイント数が少なく、シンプルな構造に適合
- **警告は 200 で返す**: ドメインが例外ではなく `(Result, Warning)` タプルで結果を返す設計のため、HTTP ステータスではなく `warning` フィールドで表現。ターンが進まないことでフロントエンドが状態変化の有無を判断できる
- **ゲームIDは GUID**: `Guid.NewGuid().ToString("N")` で生成。URL フレンドリーな短い文字列
- **CORS**: React 開発サーバー（`localhost:5173`）を許可
