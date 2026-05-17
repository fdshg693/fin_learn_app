# オーダーマッチングエンジン 仕様書

## 概要

投資家の注文とオーダーブック上の既存注文を照合して約定を成立させるエンジン。
価格優先・時間優先（FIFO）のルールで処理される。

## 対象コンポーネント

- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs`（市場価格の取得と Domain への委譲）
- Domain: `library/Domain/Entities/Exchange.cs`（`ExecuteBuyNow`, `ExecuteSellNow`, `ExecuteBuyLimit`, `ExecuteSellLimit`, `MatchCrossedOrders`）
- Domain: `library/Domain/Entities/OrderBook.cs`（`FindByTickerAndSide`, `ReplaceWithRemaining`）
- Domain: `library/Domain/Entities/Order.cs`
- Domain: `library/Domain/Entities/Trade.cs`
- Domain: `library/Domain/ValueObjects/OrderMatchResult.cs`

## マッチングルール

### 買い注文側（BuyNow / BuyLimit）の候補選定

- 対象: 同一銘柄の売り注文
- 価格フィルタ:
  - BuyNow: `売り注文価格 <= 現在の市場価格`
  - BuyLimit: `売り注文価格 <= 投資家の指値`
- ソート順: 価格昇順（安い売り注文から優先）、同価格は `CreatedAt` 昇順（FIFO）

### 売り注文側（SellNow / SellLimit）の候補選定

- 対象: 同一銘柄の買い注文
- 価格フィルタ:
  - SellNow: `買い注文価格 >= 現在の市場価格`
  - SellLimit: `買い注文価格 >= 投資家の指値`
- ソート順: 価格降順（高い買い注文から優先）、同価格は `CreatedAt` 昇順（FIFO）

## 約定処理フロー

```
1. 候補注文リストを価格・時間優先でソート
2. リストを先頭から順に処理:
   a. 残数量が0になれば終了
   b. 現在注文の約定数量 = min(残数量, 注文の数量)
   c. 買い側: 現金チェック（累積コストが保有現金を超えたら打ち切り）
   d. Trade レコードを生成（価格は相手方注文の価格）
   e. 注文をオーダーブックから削除し、残数量があれば残数量で再追加
   f. 累積約定数量・累積金額を更新
3. OrderMatchResult を返す（要求数量, 約定数量, 合計金額）
```

## 約定価格の決定

- 約定価格は **相手方（オーダーブック上）の注文価格** を使用する
- 投資家が BuyNow で購入しても、実際の約定価格は売り注文の価格（市場価格以下）
- 指値注文の場合も同様（指値は条件のみ、価格は相手注文から決まる）

## 注文の更新ルール

- 約定後、注文の残数量が0であればオーダーブックから削除
- 残数量がある場合: 元の注文を削除し、同じ `Id`・`CreatedAt` で残数量の新注文を追加（`ReplaceWithRemaining`）

## Trade レコード

約定のたびに `Trade` が生成される。

| フィールド | 内容 |
|---|---|
| Id | 新規 GUID |
| TickerId | 対象銘柄 |
| BuyOrderId | 買い側注文ID（BuyNow の場合は新規 GUID、SellNow の場合は既存注文ID） |
| SellOrderId | 売り側注文ID（SellNow の場合は新規 GUID、BuyNow の場合は既存注文ID） |
| Price | 約定価格（相手方注文の価格） |
| Quantity | 約定数量 |
| Fee | 取引手数料（500円固定） |
| ExecutedAt | 約定日時（UTC） |

## OrderMatchResult

| フィールド | 内容 |
|---|---|
| RequestedQuantity | 要求数量（元のリクエスト）|
| ExecutedQuantity | 実際の約定数量 |
| TotalAmount | 約定総額（累積） |
| RemainingQuantity | 未約定数量（= RequestedQuantity - ExecutedQuantity） |

## ビジネスルール

- 投資家注文はオーダーブックに登録されない（即時マッチングのみ）
- 約定しなかった分は消滅する（キューに残らない）
- 現金チェックは BuyNow と BuyLimit のみ。SellNow と SellLimit は現金チェックなし
- 手数料（500円）は Trade に記録されるが、ポートフォリオの現金から差し引かれない
- クロス注文の自動解消はターン進行時に別処理として行われ、即時アクションの約定ロジックとは分離されている

## 未決事項

- 手数料をポートフォリオから徴収する仕様は未実装
- 投資家の注文をオーダーブックに残して将来のターンで約定させる仕様は未実装
