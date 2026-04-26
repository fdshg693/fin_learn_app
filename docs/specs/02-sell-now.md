# 即時売り（SellNow）仕様書

## 概要

投資家が保有する銘柄を現在の市場価格で即時売却するアクション。
オーダーブック上の買い注文に対してマッチングを行い、約定した分だけポートフォリオを更新する。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
- Command: `src/Application/Actions/SellNowCommand.cs`
- Handler: `src/Application/Actions/SellNowCommandHandler.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs` (`ExecuteSellNow`)
- Domain: `src/Domain/Entities/Portfolio.cs`, `src/Domain/Entities/OrderBook.cs`

## エンドポイント

```
POST /api/actions/sell-now
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

- **前提条件**: オーダーブックに `価格 >= 現在の市場価格` を満たす買い注文が要求数量以上存在し、投資家が指定数量を保有している
- **入力**: 有効な `investorId`, `tickerId`, `quantity > 0`, 正しい `expectedTurn`
- **期待結果**:
  - `success: true`, `message: "SellNow を実行しました。"`
  - ポートフォリオの保有数量が減少し、現金が約定総額分増加する
  - 保有数量が0になった場合は保有銘柄が削除される
  - ターンが1進む

### シナリオ2: 一部約定

- **前提条件**: 買い注文は存在するが、数量が不足している
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: true`, `message: "<約定数量>株を約定しました（未約定 <残数量>株）。"`
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

### シナリオ5: 買い注文なし

- **前提条件**: 対象銘柄の買い注文が現在価格以上に存在しない
- **入力**: 有効なパラメータ
- **期待結果**:
  - `success: false`, `message: "約定する買い注文がありませんでした。"`
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

- マッチング対象は「価格が現在の市場価格以上の買い注文」のみ
- 買い注文の優先順位: 価格降順（高い価格から）、同価格は作成時刻昇順（FIFO）
- 約定価格は買い注文の価格（投資家の売り希望価格ではなく相手の注文価格）
- 保有チェックはマッチングの前に行う。保有なし・数量不足の場合はマッチングを試みない
- 約定した注文は残数量が0になればオーダーブックから削除、残りがあれば残数量で置き換える
- 約定ごとに Trade レコードが生成される（`Exchange.Fee` = 500円固定）
- ターン進行は成否に関わらず常に発生する（異常系エラー除く）
- ターン進行時に全銘柄の価格変動とシステム注文生成が発生する

## 未決事項

- 手数料 500円がいつ徴収されるか（現状は Trade に記録されるのみで、投資家現金からは差し引かれない）
