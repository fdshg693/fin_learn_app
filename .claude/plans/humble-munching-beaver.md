# 発注時資源予約モデル（Reservation Model）導入

## Context

`TurnProcessor.PlaceOrder` は当ターンに発生したプレイヤーの**新規**注文約定だけを `Player.Portfolio.ApplyTrade` で反映している。一方で `ComputerTrader.PlaceOrders` 内で computer 注文がプレイヤーの**過去ターン resting 指値**と約定するケースは Portfolio に反映されない（[ComputerTrader.cs:111-112](src/FinLearn.Core/Services/ComputerTrader.cs#L111-L112) のコメントが明示）。

責務分離として「注文生成（intent）」と「マーケット結果反映（settlement）」を分け、settlement を**当ターン発生した全 OrderFill を traderId 別 Portfolio に統一適用**する形にしたい。その際、現状の現金不足 Warning ロールバック（[TurnProcessor.cs:113-118](src/FinLearn.Core/TurnProcessor.cs#L113-L118)）は**過去 resting の約定が確定事実なので巻き戻せない**問題に直面する。

これを解決するため、**実取引所と同じ「発注時に資源を予約するモデル」**を採用する。指値発注時に必要資源を `available → reserved` に移し、約定で確定、失効で解放することで、settlement は失敗しない設計にする。

### 設計方針（ユーザー確認済み）

- **手数料**: per-order（注文単位で1回。最終約定時に確定。途中部分約定では fee=0）— **現状の per-fill 挙動からの変更点**
- **スコープ**: Backend のみ。API DTO・フロントは本タスクでは変更しない。`PlayerDto.Cash` は意味が「available のみ」に変わる（外部契約は不変、値の意味のみ変化）
- **Portfolio 統合**: `Player.Portfolio` / `Game.ComputerPortfolios` の構造は維持。settlement 時のみ `IReadOnlyDictionary<string, Portfolio>` 統合 view を一時構築

---

## 1. Portfolio 拡張 ([Models/Portfolio.cs](src/FinLearn.Core/Models/Portfolio.cs))

### 1.1 新フィールド

```csharp
private readonly int _availableCash;
private readonly int _reservedCash;
private readonly PositionSet _positionSet;          // 全保有（available + reserved 合算）
private readonly PositionSet _reservedPositions;    // 売り指値で予約中
private readonly bool _isInfinite;
```

### 1.2 新 API

```csharp
public int Cash => _availableCash;                // 既存名維持。意味は「available」に変わる
public int ReservedCash => _reservedCash;
public int QuantityOf(int instrumentId);          // 全保有（既存どおり）
public int ReservedQuantityOf(int instrumentId);
public int AvailableQuantityOf(int instrumentId);

// 予約系（指値発注時）— 失敗時は (this, warning)
public (Portfolio, string?) ReserveBuy(int instrumentId, int quantity, int price, int fee);
public (Portfolio, string?) ReserveSell(int instrumentId, int quantity);

// 約定確定（指値の resting fill / 自身の incoming fill 両方で使用）
public Portfolio SettleReservedBuy(int instrumentId, int filledQty, int actualUnitPrice, int reservedUnitPrice, int feeIfFinal);
public Portfolio SettleReservedSell(int instrumentId, int filledQty, int actualUnitPrice, int feeIfFinal);

// 失効・全約定での予約解放
public Portfolio ReleaseBuyReservation(int remainingQty, int reservedUnitPrice, int remainingFee);
public Portfolio ReleaseSellReservation(int instrumentId, int remainingQty);

// 既存：成行注文（板に乗らない注文）専用
public (Portfolio, string?) ApplyTrade(TradeResult trade);

// 既存：合計
public int TotalAmount(IExchange exchange) => _availableCash + _reservedCash + _positionSet.Amount(exchange);
```

### 1.3 メソッド意味論

**ReserveBuy(qty, price, fee)**
- Available 不足 → `(this, Messages.InsufficientCashToBuy)`
- 成功: `_availableCash -= qty*price + fee`, `_reservedCash += qty*price + fee`

**ReserveSell(instrumentId, qty)**
- `AvailableQuantityOf(instrumentId) < qty` → `(this, Messages.InsufficientQuantityToSell)`
- 成功: `_reservedPositions` に該当 instrument の qty を加算（`_positionSet` は不変）

**SettleReservedBuy(filledQty, actualUnitPrice, reservedUnitPrice, feeIfFinal)**
- `feeIfFinal` は「この約定で order が完全消化されるなら fee、そうでなければ 0」（per-order セマンティクス）
- `consumedReserved = filledQty * reservedUnitPrice + feeIfFinal`
- `actualCost = filledQty * actualUnitPrice + feeIfFinal`
- `_reservedCash -= consumedReserved`
- `_availableCash += (consumedReserved - actualCost)` ← 差額が available に戻る（買いの約定価格 ≤ 予約価格より常に非負）
- `_positionSet` の該当 instrument に filledQty 加算

**SettleReservedSell(filledQty, actualUnitPrice, feeIfFinal)**
- `_positionSet` から filledQty 減算、`_reservedPositions` からも同量減算
- `_availableCash += filledQty * actualUnitPrice - feeIfFinal`

**ReleaseBuyReservation(remainingQty, reservedUnitPrice, remainingFee)**
- `releaseAmount = remainingQty * reservedUnitPrice + remainingFee`
- `_reservedCash -= releaseAmount`, `_availableCash += releaseAmount`

**ReleaseSellReservation(instrumentId, remainingQty)**
- `_reservedPositions` から remainingQty 減算（`_positionSet` は不変）

**Infinite Portfolio**
- 上記すべて no-op（`return this` または `(this, null)`）。`CreateInfinite()` を使う computer は挙動不変。

### 1.4 既存 ApplyTrade の位置づけ

成行注文専用に意味付け直す。実装は既存のまま。コメントで「成行注文（板に乗らない fill）専用。指値の settlement は SettleReserved* を使うこと」と注記。

---

## 2. OrderBook.ExpireOrders 拡張 ([Models/OrderBook.cs:49-58](src/FinLearn.Core/Models/OrderBook.cs#L49-L58))

```csharp
public (OrderBook Updated, IReadOnlyList<Order> Expired) ExpireOrders(int currentTurn);
```

失効注文リストを返すように変更（player の予約解放に必要）。

**呼び出し元修正**:
- [TurnProcessor.cs:207](src/FinLearn.Core/TurnProcessor.cs#L207) を分解代入 + `SettlementProcessor.ReleaseExpired` 呼び出しに変更
- [tests/FinLearn.Tests/OrderBookTests.cs](tests/FinLearn.Tests/OrderBookTests.cs) の ExpireOrders 関連 5 テスト（行 684-746 周辺）を `var (book, _) = ...` に修正

---

## 3. SettlementProcessor 新設

新規ファイル `src/FinLearn.Core/Services/SettlementProcessor.cs`（internal static）。

```csharp
internal static class SettlementProcessor
{
    /// 当ターン発生した全 OrderFill を traderId 別 Portfolio に統一適用。
    /// fills は OrderBook.Match の対称な fills（incoming + 全 resting）。同一 fill を二重処理しない。
    public static IReadOnlyDictionary<string, Portfolio> SettleFills(
        IReadOnlyList<OrderFill> fills,
        IReadOnlyDictionary<int, Order> ordersById,    // fill.OrderId → Order 逆引き
        IReadOnlyDictionary<int, int> postFillRemainingQty,  // 約定後の order 残量（fee per-order 判定に使う）
        IReadOnlyDictionary<string, Portfolio> portfolios,
        int fee);

    /// 失効した注文に対応する予約を解放する。
    public static IReadOnlyDictionary<string, Portfolio> ReleaseExpired(
        IReadOnlyList<Order> expiredOrders,
        IReadOnlyDictionary<string, Portfolio> portfolios,
        int feePerOrder);
}
```

### per-order fee の判定ロジック

`SettleFills` 内で、各 fill を処理する際に「この約定で order が完全消化されるか」を `postFillRemainingQty[orderId] == 0` で判定。**完全消化なら feeIfFinal = fee、部分なら 0** を `SettleReserved*` に渡す。

`postFillRemainingQty` は呼び出し側が `originalQty - sum(fills for orderId)` で算出して渡す。

### Limit / Market の分岐

```csharp
foreach (var fill in fills)
{
    var order = ordersById[fill.OrderId];
    var pf = portfolios[order.TraderId];
    var actualUnitPrice = fill.TotalAmount / fill.FilledQuantity;  // resting 価格
    var feeIfFinal = postFillRemainingQty[order.Id] == 0 ? fee : 0;

    if (order.Type == OrderType.Limit)
    {
        pf = order.Side == OrderSide.Buy
            ? pf.SettleReservedBuy(order.Instrument.Id, fill.FilledQuantity, actualUnitPrice, order.Price!.Value, feeIfFinal)
            : pf.SettleReservedSell(order.Instrument.Id, fill.FilledQuantity, actualUnitPrice, feeIfFinal);
    }
    else // Market：板に乗らないので予約なし fill
    {
        var trade = new TradeResult(order.Instrument.Id, order.Side, fill.FilledQuantity, fill.TotalAmount, feeIfFinal);
        (pf, _) = pf.ApplyTrade(trade);   // 成行は事前 affordability チェックで弾く前提
    }
    portfolios = update(portfolios, order.TraderId, pf);
}
```

---

## 4. IOrderPlacer / ComputerTrader 改修

### 4.1 IOrderPlacer ([Services/IOrderPlacer.cs](src/FinLearn.Core/Services/IOrderPlacer.cs))

```csharp
public interface IOrderPlacer
{
    OrderPlacementResult PlaceOrders(
        OrderBook book, IExchange exchange, IReadOnlyList<Instrument> instruments,
        int startOrderId, int currentTurn,
        IReadOnlyDictionary<string, Portfolio> traderPortfolios);  // computer + player 統合
}

public sealed record OrderPlacementResult(
    OrderBook UpdatedBook,
    int NextOrderId,
    IReadOnlyList<Order> PlacedOrders,
    IReadOnlyList<OrderFill> Fills,                                // 全約定明細
    IReadOnlyDictionary<int, Order> SnapshotOrdersById,            // fill 逆引き用（pre/new 全注文）
    IReadOnlyDictionary<string, Portfolio> UpdatedTraderPortfolios);
```

### 4.2 ComputerTrader ([Services/ComputerTrader.cs](src/FinLearn.Core/Services/ComputerTrader.cs))

- `ApplyFillsToComputerPortfolios`（[行 114-140](src/FinLearn.Core/Services/ComputerTrader.cs#L114-L140)）を**削除**
- 各 computer 注文の発注時に `ReserveBuy` / `ReserveSell` を呼ぶ（Infinite なので no-op、既存動作は不変）
- 各 `book.Match(order)` の fill を蓄積し、最後に `SettlementProcessor.SettleFills` を呼んで `traderPortfolios` を更新
- これにより**player の resting 指値と computer 注文が約定したら、player Portfolio にも反映される**（バグ修正）
- 戻り値を `OrderPlacementResult` に変更

### 4.3 NoOpOrderPlacer ([tests/FinLearn.Tests/NoOpOrderPlacer.cs](tests/FinLearn.Tests/NoOpOrderPlacer.cs))

- 新シグネチャに追従。空 fills を返す

---

## 5. TurnProcessor.PlaceOrder 書き換え ([TurnProcessor.cs](src/FinLearn.Core/TurnProcessor.cs))

### 新フロー

```
1. exchange = ExchangeFactory.Create(...)
2. allPortfolios = BuildAllPortfolios(game)  // Player.Name → Player.Portfolio + ComputerPortfolios
3. var placement = OrderPlacer.PlaceOrders(book, exchange, instruments, nextId, turn, allPortfolios);
   // 内部で computer 約定（player resting 含む）の settlement 完了。allPortfolios 更新済み。
   allPortfolios = placement.UpdatedTraderPortfolios

4. プレイヤー注文を作成（Player.CreateOrder）

5. 指値の場合のみ事前予約:
     var (reserved, warn) = side == Buy
         ? allPortfolios[Player.Name].ReserveBuy(instrumentId, qty, price.Value, fee)
         : allPortfolios[Player.Name].ReserveSell(instrumentId, qty);
     if (warn != null) → Wait 化（computer 注文は確定、player 予約失敗 warning を返す）
     else allPortfolios[Player.Name] = reserved

6. matchResult = Market.Execute(placement.UpdatedBook, order, exchange)

7. プレイヤー注文 fills の settlement:
   - 指値 → SettlementProcessor.SettleFills（予約から確定、差額 refund）
   - 成行 → ApplyTrade で同期適用。warning 出たら旧フロー通りロールバック（matchResult.Fills 破棄、player Portfolio 不変、computer settlement は確定済みなので維持）

8. AddRemainingLimitOrder で残量を板に追加

9. AdvanceTurn:
     a. (newBook, expired) = book.ExpireOrders(turn+1)
     b. allPortfolios = SettlementProcessor.ReleaseExpired(expired, allPortfolios, fee)

10. SplitPortfolios(allPortfolios) → (Player, ComputerPortfolios) で Game 構築
```

### 補助 helper

```csharp
private static IReadOnlyDictionary<string, Portfolio> BuildAllPortfolios(Game game);
private static (Player, IReadOnlyDictionary<string, Portfolio>) SplitPortfolios(
    Player original, IReadOnlyDictionary<string, Portfolio> all);
```

### Wait メソッド ([TurnProcessor.cs:69-83](src/FinLearn.Core/TurnProcessor.cs#L69-L83))

同様に allPortfolios 経由に修正。`OrderPlacer.PlaceOrders` の更新後 portfolios を split して Game に書き戻す。

### ロールバック挙動の変更点

- **指値**: 事前予約成功時点で settlement は失敗しない設計 → ロールバックパスは存在しない
- **成行**: 旧フローを維持。ただしロールバック対象は「player の market fill のみ」。computer 同士・computer-vs-player resting の settlement は **確定事実として維持**（旧コードは破棄していた点が変更）

---

## 6. テスト

### 6.1 新規

**PortfolioTests** ([tests/FinLearn.Tests/PortfolioTests.cs](tests/FinLearn.Tests/PortfolioTests.cs))
- `ReserveBuy で available cash → reserved cash に移動`
- `ReserveBuy 残高不足で warning + 状態不変`
- `ReserveSell で available 数量 → reserved positions に移動（_positionSet 不変）`
- `ReserveSell 数量不足で warning`
- `SettleReservedBuy 部分約定: feeIfFinal=0、reserved cash 減、差額 refund、保有増`
- `SettleReservedBuy 全約定: feeIfFinal=fee、reserved 全消費`
- `SettleReservedSell: 保有減・reserved positions 減・cash 増`
- `ReleaseBuyReservation で reserved cash → available に戻る`
- `ReleaseSellReservation で reserved positions のみ消費`
- `Infinite Portfolio は予約系メソッドが全て no-op`
- `TotalAmount = available + reserved + 全保有評価`

**OrderBookTests** ([tests/FinLearn.Tests/OrderBookTests.cs](tests/FinLearn.Tests/OrderBookTests.cs))
- `ExpireOrders は失効した注文リストを返す`
- `ExpireOrders は失効が無い場合 空リストを返す`

**SettlementProcessorTests**（新規ファイル）
- `Limit Buy fill で予約から確定 + 差額 refund`
- `Limit Sell fill で reserved positions が消費され cash 増`
- `Market fill は ApplyTrade で適用`
- `部分約定では feeIfFinal=0、全約定で feeIfFinal=fee`
- `ReleaseExpired で player 予約のみ解放、computer は no-op`

**TurnProcessorTests** ([tests/FinLearn.Tests/TurnProcessorTests.cs](tests/FinLearn.Tests/TurnProcessorTests.cs))
- ★**バグ修正検証**: `プレイヤーの過去ターン resting 指値が当ターン computer 注文と約定すると Player.Portfolio に反映される`
- `指値買い発注で reservedCash が即増える、Cash（available）が減る`
- `指値買い約定で Cash 差額 refund + 保有増`
- `指値売り発注で reservedPositions 増・available 数量減`
- `指値が失効すると予約が available に戻る`
- `指値で予約失敗（残高不足）→ ターン進行 + warning + Fills 空、computer 注文は確定`

**TurnProcessorLoggingTests** ([tests/FinLearn.Tests/TurnProcessorLoggingTests.cs](tests/FinLearn.Tests/TurnProcessorLoggingTests.cs))
- 既存の「Buy残高不足_はFillsを空にロールバックする」（行 104-130）は成行前提なので維持
- 新規: 指値版「指値Buy予約失敗_はFillsを空にして警告を返す」

### 6.2 既存テスト修正

- [PortfolioTests.cs](tests/FinLearn.Tests/PortfolioTests.cs): 既存 `ApplyTrade` 系は成行 fill 想定として維持。Cash の意味変更により値が変わるテストはなし（既存テストは予約を使わないため）
- [OrderBookTests.cs](tests/FinLearn.Tests/OrderBookTests.cs): ExpireOrders 戻り値タプル化に伴う修正
- [ComputerTraderTests.cs](tests/FinLearn.Tests/ComputerTraderTests.cs): 戻り値 `OrderPlacementResult` 化に伴う修正、settlement が外に出たことの影響反映
- [TurnProcessorTests.cs](tests/FinLearn.Tests/TurnProcessorTests.cs)「現金不足の購入は失敗するがターンは進む」: 成行（price 指定なし）テストとして維持。挙動変更なし
- [GameApiTests.cs](tests/FinLearn.Api.Tests/GameApiTests.cs): API 契約は不変。ただし「指値発注後の Cash」を assert している箇所があれば「available のみ」の値に修正

---

## 7. ステップ順序

1. **Portfolio 拡張** + PortfolioTests 追加
2. **OrderBook.ExpireOrders 戻り値変更** + OrderBookTests 修正
3. **SettlementProcessor 新設** + SettlementProcessorTests 追加
4. **IOrderPlacer / OrderPlacementResult 定義**
5. **ComputerTrader 改修**（settlement 削除、予約呼び出し追加、戻り値変更）+ ComputerTraderTests 修正
6. **NoOpOrderPlacer** シグネチャ追従
7. **TurnProcessor 改修**（PlaceOrder / Wait / AdvanceTurn 全面書き換え）+ TurnProcessorTests / LoggingTests 修正・追加
8. **GameApiTests** で API 契約不変を確認、Cash 意味変更箇所のみ修正
9. **CLAUDE.md / .claude/rules 更新**: `core-domain.md` の Portfolio / TurnProcessor / ComputerTrader 説明を最新化

各ステップで `dotnet build` + `dotnet test` を通すこと。

---

## 8. 変更対象ファイル一覧

### Core
- [src/FinLearn.Core/Models/Portfolio.cs](src/FinLearn.Core/Models/Portfolio.cs) — 拡張
- [src/FinLearn.Core/Models/OrderBook.cs](src/FinLearn.Core/Models/OrderBook.cs) — ExpireOrders 戻り値変更
- [src/FinLearn.Core/Services/IOrderPlacer.cs](src/FinLearn.Core/Services/IOrderPlacer.cs) — シグネチャ拡張
- [src/FinLearn.Core/Services/ComputerTrader.cs](src/FinLearn.Core/Services/ComputerTrader.cs) — settlement 削除、予約呼び出し追加
- [src/FinLearn.Core/Services/SettlementProcessor.cs](src/FinLearn.Core/Services/SettlementProcessor.cs) — 新規
- [src/FinLearn.Core/TurnProcessor.cs](src/FinLearn.Core/TurnProcessor.cs) — PlaceOrder / Wait / AdvanceTurn 書き換え

### Tests
- [tests/FinLearn.Tests/PortfolioTests.cs](tests/FinLearn.Tests/PortfolioTests.cs)
- [tests/FinLearn.Tests/OrderBookTests.cs](tests/FinLearn.Tests/OrderBookTests.cs)
- [tests/FinLearn.Tests/SettlementProcessorTests.cs](tests/FinLearn.Tests/SettlementProcessorTests.cs) — 新規
- [tests/FinLearn.Tests/ComputerTraderTests.cs](tests/FinLearn.Tests/ComputerTraderTests.cs)
- [tests/FinLearn.Tests/NoOpOrderPlacer.cs](tests/FinLearn.Tests/NoOpOrderPlacer.cs)
- [tests/FinLearn.Tests/TurnProcessorTests.cs](tests/FinLearn.Tests/TurnProcessorTests.cs)
- [tests/FinLearn.Tests/TurnProcessorLoggingTests.cs](tests/FinLearn.Tests/TurnProcessorLoggingTests.cs)
- [tests/FinLearn.Api.Tests/GameApiTests.cs](tests/FinLearn.Api.Tests/GameApiTests.cs) — Cash 意味変更箇所のみ

### Docs
- [.claude/rules/src/core-domain.md](.claude/rules/src/core-domain.md) — Portfolio / TurnProcessor / ComputerTrader / Settlement の説明更新

### スコープ外（本タスクで変更しない）
- API DTO（PlayerDto, PositionDto）
- フロントエンド全般

---

## 9. リスク・注意点

1. **Per-order fee への変更は挙動変更**: 既存は per-fill。`feeIfFinal` を最後の約定で計上する形に変わるため、複数 fill にまたがる注文の cash 推移が変わる。既存テストでこの数値を assert している箇所を洗い出し修正
2. **fill 重複処理防止**: `OrderBook.Match` は incoming + 全 resting の双方の fill を返すので、`SettleFills` は一度の処理ループで全 fill を扱い、同じ orderId に対する settlement を二重に呼ばない
3. **postFillRemainingQty の算出**: 呼び出し側で `order.Quantity - sum(fills.Where(f => f.OrderId == order.Id).Sum(f => f.FilledQuantity))` を計算する。`order.Quantity` は match 直前の値（部分約定 resting なら残量、incoming なら元の発注量）
4. **成行ロールバックの意味変更**: ロールバック対象は player の市場注文 fill のみ。Computer 同士・computer-vs-player resting の settlement は確定維持。`TurnProcessorLoggingTests` の既存テストでログと Portfolio 状態の整合を再確認
5. **PlayerDto.Cash の意味変更**: 外部契約は不変だが、指値発注後に値が減る点は API クライアント（フロント）の挙動が直感的に変わる。ただしフロント変更スコープ外なので「現金が減って見える」のは想定挙動として受け入れる
6. **Self-trade**: 既存の自己約定防止（[OrderBook.cs:67-68](src/FinLearn.Core/Models/OrderBook.cs#L67-L68)）により、player が同 instrument で buy + sell の両方を出していても自己約定はしない。予約は両側独立に走るので cash と position が二重拘束される（実取引所と同じ）

---

## 10. 検証方法

### ユニット
```
dotnet test tests/FinLearn.Tests/
dotnet test tests/FinLearn.Api.Tests/
```

### E2E（手動）
1. `dotnet run --project src/FinLearn.Api` で API 起動
2. `cd frontend && npm run dev` でフロント起動
3. ゲーム作成 → 指値買い発注 → Cash 表示が `qty*price + fee` 分減少することを確認
4. ターンを進めて指値が約定 → 保有数量増、約定価格 < 指値の場合差額が Cash に refund
5. 指値が失効するまでターンを進める → Cash が予約分戻る
6. ★ 指値売りを発注し、ターン進行で computer の買い注文と約定 → 保有減・Cash 増（**バグ修正の検証**）

### バグ修正の単体検証ポイント
- ターン N でプレイヤー指値売りを発注
- ターン N+1 以降で computer の買い注文がその指値を約定するシナリオを `Random` シード固定で再現
- `Game.Player.Portfolio.Cash` が増加し、保有数量が減少することを assert
