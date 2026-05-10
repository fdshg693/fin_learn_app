# InMemoryStore 責務分離設計

**日付**: 2026-05-10  
**対象ブランチ**: takuma

---

## 背景・目的

現在 `InMemoryStore`（`backend/FinLearnApp.Api/Data/`）に以下のビジネスロジックが混在している。

- `ExecuteBuyNow` / `ExecuteSellNow` / `ExecuteBuyLimit` / `ExecuteSellLimit`：注文マッチングロジック
- `ApplyPriceFluctuation`：価格変動シミュレーション
- `GenerateSystemOrdersForTurn`：システム注文生成
- `MatchCrossedOrders` / `MatchCrossedOrdersForAllTickers`：板寄せ

`InMemoryStore` はデータの読み書き（CRUD）のみを担うべきであり、ビジネスロジックは Domain 層または Application 層に置くのが Clean Architecture の原則。

---

## 設計方針（アプローチA）

- **マッチングロジック** → `Exchange` ドメインエンティティのメソッドへ（「取引所がどう注文を捌くか」はドメインの振る舞い）
- **ターン処理ロジック** → Domain 層の `TurnDomainService` クラスへ（価格変動・注文生成・板寄せはシミュレーションのドメインルールそのもの。`Random` はメソッド引数で受け取ることで Domain 層が生成に依存しない設計にする）
- **`InMemoryStore`** → 薄いデリゲーター：Execute 系は `Exchange` に転送、`AdvanceTurn` は `TurnDomainService` を呼ぶだけ
- **`IActionExecutionStore` インターフェース** → 変更なし（Handler 側の変更ゼロ）

---

## 変更ファイル一覧

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `library/Domain/Entities/Exchange.cs` | 修正 | マッチングメソッド + `Trades` プロパティを追加 |
| `library/Domain/Services/TurnDomainService.cs` | 新規作成 | ターン処理ロジック（価格変動・注文生成・板寄せ） |
| `backend/FinLearnApp.Api/Data/InMemoryStore.cs` | 修正 | Execute系・AdvanceTurn を薄いデリゲーターに変更 |
| `backend/FinLearnApp.Api/Controllers/MarketController.cs` | 修正 | `_store.Trades` → `_store.Exchange.Trades` |
| テスト | 追加 | `TurnDomainService` と `Exchange` のマッチングを単体テスト |

---

## 変更しないもの

- `library/Application/Actions/IActionExecutionStore.cs`
- 全 Command / Handler ファイル（`BuyNowCommandHandler` 等）
- `library/Domain/Entities/OrderBook.cs`
- `backend/FinLearnApp.Api/Data/SeedData.cs`

---

## 詳細設計

### 1. `library/Domain/Entities/Exchange.cs`

マッチング実行メソッドと約定履歴を `Exchange` エンティティに追加する。
`InMemoryStore._trades` はここに移動する。

追加するプロパティ・メソッド：

```csharp
// 約定履歴（InMemoryStore._trades から移動）
public IReadOnlyList<Trade> Trades => _trades.AsReadOnly();
private readonly List<Trade> _trades = new();

// 注文マッチング実行（InMemoryStore の Execute* メソッドから移動）
public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash, Money marketPrice)
public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity, Money marketPrice)
public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)

// 板寄せ（InMemoryStore.MatchCrossedOrders から移動）
public void MatchCrossedOrders(TickerId tickerId)

// 内部ヘルパー（InMemoryStore のプライベートメソッドから移動）
private List<Order> FindSellCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
private List<Order> FindBuyCandidates(TickerId tickerId, Func<decimal, bool> pricePredicate)
private void RegisterTrade(TickerId tickerId, OrderId buyOrderId, OrderId sellOrderId, Money price, int quantity)
```

`ExecuteBuyNow` の実装例（他も同様）：

```csharp
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
```

---

### 2. `library/Domain/Services/TurnDomainService.cs`

ターン進行に必要な3ステップをドメインサービスの静的メソッドとして実装する。
`Random` はメソッド引数で受け取ることで、Domain 層が乱数生成の仕組みに依存しない設計にする。

```csharp
namespace FinLearnApp.Domain.Services;

public static class TurnDomainService
{
    private const int MaxTargetTickersPerTurn = 3;
    private const int SystemOrderQuantity = 10;
    private const decimal SystemBuyPriceRate = 0.95m;
    private const decimal SystemSellPriceRate = 1.00m;
    private const decimal MinPriceFluctuationRate = 0.97m;
    private const decimal MaxPriceFluctuationRate = 1.03m;

    public static void ApplyPriceFluctuation(IReadOnlyList<Ticker> tickers, Random random, int turn)
    // 各ティッカーの価格をランダムに変動させる（InMemoryStore.ApplyPriceFluctuation から移動）

    public static void GenerateSystemOrders(Exchange exchange, IReadOnlyList<Ticker> tickers, Random random)
    // ランダムに選んだ銘柄に買い注文・売り注文を生成（InMemoryStore.GenerateSystemOrdersForTurn から移動）

    public static void MatchCrossedOrdersForAllTickers(Exchange exchange, IReadOnlyList<Ticker> tickers)
    // 全銘柄のクロス注文を解消（InMemoryStore.MatchCrossedOrdersForAllTickers から移動）
}
```

定数（`MaxTargetTickersPerTurn` 等）は `InMemoryStore` から `TurnDomainService` に移動する。

---

### 3. `backend/FinLearnApp.Api/Data/InMemoryStore.cs`

削除するメソッド（ロジックを `Exchange` / `TurnService` に移動済み）：
- `ApplyPriceFluctuation`
- `GenerateSystemOrdersForTurn`
- `MatchCrossedOrders`
- `MatchCrossedOrdersForAllTickers`
- `FindSellCandidates`
- `FindBuyCandidates`
- `RegisterTrade`
- `NextDecimal`
- `_trades` フィールドおよび `Trades` プロパティ

定数 6 本（`MaxTargetTickersPerTurn` 等）も削除（`TurnDomainService` に移動）。

変更後の `AdvanceTurn`：

```csharp
public int AdvanceTurn(InvestorId investorId)
{
    var nextTurn = GetCurrentTurn(investorId) + 1;
    _turnByInvestor[investorId] = nextTurn;

    TurnDomainService.ApplyPriceFluctuation(Tickers, _random, nextTurn);
    TurnDomainService.GenerateSystemOrders(Exchange, Tickers, _random);
    TurnDomainService.MatchCrossedOrdersForAllTickers(Exchange, Tickers);

    return nextTurn;
}
```

変更後の `ExecuteBuyNow`（他 Execute* も同様）：

```csharp
public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash)
{
    return Exchange.ExecuteBuyNow(tickerId, quantity, availableCash, FindTicker(tickerId)!.CurrentPrice);
}
```

---

### 4. `backend/FinLearnApp.Api/Controllers/MarketController.cs`

```csharp
// 変更前
var trades = _store.Trades

// 変更後
var trades = _store.Exchange.Trades
```

---

## テスト方針

### `Exchange` のマッチングテスト

`library/Application/` 側から参照できる Domain エンティティなので、Application テストプロジェクト（または新規 Domain テストプロジェクト）に追加する。

テストケース例：
- `ExecuteBuyNow`: 売り板に条件を満たす注文があれば約定する
- `ExecuteBuyNow`: 残高不足なら約定しない
- `ExecuteSellNow`: 買い板に条件を満たす注文があれば約定する
- `ExecuteBuyLimit`: 指値以下の売り注文があれば約定する
- `MatchCrossedOrders`: 買い値 ≥ 売り値のクロスが解消される

### `TurnDomainService` のテスト

- `ApplyPriceFluctuation`: 全ティッカーの価格が変動する（固定 Random で検証）
- `GenerateSystemOrders`: 最大 `MaxTargetTickersPerTurn` 銘柄分の注文が生成される
- `MatchCrossedOrdersForAllTickers`: 全銘柄でクロス解消が呼ばれる

---

## 依存関係の方向（変更後）

```
Api (InMemoryStore)
  → Domain (TurnDomainService, Exchange, Ticker, OrderBook)
```

`IActionExecutionStore` を通じた依存は変わらず：

```
Application (Handler) → Application (IActionExecutionStore) ← Api (InMemoryStore)
```
