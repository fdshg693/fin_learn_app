### Task 3: ValueObjects フォルダ作成と移動（および Models/ フォルダ削除）

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Create: `src/FinLearn.Core/ValueObjects/` （新規フォルダ）
- Move: `src/FinLearn.Core/Models/Instrument.cs` → `src/FinLearn.Core/ValueObjects/Instrument.cs`
- Move: `src/FinLearn.Core/Models/Position.cs` → `src/FinLearn.Core/ValueObjects/Position.cs`
- Move: `src/FinLearn.Core/Models/PositionSet.cs` → `src/FinLearn.Core/ValueObjects/PositionSet.cs`
- Move: `src/FinLearn.Core/Models/OrderSide.cs` → `src/FinLearn.Core/ValueObjects/OrderSide.cs`
- Move: `src/FinLearn.Core/Models/OrderType.cs` → `src/FinLearn.Core/ValueObjects/OrderType.cs`
- Delete: `src/FinLearn.Core/Models/` （Task 1〜3 完了後に空になる前提）

**前提:** Task 1（Portfolio, OrderBook 移動）と Task 2（Player, Order 移動）が完了済み。これら 5 ファイルが残れば Models/ は空になる。

- [ ] **Step 1: 新フォルダを作成する**

```powershell
New-Item -ItemType Directory -Path src/FinLearn.Core/ValueObjects
```

期待: `src/FinLearn.Core/ValueObjects/` が存在する。

- [ ] **Step 2: 5 ファイルを `git mv` する**

```powershell
git mv src/FinLearn.Core/Models/Instrument.cs   src/FinLearn.Core/ValueObjects/Instrument.cs
git mv src/FinLearn.Core/Models/Position.cs     src/FinLearn.Core/ValueObjects/Position.cs
git mv src/FinLearn.Core/Models/PositionSet.cs  src/FinLearn.Core/ValueObjects/PositionSet.cs
git mv src/FinLearn.Core/Models/OrderSide.cs    src/FinLearn.Core/ValueObjects/OrderSide.cs
git mv src/FinLearn.Core/Models/OrderType.cs    src/FinLearn.Core/ValueObjects/OrderType.cs
```

期待: 全コマンド silent 成功。

- [ ] **Step 3: 旧 Models/ フォルダが空であることを確認し削除する**

```powershell
Get-ChildItem src/FinLearn.Core/Models -Force
```
期待: 空（出力なし）。

空であることを確認したうえで削除:
```powershell
Remove-Item src/FinLearn.Core/Models -Force
```

期待: `src/FinLearn.Core/Models/` が消える。Git は空ディレクトリを追跡しないため `git status` には現れない（OS 上のディレクトリ削除のみ）。

- [ ] **Step 4: rename 認識を確認する**

```powershell
git status
```

期待: 5 件の `renamed:` 行（`R100`）。

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
git commit -m "refactor(core): move value objects (Instrument, Position, PositionSet, OrderSide, OrderType) into ValueObjects/"
```

期待: 1 commit、`git show --stat HEAD` で 5 件の rename。
