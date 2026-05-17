# ポートフォリオ参照 仕様書

## 概要

投資家のポートフォリオ（現金・保有銘柄・評価額・損益）を参照する機能。
GET エンドポイントで現在の状態を返す。また、全アクション（BuyNow / SellNow / BuyLimit / SellLimit / Wait）のレスポンスにも最新ポートフォリオが含まれる。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/PortfoliosController.cs`
- Mapper: `backend/FinLearnApp.Api/Mappers/PortfolioMapper.cs`
- Domain: `library/Domain/Entities/Portfolio.cs`
- Domain: `library/Domain/Entities/Holding.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs`

## エンドポイント

```
GET /api/portfolios/{investorId}
```

## 正常系シナリオ

### シナリオ1: ポートフォリオ取得

- **前提条件**: 指定した `investorId` に対応するポートフォリオが存在する
- **入力**: 有効な `investorId`（UUID形式）
- **期待結果**: HTTP 200、ポートフォリオ情報を返す

レスポンス例:
```json
{
  "investorId": "<GUID>",
  "cash": { "amount": 700000, "currency": "JPY" },
  "initialAssets": { "amount": 1000000, "currency": "JPY" },
  "holdings": [
    {
      "tickerId": "<GUID>",
      "symbol": "AOKI",
      "quantity": 120,
      "currentPrice": { "amount": 1200, "currency": "JPY" },
      "marketValue": { "amount": 144000, "currency": "JPY" }
    }
  ],
  "valuation": { "amount": 844000, "currency": "JPY" },
  "profitLoss": { "amount": -156000, "currency": "JPY" }
}
```

## 異常系シナリオ

### エラー1: 投資家が見つからない

- **前提条件**: 存在しない `investorId`
- **入力**: 無効な `investorId`
- **期待結果**: HTTP 404 Not Found、`"Portfolio was not found for the specified investor."`

## ビジネスルール

- `valuation`（評価額）= 現金 + 全保有銘柄の時価（`保有数量 × 現在価格` の合計）
- `profitLoss`（損益）= 評価額 - 初期資産
- 初期資産はポートフォリオ作成時に固定され、以後変化しない
- 保有数量が0になった銘柄はポートフォリオの保有リストから自動的に削除される

## 初期データ（SeedData）

| 項目 | 値 |
|---|---|
| 投資家ID | `7b3e6c8d-6a8d-4e9f-9b7c-7c8d6c0e7f07` |
| 初期資産 | 1,000,000円 |
| 初期現金 | 700,000円（AOKI 120株、HND 80株を購入済みとして300,000円を出金） |
| AOKI 初期保有 | 120株 |
| HND 初期保有 | 80株 |
| SKR 初期保有 | 0株 |

## 未決事項

- なし
