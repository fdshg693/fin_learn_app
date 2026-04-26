# 見送り（Wait）仕様書

## 概要

投資家が売買を行わずにターンを進めるアクション。
ポートフォリオは変更されないが、ターンは1進み、価格変動とシステム注文生成が発生する。

## 対象コンポーネント

- Controller: `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
- Command: `src/Application/Actions/WaitCommand.cs`
- Handler: `src/Application/Actions/WaitCommandHandler.cs`
- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs` (`AdvanceTurn`)

## エンドポイント

```
POST /api/actions/wait
Content-Type: application/json

{
  "investorId": "<GUID>",
  "expectedTurn": <int>
}
```

## 正常系シナリオ

### シナリオ1: 見送り実行

- **前提条件**: 投資家が存在し、`expectedTurn` がサーバーの現在ターンと一致する
- **入力**: 有効な `investorId`, 正しい `expectedTurn`
- **期待結果**:
  - `success: true`, `message: "Wait を実行しました。"`
  - ポートフォリオは変更されない（現金・保有銘柄ともに同じ）
  - ターンが1進む
  - 価格変動・システム注文生成が発生する

## 異常系シナリオ

### エラー1: 投資家が見つからない

- **前提条件**: 存在しない `investorId`
- **入力**: 無効な `investorId`
- **期待結果**: HTTP 404 Not Found

### エラー2: ターン番号の不一致

- **前提条件**: クライアントが保持するターン番号とサーバーの現在ターンが異なる
- **入力**: `expectedTurn != currentTurn`
- **期待結果**: HTTP 409 Conflict、`"ExpectedTurn mismatch. expected=<X>, current=<Y>."`

## ビジネスルール

- `quantity` や `tickerId` は不要（Wait に銘柄・数量の概念はない）
- ターン進行は必ず発生する（異常系エラー除く）
- ターン進行時に全銘柄の価格変動とシステム注文生成が発生する
- ポートフォリオの変更は一切行わない

## 未決事項

- なし
