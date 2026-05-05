# 入力バリデーション・ビジネスルール検証

注文受付からポートフォリオ更新までに行われるバリデーションの全体像。

## エラーの2分類

検証失敗は性質によって 2 つに分かれる。**呼び出し側の挙動と HTTP 応答**が異なるため、追加するときはどちらに属するかを最初に決める。

| 分類 | 例 | 検出箇所 | API 応答 | ターン進行 | コンピューター注文 |
|---|---|---|---|---|---|
| **形式不正（Format error）** | `quantity <= 0`, `price <= 0`, `stopPrice <= 0`, `side` 未指定/不正値 | API ハンドラ → `TurnProcessor.Buy/Sell` 冒頭 | **400 BadRequest** または `Rejected()` | **進まない**（元の `Game` をそのまま返す） | **生成されない** |
| **状態依存失敗（State-dependent failure）** | 約定ゼロ、現金不足、保有不足 | `OrderBook.Match` の結果評価／`Portfolio.ApplyTrade` | **200 OK + `warning` フィールド** | **進む**（Wait と同等） | 生成され板に残る |

呼び出し側の判別: `TurnResult.Warning` が non-null かつ `ProcessedTurn == NextGame.Turn`（＝進んでいない）なら形式不正、`ProcessedTurn < NextGame.Turn` なら状態依存失敗。

## 3層モデル

```
┌─────────────────────────────────────────────────────────┐
│ ① UI Layer — frontend/app/components/TradeForm.tsx     │
│   HTML5 input min={1}                                  │
│   action(): Number / NaN ガード                          │
└────────────────────────┬────────────────────────────────┘
                         │ HTTP POST /api/games/{id}/orders
┌────────────────────────▼────────────────────────────────┐
│ ② API Layer — FinLearn.Api/Endpoints/GameEndpoints.cs  │
│   PlaceOrder ハンドラ冒頭で形式不正を 400 で弾く          │
│   ASP.NET モデルバインドが不正 enum (`"Hold"` 等) を 400 │
└────────────────────────┬────────────────────────────────┘
                         │ TurnProcessor.Buy/Sell(...)
┌────────────────────────▼────────────────────────────────┐
│ ③ Domain Layer                                          │
│   TurnProcessor — 形式不正 → Rejected()                 │
│   Order ctor    — 不変条件違反 → ArgumentException       │
│   Portfolio.ApplyTrade — 状態依存失敗 → Warning          │
└─────────────────────────────────────────────────────────┘
```

ドメイン層は API 層を経由しない直接呼び出しに対しても**自律的に防御**する（多重防御）。テストや将来の他経路に対する安全網。

## ① UI 層

> ソース: [`frontend/app/components/TradeForm.tsx`](../../../frontend/app/components/TradeForm.tsx) / [`frontend/app/routes/games.$id.tsx`](../../../frontend/app/routes/games.$id.tsx)

### HTML5 制約

`TradeForm` の数量／価格 input はいずれも `type="number" min={1}`。ブラウザレベルで負値・0 を抑止するが、**プログラマブルな送信は防げない**ため後段に依存する。

### action ハンドラ

`clientAction` は `Number(formData.get(...))` 後に `Number.isNaN` チェックで非数値を `throw new Error(...)`（ErrorBoundary で捕捉）。`price` は空文字 `""` を `null`（成行）として扱う。

```ts
if (Number.isNaN(instrumentId) || Number.isNaN(quantity) || (price !== null && Number.isNaN(price))) {
    throw new Error("入力値が不正です。数値を入力してください。");
}
```

UI 層は **境界条件のチェックは行わない**（`>=1` の検証はサーバーに委譲）。フロントを書き換えても安全であるという原則。

## ② API 層

> ソース: [`src/FinLearn.Api/Endpoints/GameEndpoints.cs`](../../../src/FinLearn.Api/Endpoints/GameEndpoints.cs) `PlaceOrder` ハンドラ

`POST /api/games/{id}/orders` の冒頭で形式不正を弾く。すべて `400 BadRequest` + `{ error: "..." }` JSON。

| 条件 | エラーメッセージ |
|---|---|
| `request.Side is null` | `side は必須です（"Buy" または "Sell"）` |
| `request.Quantity <= 0` | `quantity は 1 以上を指定してください` |
| `request.Price is not null && request.Price <= 0` | `price は 1 以上を指定してください` |
| `request.StopPrice is not null && request.StopPrice <= 0` | `stopPrice は 1 以上を指定してください` |

### enum バインドエラー

`OrderRequest.Side` は `OrderSide?` 型。`Program.cs` で `JsonStringEnumConverter` をグローバル登録しているため、`"Buy"` / `"Sell"` は型バインド成功、**`"Hold"` 等の不正値は ASP.NET Core のモデルバインドが自動的に 400** を返す（ハンドラに到達しない）。`null`（未指定）はハンドラの最初のガードが処理する。

### 管理 API のページング

`GET /api/admin/games/{id}/orderbook` のページングパラメータも 400 で弾く（[`AdminEndpoints.cs`](../../../src/FinLearn.Api/Endpoints/AdminEndpoints.cs)）:
- `page < 1` → `page must be >= 1`
- `pageSize < 1` → `pageSize must be >= 1`
- `pageSize > 200` → `pageSize must be <= 200`

### 400 後の不変性

形式不正で 400 を返した時、`GameStore` は更新されない。**ターン進行も、コンピューター注文の生成も発生しない**。テスト: [`GameApiTests.POST_orders_400後はターンも板も不変`](../../../tests/FinLearn.Api.Tests/GameApiTests.cs)。

## ③ ドメイン層

### TurnProcessor — 形式不正の多重防御

> ソース: [`src/FinLearn.Core/TurnProcessor.cs`](../../../src/FinLearn.Core/TurnProcessor.cs)

`Buy` / `Sell` 冒頭で同条件をチェックし、違反時は `Rejected()` を返す:

```csharp
if (quantity <= 0)        return Rejected(game, Messages.QuantityMustBePositive);
if (price is not null && price <= 0)
                          return Rejected(game, Messages.PriceMustBePositive);
```

`Rejected()` は **`Game` を不変、`SubmittedOrders` / `Fills` を空、`Warning` 設定**で `TurnResult` を返す（`game.Turn` 変化なし）。コンピューター注文も生成されない（`OrderPlacer.PlaceOrders` を呼ぶ前に return するため）。

`stopPrice` の妥当性は API 層で弾けば十分なため `TurnProcessor` 側の重複ガードはなく、`Order` のコンストラクタで例外として弾く。

### Order — 不変条件（コンストラクタ例外）

> ソース: [`src/FinLearn.Core/Models/Order.cs`](../../../src/FinLearn.Core/Models/Order.cs)

`Order` インスタンスは**不正な状態で存在し得ない**。違反すると `ArgumentException`:

| 条件 | メッセージ | 検査箇所 |
|---|---|---|
| `Quantity <= 0` | 数量は1以上である必要があります | 全コンストラクタ |
| 指値の `Price <= 0` | 価格は1以上である必要があります | 指値コンストラクタ |
| `StopPrice is not null && stopPrice <= 0` | ストップ価格は1以上である必要があります | プライベートコンストラクタ（`CreateMarket`／`WithQuantity` 経由） |

API 層・`TurnProcessor` で事前ガードしているため通常運用では到達しない。ドメイン直接利用テスト・将来の経路追加時の最終防衛線。

### Position — 不変条件

> ソース: [`src/FinLearn.Core/Models/Position.cs`](../../../src/FinLearn.Core/Models/Position.cs)

`quantity <= 0` で `ArgumentOutOfRangeException`（メッセージ: `Messages.QuantityMustBePositive`）。`PositionSet.SetQuantity` は数量 0 のポジションを自動除去するため、**実運用で 0 ポジションが構築されることはない**。

### Portfolio.ApplyTrade — 状態依存失敗

> ソース: [`src/FinLearn.Core/Models/Portfolio.cs`](../../../src/FinLearn.Core/Models/Portfolio.cs)

約定結果（`TradeResult`）を反映する際、ゲームの**現在状態**に依存する 3 種の失敗を `Warning` 文字列で返す（例外を投げない）:

| 側 | 検査 | 失敗時の Warning |
|---|---|---|
| Buy / Sell | `trade.FilledQuantity <= 0` | `Messages.QuantityMustBePositive` |
| Buy | `_cash < trade.TotalAmount + trade.Fee` | `Messages.InsufficientCashToBuy` |
| Sell | `QuantityOf(InstrumentId) < trade.FilledQuantity` | `Messages.InsufficientQuantityToSell` |

`FilledQuantity <= 0` は通常 `OrderBook.Match` 時点で `noMatchMessage` 経由で先に弾かれるが、ガードを残して `ApplyTrade` を単独で安全に呼び出せるようにしている。

### 失敗時のターン進行ルール

`TurnProcessor.PlaceOrder` 内では失敗の発生位置でターン進行ルールが分岐する:

| 失敗位置 | コンピューター注文 | プレイヤー注文 | ポートフォリオ | ターン |
|---|---|---|---|---|
| 形式不正（`Rejected`） | 未生成 | 未生成 | 不変 | 不変 |
| 約定ゼロ（成行で買い手／売り手なし） | **板に残る** | 消滅 | 不変 | **進む** |
| 状態依存失敗（現金不足／保有不足） | **板に残る** | 約定ロールバック（指値も板に残らない） | 不変 | **進む** |

> 後者 2 つが Wait と同等の挙動になる理由: コンピューター注文の生成は副作用としてすでに完了しており、巻き戻すコストが大きい。ロジック詳細は [EXCHANGE_RULE.md](../../DDD/EXCHANGE_RULE.md) のターン進行フロー参照。

## エラーメッセージカタログ

> ソース: [`src/FinLearn.Core/Messages.cs`](../../../src/FinLearn.Core/Messages.cs)

定数として一元管理。テストは値ではなく定数で比較する（`Assert.Equal(Messages.QuantityMustBePositive, warning)`）。

| 定数 | 文言 | 用途 |
|---|---|---|
| `QuantityMustBePositive` | 数量は0より大きい必要があります | 形式不正・状態依存（FilledQuantity） |
| `PriceMustBePositive` | 価格は0より大きい必要があります | 形式不正（指値価格） |
| `InsufficientCashToBuy` | 現金が不足して購入できません | 状態依存（買い時） |
| `InsufficientQuantityToSell` | 保有数量を超えて売却できません | 状態依存（売り時） |
| `NoMatchingSellOrders` | 約定できる売り注文がありません | 状態依存（成行買いで対向板なし） |
| `NoMatchingBuyOrders` | 約定できる買い注文がありません | 状態依存（成行売りで対向板なし） |

API 層の 400 メッセージ（`quantity は 1 以上を指定してください` 等）は `Messages` ではなく `GameEndpoints.cs` リテラル。クライアント向けの説明文と Domain Warning は文体を分けてある。

## 関連

- [取引ルール全体](../../DDD/EXCHANGE_RULE.md) — 手数料、注文種別、ターン進行フロー
- [API.UI.md](API.UI.md) — REST API のレスポンス形式・UI への伝搬
- [約定ロジック（FillResult）](../FillResult/LOGIC.md) — `OrderBook.Match` の約定ゼロ判定
