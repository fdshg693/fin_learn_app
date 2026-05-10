### Task 5: Abstractions フォルダ作成と interface 群の移動

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Create: `src/FinLearn.Core/Abstractions/` （新規フォルダ）
- Move: `src/FinLearn.Core/Services/IExchange.cs` → `src/FinLearn.Core/Abstractions/IExchange.cs`
- Move: `src/FinLearn.Core/Services/IExchangeFactory.cs` → `src/FinLearn.Core/Abstractions/IExchangeFactory.cs`
- Move: `src/FinLearn.Core/Services/IMarket.cs` → `src/FinLearn.Core/Abstractions/IMarket.cs`
- Move: `src/FinLearn.Core/Services/IOrderPlacer.cs` → `src/FinLearn.Core/Abstractions/IOrderPlacer.cs`
- Move: `src/FinLearn.Core/Services/IPlayerOrderHandler.cs` → `src/FinLearn.Core/Abstractions/IPlayerOrderHandler.cs`
- Move: `src/FinLearn.Core/Services/IPriceFluctuator.cs` → `src/FinLearn.Core/Abstractions/IPriceFluctuator.cs`

**前提:** namespace 変更なし。これらは全て `FinLearn.Core` namespace 直下の interface のままでよい。

- [ ] **Step 1: Abstractions フォルダを作成する**

```powershell
New-Item -ItemType Directory -Path src/FinLearn.Core/Abstractions
```

期待: `src/FinLearn.Core/Abstractions/` が存在する。

- [ ] **Step 2: 6 つの interface を `git mv` する**

```powershell
git mv src/FinLearn.Core/Services/IExchange.cs           src/FinLearn.Core/Abstractions/IExchange.cs
git mv src/FinLearn.Core/Services/IExchangeFactory.cs    src/FinLearn.Core/Abstractions/IExchangeFactory.cs
git mv src/FinLearn.Core/Services/IMarket.cs             src/FinLearn.Core/Abstractions/IMarket.cs
git mv src/FinLearn.Core/Services/IOrderPlacer.cs        src/FinLearn.Core/Abstractions/IOrderPlacer.cs
git mv src/FinLearn.Core/Services/IPlayerOrderHandler.cs src/FinLearn.Core/Abstractions/IPlayerOrderHandler.cs
git mv src/FinLearn.Core/Services/IPriceFluctuator.cs    src/FinLearn.Core/Abstractions/IPriceFluctuator.cs
```

期待: 全コマンド silent 成功。

- [ ] **Step 3: rename 認識を確認する**

```powershell
git status
```

期待: 6 件の `renamed:` 行（`R100`）。

- [ ] **Step 4: ビルドが通ることを確認する**

```powershell
dotnet build fin_learn_app.sln
```

期待: `Build succeeded`、エラー 0 件。

- [ ] **Step 5: 全テストが通ることを確認する**

```powershell
dotnet test fin_learn_app.sln
```

期待: 全テスト pass。

- [ ] **Step 6: コミットする**

```powershell
git add -A
git commit -m "refactor(core): move interfaces (IExchange/IExchangeFactory/IMarket/IOrderPlacer/IPlayerOrderHandler/IPriceFluctuator) into Abstractions/"
```

期待: 1 commit、`git show --stat HEAD` で 6 件の rename。
