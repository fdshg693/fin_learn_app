# マジックナンバー集約プラン

## Context

現状、ゲームバランス調整に関わる数値（プレイヤー初期資金、コンピュータートレーダーの提示価格レンジ、株価変動幅、銘柄数、初期株価、手数料、ページサイズ等）が3層（Core / Api / Frontend）の様々なファイルにインライン埋め込み・private const・public const と一貫性のない形で散在しており、

- プレイテストでチューニングする際に対象箇所を網羅的に把握しにくい
- デプロイ環境ごとの差し替えができない（特に Api 層のページサイズ・ログ保持日数）
- フロントエンドのアクション種別（"buy"/"Buy"/"Limit" 等）が文字列リテラルとして複数ファイルに重複している

これらを **層ごとに1箇所に集約** することで、可読性・調整容易性・型安全性を向上させる。Core はピュア・ドメインを保つため `IOptions` 等の DI に依存させず静的クラスでまとめ、Api は `appsettings.json` + Options パターンに移行、Frontend は単一の `config.ts` + `enums.ts` を新設する。

## 1. Core層 — `GameRules` 静的クラスを新設

**新規作成**: [src/FinLearn.Core/GameRules.cs](src/FinLearn.Core/GameRules.cs)

ネストされた静的クラスでドメインごとにグルーピング（フラットだと「85って何の85？」になるため）。すべて `public const int`、Core の依存ゼロ原則を維持。

```csharp
namespace FinLearn.Core;

public static class GameRules
{
    public static class Player
    {
        public const int InitialCash = 10000;
    }

    public static class ComputerTraders
    {
        public const int Count = 10;
        // Random.Next(min, maxExclusive) の上限は exclusive。命名で明示する。
        public const int BuyPriceMinPercent = 85;
        public const int BuyPriceMaxPercentExclusive = 106;   // 〜105% inclusive
        public const int SellPriceMinPercent = 95;
        public const int SellPriceMaxPercentExclusive = 116;  // 〜115% inclusive
    }

    public static class PriceFluctuation
    {
        public const int MinPercent = 95;
        public const int MaxPercentExclusive = 106;  // ±5%
        public const int MinPrice = 1;               // 最低株価フロア
    }
}
```

**修正対象**:
- [src/FinLearn.Core/Models/Player.cs:5,8,35](src/FinLearn.Core/Models/Player.cs#L5) — private const 削除、`GameRules.Player.InitialCash` 参照に置換。
- [src/FinLearn.Core/Services/ComputerTrader.cs:9,40,46,47,54,60,61](src/FinLearn.Core/Services/ComputerTrader.cs#L9) — `ComputerCount`, `(85, 106)`, `(95, 116)`, `Math.Max(1, ...)` を `GameRules.ComputerTraders.*` / `GameRules.PriceFluctuation.MinPrice` に置換。
- [src/FinLearn.Core/Services/RandomPriceFluctuator.cs:21,22](src/FinLearn.Core/Services/RandomPriceFluctuator.cs#L21) — 同様に `GameRules.PriceFluctuation.*` に置換。

**保持**: `ComputerTrader.TraderIdPrefix`（識別子であって調整値ではない）、computer order の `quantity: 1`（数量はゲームルールであって閾値ではない）。

## 2. Api層 — `appsettings.json` + Options パターン

`GameConfig` は既に DI 登録済み singleton。**コンストラクタ・シグネチャを変えずに** appsettings から差し替え可能にするため、プロパティを `{ get; set; }` 化し、`IOptions<T>.Value` を singleton として再登録する shim を採用（既存の `GameStore(GameConfig config)` ctor を変更せずに済む）。

**修正**: [src/FinLearn.Api/appsettings.json](src/FinLearn.Api/appsettings.json) にセクション追加:

```json
{
  "Logging": { ... 既存 ... },
  "AllowedHosts": "*",
  "Game":      { "InstrumentCount": 3, "InitialPrice": 100, "Fee": 10 },
  "Admin":     { "DefaultPageSize": 50, "MaxPageSize": 200 },
  "GameStore": { "MaxRecentTrades": 3 },
  "OrderLog":  { "RetainedFileCountLimit": 7 }
}
```

**修正**: [src/FinLearn.Api/Services/GameConfig.cs](src/FinLearn.Api/Services/GameConfig.cs) — プロパティを `{ get; set; }` に。デフォルト値は維持（`appsettings.json` の `Game` セクション欠損時のフォールバック）。

**新規作成** (POCO、それぞれ独立ファイル):
- `src/FinLearn.Api/Services/AdminConfig.cs` — `DefaultPageSize`, `MaxPageSize`
- `src/FinLearn.Api/Services/GameStoreConfig.cs` — `MaxRecentTrades`
- `src/FinLearn.Api/Services/OrderLogConfig.cs` — `RetainedFileCountLimit`

**修正**: [src/FinLearn.Api/Program.cs](src/FinLearn.Api/Program.cs)
- Serilog 初期化を `builder.Host.UseSerilog((ctx, lc) => lc...)` 形式に変更し、`ctx.Configuration.GetValue<int>("OrderLog:RetainedFileCountLimit", 7)` で読み込み（現状の bootstrap-logger は `builder` 構築前なので `appsettings.json` を読めないため）。
- DI 登録に追加:
  ```csharp
  builder.Services.Configure<GameConfig>(builder.Configuration.GetSection("Game"));
  builder.Services.Configure<AdminConfig>(builder.Configuration.GetSection("Admin"));
  builder.Services.Configure<GameStoreConfig>(builder.Configuration.GetSection("GameStore"));
  // 既存ctor互換のための shim (IOptions<T>.Value を直接注入できるように)
  builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GameConfig>>().Value);
  builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AdminConfig>>().Value);
  builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<GameStoreConfig>>().Value);
  ```
- 既存の `builder.Services.AddSingleton<GameConfig>();` は削除（shim に置換）。

**修正**: [src/FinLearn.Api/Endpoints/AdminEndpoints.cs:8-9](src/FinLearn.Api/Endpoints/AdminEndpoints.cs#L8) — `public const` を削除、ハンドラに `AdminConfig admin` パラメータを追加（Minimal API は DI バインドする）。

**修正**: [src/FinLearn.Api/Services/GameStore.cs:11-17,48](src/FinLearn.Api/Services/GameStore.cs#L11) — private `MaxRecentTrades` 削除、ctor に `GameStoreConfig` を追加して `_storeConfig.MaxRecentTrades` を参照。

## 3. Frontend層 — `config.ts` と `lib/enums.ts`

**新規作成**: [frontend/app/config.ts](frontend/app/config.ts)

```typescript
export const API_BASE_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5088";
export const DEFAULT_TIMEOUT_MS = 8000;
export const ORDERBOOK_PAGE_SIZE = 20;
```

**新規作成**: [frontend/app/lib/enums.ts](frontend/app/lib/enums.ts) — `as const` オブジェクト + 派生 union 型（JSX の比較式でランタイム値が必要なため bare union ではなくこちらを採用）。

```typescript
export const TradeIntent = { Buy: "buy", Sell: "sell", Wait: "wait" } as const;
export type TradeIntent = typeof TradeIntent[keyof typeof TradeIntent];

export const OrderSide = { Buy: "Buy", Sell: "Sell" } as const;  // API casing
export type OrderSide = typeof OrderSide[keyof typeof OrderSide];

export const OrderType = { Limit: "Limit", Market: "Market" } as const;
export type OrderType = typeof OrderType[keyof typeof OrderType];
```

`TradeIntent`（小文字 form intent）と `OrderSide`（大文字 API enum）は意図的に別物として扱う — 既存の casing ミスマッチをそのまま温存（API コントラクト変更を伴わないため）。

**修正対象**:
- [frontend/app/api/gameApi.ts:3-4](frontend/app/api/gameApi.ts#L3) — ローカルの `BASE`/`DEFAULT_TIMEOUT_MS` 削除、`~/config` から import。
- [frontend/app/components/OrderBookPanel.tsx:6,110,117,118](frontend/app/components/OrderBookPanel.tsx#L6) — `ORDERBOOK_PAGE_SIZE` を `~/config` から import に変更。`order.side === "Buy"`、`order.type === "Limit"` を `OrderSide.Buy` / `OrderType.Limit` 比較に置換。
- [frontend/app/routes/games.$id.tsx:22](frontend/app/routes/games.$id.tsx#L22) — `ORDERBOOK_PAGE_SIZE` の import 元を `~/components/OrderBookPanel` から `~/config` に変更。intent / side 比較も enums を使用。
- [frontend/app/components/TradeForm.tsx](frontend/app/components/TradeForm.tsx) — `intent="buy"|"sell"|"wait"` を `TradeIntent.*` に。
- [frontend/app/components/TradeHistory.tsx](frontend/app/components/TradeHistory.tsx) — `trade.side === "Buy"` を `OrderSide.Buy` に。

## 4. 対象外（触らない）

- バリデーション・リテラル: `quantity > 0`, `price > 0`, `pageValue < 1` 等 — 不変条件であって調整値ではない。
- HTTP ステータスコード（400/404/500）、`gameApi.ts` の `ERROR_MESSAGES`、`Messages.cs` の日本語メッセージ。
- launchSettings.json のポート（5088, 7079）、Vite dev server のポート（5173）。
- CORS フォールバック origin — 既に `CORS_ALLOWED_ORIGINS` 環境変数で差し替え可能。
- ルートパス文字列（`"/api/games"` 等）、JSON プロパティ名。
- `Guid.NewGuid().ToString("N")`、computer order の `quantity: 1`（ゲームルール）。
- **テストコード内の `10000` 等の数値リテラル** — 観測される振る舞いを assert しているもので、定数化するとテストの自己完結性が崩れる。インラインのまま残す。

## 5. 実装順序と検証

各層完了時にテストを必ず通す（一括ではなく層ごとに）:

1. **Core層**
   - `GameRules.cs` 追加 → 3ファイル修正
   - `dotnet test tests/FinLearn.Tests/FinLearn.Tests.csproj`
   - `dotnet test tests/FinLearn.Api.Tests/FinLearn.Api.Tests.csproj`（Api テストも Core を経由するためここで一緒に通す）

2. **Api層**
   - 4つの POCO 追加 → `appsettings.json` 修正 → `Program.cs` の DI 配線変更 → `AdminEndpoints` / `GameStore` 修正
   - `dotnet test tests/FinLearn.Api.Tests/FinLearn.Api.Tests.csproj`
   - 手動スモーク: `dotnet run --project src/FinLearn.Api` で `POST /api/games` → 銘柄3件・初期価格100・手数料10 が返ることを確認

3. **Frontend層**
   - `config.ts`, `lib/enums.ts` 追加 → 5ファイル修正
   - `cd frontend && npm run typecheck && npm test`
   - `npm run dev` で起動して、注文板ページネーション・買い/売り/待機の挙動が回ることを目視確認

## 6. リスクと注意点

- **`Random.Next` の上限は exclusive**: 定数名に `…MaxPercentExclusive` を含めること。誤って 105/115 にすると分布が1ポイントずれる。
- **`GameConfig` 初期化子**: `appsettings.json` に `Game` セクションが無い場合でも、プロパティ初期化子（`= 3` 等）でフォールバックされる。両方残すこと。
- **DI shim を採用する理由**: `GameStore` の ctor は `GameConfig` を直接取る既存契約。`IOptions<T>.Value` シングルトン登録によりコンストラクタを変えずに移行できる。これにより `WebApplicationFactory<Program>` を使う統合テストの override も無修正で済む。
- **Serilog の bootstrap-logger 問題**: 現状の `Log.Logger = ...` は `WebApplication.CreateBuilder` より前で実行されるため `builder.Configuration` を読めない。`builder.Host.UseSerilog((ctx, lc) => ...)` 形式に書き換える（推奨）。書き換え後は try/finally の構造を保ち、`Log.CloseAndFlush()` も維持する。
- **テスト内の `10000` リテラル**: 5つのテストファイル（`PlayerTests.cs`, `GameTests.cs`, `TurnProcessorTests.cs`, `TurnProcessorLoggingTests.cs`, `GameApiTests.cs`）にあるが、Player の InitialCash 参照ではなく観測値の assert として使われている。Production コードのみリファクタし、テストはそのまま。
- **`ORDERBOOK_PAGE_SIZE` の外部参照**: [frontend/app/routes/games.$id.tsx:22](frontend/app/routes/games.$id.tsx#L22) が `OrderBookPanel` から import している。移動と同時に import 元を `~/config` に書き換えること（再 export は残さない — 1箇所完結のほうが綺麗）。
- **循環依存なし**: `GameRules` は `FinLearn.Core` 内に置き、Core 内・Api からのみ参照。新しい依存方向は発生しない。

## Critical Files

**新規**:
- `src/FinLearn.Core/GameRules.cs`
- `src/FinLearn.Api/Services/AdminConfig.cs`
- `src/FinLearn.Api/Services/GameStoreConfig.cs`
- `src/FinLearn.Api/Services/OrderLogConfig.cs`
- `frontend/app/config.ts`
- `frontend/app/lib/enums.ts`

**修正**:
- `src/FinLearn.Core/Models/Player.cs`
- `src/FinLearn.Core/Services/ComputerTrader.cs`
- `src/FinLearn.Core/Services/RandomPriceFluctuator.cs`
- `src/FinLearn.Api/Program.cs`
- `src/FinLearn.Api/appsettings.json`
- `src/FinLearn.Api/Services/GameConfig.cs`
- `src/FinLearn.Api/Services/GameStore.cs`
- `src/FinLearn.Api/Endpoints/AdminEndpoints.cs`
- `frontend/app/api/gameApi.ts`
- `frontend/app/components/OrderBookPanel.tsx`
- `frontend/app/components/TradeForm.tsx`
- `frontend/app/components/TradeHistory.tsx`
- `frontend/app/routes/games.$id.tsx`
