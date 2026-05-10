### Task 2: Entities フォルダ作成と移動

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Create: `src/FinLearn.Core/Entities/` （新規フォルダ）
- Move: `src/FinLearn.Core/Models/Player.cs` → `src/FinLearn.Core/Entities/Player.cs`
- Move: `src/FinLearn.Core/Models/Order.cs` → `src/FinLearn.Core/Entities/Order.cs`

**前提:** namespace 変更なし、内容変更なし。

- [ ] **Step 1: 新フォルダを作成する**

```powershell
New-Item -ItemType Directory -Path src/FinLearn.Core/Entities
```

期待: `src/FinLearn.Core/Entities/` が存在する。

- [ ] **Step 2: 2 ファイルを `git mv` する**

```powershell
git mv src/FinLearn.Core/Models/Player.cs src/FinLearn.Core/Entities/Player.cs
git mv src/FinLearn.Core/Models/Order.cs src/FinLearn.Core/Entities/Order.cs
```

期待: silent 成功。

- [ ] **Step 3: rename 認識を確認する**

```powershell
git status
```

期待: 2 件の `renamed:` 行（`R100`）。

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
git commit -m "refactor(core): move entities (Player, Order) into Entities/"
```

期待: 1 commit、`git show --stat HEAD` で 2 件の rename。
