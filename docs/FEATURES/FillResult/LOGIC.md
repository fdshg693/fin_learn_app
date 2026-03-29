# 約定ロジック

> ソース: [`OrderBook.Match`](../../../src/FinLearn.Core/Models/OrderBook.cs) / [`FillResult`](../../../src/FinLearn.Core/Results/FillResult.cs) / [`OrderFill`](../../../src/FinLearn.Core/Results/OrderFill.cs)

## 概要

`OrderBook.Match(Order incoming)` が約定の中核。受注注文（incoming）を板の反対側注文と価格優先でマッチングし、`FillResult` を返す。

## データ構造

```
FillResult
├── Fills: IReadOnlyList<OrderFill>   … 全参加注文の約定明細
│   └── OrderFill(OrderId, FilledQuantity, TotalAmount)
└── UpdatedBook: OrderBook            … 約定後の板状態
```

- `GetFill(orderId)` で特定注文の約定結果を取得（未約定なら `null`）
- Fills には incoming 注文 **と** マッチした待機注文の両方が含まれる

## ソート順（価格優先）

| 側 | ソート | 意味 |
|---|---|---|
| 売り注文 | 価格昇順 (`OrderBy(Price)`) | 安い順にマッチ |
| 買い注文 | 価格降順 (`OrderByDescending(Price)`) | 高い順にマッチ |

## マッチング手順

### 1. 対象注文の抽出

受注注文の反対側から、価格条件を満たす注文を `TakeWhile` で抽出する。ソート済みリストに対する `TakeWhile` なので、条件を満たさない最初の注文以降は全てスキップされる。

**指値注文の場合:**

| incoming | 対象 | 条件 |
|---|---|---|
| 買い指値 | 売り注文（安い順） | `売り価格 <= 買い指値` |
| 売り指値 | 買い注文（高い順） | `買い価格 >= 売り指値` |

**成行注文の場合:**

| incoming | 対象 | 条件 |
|---|---|---|
| 買い成行（StopPrice なし） | 売り注文（安い順） | 全て |
| 買い成行（StopPrice あり） | 売り注文（安い順） | `売り価格 <= StopPrice` |
| 売り成行（StopPrice なし） | 買い注文（高い順） | 全て |
| 売り成行（StopPrice あり） | 買い注文（高い順） | `買い価格 >= StopPrice` |

### 2. Fill ループ

```
remaining = incoming.Quantity

for each matchingOrder:
    if remaining <= 0: break
    fill     = Min(remaining, matchingOrder.Quantity)
    amount   = fill × matchingOrder.Price        ← 常に待機注文の価格
    remaining -= fill

    fills.Add(OrderFill(matchingOrder.Id, fill, amount))

    if fill == matchingOrder.Quantity:
        板から除去（完全約定）
    else:
        WithQuantity(残数量) で更新（部分約定）

fills.Add(OrderFill(incoming.Id, 約定数量, 合計金額))   ← incoming 分を末尾に追加
```

### 3. 重要ルール

- **約定価格は常に待機注文（板に既存の注文）の価格**。incoming の価格ではない
- incoming の `OrderFill` は Fills の**末尾**に追加される
- `OrderBook` は不変。`Fill` は新しい `OrderBook` インスタンスを返す

## 約定後の処理（呼び出し側）

約定後の処理は `OrderBook.Match` の外で行われる:

| 処理 | 責任者 | 挙動 |
|---|---|---|
| 指値の未約定分を板に追加 | `TurnProcessor` | `AddRemainingLimitOrder` |
| 成行の未約定分 | — | **板に追加されない**（消滅） |
| ポートフォリオ更新 | `Portfolio.ApplyTrade` | `FillResult` → `TradeResult` 経由 |

## 約定ゼロ時

- incoming の `OrderFill` は `FilledQuantity=0, TotalAmount=0` で生成される
- 成行注文: 警告を返す。ポートフォリオ不変
- 指値注文: 全数量を板に追加（次ターン以降に約定可能）

## 関連

- [取引ルール全体](../../DDD/EXCHANGE_RULE.md) — 手数料、注文種別、ターン進行フロー
- [テスト](../../../tests/FinLearn.Tests/OrderBookTests.cs) — 77件のマッチングテスト
