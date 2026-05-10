### Task 8: ドキュメントパス参照の更新

[← プランに戻る](../core-folder-restructure.md)

**Files:**
- Modify: `.claude/rules/src/core-domain.md` — 38〜40 行目のテーブル内パス文字列を新パスへ置換

**背景:** 当該ファイルのテーブルでは大半のエントリがファイル名のみ（パス無し）で記述されているが、3 行だけ `Services/` プレフィックス付きで書かれている箇所があり、Task 4 / 5 のフォルダ移動でパスが古くなっている。本文中の `IPlayerOrderHandler (LimitOrderHandler / MarketOrderHandler)` 等は型名であり修正不要。

**事前確認用:** 修正前に `.claude/rules/src/core-domain.md` 内の対象 3 行は以下のとおり（ヘッダ列を含む完全な行で識別する）。

- [ ] **Step 1: 修正対象 3 行を Grep で確認する**

```powershell
# 既存の Grep ツールで以下のパターンを検索
# pattern: ^\| `Services/(IPlayerOrderHandler|LimitOrderHandler|MarketOrderHandler)\.cs`
# path: .claude/rules/src/core-domain.md
```

期待: 3 件ヒット（38〜40 行目相当）。

- [ ] **Step 2: 1 行目（IPlayerOrderHandler）を Edit で置換する**

```text
old_string: | `Services/IPlayerOrderHandler.cs` | プレイヤー注文戦略
new_string: | `Abstractions/IPlayerOrderHandler.cs` | プレイヤー注文戦略
```

期待: 1 箇所のみ置換され成功する。

- [ ] **Step 3: 2 行目（LimitOrderHandler）を Edit で置換する**

```text
old_string: | `Services/LimitOrderHandler.cs` | 限値戦略
new_string: | `Services/OrderHandlers/LimitOrderHandler.cs` | 限値戦略
```

期待: 1 箇所のみ置換され成功する。

- [ ] **Step 4: 3 行目（MarketOrderHandler）を Edit で置換する**

```text
old_string: | `Services/MarketOrderHandler.cs` | 成行戦略
new_string: | `Services/OrderHandlers/MarketOrderHandler.cs` | 成行戦略
```

期待: 1 箇所のみ置換され成功する。

- [ ] **Step 5: 残存パスが無いことを確認する**

```powershell
# Grep で以下を検索
# pattern: `Services/(IPlayerOrderHandler|LimitOrderHandler|MarketOrderHandler)\.cs`
# path: .claude/rules/src/core-domain.md
```

期待: ヒット 0 件（旧パスが完全に消えている）。

- [ ] **Step 6: 念のためリポジトリ全体に旧パスが残っていないか確認する**

```powershell
# Grep で repo 全体（テストコード等含む）に対して
# pattern: Services/IPlayerOrderHandler\.cs|Services/LimitOrderHandler\.cs|Services/MarketOrderHandler\.cs
# 除外: src/FinLearn.Core/bin, src/FinLearn.Core/obj, frontend/node_modules
```

期待: ヒット 0 件、または `docs/specs/core-folder-restructure-design.md` の「移動マッピング表」の左列のみがヒット（仕様書なので変更不要）。`.claude/rules/src/core-domain.md` がヒットしないこと。

- [ ] **Step 7: コミットする**

```powershell
git add .claude/rules/src/core-domain.md
git commit -m "docs(rules): update core-domain.md paths after folder restructure"
```

期待: 1 commit、変更行数は最大 3 行。
