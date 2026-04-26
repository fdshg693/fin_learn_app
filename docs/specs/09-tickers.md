# 銘柄参照 仕様書

## 概要

取引可能な銘柄（Ticker）の一覧および詳細を参照する機能。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/TickersController.cs`
- Domain: `src/Domain/Entities/Ticker.cs`
- Domain: `src/Domain/Entities/Company.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs`

## エンドポイント

### 銘柄一覧取得

```
GET /api/tickers
```

### 銘柄詳細取得

```
GET /api/tickers/{tickerId}
```

## 正常系シナリオ

### シナリオ1: 銘柄一覧取得

- **前提条件**: なし
- **入力**: なし
- **期待結果**: HTTP 200、全銘柄のサマリー一覧を返す

レスポンス例:
```json
[
  {
    "id": "<GUID>",
    "symbol": "AOKI",
    "companyName": "Aoki Holdings",
    "currentPrice": { "amount": 1200, "currency": "JPY" }
  },
  {
    "id": "<GUID>",
    "symbol": "HND",
    "companyName": "Hinode Systems",
    "currentPrice": { "amount": 860, "currency": "JPY" }
  },
  {
    "id": "<GUID>",
    "symbol": "SKR",
    "companyName": "Sakura Foods",
    "currentPrice": { "amount": 540, "currency": "JPY" }
  }
]
```

### シナリオ2: 銘柄詳細取得

- **前提条件**: 指定した `tickerId` が存在する
- **入力**: 有効な `tickerId`（UUID形式）
- **期待結果**: HTTP 200、銘柄の詳細情報を返す

レスポンス例:
```json
{
  "id": "<GUID>",
  "symbol": "AOKI",
  "companyName": "Aoki Holdings",
  "unitSize": 1,
  "currentPrice": { "amount": 1200, "currency": "JPY" }
}
```

## 異常系シナリオ

### エラー1: 銘柄が見つからない（詳細取得）

- **前提条件**: 存在しない `tickerId`
- **入力**: 無効な `tickerId`
- **期待結果**: HTTP 404 Not Found、`"Ticker was not found."`

## 初期データ（SeedData）

| 銘柄 | 企業名 | 初期価格 | UnitSize |
|---|---|---|---|
| AOKI | Aoki Holdings | 1,200円 | 1 |
| HND | Hinode Systems | 860円 | 1 |
| SKR | Sakura Foods | 540円 | 1 |

## ビジネスルール

- `currentPrice` はターンが進むたびに変動する（97%〜103%のランダム変動）
- `unitSize` は売買の最小単位だが、現状は全銘柄で 1（1株単位で売買可能）
- 銘柄の追加・削除は起動時の `SeedData` のみで決定される（永続化なし）

## 未決事項

- なし
