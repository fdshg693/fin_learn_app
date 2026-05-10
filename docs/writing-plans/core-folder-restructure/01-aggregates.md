### Task 1: Aggregates フォルダ作成と移動

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Create: `src/FinLearn.Core/Aggregates/` （新規フォルダ）
- Move: `src/FinLearn.Core/Game.cs` → `src/FinLearn.Core/Aggregates/Game.cs`
- Move: `src/FinLearn.Core/Models/Portfolio.cs` → `src/FinLearn.Core/Aggregates/Portfolio.cs`
- Move: `src/FinLearn.Core/Models/OrderBook.cs` → `src/FinLearn.Core/Aggregates/OrderBook.cs`

**前提:** namespace は `FinLearn.Core` フラットのまま、ファイル内容は1バイトも変更しない。`git mv` を使い rename として記録する。

- [ ] **Step 1: 新フォルダを作成する**

PowerShell で:
```powershell
New-Item -ItemType Directory -Path src/FinLearn.Core/Aggregates
```

期待: `src/FinLearn.Core/Aggregates/` ディレクトリが存在すること。

- [ ] **Step 2: 3 ファイルを `git mv` する**

```powershell
git mv src/FinLearn.Core/Game.cs src/FinLearn.Core/Aggregates/Game.cs
git mv src/FinLearn.Core/Models/Portfolio.cs src/FinLearn.Core/Aggregates/Portfolio.cs
git mv src/FinLearn.Core/Models/OrderBook.cs src/FinLearn.Core/Aggregates/OrderBook.cs
```

期待: 各コマンドが silent に成功する。

- [ ] **Step 3: rename として認識されているか確認する**

```powershell
git status
```

期待: 3 件の `renamed:` 行が表示される（`R100` の 100% 同一）。`modified:` であってはならない。

- [ ] **Step 4: ビルドが通ることを確認する**

```powershell
dotnet build fin_learn_app.sln
```

期待: `Build succeeded`、エラー 0 件。namespace は変わらないため警告増加もないはず。

- [ ] **Step 5: 全テストが通ることを確認する**

```powershell
dotnet test fin_learn_app.sln
```

期待: 全テスト pass。Core / Api 両プロジェクトのテストが緑。

- [ ] **Step 6: コミットする**

```powershell
git add -A
git commit -m "refactor(core): move aggregates (Game, Portfolio, OrderBook) into Aggregates/"
```

期待: 1 commit が作成され、`git show --stat HEAD` で 3 件の rename が表示される。
