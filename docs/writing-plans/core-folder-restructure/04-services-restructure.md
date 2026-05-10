### Task 4: Services 内の整理（TurnProcessor 移動 + OrderHandlers サブフォルダ作成）

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Move: `src/FinLearn.Core/TurnProcessor.cs` → `src/FinLearn.Core/Services/TurnProcessor.cs`
- Create: `src/FinLearn.Core/Services/OrderHandlers/` （新規サブフォルダ）
- Move: `src/FinLearn.Core/Services/LimitOrderHandler.cs` → `src/FinLearn.Core/Services/OrderHandlers/LimitOrderHandler.cs`
- Move: `src/FinLearn.Core/Services/MarketOrderHandler.cs` → `src/FinLearn.Core/Services/OrderHandlers/MarketOrderHandler.cs`

**変更しないファイル（参考、`Services/` に残るドメインサービス）:**
- `Services/SettlementProcessor.cs`
- `Services/Market.cs`
- `Services/ComputerTrader.cs`
- `Services/SimpleExchange.cs`
- `Services/SimpleExchangeFactory.cs`
- `Services/RandomPriceFluctuator.cs`

**前提:** namespace 変更なし。`Services/I*.cs`（interface 群）はこの Task では触らず Task 5 で別途移動する。

- [ ] **Step 1: OrderHandlers サブフォルダを作成する**

```powershell
New-Item -ItemType Directory -Path src/FinLearn.Core/Services/OrderHandlers
```

期待: `src/FinLearn.Core/Services/OrderHandlers/` が存在する。

- [ ] **Step 2: TurnProcessor.cs を Services/ 配下へ移動する**

```powershell
git mv src/FinLearn.Core/TurnProcessor.cs src/FinLearn.Core/Services/TurnProcessor.cs
```

期待: silent 成功。

- [ ] **Step 3: 2 つの OrderHandler を OrderHandlers/ へ移動する**

```powershell
git mv src/FinLearn.Core/Services/LimitOrderHandler.cs  src/FinLearn.Core/Services/OrderHandlers/LimitOrderHandler.cs
git mv src/FinLearn.Core/Services/MarketOrderHandler.cs src/FinLearn.Core/Services/OrderHandlers/MarketOrderHandler.cs
```

期待: silent 成功。

- [ ] **Step 4: rename 認識を確認する**

```powershell
git status
```

期待: 3 件の `renamed:` 行（`R100`）。`Services/` 直下に残るファイル（`SettlementProcessor.cs`, `Market.cs`, `ComputerTrader.cs`, `SimpleExchange.cs`, `SimpleExchangeFactory.cs`, `RandomPriceFluctuator.cs`, および `I*.cs` 6 件）は status に出ないこと。

- [ ] **Step 5: ビルドが通ることを確認する**

```powershell
dotnet build fin_learn_app.sln
```

期待: `Build succeeded`、エラー 0 件。

- [ ] **Step 6: 全テストが通ることを確認する**

```powershell
dotnet test fin_learn_app.sln
```

期待: 全テスト pass。

- [ ] **Step 7: コミットする**

```powershell
git add -A
git commit -m "refactor(core): relocate TurnProcessor into Services/ and nest OrderHandlers/"
```

期待: 1 commit、`git show --stat HEAD` で 3 件の rename。
