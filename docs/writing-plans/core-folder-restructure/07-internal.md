### Task 7: Internal フォルダ作成と World.cs の隔離

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Create: `src/FinLearn.Core/Internal/` （新規フォルダ）
- Move: `src/FinLearn.Core/World.cs` → `src/FinLearn.Core/Internal/World.cs`

**前提:** `World` は internal record。`InternalsVisibleTo("FinLearn.Tests")` 設定で `tests/FinLearn.Tests` からアクセス可能な前提を維持する（属性は `AssemblyInfo.cs` または csproj に記述されているはずで、ファイル移動では影響しない）。namespace 変更なし。

- [ ] **Step 1: Internal フォルダを作成する**

```powershell
New-Item -ItemType Directory -Path src/FinLearn.Core/Internal
```

期待: `src/FinLearn.Core/Internal/` が存在する。

- [ ] **Step 2: World.cs を移動する**

```powershell
git mv src/FinLearn.Core/World.cs src/FinLearn.Core/Internal/World.cs
```

期待: silent 成功。

- [ ] **Step 3: rename 認識を確認する**

```powershell
git status
```

期待: 1 件の `renamed:` 行（`R100`）。

- [ ] **Step 4: ビルドが通ることを確認する**

```powershell
dotnet build fin_learn_app.sln
```

期待: `Build succeeded`、エラー 0 件。internal アクセス（`FinLearn.Tests` から `World` 等）も従来どおり通ること。

- [ ] **Step 5: 全テストが通ることを確認する**

```powershell
dotnet test fin_learn_app.sln
```

期待: 全テスト pass（`World` を内部で参照するテストがあれば、それも通る）。

- [ ] **Step 6: コミットする**

```powershell
git add -A
git commit -m "refactor(core): isolate World (internal) into Internal/"
```

期待: 1 commit、`git show --stat HEAD` で 1 件の rename。
