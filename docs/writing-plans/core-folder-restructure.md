### Core Folder Restructure Implementation Plan

**Goal:** `src/FinLearn.Core/` 内のファイルを DDD 語彙（Aggregates / Entities / ValueObjects / Services / Abstractions / Results / Constants / Internal）に沿った階層へ物理的に再編する。

**Architecture:** namespace は `FinLearn.Core` のフラットなまま維持し、ファイル移動のみで完結する。`FinLearn.Core.csproj` は MSBuild 暗黙の `Compile` 収集のため csproj 編集不要。各タスクは「フォルダ単位でまとめて `git mv` → ビルド → コミット」の繰り返し。`InternalsVisibleTo` 設定（`World.cs` 等の internal アクセス）は維持される。

**Tech Stack:** .NET 9 / C# (nullable enabled) / xUnit / Git (rename 検出のため `git mv`)

---

### File Structure

新フォルダ構造（最終形）:

```
src/FinLearn.Core/
├── Aggregates/
│   ├── Game.cs
│   ├── Portfolio.cs
│   └── OrderBook.cs
├── Entities/
│   ├── Player.cs
│   └── Order.cs
├── ValueObjects/
│   ├── Instrument.cs
│   ├── Position.cs
│   ├── PositionSet.cs
│   ├── OrderSide.cs
│   └── OrderType.cs
├── Services/
│   ├── TurnProcessor.cs
│   ├── SettlementProcessor.cs
│   ├── Market.cs
│   ├── ComputerTrader.cs
│   ├── SimpleExchange.cs
│   ├── SimpleExchangeFactory.cs
│   ├── RandomPriceFluctuator.cs
│   └── OrderHandlers/
│       ├── LimitOrderHandler.cs
│       └── MarketOrderHandler.cs
├── Abstractions/
│   ├── IExchange.cs
│   ├── IExchangeFactory.cs
│   ├── IMarket.cs
│   ├── IOrderPlacer.cs
│   ├── IPlayerOrderHandler.cs
│   └── IPriceFluctuator.cs
├── Results/                     ← 既存のまま（変更なし）
│   ├── FillResult.cs
│   ├── OrderFill.cs
│   ├── MatchResult.cs
│   ├── TradeResult.cs
│   └── TurnResult.cs
├── Constants/
│   ├── GameRules.cs
│   └── Messages.cs
├── Internal/
│   └── World.cs
└── FinLearn.Core.csproj
```

修正対象（コード以外）:
- [.claude/rules/src/core-domain.md](../../.claude/rules/src/core-domain.md) — テーブル内の `Services/IPlayerOrderHandler.cs` / `Services/LimitOrderHandler.cs` / `Services/MarketOrderHandler.cs` のパス文字列を更新

修正不要:
- `docs/DDD/MAIN.md` — 型名のみ（パス無し）
- `src/FinLearn.Api/`、`tests/FinLearn.Tests`、`tests/FinLearn.Api.Tests` — 全て型名参照
- `frontend/` — Core 非依存
- `FinLearn.Core.csproj` — 暗黙 `Compile` 収集

---

### Tasks

1. [Aggregates フォルダ作成と移動](core-folder-restructure/01-aggregates.md) — `Game.cs` / `Portfolio.cs` / `OrderBook.cs` を `Aggregates/` へ集約。
2. [Entities フォルダ作成と移動](core-folder-restructure/02-entities.md) — `Player.cs` / `Order.cs` を `Entities/` へ移動。
3. [ValueObjects フォルダ作成と移動](core-folder-restructure/03-value-objects.md) — `Instrument` / `Position` / `PositionSet` / `OrderSide` / `OrderType` を `ValueObjects/` へ移動し、空になった `Models/` を削除。
4. [Services 内の整理](core-folder-restructure/04-services-restructure.md) — ルートの `TurnProcessor.cs` を `Services/` 配下へ移し、`Limit/MarketOrderHandler` を `Services/OrderHandlers/` へネスト。
5. [Abstractions フォルダ作成と移動](core-folder-restructure/05-abstractions.md) — `Services/I*.cs` 6 ファイルを `Abstractions/` へ移動。
6. [Constants フォルダ作成と移動](core-folder-restructure/06-constants.md) — `GameRules.cs` / `Messages.cs` を `Constants/` へ移動。
7. [Internal フォルダ作成と移動](core-folder-restructure/07-internal.md) — `World.cs` を `Internal/` へ隔離。
8. [ドキュメントパス参照の更新](core-folder-restructure/08-doc-update.md) — `.claude/rules/src/core-domain.md` のテーブル内パスを新パスに置換。
9. [最終検証とまとめコミット確認](core-folder-restructure/09-final-verification.md) — 全体ビルド・全テスト通過・`git log --follow` で rename 認識を検証。
