# InMemoryStore 責務分離 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** InMemoryStore に混在するビジネスロジック（マッチング・ターン処理）を Domain 層に移動し、InMemoryStore を薄いデリゲーターに変える。

**Architecture:** マッチングロジックは `Exchange` ドメインエンティティのメソッドに移動する。ターン処理（価格変動・注文生成・板寄せ）は `TurnDomainService`（Domain サービス）に移動する。`InMemoryStore` は Execute 系メソッドを `Exchange` に、`AdvanceTurn` を `TurnDomainService` に転送するだけになる。

**Tech Stack:** .NET 9, C#, xUnit, Clean Architecture (Domain → Application → Infrastructure/Api)

---

## ファイル構成

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `library/Application/Actions/OrderMatchResult.cs` | 移動 | `library/Domain/ValueObjects/OrderMatchResult.cs` へ移動・namespace 変更 |
| `library/Application/Actions/IActionExecutionStore.cs` | using 更新 | `OrderMatchResult` の namespace 変更に追従 |
| `backend/FinLearnApp.Api/Data/InMemoryStore.cs` | using 更新 | 同上 |
| `backend/FinLearnApp.Tests/OrderMatchingTests.cs` | using 更新 | 同上 |
| `library/Domain/Entities/Exchange.cs` | 修正 | マッチングメソッド・`Trades` 追加 |
| `library/Domain/Services/TurnDomainService.cs` | 新規作成 | ターン処理ドメインサービス |
| `backend/FinLearnApp.Tests/Domain/ExchangeMatchingTests.cs` | 新規作成 | Exchange 直接テスト |
| `backend/FinLearnApp.Tests/Domain/TurnDomainServiceTests.cs` | 新規作成 | TurnDomainService 直接テスト |
| `backend/FinLearnApp.Api/Data/InMemoryStore.cs` | 修正 | Execute 系・AdvanceTurn をデリゲーターに変更 |

---

## Task 1: OrderMatchResult を Domain 層へ移動

**Files:**
- Create: `library/Domain/ValueObjects/OrderMatchResult.cs`
- Delete: `library/Application/Actions/OrderMatchResult.cs`
- Modify: `library/Application/Actions/IActionExecutionStore.cs`
- Modify: `backend/FinLearnApp.Api/Data/InMemoryStore.cs`（using のみ）
- Modify: `backend/FinLearnApp.Tests/OrderMatchingTests.cs`（using のみ）

**背景:** `Exchange`（Domain 層）がマッチング結果を返すには `OrderMatchResult` が Domain 層にある必要がある。Application → Domain の依存方向は OK だが、Domain → Application は NG。

- [ ] **Step 1: 新しいファイルを作成する**

`library/Domain/ValueObjects/OrderMatchResult.cs` を作成する。namespace を `FinLearnApp.Domain.ValueObjects` に変更する以外、内容は完全に同じ。

```csharp
namespace FinLearnApp.Domain.ValueObjects;

/// <summary>
/// 投資家注文を即時マッチングした結果。
/// RequestedQuantity 株を要求し、ExecutedQuantity 株が約定したことを表す。
/// </summary>
public sealed class OrderMatchResult
{
    /// <summary>要求した株数。</summary>
    public int RequestedQuantity { get; }

    /// <summary>実際に約定した株数。</summary>
    public int ExecutedQuantity { get; }

    /// <summary>約定した総金額（約定しなかった分は含まない）。</summary>
    public Money TotalAmount { get; }

    /// <summary>未約定の株数（RequestedQuantity - ExecutedQuantity）。</summary>
    public int RemainingQuantity => RequestedQuantity - ExecutedQuantity;

    /// <summary>マッチング結果を生成する。</summary>
    /// <param name="requestedQuantity">要求株数。</param>
    /// <param name="executedQuantity">約定株数。</param>
    /// <param name="totalAmount">約定総額。</param>
    public OrderMatchResult(int requestedQuantity, int executedQuantity, Money totalAmount)
    {
        RequestedQuantity = requestedQuantity;
        ExecutedQuantity = executedQuantity;
        TotalAmount = totalAmount;
    }
}
```

- [ ] **Step 2: 旧ファイルを削除する**

```bash
rm library/Application/Actions/OrderMatchResult.cs
```

- [ ] **Step 3: IActionExecutionStore.cs の using を更新する**

`library/Application/Actions/IActionExecutionStore.cs` を以下に書き換える。

```csharp
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

/// <summary>
/// アクション実行に必要なデータアクセスを抽象化するインターフェース。
/// Application 層のハンドラーはこのインターフェースのみに依存し、
/// 具体的な保存方法（InMemoryStore 等）を知らない。
/// </summary>
public interface IActionExecutionStore
{
    Portfolio? FindPortfolioByInvestor(InvestorId investorId);
    Ticker? FindTicker(TickerId tickerId);
    int GetCurrentTurn(InvestorId investorId);
    int AdvanceTurn(InvestorId investorId);
    OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash);
    OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity);
    OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash);
    OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice);
}
```

- [ ] **Step 4: InMemoryStore.cs の using を更新する**

`backend/FinLearnApp.Api/Data/InMemoryStore.cs` の先頭 using ブロックを以下に置き換える（`FinLearnApp.Application.Actions` は引き続き残す）。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;
```

（変更なし。`OrderMatchResult` は `FinLearnApp.Domain.ValueObjects` に移動したが、そのusing は既に存在する）

- [ ] **Step 5: OrderMatchingTests.cs の using を確認する**

`backend/FinLearnApp.Tests/OrderMatchingTests.cs` の先頭 using に `FinLearnApp.Domain.ValueObjects` が含まれているか確認する。なければ追加する。テストコード本体の変更は不要（`OrderMatchResult` を型名として明示使用していないため）。

- [ ] **Step 6: ビルドしてエラーがないことを確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet build
```

Expected: `Build succeeded.`

- [ ] **Step 7: テストを実行する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --no-build
```

Expected: 全テスト PASS

- [ ] **Step 8: コミットする**

```bash
git add library/Domain/ValueObjects/OrderMatchResult.cs \
        library/Application/Actions/OrderMatchResult.cs \
        library/Application/Actions/IActionExecutionStore.cs
git commit -m "refactor: OrderMatchResultをDomain層に移動"
```

---

## Task 2: Exchange にマッチングメソッドと Trades を追加

**Files:**
- Modify: `library/Domain/Entities/Exchange.cs`
- Create: `backend/FinLearnApp.Tests/Domain/ExchangeMatchingTests.cs`

**背景:** マッチングロジック（売買候補の探索・OrderBook の更新・Trade 記録）は「取引所の振る舞い」であり Domain エンティティが持つべき責務。現在は InMemoryStore のプライベートメソッドに埋まっている。

- [ ] **Step 1: テストファイルを作成する（Red）**

`backend/FinLearnApp.Tests/Domain/ExchangeMatchingTests.cs` を作成する。

```csharp
using System;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

/// <summary>
/// Exchange エンティティのマッチングロジックを直接検証するテスト。
/// InMemoryStore を介さず Exchange を単体でテストする。
/// </summary>
public class ExchangeMatchingTests
{
    private static readonly CompanyId TestCompanyId = new(Guid.Parse("cccccccc-0000-0000-0000-000000000001"));
    private static readonly TickerId  TestTickerId  = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));

    /// <summary>手数料 500 円の Exchange を生成するヘルパー。</summary>
    private static Exchange CreateExchange() => new(Money.Jpy(500m));

    /// <summary>テスト用 Ticker を生成するヘルパー。</summary>
    private static Ticker CreateTicker(decimal price = 1_000m)
        => new(TestTickerId, TestCompanyId, "AOKI", 1, Money.Jpy(price));

    private static void AddSellOrder(Exchange exchange, TickerId tickerId, decimal price, int quantity)
        => exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), tickerId, OrderSide.Sell,
            Money.Jpy(price), quantity, OrderOrigin.System, DateTimeOffset.UtcNow));

    private static void AddBuyOrder(Exchange exchange, TickerId tickerId, decimal price, int quantity)
        => exchange.OrderBook.Add(new Order(
            new OrderId(Guid.NewGuid()), tickerId, OrderSide.Buy,
            Money.Jpy(price), quantity, OrderOrigin.System, DateTimeOffset.UtcNow));

    // ================================================================
    // ExecuteBuyNow
    // ================================================================

    [Fact]
    public void Exchange_ExecuteBuyNow_MatchingOrder_ReturnsCorrectResult()
    {
        // Arrange: 売り板に 1,000 円 × 10 株がある状態で 5 株成行買い
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddSellOrder(exchange, ticker.Id, price: 1_000m, quantity: 10);

        // Act
        var result = exchange.ExecuteBuyNow(ticker.Id, quantity: 5,
            availableCash: Money.Jpy(1_000_000m), marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(1_000m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteBuyNow_NoMatchingOrder_ReturnsZeroExecution()
    {
        // Arrange: 売り板が空
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);

        // Act
        var result = exchange.ExecuteBuyNow(ticker.Id, quantity: 5,
            availableCash: Money.Jpy(1_000_000m), marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
        Assert.Equal(0m, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteBuyNow_MatchingOrder_RecordsTrade()
    {
        // Arrange
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddSellOrder(exchange, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        exchange.ExecuteBuyNow(ticker.Id, quantity: 5,
            availableCash: Money.Jpy(1_000_000m), marketPrice: ticker.CurrentPrice);

        // Assert: Trades に 1 件記録されている
        Assert.Single(exchange.Trades);
        Assert.Equal(1_000m, exchange.Trades[0].Price.Amount);
        Assert.Equal(500m, exchange.Trades[0].Fee.Amount);
    }

    // ================================================================
    // ExecuteSellNow
    // ================================================================

    [Fact]
    public void Exchange_ExecuteSellNow_MatchingOrder_ReturnsCorrectResult()
    {
        // Arrange: 買い板に 1,000 円 × 10 株がある状態で 5 株成行売り
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddBuyOrder(exchange, ticker.Id, price: 1_000m, quantity: 10);

        // Act
        var result = exchange.ExecuteSellNow(ticker.Id, quantity: 5,
            marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(1_000m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteSellNow_NoMatchingOrder_ReturnsZeroExecution()
    {
        // Arrange: 買い板が空
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);

        // Act
        var result = exchange.ExecuteSellNow(ticker.Id, quantity: 5,
            marketPrice: ticker.CurrentPrice);

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteBuyLimit
    // ================================================================

    [Fact]
    public void Exchange_ExecuteBuyLimit_SellPriceBelowLimit_Matches()
    {
        // Arrange: 指値 1,000 円、売り注文 900 円 → マッチする
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddSellOrder(exchange, ticker.Id, price: 900m, quantity: 5);

        // Act
        var result = exchange.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(900m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteBuyLimit_SellPriceAboveLimit_NoMatch()
    {
        // Arrange: 指値 1,000 円、売り注文 1,001 円 → マッチしない
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddSellOrder(exchange, ticker.Id, price: 1_001m, quantity: 5);

        // Act
        var result = exchange.ExecuteBuyLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(1_000m), availableCash: Money.Jpy(1_000_000m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    // ================================================================
    // ExecuteSellLimit
    // ================================================================

    [Fact]
    public void Exchange_ExecuteSellLimit_BuyPriceAboveLimit_Matches()
    {
        // Arrange: 指値 900 円、買い注文 1,000 円 → マッチする
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddBuyOrder(exchange, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        var result = exchange.ExecuteSellLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(5, result.ExecutedQuantity);
        Assert.Equal(1_000m * 5, result.TotalAmount.Amount);
    }

    [Fact]
    public void Exchange_ExecuteSellLimit_BuyPriceBelowLimit_NoMatch()
    {
        // Arrange: 指値 900 円、買い注文 899 円 → マッチしない
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 500m);
        AddBuyOrder(exchange, ticker.Id, price: 899m, quantity: 5);

        // Act
        var result = exchange.ExecuteSellLimit(ticker.Id, quantity: 5,
            limitPrice: Money.Jpy(900m));

        // Assert
        Assert.Equal(0, result.ExecutedQuantity);
    }

    // ================================================================
    // MatchCrossedOrders
    // ================================================================

    [Fact]
    public void Exchange_MatchCrossedOrders_CrossedOrders_AreFilledAndRecordedAsTrade()
    {
        // Arrange: 買い 1,000 円・売り 950 円 → クロスしているので自動約定
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddBuyOrder(exchange,  ticker.Id, price: 1_000m, quantity: 5);
        AddSellOrder(exchange, ticker.Id, price: 950m,   quantity: 5);

        // Act
        exchange.MatchCrossedOrders(ticker.Id);

        // Assert: 両注文が消え、Trade が 1 件記録される
        Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy));
        Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell));
        Assert.Single(exchange.Trades);
    }

    [Fact]
    public void Exchange_MatchCrossedOrders_NonCrossedOrders_StayInOrderBook()
    {
        // Arrange: 買い 900 円・売り 1,000 円 → クロスしていないので注文は残る
        var exchange = CreateExchange();
        var ticker   = CreateTicker(price: 1_000m);
        AddBuyOrder(exchange,  ticker.Id, price: 900m,   quantity: 5);
        AddSellOrder(exchange, ticker.Id, price: 1_000m, quantity: 5);

        // Act
        exchange.MatchCrossedOrders(ticker.Id);

        // Assert: 注文はそのまま残る
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy));
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell));
        Assert.Empty(exchange.Trades);
    }
}
```

- [ ] **Step 2: テストを実行して Red を確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --filter "FullyQualifiedName~ExchangeMatchingTests" 2>&1 | tail -20
```

Expected: コンパイルエラーまたは全テスト FAIL

- [ ] **Step 3: Exchange にマッチングメソッドと Trades を実装する**

`library/Domain/Entities/Exchange.cs` を以下に書き換える。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

/// <summary>
/// 株式市場の取引所。注文板（OrderBook）の管理と売買マッチングを担う。
/// </summary>
public sealed class Exchange
{
    private readonly List<Trade> _trades = new();

    /// <summary>約定ごとに徴収する固定手数料。</summary>
    public Money Fee { get; }

    /// <summary>売買注文を管理する注文板。</summary>
    public OrderBook OrderBook { get; }

    /// <summary>これまでの約定履歴。</summary>
    public IReadOnlyList<Trade> Trades => _trades.AsReadOnly();

    /// <summary>取引所を初期化する。</summary>
    /// <param name="fee">1 約定あたりの手数料。</param>
    public Exchange(Money fee)
    {
        Fee = fee;
        OrderBook = new OrderBook();
    }

    /// <summary>
    /// 成行買い注文を執行する。
    /// 市場価格以下の売り注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// 現金残高を超える約定は行わない。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">購入希望株数。</param>
    /// <param name="availableCash">使用可能な現金残高。</param>
    /// <param name="marketPrice">現在の市場価格（この価格以下の売り注文のみ照合対象）。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash, Money marketPrice)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalCost = Money.Jpy(0m);

        var candidates = FindSellCandidates(tickerId, price => price <= marketPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var tradeCost = order.Price.Multiply(fillQuantity);
            if (totalCost.Add(tradeCost).Amount > availableCash.Amount) break;

            totalCost = totalCost.Add(tradeCost);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, new OrderId(Guid.NewGuid()), order.Id, order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalCost);
    }

    /// <summary>
    /// 成行売り注文を執行する。
    /// 市場価格以上の買い注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">売却希望株数。</param>
    /// <param name="marketPrice">現在の市場価格（この価格以上の買い注文のみ照合対象）。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity, Money marketPrice)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalProceeds = Money.Jpy(0m);

        var candidates = FindBuyCandidates(tickerId, price => price >= marketPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var proceeds = order.Price.Multiply(fillQuantity);
            totalProceeds = totalProceeds.Add(proceeds);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, order.Id, new OrderId(Guid.NewGuid()), order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalProceeds);
    }

    /// <summary>
    /// 指値買い注文を執行する。
    /// 指値以下の売り注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// 現金残高を超える約定は行わない。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">購入希望株数。</param>
    /// <param name="limitPrice">指値価格（この価格以下の売り注文のみ照合対象）。</param>
    /// <param name="availableCash">使用可能な現金残高。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalCost = Money.Jpy(0m);

        var candidates = FindSellCandidates(tickerId, price => price <= limitPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var tradeCost = order.Price.Multiply(fillQuantity);
            if (totalCost.Add(tradeCost).Amount > availableCash.Amount) break;

            totalCost = totalCost.Add(tradeCost);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, new OrderId(Guid.NewGuid()), order.Id, order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalCost);
    }

    /// <summary>
    /// 指値売り注文を執行する。
    /// 指値以上の買い注文を価格優先・時間優先で照合し、約定可能な分を即時約定させる。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    /// <param name="quantity">売却希望株数。</param>
    /// <param name="limitPrice">指値価格（この価格以上の買い注文のみ照合対象）。</param>
    /// <returns>マッチング結果（約定株数・約定総額）。</returns>
    public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)
    {
        var remaining = quantity;
        var executedQuantity = 0;
        var totalProceeds = Money.Jpy(0m);

        var candidates = FindBuyCandidates(tickerId, price => price >= limitPrice.Amount);

        foreach (var order in candidates)
        {
            if (remaining <= 0) break;
            var fillQuantity = Math.Min(remaining, order.Quantity);
            var proceeds = order.Price.Multiply(fillQuantity);
            totalProceeds = totalProceeds.Add(proceeds);
            executedQuantity += fillQuantity;
            remaining -= fillQuantity;

            RegisterTrade(tickerId, order.Id, new OrderId(Guid.NewGuid()), order.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(order, order.Quantity - fillQuantity);
        }

        return new OrderMatchResult(quantity, executedQuantity, totalProceeds);
    }

    /// <summary>
    /// 指定銘柄のクロス注文（買い値 ≥ 売り値）を自動解消する。
    /// 価格優先・時間優先で照合し、クロスがなくなるまで繰り返す。
    /// </summary>
    /// <param name="tickerId">対象銘柄 ID。</param>
    public void MatchCrossedOrders(TickerId tickerId)
    {
        while (true)
        {
            var bestBuy = OrderBook
                .FindByTickerAndSide(tickerId, OrderSide.Buy)
                .OrderByDescending(o => o.Price.Amount)
                .ThenBy(o => o.CreatedAt)
                .FirstOrDefault();

            var bestSell = OrderBook
                .FindByTickerAndSide(tickerId, OrderSide.Sell)
                .OrderBy(o => o.Price.Amount)
                .ThenBy(o => o.CreatedAt)
                .FirstOrDefault();

            if (bestBuy is null || bestSell is null || bestBuy.Price.Amount < bestSell.Price.Amount)
                break;

            var fillQuantity = Math.Min(bestBuy.Quantity, bestSell.Quantity);
            RegisterTrade(tickerId, bestBuy.Id, bestSell.Id, bestSell.Price, fillQuantity);
            OrderBook.ReplaceWithRemaining(bestBuy, bestBuy.Quantity - fillQuantity);
            OrderBook.ReplaceWithRemaining(bestSell, bestSell.Quantity - fillQuantity);
        }
    }

    /// <summary>
    /// 指定銘柄の売り注文から照合候補を抽出する。
    /// pricePredicate を満たす注文のみ対象とし、価格昇順・時刻昇順（価格優先・時間優先）でソートして返す。
    /// </summary>
    private List<Order> FindSellCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
        => OrderBook.FindByTickerAndSide(tickerId, OrderSide.Sell)
            .Where(o => pricePredicate(o.Price.Amount))
            .OrderBy(o => o.Price.Amount)
            .ThenBy(o => o.CreatedAt)
            .ToList();

    /// <summary>
    /// 指定銘柄の買い注文から照合候補を抽出する。
    /// pricePredicate を満たす注文のみ対象とし、価格降順・時刻昇順（価格優先・時間優先）でソートして返す。
    /// </summary>
    private List<Order> FindBuyCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
        => OrderBook.FindByTickerAndSide(tickerId, OrderSide.Buy)
            .Where(o => pricePredicate(o.Price.Amount))
            .OrderByDescending(o => o.Price.Amount)
            .ThenBy(o => o.CreatedAt)
            .ToList();

    /// <summary>
    /// 約定を1件記録する。Trade オブジェクトを生成して内部リストに追加する。
    /// </summary>
    private void RegisterTrade(TickerId tickerId, OrderId buyOrderId, OrderId sellOrderId, Money price, int quantity)
        => _trades.Add(new Trade(
            new TradeId(Guid.NewGuid()),
            tickerId,
            buyOrderId,
            sellOrderId,
            price,
            quantity,
            Fee,
            DateTimeOffset.UtcNow));
}
```

- [ ] **Step 4: テストを実行して Green を確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --filter "FullyQualifiedName~ExchangeMatchingTests" 2>&1 | tail -20
```

Expected: 全テスト PASS

- [ ] **Step 5: 全テストを実行して既存テストが壊れていないことを確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --no-build
```

Expected: 全テスト PASS（Exchange のメソッドはまだ使われていないので既存テストに影響なし）

- [ ] **Step 6: コミットする**

```bash
git add library/Domain/Entities/Exchange.cs \
        backend/FinLearnApp.Tests/Domain/ExchangeMatchingTests.cs
git commit -m "feat: Exchangeにマッチングメソッドとトレード履歴を追加"
```

---

## Task 3: TurnDomainService を新規作成

**Files:**
- Create: `library/Domain/Services/TurnDomainService.cs`
- Create: `backend/FinLearnApp.Tests/Domain/TurnDomainServiceTests.cs`

**背景:** 価格変動・システム注文生成・板寄せはシミュレーションのドメインルール。現在 InMemoryStore のプライベートメソッドに埋まっている。Domain サービスとして独立させ、テスト可能にする。

- [ ] **Step 1: テストファイルを作成する（Red）**

`backend/FinLearnApp.Tests/Domain/TurnDomainServiceTests.cs` を作成する。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.Services;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

/// <summary>
/// TurnDomainService のユニットテスト。
/// Exchange と Ticker を直接組み立て、ターン処理の各ステップを検証する。
/// </summary>
public class TurnDomainServiceTests
{
    private static readonly CompanyId TestCompanyId = new(Guid.Parse("cccccccc-0000-0000-0000-000000000001"));

    /// <summary>テスト用 Ticker を指定株数生成するヘルパー。</summary>
    private static List<Ticker> CreateTickers(int count, decimal price = 1_000m)
        => Enumerable.Range(0, count)
            .Select(i => new Ticker(
                new TickerId(Guid.NewGuid()),
                TestCompanyId,
                $"T{i}",
                1,
                Money.Jpy(price)))
            .ToList();

    /// <summary>手数料 500 円の Exchange を生成するヘルパー。</summary>
    private static Exchange CreateExchange() => new(Money.Jpy(500m));

    // ================================================================
    // ApplyPriceFluctuation
    // ================================================================

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_ChangesAllTickerPrices()
    {
        // Arrange: 3 銘柄、初期価格 1,000 円
        var tickers = CreateTickers(3, price: 1_000m);
        var originalPrices = tickers.Select(t => t.CurrentPrice.Amount).ToList();

        // Act: seed 固定の Random を使って価格を変動させる（再現性のある結果）
        TurnDomainService.ApplyPriceFluctuation(tickers, new Random(42), turn: 1);

        // Assert: 全銘柄の価格が 97%〜103% の範囲内に変動している
        for (var i = 0; i < tickers.Count; i++)
        {
            Assert.InRange(tickers[i].CurrentPrice.Amount,
                originalPrices[i] * 0.97m,
                originalPrices[i] * 1.03m);
        }
    }

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_PriceNeverFallsBelowOne()
    {
        // Arrange: 最低価格 1 円（下限テスト）
        var tickers = CreateTickers(1, price: 1m);

        // Act: 10 回変動させても 1 円未満にならないことを確認
        for (var i = 1; i <= 10; i++)
        {
            TurnDomainService.ApplyPriceFluctuation(tickers, Random.Shared, turn: i);
        }

        // Assert
        Assert.True(tickers[0].CurrentPrice.Amount >= 1m);
    }

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_UpdatesPriceHistory()
    {
        // Arrange
        var tickers = CreateTickers(1, price: 1_000m);
        var initialHistoryCount = tickers[0].PriceHistory.Count;

        // Act
        TurnDomainService.ApplyPriceFluctuation(tickers, new Random(42), turn: 1);

        // Assert: 価格履歴に 1 件追加されている
        Assert.Equal(initialHistoryCount + 1, tickers[0].PriceHistory.Count);
    }

    [Fact]
    public void TurnDomainService_ApplyPriceFluctuation_EmptyTickers_DoesNotThrow()
    {
        // Arrange: 銘柄なし
        var tickers = new List<Ticker>();

        // Act & Assert: 例外が発生しない
        var ex = Record.Exception(
            () => TurnDomainService.ApplyPriceFluctuation(tickers, new Random(42), turn: 1));
        Assert.Null(ex);
    }

    // ================================================================
    // GenerateSystemOrders
    // ================================================================

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_OneTicker_GeneratesOneBuyAndOneSell()
    {
        // Arrange: 1 銘柄のみ（必ずその銘柄が選ばれる）
        var tickers  = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: 買い 1 件・売り 1 件
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy));
        Assert.Single(exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell));
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_FourTickers_GeneratesAtMostSixOrders()
    {
        // Arrange: 4 銘柄（MaxTargetTickersPerTurn = 3 なので最大 3 銘柄 × 2 件 = 6 件）
        var tickers  = CreateTickers(4, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: 合計注文数が 2〜6 件の範囲内
        var total = tickers.Sum(t =>
            exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Buy).Count() +
            exchange.OrderBook.FindByTickerAndSide(t.Id, OrderSide.Sell).Count());
        Assert.InRange(total, 2, 6);
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_SystemOrderQuantityIsTen()
    {
        // Arrange: 1 銘柄
        var tickers  = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: 各注文の数量は 10 株
        var buyOrders  = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy);
        var sellOrders = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell);
        Assert.All(buyOrders,  o => Assert.Equal(10, o.Quantity));
        Assert.All(sellOrders, o => Assert.Equal(10, o.Quantity));
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_OrderOriginIsSystem()
    {
        // Arrange
        var tickers  = CreateTickers(1, price: 1_000m);
        var exchange = CreateExchange();

        // Act
        TurnDomainService.GenerateSystemOrders(exchange, tickers, new Random(42));

        // Assert: Origin が System
        var buyOrders  = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Buy);
        var sellOrders = exchange.OrderBook.FindByTickerAndSide(tickers[0].Id, OrderSide.Sell);
        Assert.All(buyOrders,  o => Assert.Equal(OrderOrigin.System, o.Origin));
        Assert.All(sellOrders, o => Assert.Equal(OrderOrigin.System, o.Origin));
    }

    [Fact]
    public void TurnDomainService_GenerateSystemOrders_EmptyTickers_DoesNotThrow()
    {
        // Arrange
        var exchange = CreateExchange();

        // Act & Assert
        var ex = Record.Exception(
            () => TurnDomainService.GenerateSystemOrders(exchange, new List<Ticker>(), new Random(42)));
        Assert.Null(ex);
    }

    // ================================================================
    // MatchCrossedOrdersForAllTickers
    // ================================================================

    [Fact]
    public void TurnDomainService_MatchCrossedOrdersForAllTickers_CrossedOrders_AreFilledForAllTickers()
    {
        // Arrange: 2 銘柄それぞれにクロス注文を入れる
        var tickers  = CreateTickers(2, price: 1_000m);
        var exchange = CreateExchange();

        foreach (var ticker in tickers)
        {
            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
                Money.Jpy(1_000m), 5, OrderOrigin.System, DateTimeOffset.UtcNow));
            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
                Money.Jpy(950m), 5, OrderOrigin.System, DateTimeOffset.UtcNow));
        }

        // Act
        TurnDomainService.MatchCrossedOrdersForAllTickers(exchange, tickers);

        // Assert: 両銘柄の注文板が空になっている
        foreach (var ticker in tickers)
        {
            Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Buy));
            Assert.Empty(exchange.OrderBook.FindByTickerAndSide(ticker.Id, OrderSide.Sell));
        }
    }
}
```

- [ ] **Step 2: テストを実行して Red を確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --filter "FullyQualifiedName~TurnDomainServiceTests" 2>&1 | tail -20
```

Expected: コンパイルエラー（`TurnDomainService` が存在しない）

- [ ] **Step 3: TurnDomainService を実装する**

`library/Domain/Services/TurnDomainService.cs` を新規作成する（`Services/` フォルダが存在しない場合は作成）。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Enums;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Services;

/// <summary>
/// ターン進行に関わるドメインロジックを集約するドメインサービス。
/// 価格変動・システム注文生成・クロス注文解消の 3 ステップを提供する。
/// Random はメソッド引数で受け取ることで、Domain 層が生成方法に依存しない設計にする。
/// </summary>
public static class TurnDomainService
{
    /// <summary>1 ターンに注文を生成する最大銘柄数。</summary>
    private const int MaxTargetTickersPerTurn = 3;

    /// <summary>1 銘柄あたりのシステム注文株数。</summary>
    private const int SystemOrderQuantity = 10;

    /// <summary>システム買い注文の価格（現在価格 × この倍率）。</summary>
    private const decimal SystemBuyPriceRate = 0.95m;

    /// <summary>システム売り注文の価格（現在価格 × この倍率）。</summary>
    private const decimal SystemSellPriceRate = 1.00m;

    /// <summary>価格変動率の下限（現在価格の 97%）。</summary>
    private const decimal MinPriceFluctuationRate = 0.97m;

    /// <summary>価格変動率の上限（現在価格の 103%）。</summary>
    private const decimal MaxPriceFluctuationRate = 1.03m;

    /// <summary>
    /// 全銘柄の価格をランダムに変動させる。
    /// 変動率は MinPriceFluctuationRate 〜 MaxPriceFluctuationRate の一様分布。
    /// 変動後の価格が 1 円未満になる場合は 1 円にクランプする。
    /// </summary>
    /// <param name="tickers">変動対象の銘柄リスト。</param>
    /// <param name="random">乱数生成器（呼び出し元が管理する）。</param>
    /// <param name="turn">現在のターン番号（価格履歴に記録される）。</param>
    public static void ApplyPriceFluctuation(IReadOnlyList<Ticker> tickers, Random random, int turn)
    {
        foreach (var ticker in tickers)
        {
            var rate = NextDecimal(random, MinPriceFluctuationRate, MaxPriceFluctuationRate);
            var newAmount = decimal.Round(ticker.CurrentPrice.Amount * rate, 2, MidpointRounding.AwayFromZero);
            if (newAmount < 1m) newAmount = 1m;
            ticker.UpdatePrice(Money.Jpy(newAmount), turn);
        }
    }

    /// <summary>
    /// ランダムに選んだ最大 MaxTargetTickersPerTurn 銘柄に対して、
    /// システムが自動発注する買い注文・売り注文を Exchange の注文板に追加する。
    /// 買い注文は現在価格の 95%、売り注文は現在価格で発注する。
    /// </summary>
    /// <param name="exchange">注文を追加する取引所。</param>
    /// <param name="tickers">発注対象の銘柄候補リスト。</param>
    /// <param name="random">銘柄ランダム選択に使用する乱数生成器。</param>
    public static void GenerateSystemOrders(Exchange exchange, IReadOnlyList<Ticker> tickers, Random random)
    {
        if (tickers.Count == 0) return;

        var targetTickers = tickers
            .OrderBy(_ => random.Next())
            .Take(MaxTargetTickersPerTurn)
            .ToList();

        foreach (var ticker in targetTickers)
        {
            var createdAt = DateTimeOffset.UtcNow;
            var buyPrice  = Money.Jpy(decimal.Round(
                ticker.CurrentPrice.Amount * SystemBuyPriceRate, 2, MidpointRounding.AwayFromZero));
            var sellPrice = Money.Jpy(decimal.Round(
                ticker.CurrentPrice.Amount * SystemSellPriceRate, 2, MidpointRounding.AwayFromZero));

            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Buy,
                buyPrice, SystemOrderQuantity, OrderOrigin.System, createdAt));
            exchange.OrderBook.Add(new Order(
                new OrderId(Guid.NewGuid()), ticker.Id, OrderSide.Sell,
                sellPrice, SystemOrderQuantity, OrderOrigin.System, createdAt));
        }
    }

    /// <summary>
    /// 全銘柄のクロス注文（買い値 ≥ 売り値の組み合わせ）を自動解消する。
    /// 各銘柄について Exchange.MatchCrossedOrders を呼び出す。
    /// </summary>
    /// <param name="exchange">対象の取引所。</param>
    /// <param name="tickers">解消対象の全銘柄リスト。</param>
    public static void MatchCrossedOrdersForAllTickers(Exchange exchange, IReadOnlyList<Ticker> tickers)
    {
        foreach (var ticker in tickers)
        {
            exchange.MatchCrossedOrders(ticker.Id);
        }
    }

    /// <summary>
    /// minInclusive 〜 maxInclusive の範囲で一様分布の decimal 乱数を生成する。
    /// </summary>
    private static decimal NextDecimal(Random random, decimal minInclusive, decimal maxInclusive)
    {
        var sample = (decimal)random.NextDouble();
        return minInclusive + ((maxInclusive - minInclusive) * sample);
    }
}
```

- [ ] **Step 4: テストを実行して Green を確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --filter "FullyQualifiedName~TurnDomainServiceTests" 2>&1 | tail -20
```

Expected: 全テスト PASS

- [ ] **Step 5: 全テストが引き続き PASS していることを確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --no-build
```

Expected: 全テスト PASS

- [ ] **Step 6: コミットする**

```bash
git add library/Domain/Services/TurnDomainService.cs \
        backend/FinLearnApp.Tests/Domain/TurnDomainServiceTests.cs
git commit -m "feat: TurnDomainServiceを新規作成（価格変動・注文生成・板寄せ）"
```

---

## Task 4: InMemoryStore をデリゲーターに変更

**Files:**
- Modify: `backend/FinLearnApp.Api/Data/InMemoryStore.cs`

**背景:** Task 2・3 でビジネスロジックを Domain 層に移したので、InMemoryStore はそれらを呼ぶだけのデリゲーターになる。ビジネスロジックのコードをすべて削除する。

- [ ] **Step 1: InMemoryStore.cs を書き換える**

`backend/FinLearnApp.Api/Data/InMemoryStore.cs` を以下に書き換える。

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Services;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Data;

/// <summary>
/// アプリケーション全状態をメモリ上で管理するリポジトリ。
/// ビジネスロジックは持たず、データの読み書きと Domain 層への委譲のみを担う。
/// </summary>
public sealed class InMemoryStore
{
    private readonly Dictionary<CompanyId, Company> _companiesById;
    private readonly Dictionary<TickerId, Ticker> _tickersById;
    private readonly Dictionary<InvestorId, int> _turnByInvestor;
    private readonly Random _random;

    public IReadOnlyList<Company> Companies { get; }
    public IReadOnlyList<Ticker> Tickers { get; }
    public IReadOnlyList<Investor> Investors { get; }
    public IReadOnlyList<Portfolio> Portfolios { get; }
    public Exchange Exchange { get; }

    /// <summary>約定履歴。Exchange.Trades に委譲する。</summary>
    public IReadOnlyList<Trade> Trades => Exchange.Trades;

    public InMemoryStore(
        IReadOnlyList<Company> companies,
        IReadOnlyList<Ticker> tickers,
        IReadOnlyList<Investor> investors,
        IReadOnlyList<Portfolio> portfolios,
        IReadOnlyDictionary<InvestorId, int>? turnByInvestor = null,
        Random? random = null)
    {
        Companies = companies;
        Tickers = tickers;
        Investors = investors;
        Portfolios = portfolios;
        Exchange = new Exchange(Money.Jpy(500m));

        _companiesById = companies.ToDictionary(c => c.Id, c => c);
        _tickersById = tickers.ToDictionary(t => t.Id, t => t);
        _turnByInvestor = turnByInvestor is null
            ? investors.ToDictionary(investor => investor.Id, _ => 0)
            : new Dictionary<InvestorId, int>(turnByInvestor);
        _random = random ?? Random.Shared;
    }

    /// <summary>CompanyId から Company を取得する。存在しない場合は KeyNotFoundException を投げる。</summary>
    public Company GetCompany(CompanyId id) => _companiesById[id];

    /// <summary>TickerId から Ticker を検索する。存在しない場合は null を返す。</summary>
    public Ticker? FindTicker(TickerId id)
        => _tickersById.TryGetValue(id, out var ticker) ? ticker : null;

    /// <summary>投資家 ID からポートフォリオを検索する。存在しない場合は null を返す。</summary>
    public Portfolio? FindPortfolioByInvestor(InvestorId investorId)
        => Portfolios.FirstOrDefault(p => p.InvestorId == investorId);

    /// <summary>投資家の現在ターン番号を返す。未登録の場合は 0 を返す。</summary>
    public int GetCurrentTurn(InvestorId investorId)
        => _turnByInvestor.TryGetValue(investorId, out var turn) ? turn : 0;

    /// <summary>
    /// ターンを 1 進め、価格変動・システム注文生成・クロス注文解消を実行する。
    /// 各処理は TurnDomainService に委譲する。
    /// </summary>
    /// <returns>進行後のターン番号。</returns>
    public int AdvanceTurn(InvestorId investorId)
    {
        var nextTurn = GetCurrentTurn(investorId) + 1;
        _turnByInvestor[investorId] = nextTurn;

        TurnDomainService.ApplyPriceFluctuation(Tickers, _random, nextTurn);
        TurnDomainService.GenerateSystemOrders(Exchange, Tickers, _random);
        TurnDomainService.MatchCrossedOrdersForAllTickers(Exchange, Tickers);

        return nextTurn;
    }

    /// <summary>成行買いを執行する。Exchange.ExecuteBuyNow に委譲する。</summary>
    public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash)
        => Exchange.ExecuteBuyNow(tickerId, quantity, availableCash, FindTicker(tickerId)!.CurrentPrice);

    /// <summary>成行売りを執行する。Exchange.ExecuteSellNow に委譲する。</summary>
    public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity)
        => Exchange.ExecuteSellNow(tickerId, quantity, FindTicker(tickerId)!.CurrentPrice);

    /// <summary>指値買いを執行する。Exchange.ExecuteBuyLimit に委譲する。</summary>
    public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
        => Exchange.ExecuteBuyLimit(tickerId, quantity, limitPrice, availableCash);

    /// <summary>指値売りを執行する。Exchange.ExecuteSellLimit に委譲する。</summary>
    public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)
        => Exchange.ExecuteSellLimit(tickerId, quantity, limitPrice);
}
```

- [ ] **Step 2: ビルドしてコンパイルエラーがないことを確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet build 2>&1 | tail -10
```

Expected: `Build succeeded.`

- [ ] **Step 3: 全テストを実行して全て PASS することを確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/backend
dotnet test --no-build
```

Expected: 全テスト PASS（既存の OrderMatchingTests・TurnSystemTests は InMemoryStore 経由のまま動く）

- [ ] **Step 4: コミットする**

```bash
git add backend/FinLearnApp.Api/Data/InMemoryStore.cs
git commit -m "refactor: InMemoryStoreをデリゲーターに変更（ビジネスロジックをDomain層に移動）"
```
