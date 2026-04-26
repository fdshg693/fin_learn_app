# 即時買い（BuyNow）仕様書

## 概要

投資家が指定した銘柄を現在の市場価格で即時購入するアクション。
オーダーブック上の売り注文に対してマッチングを行い、約定した分だけポートフォリオを更新する。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
- Command: `src/Application/Actions/BuyNowCommand.cs`
- Handler: `src/Application/Actions/BuyNowCommandHandler.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs` (`ExecuteBuyNow`)
- Domain: `src/Domain/Entities/Portfolio.cs`, `src/Domain/Entities/OrderBook.cs`

## エンドポイント

```
POST /api/actions/buy-now
Content-Type: application/json

{
  "investorId": "<GUID>",
  "tickerId": "<GUID>",
  "quantity": <int>,
  "expectedTurn": <int>
}
```

## 正常系シナリオ

### シナリオ1: 全数量約定

- **前提条件**: オーダーブックに `現在価格 <= 売り注文価格` を満たす売り注文が要求数量以上存在し、投資家の現金が購入代金を賄える
- **入力**: 有効な `investorId`, `tickerId`, `quantity > 0`, 正しい `expectedTurn`
- **期待結果**:
  - `success: true`, `message: "BuyNow を実行しました。"`
  - ポートフォリオの現金が約定総額分減少する
  - ポートフォリオに約定数量分の保有が追加 or 増加する
  - ターンが1進む
  - 価格変動・システム注文生成が発生する

### シナリオ2: 一部約定（数量不足または現金不足）

- **前提条件**: 売り注文は存在するが、数量が足りない、または途中で現金が枯渇する
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: true`, `message: "<約定数量>株を約定しました（未約定 <残数量>株）。"`
  - 約定した分だけポートフォリオが更新される
  - ターンが1進む

### シナリオ3: 売り注文なし

- **前提条件**: 対象銘柄の売り注文が現在価格以下に存在しない
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: false`, `message: "約定する売り注文がありませんでした。"`
  - ポートフォリオは変更されない
  - ターンが1進む

## 異常系シナリオ

### エラー1: 数量が0以下

- **前提条件**: なし
- **入力**: `quantity <= 0`
- **期待結果**: HTTP 400 Bad Request、`"Quantity must be greater than 0."`

### エラー2: 投資家が見つからない

- **前提条件**: 存在しない `investorId`
- **入力**: 無効な `investorId`
- **期待結果**: HTTP 404 Not Found

### エラー3: 銘柄が見つからない

- **前提条件**: 存在しない `tickerId`
- **入力**: 無効な `tickerId`
- **期待結果**: HTTP 404 Not Found

### エラー4: ターン番号の不一致

- **前提条件**: クライアントが保持するターン番号とサーバーの現在ターンが異なる
- **入力**: `expectedTurn != currentTurn`
- **期待結果**: HTTP 409 Conflict、`"ExpectedTurn mismatch. expected=<X>, current=<Y>."`

## ビジネスルール

- マッチング対象は「価格が現在の市場価格以下の売り注文」のみ
- 売り注文の優先順位: 価格昇順（低い価格から）、同価格は作成時刻昇順（FIFO）
- 約定価格は売り注文の価格（投資家の指値ではなく相手の注文価格）
- 現金チェックは各注文の約定コストを累積しながら行う。超過する注文は途中でスキップして終了
- 約定した注文は残数量が0になればオーダーブックから削除、残りがあれば残数量で置き換える
- 約定ごとに Trade レコードが生成される（`Exchange.Fee` = 500円固定）
- ターン進行は成否に関わらず常に発生する（異常系エラー除く）
- ターン進行時に全銘柄の価格変動とシステム注文生成が発生する

## 未決事項

- 現金不足の場合に一部約定が起きた後、残りの注文を無視して終了するか、一部約定すら行わないべきか（現状は一部約定あり）
- 手数料 500円がいつ徴収されるか（現状は Trade に記録されるのみで、投資家現金からは差し引かれない）
