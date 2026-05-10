# 03 — Migration Steps

## このファイルは何か

段階的移行の Step 1〜7 を順番に並べる。各ステップで:

- **目的** — このステップで何を達成するか
- **やること** — 具体的な作業内容と作成/編集するファイル
- **影響テスト** — 既存テストへの影響範囲
- **新規テスト** — 追加すべきテスト
- **完了確認** — 各ステップの出口条件

新規セッションは [00-index.md](00-index.md) → [01-context.md](01-context.md) → [02-target-design.md](02-target-design.md) を読んでからこのファイルに来ること。最終形の型シグネチャは `02-target-design.md` に書かれている。

## 大方針

- **旧コードと並走**: `PlaceOrder` / `ExecutePlayerOrder` を残したまま、新しい型と Pipeline を追加していく。並走期間中は両方が共存する。
- **常時グリーン**: 各ステップの末尾で `dotnet test` 全グリーンを保つ。途中コミットでも CI が通る状態。
- **1 step = 1 commit/PR**: レビュー粒度として step を採用。
- **Public API シグネチャ維持**: `TurnProcessor.Buy/Sell/Wait` の引数・戻り値は変えない。

## Step 一覧 (概観)

| Step | 内容 | 旧コード触る？ | テスト影響 |
|---|---|---|---|
| 1 | `World` 型を追加 | しない | なし (新規) |
| 2 | `IPlayerOrderHandler` + Limit/Market 実装を追加 | しない | なし (新規) |
| 3 | 新 `RunTurn` pipeline を private で追加 (まだ呼ばれない) | しない | なし |
| 4 | `Wait` を新 pipeline に切替 | する (Wait 本体置換) | Wait 系テスト |
| 5 | `Buy/Sell` を新 pipeline に切替 | する (PlaceOrder 廃止) | Buy/Sell 系テスト |
| 6 | 旧コード削除 | する (削除のみ) | なし |
| 7 | ドキュメント更新 | しない | なし |

---

## Step 1 — World 型を内部追加

### 目的

`World` 型を `src/FinLearn.Core/` に追加する。**まだ誰も使わない** 状態。

### やること

1. **新規ファイル**: [src/FinLearn.Core/World.cs](../../../src/FinLearn.Core/World.cs)
   - `internal sealed record World(...)` (詳細は [02-target-design.md](02-target-design.md) 参照)
   - `WithBook`, `WithPortfolios`, `WithPlayerPortfolio`, `WithNextOrderId` メソッド
   - `PlayerPortfolio` プロパティ (dict 直接アクセスを排除)
   - `static World FromGame(Game, int fee, IExchange)` ファクトリ

2. **新規ファイル**: [tests/FinLearn.Tests/WorldTests.cs](../../../tests/FinLearn.Tests/WorldTests.cs)

   xUnit + Japanese テスト名で:
   - `FromGame_でPlayer_Portfolioが統合された_Portfoliosに含まれる`
   - `FromGame_でComputerPortfoliosが統合された_Portfoliosに含まれる`
   - `PlayerPortfolio_でPlayerNameに対応するPortfolioが取得できる`
   - `WithPlayerPortfolio_でPlayerだけ更新され_Computer分は変わらない`
   - `WithPlayerPortfolio_は元のWorldを変更しない`
   - `WithBook_でBookだけ更新される`

   テストヘルパー:
   - `TestData.CreateInfiniteComputerPortfolios()` を再利用
   - `TestExchange` を流用

### 影響テスト

なし (新規追加のみ)。

### 完了確認

```powershell
dotnet test fin_learn_app.sln --nologo --verbosity quiet
```

`Passed: 222 + (新規 World テスト数)` で全グリーン。

---

## Step 2 — IPlayerOrderHandler + Limit/Market 実装を追加

### 目的

Handler 戦略型を追加。Pipeline はまだ旧コード (`PlaceOrder`) を使う。

### 前提

Step 1 の World 型が存在すること。

### やること

1. **新規ファイル**: [src/FinLearn.Core/Services/IPlayerOrderHandler.cs](../../../src/FinLearn.Core/Services/IPlayerOrderHandler.cs)
   - `internal interface IPlayerOrderHandler`
   - `(World, string?) Receive(World world, Order order)`
   - `(World, string?) Settle(World world, Order order, MatchResult match)`

2. **新規ファイル**: [src/FinLearn.Core/Services/LimitOrderHandler.cs](../../../src/FinLearn.Core/Services/LimitOrderHandler.cs)
   - `internal sealed class LimitOrderHandler : IPlayerOrderHandler`
   - `Receive`: `Portfolio.ReserveBuy/ReserveSell` を呼ぶ。失敗時 warning。
   - `Settle`: `BuildOrdersByIdSnapshot` + `SettlementProcessor.ComputePostFillRemainingQty` + `SettlementProcessor.SettleFills`。失敗パスなし。
   - `private static BuildOrdersByIdSnapshot(...)` を内部に持つ (旧 TurnProcessor から移設は Step 6)

3. **新規ファイル**: [src/FinLearn.Core/Services/MarketOrderHandler.cs](../../../src/FinLearn.Core/Services/MarketOrderHandler.cs)
   - `internal sealed class MarketOrderHandler : IPlayerOrderHandler`
   - `Receive`: no-op
   - `Settle`: fill=0 で warning (Messages.NoMatchingSellOrders/NoMatchingBuyOrders)、`Portfolio.ApplyTrade` 失敗時 warning。

4. **新規テスト**: [tests/FinLearn.Tests/LimitOrderHandlerTests.cs](../../../tests/FinLearn.Tests/LimitOrderHandlerTests.cs)
   - `Receive_で買い注文が指値予約される`
   - `Receive_で売り注文が指値予約される`
   - `Receive_で残高不足の場合_World不変でWarningが返る`
   - `Receive_で数量不足の場合_World不変でWarningが返る (売り)`
   - `Settle_で完全約定の場合_予約が消費されPortfolioが確定する`
   - `Settle_で部分約定の場合_差額が予約から返金される`
   - `Settle_で約定ゼロの場合_World不変でWarningはnull` (限値は no-match で板に残るので OK)

5. **新規テスト**: [tests/FinLearn.Tests/MarketOrderHandlerTests.cs](../../../tests/FinLearn.Tests/MarketOrderHandlerTests.cs)
   - `Receive_は常にWorld不変でWarning_null`
   - `Settle_で成行買いが約定するとPortfolioに反映される`
   - `Settle_で約定ゼロの場合_買いはNoMatchingSellOrdersのWarning_World不変`
   - `Settle_で約定ゼロの場合_売りはNoMatchingBuyOrdersのWarning_World不変`
   - `Settle_で残高不足の場合_World不変でWarning`

   テストヘルパー:
   - World は `World.FromGame(testGame, fee, testExchange)` で構築
   - 板に注文を仕込むには `OrderBook` を直接組み立てる
   - `Order` は `Player.CreateOrder` 経由か直接 `new Order(...)` で構築 (Limit/Market でファクトリが違うので注意 — Order.cs を確認)

### 影響テスト

なし (新規追加のみ)。

### 完了確認

```powershell
dotnet test fin_learn_app.sln --nologo --verbosity quiet
```

全グリーン (Step 1 の件数 + 新規 Handler テスト数)。

---

## Step 3 — 新 RunTurn pipeline を private で追加 (まだ呼ばれない)

### 目的

`TurnProcessor` 内に新 `RunTurn` を共存させる。`Buy/Sell/Wait` はまだ旧 `PlaceOrder` / 旧 `Wait` 本体を使う。

### 前提

Step 1 (World), Step 2 (Handler) が完了。

### やること

1. **編集**: [src/FinLearn.Core/TurnProcessor.cs](../../../src/FinLearn.Core/TurnProcessor.cs)

   既存の `PlaceOrder` / `ExecutePlayerOrder` / `Wait` 本体は触らずに、以下の private メソッドを **追加**:

   - `private TurnResult RunTurn(Game game, int fee, IPlayerOrderHandler? handler, Func<int, Order>? intentFactory)` — 02-target-design.md の通り
   - `private TurnResult BuildTurnResult(Game inputGame, World world, TradeResult? trade, string? warning, IReadOnlyList<Order> submittedOrders, IReadOnlyList<OrderFill> fills)`
   - `private static IPlayerOrderHandler SelectHandler(int? price)`

   `AdvanceTurn` に新オーバーロード `private Game AdvanceTurn(Game inputGame, World world)` を追加 (旧オーバーロードは並走中)。

   `Combine`, `AddRemainingLimitOrder` は旧コードと共有 (新 RunTurn からも参照)。

2. **テスト**: 新規追加なし (まだ呼ばれない)。

### 影響テスト

なし。`Buy/Sell/Wait` は旧コードを呼び続ける。

### 完了確認

```powershell
dotnet test fin_learn_app.sln --nologo --verbosity quiet
```

Step 2 と同じ件数で全グリーン。

⚠️ **コンパイル時の注意**: `Func<int, Order>` の Order 型、`MatchResult` の構造、`World` の visibility (internal) と `RunTurn` の visibility (private) が整合するか確認。`TurnProcessor` は public なので、internal 型 World を private メソッドのシグネチャに使うのは OK (アセンブリ内なので)。

---

## Step 4 — Wait を新 pipeline に切替

### 目的

最も単純な `Wait` を新 pipeline に乗せる。動作の等価性を確認。

### 前提

Step 3 まで完了。

### やること

1. **編集**: [src/FinLearn.Core/TurnProcessor.cs](../../../src/FinLearn.Core/TurnProcessor.cs)

   ```csharp
   public TurnResult Wait(Game game, int fee) =>
       RunTurn(game, fee, handler: null, intentFactory: null);
   ```

   旧 `Wait` 本体は削除。

2. **テスト**: 新規追加なし。既存テストが全部通るかが確認軸。

### 影響テスト

`Wait` を直接呼ぶテスト (要確認、おおむね):

- [tests/FinLearn.Tests/TurnProcessorTests.cs](../../../tests/FinLearn.Tests/TurnProcessorTests.cs):
  - `待つではTradeResultがnullになる`
  - `待つと価格が変動する`
  - `アクション失敗時でも価格が変動する` (Sell 失敗 → Wait 挙動)
- [tests/FinLearn.Tests/TurnProcessorLoggingTests.cs](../../../tests/FinLearn.Tests/TurnProcessorLoggingTests.cs):
  - `Wait_はSubmittedOrdersにコンピューター注文のみを含めFillsは空`
- [tests/FinLearn.Api.Tests/GameApiTests.cs](../../../tests/FinLearn.Api.Tests/GameApiTests.cs):
  - `POST /api/games/{id}/wait` 系

これらのテストは**書き換え不要**で通るはず。

### 完了確認

```powershell
dotnet test fin_learn_app.sln --filter "Wait" --nologo
dotnet test fin_learn_app.sln --nologo --verbosity quiet
```

全グリーン。Wait 関連テストの assertion (Trade=null, ProcessedTurn=入力Turn, SubmittedOrders=computer のみ, Fills 空, Game.Turn が +1) が新 pipeline で再現できているか確認。

### トラブルシューティング

もしテストが落ちたら:

- **`SubmittedOrders` の中身が違う**: `Combine` の挙動を確認。Wait は `placement.PlacedOrders` をそのまま返すべき (player order を含めない)。
- **`Fills` の中身が違う**: Wait は `Array.Empty<OrderFill>()` を返すべき。RunTurn の Wait 分岐で `fills` 引数を確認。
- **`Game.NextOrderId` が違う**: Wait は player order を採番しないので `placement.NextOrderId` のまま。`+1` していないか確認。
- **`Game.Turn` が違う**: AdvanceTurn が `world.Turn + 1` で進めているか確認。

---

## Step 5 — Buy/Sell を新 pipeline に切替

### 目的

`Buy/Sell` を新 pipeline に乗せ、旧 `PlaceOrder` の呼び出しを完全に消す。

### 前提

Step 4 まで完了。Wait が新 pipeline で動いている。

### やること

1. **編集**: [src/FinLearn.Core/TurnProcessor.cs](../../../src/FinLearn.Core/TurnProcessor.cs)

   `Buy` / `Sell` の本体を [02-target-design.md](02-target-design.md) の通りに置換:

   ```csharp
   public TurnResult Buy(Game game, int fee, int instrumentId, int quantity,
       int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl)
   {
       if (quantity <= 0) return Rejected(game, Messages.QuantityMustBePositive);
       if (price is not null && price <= 0) return Rejected(game, Messages.PriceMustBePositive);
       if (expiresInTurns <= 0) return Rejected(game, Messages.ExpiresInTurnsMustBePositive);

       return RunTurn(game, fee,
           handler: SelectHandler(price),
           intentFactory: nextOrderId => game.Player.CreateOrder(
               nextOrderId, new Instrument(instrumentId), OrderSide.Buy,
               quantity, price, stopPrice, game.Turn, game.Turn + expiresInTurns));
   }
   ```

   `Sell` も対称に。

   旧 `PlaceOrder(...)` メソッドは**まだ削除しない** (Step 6 で削除)。コンパイラから unused 警告が出る場合はこのステップでも消してよい。

2. **テスト**: 新規追加なし。既存テストの動作確認が中心。

### 影響テスト

`Buy/Sell` を直接呼ぶテスト (大量):

- [tests/FinLearn.Tests/TurnProcessorTests.cs](../../../tests/FinLearn.Tests/TurnProcessorTests.cs) — 約 42 テスト中、Buy/Sell を使う 35+ 個
- [tests/FinLearn.Tests/TurnProcessorLoggingTests.cs](../../../tests/FinLearn.Tests/TurnProcessorLoggingTests.cs) — 約 12 テスト
- [tests/FinLearn.Api.Tests/GameApiTests.cs](../../../tests/FinLearn.Api.Tests/GameApiTests.cs) — `POST /api/games/{id}/orders` 系約 30 テスト

これらは**書き換え不要**で通るはず。

### 完了確認

```powershell
dotnet test fin_learn_app.sln --nologo --verbosity quiet
```

全 222 + 新規分 全グリーン。

### 重点確認テストケース (落ちやすい)

このステップでデバッグの起点になりやすいテスト:

- `指値買い発注で即座にreservedCashが増えavailableCashが減る` — Receive で予約された後の World が AdvanceTurn 経由で Game に戻ってきているか
- `指値で予約失敗_残高不足_はターン進行_warning_Fills空_player注文も含む` — Receive 失敗時に submittedOrders に player order が含まれること
- `Buy残高不足_はFillsを空にロールバック` — Settle 失敗時に matchResult を捨てる挙動
- `プレイヤーの過去ターン_resting_指値が_computer_注文と約定するとPortfolioに反映される` — computer phase の SettleFills が player resting 約定を反映する挙動 (これは OrderPlacer 側のロジックで、TurnProcessor リファクタとは独立だが念のため)
- `Buy成行約定ゼロ_はFillsが空でSubmittedOrdersはコンピューターのみ` — ⚠️ ただしテスト名と現状コードを再確認すること。現状コードでは成行 fill=0 でも submittedOrders は player を含む (旧来挙動)。テストを再読し、新コードもこれを再現すること

### トラブルシューティング

もしテストが落ちたら:

- **限値 vs 成行の判定**: `SelectHandler` が `price is null` で正しく分岐しているか
- **Order の Id**: `intentFactory(world.NextOrderId)` で渡している nextOrderId が `placement.NextOrderId` と一致しているか
- **submittedOrders**: 失敗パス全部で player order が含まれているか (`order is null || handler is null` の分岐より後の return は全部 `Combine` 後の submittedOrders を渡す)
- **AdvanceTurn 内の SplitPortfolios**: `playerName` が正しく扱われているか (元コードでは `game.Player.Name`、新コードでは `world.PlayerName`)

---

## Step 6 — 旧コード削除

### 目的

並走していた旧 `PlaceOrder` / `ExecutePlayerOrder` を削除する。

### 前提

Step 5 が完了し、全テストが新 pipeline で通っている。

### やること

[src/FinLearn.Core/TurnProcessor.cs](../../../src/FinLearn.Core/TurnProcessor.cs) から以下を削除:

| 削除対象 | 種類 |
|---|---|
| `PlaceOrder(Game, int, Instrument, OrderSide, int, int?, int?, int, string)` | private method |
| `ExecutePlayerOrder(string, Order, int, Dictionary<string, Portfolio>, OrderBook, IExchange, string)` | private method |
| `PlayerOrderOutcome(...)` (record struct) | nested type |
| `Failed(OrderBook, IReadOnlyDictionary<string, Portfolio>, string)` | private static method |
| `BuildAllPortfolios(Game)` | private static method (`World.FromGame` に統合済み) |
| `BuildOrdersByIdSnapshot(OrderBook, Order)` | private static method (`LimitOrderHandler` 内に移設済み) |
| 旧 `AdvanceTurn(Game, OrderBook, int, IReadOnlyDictionary<string, Portfolio>, int)` | 新オーバーロード `AdvanceTurn(Game, World)` のみ残す |
| 旧 `SplitPortfolios(Player, IReadOnlyDictionary<string, Portfolio>)` | 新シグネチャ `SplitPortfolios(Player, IReadOnlyDictionary<string, Portfolio>, string playerName)` に統合 |

クラス冒頭の `<summary>` コメント (注文生成と settlement の責務分離の説明) も新構造を反映するよう書き換え。

### 影響テスト

なし (機能は維持)。

### 完了確認

```powershell
dotnet build fin_learn_app.sln --nologo
dotnet test fin_learn_app.sln --nologo --verbosity quiet
```

- ビルド warning なし (unused private 警告含む)
- 全テストグリーン

### 検証用 grep

旧型/旧メソッドの参照が残っていないこと:

```powershell
# Grep ツールを使うのがベターだが、bash があれば
grep -r "PlaceOrder\b" src/ --include="*.cs"
grep -r "ExecutePlayerOrder" src/ --include="*.cs"
grep -r "PlayerOrderOutcome" src/ --include="*.cs"
grep -r "BuildAllPortfolios" src/ --include="*.cs"
grep -r "BuildOrdersByIdSnapshot" src/ --include="*.cs"
```

すべて 0 件であること (LimitOrderHandler 内の `BuildOrdersByIdSnapshot` は private 化されている前提なら名前変更しても良い、例: `SnapshotOrders`)。

---

## Step 7 — ドキュメント更新

### 目的

リファクタ後の構造をドキュメントに反映する。次の AI セッションが古い情報を参照しないようにする。

### やること

1. **編集**: [.claude/rules/src/core-domain.md](../../rules/src/core-domain.md)

   - "File Overview" テーブルに `World.cs`, `IPlayerOrderHandler.cs`, `LimitOrderHandler.cs`, `MarketOrderHandler.cs` を追加
   - `TurnProcessor` の責務説明を「Pipeline = World 遷移の合成、Handler が限値/成行戦略」に書き換え
   - "Game Turn Flow" セクションの番号付きステップを **新 Phase 構造** (Computer / Receive / Match / Settle / BookUpdate / TurnAdvance) に書き換え
   - "Responsibility boundaries" の `TurnProcessor` を「pipeline オーケストレーション、handler への dispatch」に変更

2. **編集**: [docs/DDD/MAIN.md](../../../docs/DDD/MAIN.md) (該当箇所があれば)

   - World, Handler の用語追加
   - Pipeline の責務記述

3. **オプション**: [src/FinLearn.Core/CLAUDE.md](../../../src/FinLearn.Core/CLAUDE.md) (もし存在すれば、または新規作成は不要)

### 完了確認

- ドキュメント記述が新コードと一致
- 古い `PlaceOrder` / `ExecutePlayerOrder` 用語が残っていないこと

```powershell
grep -r "ExecutePlayerOrder" docs/ .claude/
grep -r "PlaceOrder" .claude/rules/
```

両方とも 0 件であること (PlaceOrder は OrderPlacer.PlaceOrders インターフェースで残るので、そちらは OK)。

---

## 全体の進捗チェックリスト

```
[ ] Step 1: World 型 + WorldTests
[ ] Step 2: IPlayerOrderHandler + Limit/Market + Handler tests
[ ] Step 3: RunTurn pipeline 共存
[ ] Step 4: Wait を新 pipeline に切替
[ ] Step 5: Buy/Sell を新 pipeline に切替
[ ] Step 6: 旧コード削除
[ ] Step 7: ドキュメント更新
```

各 step 完了時のコミットメッセージ案:

- `refactor(core): introduce World snapshot type`
- `refactor(core): add IPlayerOrderHandler with Limit/Market strategies`
- `refactor(core): add RunTurn pipeline (not yet wired)`
- `refactor(core): wire Wait through new pipeline`
- `refactor(core): wire Buy/Sell through new pipeline`
- `refactor(core): remove obsolete PlaceOrder/ExecutePlayerOrder`
- `docs(core): update domain rules for World/Handler architecture`

## ロールバック方針

問題が発生した step ごとにロールバック:

- Step 1-3: 単独 revert で完全に戻る (旧コードに影響なし)
- Step 4: Wait の本体を旧 `Wait` 実装に戻す。Step 1-3 の追加コードは残しても無害
- Step 5: Buy/Sell の本体を旧 `PlaceOrder` 呼び出しに戻す
- Step 6 以降: revert

各 step を独立した PR にしておけば、リリースサイクルにも合わせやすい。
