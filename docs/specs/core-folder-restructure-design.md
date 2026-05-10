# `src/FinLearn.Core` フォルダ構造リファクタリング設計

## 目的

`src/FinLearn.Core` のファイル構造を、DDDの語彙（Aggregates / Entities / Value Objects / Services / Abstractions）に沿った階層へ再編する。これによりドメイン上の役割がフォルダ構造から一目で読み取れるようにし、新規ファイル追加時の「どこに置くか」の判断を容易にする。

## スコープ

- **含む**: `src/FinLearn.Core/` 内のファイル移動。`docs/DDD/MAIN.md` と `.claude/rules/src/core-domain.md` 内のファイルパス参照の更新。
- **含まない**: namespace の階層化、コードのロジック変更、API/テスト/フロントエンドの修正、他プロジェクトへの波及。

## 制約と前提

- **namespace は `FinLearn.Core` のフラットなまま維持する**。フォルダはあくまで物理的な整理目的で、論理的な namespace 階層とは独立。これにより `using` 文や型参照は無修正のまま動作する。
- `FinLearn.Core.csproj` は MSBuild の暗黙の `Compile` 収集（`<Compile Remove>` / 明示 `<Compile Include>` 無し）のため、ファイル移動だけで再ビルド可能。csproj 編集不要。
- `InternalsVisibleTo` で `FinLearn.Tests` から internal 型へのアクセスが許可されている前提を維持（`World.cs` 等が internal）。
- 全ファイルは immutable な sealed class / record で、ファイル単体の責務は変えない。

## 新フォルダ構造

```
src/FinLearn.Core/
├── Aggregates/
│   ├── Game.cs
│   ├── Portfolio.cs
│   └── OrderBook.cs
│
├── Entities/
│   ├── Player.cs
│   └── Order.cs
│
├── ValueObjects/
│   ├── Instrument.cs
│   ├── Position.cs
│   ├── PositionSet.cs
│   ├── OrderSide.cs
│   └── OrderType.cs
│
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
│
├── Abstractions/
│   ├── IExchange.cs
│   ├── IExchangeFactory.cs
│   ├── IMarket.cs
│   ├── IOrderPlacer.cs
│   ├── IPlayerOrderHandler.cs
│   └── IPriceFluctuator.cs
│
├── Results/
│   ├── FillResult.cs
│   ├── OrderFill.cs
│   ├── MatchResult.cs
│   ├── TradeResult.cs
│   └── TurnResult.cs
│
├── Constants/
│   ├── GameRules.cs
│   └── Messages.cs
│
├── Internal/
│   └── World.cs
│
└── FinLearn.Core.csproj
```

## 分類の根拠

### Aggregates（整合性境界を持つ複合体）

- **`Game`** — ターン全体の状態 snapshot を束ねる aggregate root。Player / OrderBook / ComputerPortfolios / Prices を所有。
- **`Portfolio`** — Cash + PositionSet + Reserved 状態の整合性を `ReserveBuy` / `SettleReservedBuy` / `ReleaseBuyReservation` 等で守る。「予約成功なら settlement は失敗しない」という不変条件の境界。
- **`OrderBook`** — Buy/Sell リストの順序とマッチング条件、有効期限切れ処理を内包。

### Entities（identity を持つが Aggregate Root ではない）

- **`Player`** — `Name` で identity。注文生成と portfolio 更新を担うが、自身の整合性は Portfolio に委譲。
- **`Order`** — 注文 ID で identity。OrderBook の中の構成要素として存在。

### Value Objects（immutable, identity 無し）

- `Instrument`, `Position`, `PositionSet`, `OrderSide` (enum), `OrderType` (enum)。値による等価性で振る舞う。

### Services（ドメインサービスの実装）

- パイプライン: `TurnProcessor`, `SettlementProcessor`
- 市場: `Market`, `ComputerTrader`
- 取引所: `SimpleExchange`, `SimpleExchangeFactory`
- 価格変動: `RandomPriceFluctuator`
- **OrderHandlers サブフォルダ**: `LimitOrderHandler` / `MarketOrderHandler` は `IPlayerOrderHandler` の戦略パターン実装で対になっているため1段ネスト。

### Abstractions（interfaces のみ）

- `IExchange`, `IExchangeFactory`, `IMarket`, `IOrderPlacer`, `IPlayerOrderHandler`, `IPriceFluctuator`
- 実装と interface を分離することで「DIポイント / テスト差し替え点」を一覧できる。

### Results（戻り値の型 / DTO相当）

- `FillResult`, `OrderFill`, `MatchResult`, `TradeResult`, `TurnResult`
- 既存の Results/ フォルダを維持。ドメイン操作の結果を表す不変データ構造のみを置く。

### Constants（ドメイン定数）

- `GameRules` — ゲームバランス調整値
- `Messages` — 日本語エラーメッセージ定数

### Internal（internal 修飾型）

- `World.cs` — pipeline 内部状態 snapshot。internal record。`FinLearn.Tests` のみがアクセスできる前提を維持。
- 公開ドメインモデルとの混在を避けるため隔離。

## 移動マッピング表

| 移動元 | 移動先 |
|---|---|
| `Game.cs` | `Aggregates/Game.cs` |
| `Models/Portfolio.cs` | `Aggregates/Portfolio.cs` |
| `Models/OrderBook.cs` | `Aggregates/OrderBook.cs` |
| `Models/Player.cs` | `Entities/Player.cs` |
| `Models/Order.cs` | `Entities/Order.cs` |
| `Models/Instrument.cs` | `ValueObjects/Instrument.cs` |
| `Models/Position.cs` | `ValueObjects/Position.cs` |
| `Models/PositionSet.cs` | `ValueObjects/PositionSet.cs` |
| `Models/OrderSide.cs` | `ValueObjects/OrderSide.cs` |
| `Models/OrderType.cs` | `ValueObjects/OrderType.cs` |
| `TurnProcessor.cs` | `Services/TurnProcessor.cs` |
| `Services/SettlementProcessor.cs` | `Services/SettlementProcessor.cs`（変更なし） |
| `Services/Market.cs` | `Services/Market.cs`（変更なし） |
| `Services/ComputerTrader.cs` | `Services/ComputerTrader.cs`（変更なし） |
| `Services/SimpleExchange.cs` | `Services/SimpleExchange.cs`（変更なし） |
| `Services/SimpleExchangeFactory.cs` | `Services/SimpleExchangeFactory.cs`（変更なし） |
| `Services/RandomPriceFluctuator.cs` | `Services/RandomPriceFluctuator.cs`（変更なし） |
| `Services/LimitOrderHandler.cs` | `Services/OrderHandlers/LimitOrderHandler.cs` |
| `Services/MarketOrderHandler.cs` | `Services/OrderHandlers/MarketOrderHandler.cs` |
| `Services/IExchange.cs` | `Abstractions/IExchange.cs` |
| `Services/IExchangeFactory.cs` | `Abstractions/IExchangeFactory.cs` |
| `Services/IMarket.cs` | `Abstractions/IMarket.cs` |
| `Services/IOrderPlacer.cs` | `Abstractions/IOrderPlacer.cs` |
| `Services/IPlayerOrderHandler.cs` | `Abstractions/IPlayerOrderHandler.cs` |
| `Services/IPriceFluctuator.cs` | `Abstractions/IPriceFluctuator.cs` |
| `Results/FillResult.cs` | `Results/FillResult.cs`（変更なし） |
| `Results/OrderFill.cs` | `Results/OrderFill.cs`（変更なし） |
| `Results/MatchResult.cs` | `Results/MatchResult.cs`（変更なし） |
| `Results/TradeResult.cs` | `Results/TradeResult.cs`（変更なし） |
| `Results/TurnResult.cs` | `Results/TurnResult.cs`（変更なし） |
| `GameRules.cs` | `Constants/GameRules.cs` |
| `Messages.cs` | `Constants/Messages.cs` |
| `World.cs` | `Internal/World.cs` |

## 影響範囲

### 修正が必要なファイル

1. **`.claude/rules/src/core-domain.md`** — 「`Services/IPlayerOrderHandler.cs`」「`Services/LimitOrderHandler.cs`」「`Services/MarketOrderHandler.cs`」のパス記述を新パス（`Abstractions/IPlayerOrderHandler.cs`、`Services/OrderHandlers/LimitOrderHandler.cs`、`Services/OrderHandlers/MarketOrderHandler.cs`）へ更新。同ファイル内の本文記述（`IPlayerOrderHandler (LimitOrderHandler / MarketOrderHandler)` 等）も同様に確認。

### 修正不要

- `docs/DDD/MAIN.md` — モデル一覧表は型名のみ列挙でファイルパスを含まないため変更不要。
- `src/FinLearn.Api/` の参照コード — 全て型名参照、namespace は `FinLearn.Core` のまま。
- `tests/FinLearn.Tests` / `tests/FinLearn.Api.Tests` — 同上。
- `frontend/` — Coreに依存しない。
- `FinLearn.Core.csproj` — 暗黙の `Compile` 収集のため変更不要。

## 検証手順

1. `dotnet build fin_learn_app.sln` がエラー無く通る。
2. `dotnet test` で全テストが通る（テストコードに変更が無いことの裏付け）。
3. Visual Studio / Rider のソリューションエクスプローラ上で新フォルダ構造が表示される。
4. `git status` で「ファイル移動（rename）」として認識される（内容変更なし）。
5. `docs/DDD/MAIN.md` を grep して `Services/` `Models/` `Results/` 等のフォルダ名を含むパス参照が無いことを確認（無ければ予定どおり変更不要）。

## ロールバック容易性

- 全ての変更が物理的なファイル移動のみで、ロジック変更や namespace 変更を伴わない。
- 単一の `git revert` で完全に元に戻せる。
- ビルドが通らない場合は `git mv` の取り消しのみで回復。

## 実装の注意点

- ファイル移動は `git mv` を使い、Git に rename として認識させる（履歴 blame の継続性を保つ）。
- 1ファイルずつではなく、フォルダ単位でまとめて移動・コミットする（PR / commit の見通しが良くなる）。
- 移動完了後にビルド・テストを実行して確認する。
