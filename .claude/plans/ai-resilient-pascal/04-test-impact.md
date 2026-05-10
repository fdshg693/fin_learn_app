# 04 — Test Impact and Strategy

## このファイルは何か

リファクタが既存テストにどう影響するか、どんな新規テストを追加するかを整理する。`Buy/Sell/Wait` の public シグネチャを維持する方針なので、**既存テストはほぼ書き換え不要のはず** という前提を置いた上で、要注意ポイントを列挙する。

各ステップでのテスト戦略は [03-migration-steps.md](03-migration-steps.md) を参照。

## 全体方針

| 観点 | 方針 |
|---|---|
| 既存テスト | **書き換え不要** が原則。書き換えが必要 = リファクタの範囲を間違えた合図 |
| 新規テスト | 新型 (`World`, `LimitOrderHandler`, `MarketOrderHandler`) は単独でテスト |
| Pipeline テスト | `RunTurn` 単独のテストは不要 (既存 TurnProcessorTests でカバーされる) |
| 回帰防止 | 各 step 末尾で `dotnet test` 全グリーンを死守 |

## 既存テストの全体像

| ファイル | テスト数 | リファクタの影響 |
|---|---|---|
| [tests/FinLearn.Tests/TurnProcessorTests.cs](../../../tests/FinLearn.Tests/TurnProcessorTests.cs) | 42 | Step 4-5 で挙動再現確認 (書き換え不要) |
| [tests/FinLearn.Tests/TurnProcessorLoggingTests.cs](../../../tests/FinLearn.Tests/TurnProcessorLoggingTests.cs) | 12 | Step 4-5 で挙動再現確認 (書き換え不要) |
| [tests/FinLearn.Api.Tests/GameApiTests.cs](../../../tests/FinLearn.Api.Tests/GameApiTests.cs) | 40 | Step 5 で挙動再現確認 (書き換え不要) |
| [tests/FinLearn.Tests/GameTests.cs](../../../tests/FinLearn.Tests/GameTests.cs) | 7 | 影響なし (Game 自体は変えない) |
| [tests/FinLearn.Tests/PlayerTests.cs](../../../tests/FinLearn.Tests/PlayerTests.cs) | 11 | 影響なし |
| [tests/FinLearn.Tests/PortfolioTests.cs](../../../tests/FinLearn.Tests/PortfolioTests.cs) | 27 | 影響なし |
| [tests/FinLearn.Tests/ComputerTraderTests.cs](../../../tests/FinLearn.Tests/ComputerTraderTests.cs) | 5 | 影響なし |
| [tests/FinLearn.Tests/SettlementProcessorTests.cs](../../../tests/FinLearn.Tests/SettlementProcessorTests.cs) | ~15 | 影響なし |
| [tests/FinLearn.Tests/MarketTests.cs](../../../tests/FinLearn.Tests/MarketTests.cs) | 7 | 影響なし |
| [tests/FinLearn.Tests/OrderBookTests.cs](../../../tests/FinLearn.Tests/OrderBookTests.cs) | ~15 | 影響なし |
| その他 (Order/Position/Exchange/PriceFluctuator) | ~30 | 影響なし |
| **合計** | 222 + α | — |

## 新規テスト

### tests/FinLearn.Tests/WorldTests.cs (Step 1 で追加)

`World` 型の単体テスト。

**テストケース** (Japanese name):

```
World_テスト
├─ FromGame_でPlayerPortfolioとComputerPortfoliosが統合されたPortfoliosが構築される
├─ FromGame_でWorld_PlayerNameはGame_Player_Nameと一致する
├─ FromGame_でWorld_TurnはGame_Turnと一致する
├─ FromGame_でWorld_NextOrderIdはGame_NextOrderIdと一致する
├─ FromGame_でWorld_BookはGame_OrderBookと一致する
├─ FromGame_でWorld_PricesはGame_Pricesと一致する
├─ PlayerPortfolio_でPlayerNameに対応するPortfolioが取得できる
├─ WithPlayerPortfolio_でPlayerだけ更新されComputer分は変わらない
├─ WithPlayerPortfolio_は元のWorldを変更しない
├─ WithBook_でBookだけ更新される
├─ WithBook_は元のWorldを変更しない
├─ WithPortfolios_は元のWorldを変更しない
└─ WithNextOrderId_でNextOrderIdだけ更新される
```

**テストヘルパー**:

```csharp
private static World CreateWorld(
    Player? player = null, OrderBook? book = null,
    int nextOrderId = 1, int turn = 1, int fee = 0)
{
    var p = player ?? new Player();
    var prices = new Dictionary<int, int> { [1] = 100 };
    var instruments = new[] { TestData.Instrument1 };
    var game = new Game(p, turn, book ?? new OrderBook(), nextOrderId, instruments, prices);
    return World.FromGame(game, fee, TestData.CreateExchange((1, 100)));
}
```

`TestData.CreateInfiniteComputerPortfolios()` を活用。

### tests/FinLearn.Tests/LimitOrderHandlerTests.cs (Step 2 で追加)

`LimitOrderHandler` の単体テスト。Pipeline を通らずに Receive / Settle 単独で動作検証。

**テストケース**:

```
LimitOrderHandler_テスト
Receive
├─ 買い注文_で指値予約される_AvailableCashが減りReservedCashが増える
├─ 売り注文_で指値予約される_AvailableQtyが減りReservedQtyが増える
├─ 残高不足の場合_World不変でWarningが返る
├─ 数量不足の場合_World不変でWarningが返る (売り)
└─ World_Bookは変更されない

Settle
├─ 完全約定_予約が消費されAvailableCashが増える (差額返金)
├─ 部分約定_部分だけ予約消費される
├─ 約定ゼロ_World不変でWarningはnull (限値は板に残るため)
└─ Settle_でportfolioはSettleFills経由で更新される (computer分も含む)
```

**テスト構築のポイント**:

- `MatchResult` を組み立てるには `Market.Execute` を実際に呼ぶか、`new MatchResult(...)` で直接構築 (record なので可)
- 板に対側の resting 注文を仕込む方法: `OrderBook.Add(new Order(...))` を直接呼ぶ
- `Order` の構築: `Player.CreateOrder` 経由 (限値の場合は price を渡す)

**ヘルパー実装例**:

```csharp
private static LimitOrderHandler _handler = new();

private static (World, Order) SetupBuyLimit(int price, int quantity = 10, int initialCash = 10000)
{
    var player = new Player(); // Cash = GameRules.Player.InitialCash
    var book = new OrderBook(); // または対側注文を仕込む
    var instruments = new[] { TestData.Instrument1 };
    var prices = new Dictionary<int, int> { [1] = 100 };
    var game = new Game(player, turn: 1, book, nextOrderId: 1, instruments, prices);
    var world = World.FromGame(game, fee: 0, TestData.CreateExchange((1, 100)));
    var order = player.CreateOrder(
        orderId: 1, TestData.Instrument1, OrderSide.Buy,
        quantity, price, stopPrice: null,
        createdAtTurn: 1, expiresAtTurn: 3);
    return (world, order);
}
```

### tests/FinLearn.Tests/MarketOrderHandlerTests.cs (Step 2 で追加)

```
MarketOrderHandler_テスト
Receive
├─ Buy_は常にWorld不変でWarning_null
└─ Sell_は常にWorld不変でWarning_null

Settle
├─ 成行買い_約定するとPortfolio_Cashが減りPositionが増える
├─ 成行売り_約定するとPortfolio_Cashが増えPositionが減る
├─ 約定ゼロ_買い_NoMatchingSellOrdersのWarning_World不変
├─ 約定ゼロ_売り_NoMatchingBuyOrdersのWarning_World不変
├─ 残高不足_買い_Warning_World不変
└─ 数量不足_売り_Warning_World不変
```

## 既存テストで要注意のケース

リファクタで挙動が微妙に変わるとテストが落ちる可能性のあるケース。Step 5 完了時に必ずチェック:

### TurnProcessorTests.cs

| テスト名 | 注意点 |
|---|---|
| `購入成功でターンが1進む` | `Game.Turn = inputTurn + 1` の不変条件 |
| `指値買い発注で即座にreservedCashが増えavailableCashが減る` | Receive 直後の World が AdvanceTurn で正しく Game に書き戻されているか |
| `指値買い注文が約定せず注文が板に残りターンが進む` | Settle で warning=null かつ matchResult.UpdatedBook が反映され、AddRemainingLimitOrder で残量が板に乗る |
| `指値買い注文が部分約定し未約定分が板に残る` | 部分約定 → SettleFills で部分予約消費 → 残量は AddRemainingLimitOrder で板に追加 |
| `指値が失効すると予約が_available_に戻る` | AdvanceTurn の `ReleaseExpired` が新コードでも呼ばれているか |
| `指値で予約失敗_残高不足_はターン進行_warning_Fills空_player注文も含む` | **重要**: Receive 失敗でも submittedOrders に player order を含めること、ターンは進む (AdvanceTurn 呼ばれる)、Fills は空 |
| `プレイヤーの過去ターン_resting_指値が_computer_注文と約定するとPortfolioに反映される` | Computer phase 内の SettleFills が player Portfolio を更新 (これは OrderPlacer.PlaceOrders の挙動) |

### TurnProcessorLoggingTests.cs

| テスト名 | 注意点 |
|---|---|
| `Buy通常約定_のFillsはMatchResultの全約定明細と一致する` | `match.Fills` が TurnResult.Fills にそのまま渡される |
| `Buy成行約定ゼロ_はFillsが空でSubmittedOrdersはコンピューターのみ` | ⚠️ **テスト名と現状実装の整合を再確認**。現状コードでは成行 fill=0 でも submittedOrders は player を含む (`Combine` の後で早期 return しているため)。テスト名が "コンピューターのみ" と言っているのが正しいなら、新コードもそれに合わせる必要あり。実装前にこのテストの assertion を読むこと |
| `Buy残高不足_はFillsを空にロールバック` | Settle 失敗時 (成行残高不足) に Fills 空、submittedOrders に player 含む |
| `ProcessedTurnは入力ゲームのターンと一致する` | `BuildTurnResult(inputGame, ...)` で `inputGame.Turn` を `ProcessedTurn` に設定 |

### GameApiTests.cs

| 観点 | 注意点 |
|---|---|
| `POST /api/games/{id}/orders` (Buy/Sell) | Buy/Sell の TurnResult.Game が正しく GameStore に保存される |
| `POST /api/games/{id}/wait` | Wait の TurnResult.Game が正しく保存される |
| 警告系 | Warning が API レスポンスに反映される (Game は更新されない or される、ロジック次第) |

⚠️ Warning 時に Game を保存するか否かは GameEndpoints の責務。リファクタで TurnResult.Warning の意味が変わっていないので影響なしのはず。

## 影響範囲の検証手順 (Step 5 完了時)

1. **全テスト実行**
   ```powershell
   dotnet test fin_learn_app.sln --nologo --verbosity quiet
   ```

2. **TurnProcessor 系のみ実行 (高速反復用)**
   ```powershell
   dotnet test tests/FinLearn.Tests --filter "TurnProcessor" --nologo
   ```

3. **API 系のみ実行**
   ```powershell
   dotnet test tests/FinLearn.Api.Tests --nologo
   ```

4. **新規テスト実行**
   ```powershell
   dotnet test --filter "World|Handler" --nologo
   ```

落ちたテストは [03-migration-steps.md](03-migration-steps.md) Step 4 / Step 5 のトラブルシューティング節を参照。

## テスト件数の期待値

| Step | Core テスト | API テスト | 合計 |
|---|---|---|---|
| 開始時 | 185 | 37 | 222 |
| Step 1 完了 | 185 + ~12 | 37 | ~234 |
| Step 2 完了 | 185 + ~12 + ~17 | 37 | ~251 |
| Step 3-7 完了 | 同上 | 37 | ~251 |

新規テスト数は概算。実装時に丁寧に書けば若干増減する。

## 新規テストの設計ポリシー

- **既存テスト命名規約に従う**: 日本語アンダースコア区切り (`Receive_で買い注文が指値予約される`)
- **Arrange-Act-Assert パターン**: 既存ファイル ([PortfolioTests.cs](../../../tests/FinLearn.Tests/PortfolioTests.cs) を参考) と同じ書き方
- **TestData.cs を活用**: `Instrument1`, `CreateExchange`, `CreateInfiniteComputerPortfolios`
- **Fact / Theory の使い分け**: パラメタライズしたい場合のみ Theory
- **私的ヘルパー関数**: 各テストファイルで private static で定義 (TestData にむやみに足さない)

## 補足: テスト実装時の落とし穴

### LimitOrderHandlerTests でハマりやすい点

- `LimitOrderHandler.Settle` は `world.Book` から ordersById を構築する。テストで `MatchResult` を直接組み立てる場合、`world.Book` に対側 resting 注文が乗っていることを忘れずに (= player match の前提)
- `Order.Quantity` は元の発注数量。`SettleFills` の `postFillRemainingQty` 計算で `order.Quantity - filledQty` が使われる

### MarketOrderHandlerTests でハマりやすい点

- 成行注文には `Order.CreateMarket(...)` ファクトリを使う必要があるかもしれない (Order.cs を確認)
- `MatchResult.Trade.FilledQuantity == 0` の状況を作るには、板を空にして Match を呼ぶか、`new MatchResult(new TradeResult(...0...), book, [])` で直接構築

### 共通

- `World` は internal なので、テストプロジェクトから参照するには `InternalsVisibleTo` 属性が必要かもしれない (`src/FinLearn.Core/AssemblyInfo.cs` または .csproj を確認)。既存 internal 型 (例: `IPlayerOrderHandler` 含めて) も同じく要確認

## InternalsVisibleTo の確認

リファクタ開始前に [src/FinLearn.Core/FinLearn.Core.csproj](../../../src/FinLearn.Core/FinLearn.Core.csproj) または `AssemblyInfo` を確認:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="FinLearn.Tests" />
</ItemGroup>
```

これがなければ、`World` / `IPlayerOrderHandler` を public にするか、`InternalsVisibleTo` を追加する。Step 1 開始時にチェック。
