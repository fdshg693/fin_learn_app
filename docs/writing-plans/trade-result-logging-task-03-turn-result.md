# Task 3: TurnResult レコードを追加

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Create: `src/FinLearn.Core/Results/TurnResult.cs`

レコード型の追加だけなので TDD は省略（次タスクの `TurnProcessor` 変更で同時に検証される）。

- [ ] **Step 1: TurnResult を作成**

`src/FinLearn.Core/Results/TurnResult.cs` を新規作成:

```csharp
namespace FinLearn.Core;

/// <summary>
/// ターン処理の結果（ロギング用に提出注文・約定明細を含む）
/// </summary>
/// <param name="Game">処理後のゲーム状態</param>
/// <param name="Trade">プレイヤー注文の取引結果（Wait・失敗時は null）</param>
/// <param name="Warning">エラーメッセージ（成功時は null）</param>
/// <param name="ProcessedTurn">この処理が対象としたターン番号（game.Turn と等価）</param>
/// <param name="SubmittedOrders">そのターンに板へ届いた全注文（コンピューター + プレイヤー）</param>
/// <param name="Fills">そのターンに発生した全約定明細</param>
public sealed record TurnResult(
    Game Game,
    TradeResult? Trade,
    string? Warning,
    int ProcessedTurn,
    IReadOnlyList<Order> SubmittedOrders,
    IReadOnlyList<OrderFill> Fills);
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build src/FinLearn.Core/FinLearn.Core.csproj`
Expected: ビルド成功（warning 0 件）

- [ ] **Step 3: コミット**

```bash
git add src/FinLearn.Core/Results/TurnResult.cs
git commit -m "feat(core): add TurnResult record"
```
