# 指値売り（SellLimit）仕様書

## 概要

投資家が保有する銘柄を指定した下限価格（指値）以上の買い注文にマッチングして即時売却するアクション。
SellNow と異なり、現在の市場価格ではなく投資家が指定した指値を条件として使用する。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
- Command: `src/Application/Actions/SellLimitCommand.cs`
- Handler: `src/Application/Actions/SellLimitCommandHandler.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs` (`ExecuteSellLimit`)
- Domain: `src/Domain/Entities/Portfolio.cs`, `src/Domain/Entities/OrderBook.cs`

## エンドポイント

```
POST /api/actions/sell-limit
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

- **前提条件**: オーダーブックに `指値以上` の買い注文が要求数量以上存在し、投資家が指定数量を保有している
- **入力**: 有効な `investorId`, `tickerId`, `quantity > 0`, `limitPriceAmount > 0`, 正しい `expectedTurn`
- **期待結果**:
  - `success: true`, `message: "SellLimit を実行しました。"`
  - ポートフォリオの保有数量が減少し、現金が約定総額分増加する
  - 保有数量が0になった場合は保有銘柄が削除される
  - ターンが1進む

### シナリオ2: 一部約定

- **前提条件**: 指値以上の買い注文は存在するが、数量が不足している
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: true`, `message: "指値売りで <約定数量>株を約定（未約定 <残数量>株）。"`
  - 約定した分だけポートフォリオが更新される
  - ターンが1進む

### シナリオ3: 保有なし

- **前提条件**: 投資家が指定銘柄を保有していない
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: false`, `message: "保有がありません。"`
  - ポートフォリオは変更されない
  - ターンが1進む

### シナリオ4: 保有数量不足

- **前提条件**: 投資家の保有数量がリクエストの `quantity` より少ない
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: false`, `message: "保有数量が不足しています。"`
  - ポートフォリオは変更されない
  - ターンが1進む

### シナリオ5: 条件に合う買い注文なし

- **前提条件**: 指値以上の買い注文が存在しない
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: false`, `message: "条件に合う買い注文がありませんでした。"`
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

- マッチング対象は「価格が `limitPriceAmount` 以上の買い注文」のみ（市場価格を参照しない）
- 買い注文の優先順位: 価格降順（高い価格から）、同価格は作成時刻昇順（FIFO）
- 約定価格は買い注文の価格（指値ではなく相手の注文価格）
- 保有チェックはマッチングの前に行う。保有なし・数量不足の場合はマッチングを試みない
- 約定した注文は残数量が0になればオーダーブックから削除、残りがあれば残数量で置き換える
- 約定ごとに Trade レコードが生成される（`Exchange.Fee` = 500円固定）
- ターン進行は成否に関わらず常に発生する（異常系エラー除く）

## 未決事項

- 手数料 500円がいつ徴収されるか（現状は Trade に記録されるのみで、投資家現金からは差し引かれない）
- 指値注文を「その場でマッチングしない場合、次のターン以降でも待機させる」仕様は未実装
