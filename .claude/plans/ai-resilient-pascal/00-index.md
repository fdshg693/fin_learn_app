# AI Resilient Pascal — Plan Index

## このプランは何か

`src/FinLearn.Core/TurnProcessor.cs` の `PlaceOrder` / `ExecutePlayerOrder` を、ドメイン軸に沿って再設計する大規模リファクタの計画書。具体的には:

- **`World` 型の導入** — `book / portfolios / nextOrderId / exchange / fee / playerName / turn / instruments / prices` を一塊にした世界スナップショット
- **`IPlayerOrderHandler` 戦略の導入** — Limit / Market を `Receive` / `Settle` の 2 メソッド単位で分離
- **Match を pipeline 共通実行に切り出し** — handler 間で重複させない (DRY)
- **Wait / PlaceOrder を統一 pipeline に集約** — Wait は "intent なし" ケース

最終的に "並んでいるだけの行" と "ドメイン的に順序必須の行" がコード構造で区別される状態を目指す。

## 規模感

**複数日仕事**。テストは段階的に旧コードと並走させて常時グリーンを保つ方針。各ステップは 1 commit (or 1 PR) に収まる粒度で分割している。

## ファイル一覧と読む順

新規セッションで作業を引き継ぐ AI/開発者は **必ずこの順で読む**:

1. **[01-context.md](01-context.md)** — 既存コードの責務マップと参考資料一覧。リファクタの **動機** と **既存ドメインの全体像** を理解する
2. **[02-target-design.md](02-target-design.md)** — 最終形の型 (`World`, `IPlayerOrderHandler`, `LimitOrderHandler`, `MarketOrderHandler`) と新 Pipeline の動き
3. **[03-migration-steps.md](03-migration-steps.md)** — Step 1〜7 の段階的移行手順。各ステップで何を作り、何のテストが影響し、どう緑にするか
4. **[04-test-impact.md](04-test-impact.md)** — テストへの影響と新規テストの方針

各ファイルは独立して読めるよう冒頭にコンテキストを置いてあるが、初読時は順番通りに読むのが効率的。

## 確定済み設計判断 (ユーザー承認済み)

| 項目 | 決定 | 理由 |
|---|---|---|
| Handler 境界 | `Receive` / `Settle` 分離 + `Match` は pipeline 共通 | Match ロジックを Limit/Market で重複させない (DRY) |
| Public API (`Buy/Sell/Wait`) | シグネチャ維持 (内部実装のみ改造) | 既存テスト 54 件 + API テスト 40 件への影響を最小化 |
| 進め方 | 段階的 (旧コードと並走、最後に旧削除) | 途中コミットも常にグリーン保証 |

## このプランの完了条件

- [ ] 旧 `PlaceOrder` / `ExecutePlayerOrder` / `PlayerOrderOutcome` / `Failed` / `BuildAllPortfolios` / `SplitPortfolios` / `BuildOrdersByIdSnapshot` が削除されている
- [ ] 新 `World` / `IPlayerOrderHandler` / `LimitOrderHandler` / `MarketOrderHandler` / `RunTurn` (新 pipeline) が機能している
- [ ] `TurnProcessor.Buy/Sell/Wait` の public シグネチャが従来通り
- [ ] `dotnet test` 全グリーン (Core 185 + Api 37 = 222 件)
- [ ] 新規テスト (`WorldTests`, `LimitOrderHandlerTests`, `MarketOrderHandlerTests`) が追加されている
- [ ] `.claude/rules/src/core-domain.md` が新構造に更新されている

## 各ステップ完了時の確認コマンド

```powershell
# Windows / PowerShell (リポジトリルートから)
dotnet test fin_learn_app.sln --nologo --verbosity quiet
```

`Failed: 0, Passed: 222, Skipped: 0` を期待値とする (リファクタで件数増減しうるが減ってはいけない)。

## 用語

- **World** — このプランで導入する新型。`book / portfolios / nextOrderId / exchange / fee / playerName / turn / instruments / prices` を持つ immutable な世界スナップショット
- **Handler** — `IPlayerOrderHandler` の実装 (Limit/Market)
- **Pipeline** — TurnProcessor 内の新メソッド `RunTurn`。World と Handler を組み合わせて 1 ターンを進める
- **Receive** — 注文の受付段階 (限値の予約、成行は no-op)
- **Settle** — 約定結果を World に反映する段階
- **Intent** — Player のリクエストから生成される `Order` entity (純粋変換)
- **旧コード** — `PlaceOrder` / `ExecutePlayerOrder` 周辺。Step 6 で削除予定
