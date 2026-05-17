# マーケットスナップショット 仕様書

## 概要

現在のオーダーブック（買い注文・売り注文一覧）と成立済み取引履歴（Trade）を参照する機能。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/MarketController.cs`
- Domain: `library/Domain/Entities/OrderBook.cs`
- Domain: `library/Domain/Entities/Order.cs`
- Domain: `library/Domain/Entities/Trade.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs`

## エンドポイント

```
GET /api/market/snapshot
```

## 正常系シナリオ

### シナリオ1: マーケットスナップショット取得

- **前提条件**: なし（常に200を返す）
- **入力**: なし
- **期待結果**: HTTP 200、オーダーブックと取引履歴を返す

レスポンス例:
```json
{
  "buyOrders": [
    {
      "id": "<GUID>",
      "tickerId": "<GUID>",
      "symbol": "AOKI",
      "side": "Buy",
      "origin": "System",
      "price": { "amount": 1140, "currency": "JPY" },
      "quantity": 10,
      "createdAt": "2025-01-01T00:00:00Z"
    }
  ],
  "sellOrders": [
    {
      "id": "<GUID>",
      "tickerId": "<GUID>",
      "symbol": "AOKI",
      "side": "Sell",
      "origin": "System",
      "price": { "amount": 1200, "currency": "JPY" },
      "quantity": 10,
      "createdAt": "2025-01-01T00:00:00Z"
    }
  ],
  "trades": [
    {
      "id": "<GUID>",
      "tickerId": "<GUID>",
      "symbol": "AOKI",
      "quantity": 5,
      "price": { "amount": 1200, "currency": "JPY" },
      "fee": { "amount": 500, "currency": "JPY" },
      "executedAt": "2025-01-01T00:00:01Z"
    }
  ]
}
```

## ビジネスルール

- 買い注文・売り注文は作成日時の降順（新しい順）で返される
- 取引履歴は実行日時の降順（新しい順）で返される
- `origin` は `System`（コンピュータ注文）または `Investor`（投資家注文）の文字列
- `side` は `Buy` または `Sell` の文字列
- 取引手数料は `Exchange.Fee` = 固定500円（JPY）
- 注文は約定後も残数量があればリスト上に残る。残数量0になった注文はリストから消える

## 未決事項

- ページネーションなし（全件返す）
- 取引履歴の件数が増えた場合のパフォーマンス対策は未実装
