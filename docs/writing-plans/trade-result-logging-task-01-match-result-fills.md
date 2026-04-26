# Task 1: MatchResult に Fills を追加し Market から渡す

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Modify: `src/FinLearn.Core/Results/MatchResult.cs`
- Modify: `src/FinLearn.Core/Services/Market.cs`
- Test: `tests/FinLearn.Tests/MarketTests.cs`

- [ ] **Step 1: 失敗するテストを書く**

`tests/FinLearn.Tests/MarketTests.cs` の末尾（クラス内）に追加:

```csharp
[Fact]
public void Execute_は約定明細をMatchResultのFillsに含める()
{
    var instrument = TestData.Instrument1;
    var exchange = TestData.CreateExchange(price: 100, fee: 0);
    var book = new OrderBook()
        .Add(new Order(1, "computer", instrument, OrderSide.Sell, 1, 100, createdAtTurn: 1));
    var incoming = new Order(2, "player", instrument, OrderSide.Buy, 1, 100, createdAtTurn: 1);

    var result = new Market().Execute(book, incoming, exchange);

    // 双方の注文（incoming + resting）が Fills に含まれる
    Assert.Equal(2, result.Fills.Count);
    Assert.Contains(result.Fills, f => f.OrderId == 1 && f.FilledQuantity == 1);
    Assert.Contains(result.Fills, f => f.OrderId == 2 && f.FilledQuantity == 1);
}
```

- [ ] **Step 2: テスト実行で失敗を確認**

Run: `dotnet test tests/FinLearn.Tests/FinLearn.Tests.csproj --filter "FullyQualifiedName~MarketTests.Execute_は約定明細をMatchResultのFillsに含める"`
Expected: FAIL（コンパイルエラー: `MatchResult.Fills` が存在しない）

- [ ] **Step 3: MatchResult に Fills を追加**

`src/FinLearn.Core/Results/MatchResult.cs` を以下に置き換え:

```csharp
namespace FinLearn.Core;

/// <summary>
/// マッチング結果（取引結果 + 更新後の注文帳 + 全約定明細、Game内部で使用）
/// </summary>
public sealed record MatchResult(
    TradeResult Trade,
    OrderBook UpdatedBook,
    IReadOnlyList<OrderFill> Fills);
```

- [ ] **Step 4: Market.Execute から fillResult.Fills を渡す**

`src/FinLearn.Core/Services/Market.cs` の return 文を変更:

```csharp
return new MatchResult(trade, fillResult.UpdatedBook, fillResult.Fills);
```

- [ ] **Step 5: テスト実行でパスを確認**

Run: `dotnet test tests/FinLearn.Tests/FinLearn.Tests.csproj`
Expected: PASS（`MarketTests` の新規テストを含む全テストが通る。`TurnProcessor` 経路は `MatchResult` を分解して使う場面が無いため既存テストは破壊されない想定）

- [ ] **Step 6: コミット**

```bash
git add src/FinLearn.Core/Results/MatchResult.cs src/FinLearn.Core/Services/Market.cs tests/FinLearn.Tests/MarketTests.cs
git commit -m "feat(core): expose all order fills on MatchResult"
```
