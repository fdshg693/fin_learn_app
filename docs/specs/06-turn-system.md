# ターン制システム仕様書

## 概要

fin_learn_app のシミュレーションはターン制で動作する。
投資家が BuyNow / SellNow / BuyLimit / SellLimit / Wait のいずれかのアクションを実行するたびに、
その投資家のターンが1増加し、価格変動とコンピュータ注文生成が発生する。

## 対象コンポーネント

- Store: `backend/FinLearnApp.Api/Data/InMemoryStore.cs` (`AdvanceTurn`, `ApplyPriceFluctuation`, `GenerateSystemOrdersForTurn`)
- Domain: `src/Domain/Entities/Ticker.cs` (`UpdatePrice`)
- Domain: `src/Domain/Entities/OrderBook.cs` (`Add`)

## ターン管理

### ターン番号の初期化

- 投資家ごとにターン番号を管理する（`Dictionary<InvestorId, int>`）
- 初期値は `0`

### ターンの進め方

- アクション実行成功後（または `success: false` の場合も含む）に `AdvanceTurn` が呼ばれる
- `AdvanceTurn` は `currentTurn + 1` を返し、内部状態を更新する
- HTTP 400 / 404 / 409 の場合はターンが進まない

### ターン不一致の保護（楽観ロック）

- 各アクションリクエストには `expectedTurn` フィールドが必須
- サーバーの `currentTurn` と `expectedTurn` が一致しない場合は HTTP 409 Conflict を返す
- これにより、二重送信や古いターン番号での操作を防ぐ

## 価格変動

### 仕様

- ターン進行時に全銘柄の価格が変動する
- 変動率: `97%〜103%` のランダムな範囲（`Random.NextDouble()` による一様乱数）
- 計算式: `新価格 = round(現在価格 × rate, 2, AwayFromZero)`
- 最低価格保証: 計算結果が `1円` 未満になった場合は `1円` に丸める
- 全銘柄に対して毎ターン独立して変動が適用される

## コンピュータ注文生成

### 仕様

- ターン進行時に発生する
- 対象銘柄: 全銘柄からランダムに最大3銘柄を選択
- 各対象銘柄に対して以下の2注文を生成する:
  - 買い注文: 価格 = `round(現在価格 × 0.95, 2, AwayFromZero)`, 数量 = 10株, オリジン = `System`
  - 売り注文: 価格 = `round(現在価格 × 1.00, 2, AwayFromZero)`, 数量 = 10株, オリジン = `System`
- 生成された注文はオーダーブックに追加される（既存の注文と共存）
- 注文の `CreatedAt` はターン進行時の UTC 日時

### 定数

| 定数名 | 値 |
|---|---|
| MaxTargetTickersPerTurn | 3 |
| SystemOrderQuantity | 10 |
| SystemBuyPriceRate | 0.95 |
| SystemSellPriceRate | 1.00 |
| MinPriceFluctuationRate | 0.97 |
| MaxPriceFluctuationRate | 1.03 |

## ビジネスルール

- ターン進行の順序: `AdvanceTurn` → `ApplyPriceFluctuation` → `GenerateSystemOrdersForTurn`
- 価格変動後の新価格を使ってコンピュータ注文の価格を計算する
- コンピュータ注文生成時の銘柄選択はランダム（再現性を求める場合は `Random` に任意のシードを指定可能）
- 投資家のアクション実行結果とターン番号はレスポンスに含まれる（`currentTurn`）

## 未決事項

- 銘柄数が3より少ない場合のコンピュータ注文生成挙動（現状は `Take(MaxTargetTickersPerTurn)` で実際の銘柄数以下になる）
- ターン数に上限はない（ゲーム終了条件が未定義）
