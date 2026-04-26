# 指値買い（BuyLimit）仕様書

## 概要

投資家が指定した上限価格（指値）以下の売り注文にマッチングして即時購入するアクション。
BuyNow と異なり、現在の市場価格ではなく投資家が指定した指値を条件として使用する。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
- Command: `src/Application/Actions/BuyLimitCommand.cs`
- Handler: `src/Application/Actions/BuyLimitCommandHandler.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs` (`ExecuteBuyLimit`)
- Domain: `src/Domain/Entities/Portfolio.cs`, `src/Domain/Entities/OrderBook.cs`

## エンドポイント

```
POST /api/actions/buy-limit
Content-Type: application/json

{
  "investorId": "<GUID>",
  "tickerId": "<GUID>",
  "quantity": <int>,
  "limitPriceAmount": <decimal>,
  "expectedTurn": <int>
}
```

## 正常系シナリオ

### シナリオ1: 全数量約定

- **前提条件**: オーダーブックに `指値以下` の売り注文が要求数量以上存在し、投資家の現金が `指値 × quantity` 以上ある
- **入力**: 有効な `investorId`, `tickerId`, `quantity > 0`, `limitPriceAmount > 0`, 正しい `expectedTurn`
- **期待結果**:
  - `success: true`, `message: "BuyLimit を実行しました。"`
  - ポートフォリオの現金が約定総額分減少する
  - ポートフォリオに約定数量分の保有が追加 or 増加する
  - ターンが1進む

### シナリオ2: 一部約定

- **前提条件**: 指値以下の売り注文が存在するが、数量不足または途中で現金が枯渇する
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: true`, `message: "指値買いで <約定数量>株を約定（未約定 <残数量>株）。"`
  - 約定した分だけポートフォリオが更新される
  - ターンが1進む

### シナリオ3: 条件に合う売り注文なし

- **前提条件**: 指値以下の売り注文が存在しない
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: false`, `message: "条件に合う売り注文がありませんでした。"`
  - ポートフォリオは変更されない
  - ターンが1進む

### シナリオ4: 現金不足（指値 × 数量 > 保有現金）

- **前提条件**: 投資家の現金が `limitPriceAmount × quantity` より少ない
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: false`, `message: "指値注文に必要な現金が不足しています。"`
  - ポートフォリオは変更されない
  - ターンが1進む

## 異常系シナリオ

### エラー1: 数量が0以下

- **前提条件**: なし
- **入力**: `quantity <= 0`
- **期待結果**: HTTP 400 Bad Request、`"Quantity must be greater than 0."`

### エラー2: 指値が0以下

- **前提条件**: なし
- **入力**: `limitPriceAmount <= 0`
- **期待結果**: HTTP 400 Bad Request、`"Limit price must be greater than 0."`

### エラー3: 投資家が見つからない

- **前提条件**: 存在しない `investorId`
- **入力**: 無効な `investorId`
- **期待結果**: HTTP 404 Not Found

### エラー4: 銘柄が見つからない

- **前提条件**: 存在しない `tickerId`
- **入力**: 無効な `tickerId`
- **期待結果**: HTTP 404 Not Found

### エラー5: ターン番号の不一致

- **前提条件**: クライアントが保持するターン番号とサーバーの現在ターンが異なる
- **入力**: `expectedTurn != currentTurn`
- **期待結果**: HTTP 409 Conflict、`"ExpectedTurn mismatch. expected=<X>, current=<Y>."`

## ビジネスルール

- マッチング対象は「価格が `limitPriceAmount` 以下の売り注文」のみ（市場価格を参照しない）
- 売り注文の優先順位: 価格昇順（低い価格から）、同価格は作成時刻昇順（FIFO）
- 約定価格は売り注文の価格（指値ではなく相手の注文価格）
- 現金の事前チェック: `limitPriceAmount × quantity > portfolio.Cash` の場合は即エラーを返す
- 現金の逐次チェック: マッチング中に現金が枯渇した時点で打ち切る
- 約定した注文は残数量が0になればオーダーブックから削除、残りがあれば残数量で置き換える
- 約定ごとに Trade レコードが生成される（`Exchange.Fee` = 500円固定）
- ターン進行は成否に関わらず常に発生する（異常系エラー除く）

## 未決事項

- 手数料 500円がいつ徴収されるか（現状は Trade に記録されるのみで、投資家現金からは差し引かれない）
- 指値注文を「その場でマッチングしない場合、次のターン以降でも待機させる」仕様は未実装
