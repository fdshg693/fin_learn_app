### Task 9: 最終検証（フォルダ構造 / ビルド / テスト / Git rename 履歴）

[← プランに戻る](../core-folder-restructure.md)

**Files:** （変更なし。検証のみ）

**目的:** Task 1〜8 が完了した状態で、最終形が仕様書の「新フォルダ構造」と一致し、ビルド・テスト・Git rename 履歴が全て期待どおりであることを確認する。

- [ ] **Step 1: 新フォルダ構造を確認する**

```powershell
Get-ChildItem -Recurse src/FinLearn.Core -Exclude bin,obj |
    Where-Object { $_.PSIsContainer -or $_.Extension -eq '.cs' -or $_.Extension -eq '.csproj' } |
    Select-Object FullName
```

期待: 以下の構成（順不同）が全て確認できる。
- `src/FinLearn.Core/Aggregates/Game.cs`
- `src/FinLearn.Core/Aggregates/Portfolio.cs`
- `src/FinLearn.Core/Aggregates/OrderBook.cs`
- `src/FinLearn.Core/Entities/Player.cs`
- `src/FinLearn.Core/Entities/Order.cs`
- `src/FinLearn.Core/ValueObjects/Instrument.cs`
- `src/FinLearn.Core/ValueObjects/Position.cs`
- `src/FinLearn.Core/ValueObjects/PositionSet.cs`
- `src/FinLearn.Core/ValueObjects/OrderSide.cs`
- `src/FinLearn.Core/ValueObjects/OrderType.cs`
- `src/FinLearn.Core/Services/TurnProcessor.cs`
- `src/FinLearn.Core/Services/SettlementProcessor.cs`
- `src/FinLearn.Core/Services/Market.cs`
- `src/FinLearn.Core/Services/ComputerTrader.cs`
- `src/FinLearn.Core/Services/SimpleExchange.cs`
- `src/FinLearn.Core/Services/SimpleExchangeFactory.cs`
- `src/FinLearn.Core/Services/RandomPriceFluctuator.cs`
- `src/FinLearn.Core/Services/OrderHandlers/LimitOrderHandler.cs`
- `src/FinLearn.Core/Services/OrderHandlers/MarketOrderHandler.cs`
- `src/FinLearn.Core/Abstractions/IExchange.cs`
- `src/FinLearn.Core/Abstractions/IExchangeFactory.cs`
- `src/FinLearn.Core/Abstractions/IMarket.cs`
- `src/FinLearn.Core/Abstractions/IOrderPlacer.cs`
- `src/FinLearn.Core/Abstractions/IPlayerOrderHandler.cs`
- `src/FinLearn.Core/Abstractions/IPriceFluctuator.cs`
- `src/FinLearn.Core/Results/FillResult.cs` (および OrderFill / MatchResult / TradeResult / TurnResult)
- `src/FinLearn.Core/Constants/GameRules.cs`
- `src/FinLearn.Core/Constants/Messages.cs`
- `src/FinLearn.Core/Internal/World.cs`
- `src/FinLearn.Core/FinLearn.Core.csproj`

`src/FinLearn.Core/Models/` が存在しないこと、`Game.cs` / `TurnProcessor.cs` / `World.cs` / `GameRules.cs` / `Messages.cs` がルート直下に残っていないことを併せて確認する。

- [ ] **Step 2: クリーンビルドが通ることを確認する**

```powershell
dotnet build fin_learn_app.sln --no-incremental
```

期待: `Build succeeded`、エラー 0 件、警告は restructure 前と同数（増加なし）。

- [ ] **Step 3: 全テストが通ることを確認する**

```powershell
dotnet test fin_learn_app.sln
```

期待: 全テスト pass。Core / Api 両プロジェクト緑。

- [ ] **Step 4: Git の rename 履歴を確認する**

例として `Game.cs` の履歴を rename 追跡で表示:
```powershell
git log --follow --oneline -- src/FinLearn.Core/Aggregates/Game.cs
```

期待: ファイル移動前のコミットも履歴に表示される（rename 追跡が成功）。`git log --diff-filter=R --name-status` で該当 commit が `R100` を含む rename として記録されていることを確認できる。

- [ ] **Step 5: 旧パス参照がリポジトリに残っていないことを確認する**

```powershell
# Grep で repo 全体を検索（除外: src/FinLearn.Core/bin, obj, frontend/node_modules, docs/specs/core-folder-restructure-design.md, docs/writing-plans）
# pattern: Models/(Instrument|Position|PositionSet|OrderSide|OrderType|Player|Order|Portfolio|OrderBook)\.cs
```

期待: ヒット 0 件。

```powershell
# 同様に
# pattern: Services/(IExchange|IExchangeFactory|IMarket|IOrderPlacer|IPlayerOrderHandler|IPriceFluctuator|LimitOrderHandler|MarketOrderHandler)\.cs
```

期待: ヒット 0 件（仕様書 / プランファイルを除外した状態で）。

- [ ] **Step 6: 必要に応じてマージ用のサマリ commit を作る（オプション）**

通常は不要。Task 1〜8 の各コミットがそのまま PR の履歴になる。もし PR 説明用にまとめが必要であれば、追加コミットは作らず、PR 説明文に「Task 1〜8 のコミットでフォルダ単位ごとに移動。各 commit でビルド・テスト pass を確認済み」と記載する。

期待: ここで `git log --oneline` を見ると Task 1〜8 で 8 件のコミット（リファクタ 7 件 + ドキュメント更新 1 件）が綺麗に並んでいる。
