### Task 6: Constants フォルダ作成と移動

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Create: `src/FinLearn.Core/Constants/` （新規フォルダ）
- Move: `src/FinLearn.Core/GameRules.cs` → `src/FinLearn.Core/Constants/GameRules.cs`
- Move: `src/FinLearn.Core/Messages.cs` → `src/FinLearn.Core/Constants/Messages.cs`

**前提:** namespace 変更なし。`GameRules` はゲームバランス調整定数、`Messages` は日本語エラーメッセージ定数。

- [ ] **Step 1: Constants フォルダを作成する**

```powershell
New-Item -ItemType Directory -Path src/FinLearn.Core/Constants
```

期待: `src/FinLearn.Core/Constants/` が存在する。

- [ ] **Step 2: 2 ファイルを `git mv` する**

```powershell
git mv src/FinLearn.Core/GameRules.cs src/FinLearn.Core/Constants/GameRules.cs
git mv src/FinLearn.Core/Messages.cs  src/FinLearn.Core/Constants/Messages.cs
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
git commit -m "refactor(core): move constants (GameRules, Messages) into Constants/"
```

期待: 1 commit、`git show --stat HEAD` で 2 件の rename。
