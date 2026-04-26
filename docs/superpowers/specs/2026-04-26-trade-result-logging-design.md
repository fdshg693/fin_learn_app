# 取引結果ロギング設計書

- **作成日**: 2026-04-26
- **対象**: `FinLearn.Core` / `FinLearn.Api`
- **目的**: 各ターンに取引所に届いた全注文と、その結果約定された全注文をログファイルに永続化し、後からファイルベースで確認・調査できるようにする

## 1. 動機・用途

主に以下の2用途に絞ってスコープを決定:

1. **デバッグ・障害調査** — 「あのターンで何が起きたか」を後追いするため
2. **テスト・開発時の挙動検証** — 約定ロジックや市場挙動を目視で確認しながら開発を進めるため

ユーザー向け取引履歴 API や教材としての可視化はスコープ外（必要になった時点で別仕様として切り出す）。

## 2. 重要な設計上の制約

`FinLearn.Core` は「外部依存ゼロのドメイン純粋層」という既存原則がある（`.claude/rules/architecture.md` 参照）。本設計はこの原則を維持する。

そのため Serilog や `Microsoft.Extensions.Logging` への依存は **`FinLearn.Api` のみ** に閉じ込め、Core 側はターンごとに「何が起きたか」をデータ（`TurnResult`）として返すだけとする。ログ出力という副作用は Api 層が担う。

## 3. 全体アーキテクチャ

```
[GameEndpoints (Api)]
        │
        │ Buy/Sell/Wait
        ▼
[TurnProcessor (Core)]──→ returns TurnResult {
        │                   Game, TradeResult?, Warning,
        │                   ProcessedTurn,
        │                   SubmittedOrders, Fills
        │                 }
        ▼
[GameEndpoints (Api)]
        │
        │ logger.LogInformation(...)
        ▼
[Serilog]──→ Console (人間可読) + File (CompactJson, 日次, 7日保持)
```

**鍵となる原則:**

- `FinLearn.Core` は Serilog/`ILogger` を一切知らない
- Core が返すのは「実際にゲーム状態に確定した結果」のデータ
- ログ機能の有無は Core のテストに影響しない

## 4. 採用した方針（一覧）

| 論点 | 採用案 |
|---|---|
| ロギングの組み込み位置 | (c) Core を触らず、戻り値を拡張して Api 側でログ |
| ログイベントの粒度 | (a) ターン単位で2イベント（OrdersSubmitted / OrdersMatched） |
| ファイル分け方 | (a) 単一ファイル + 日次ローテーション |
| 出力フォーマット | (c) コンソール=人間可読、ファイル=CompactJson |
| リテンション | (b) 7日で自動削除 (`retainedFileCountLimit: 7`) |
| Warning の扱い | OrdersSubmitted イベント内に含める（2イベントを維持） |

## 5. Core 側の変更

### 5.1 新規型: `TurnResult`

```csharp
public sealed record TurnResult(
    Game Game,
    TradeResult? Trade,
    string? Warning,
    int ProcessedTurn,
    IReadOnlyList<Order> SubmittedOrders, // そのターンに板に届いた全注文
    IReadOnlyList<OrderFill> Fills);      // そのターンに発生した全約定明細
```

`TurnProcessor.Buy` / `Sell` / `Wait` の戻り値を従来のタプルからこの record に置き換える。`ProcessedTurn` を明示することで Api 側がターン番号を計算する必要をなくす。

### 5.2 `IOrderPlacer.PlaceOrders` のシグネチャ拡張

```csharp
// before
(OrderBook, int nextId) PlaceOrders(...)

// after
(OrderBook book, int nextId, IReadOnlyList<Order> placedOrders) PlaceOrders(...)
```

`ComputerTrader` は実際に板に追加した注文をそのままリスト化して返す（既に内部で生成しているのでコストはわずか）。

### 5.3 `MatchResult` に全約定明細を追加

```csharp
public sealed record MatchResult(
    TradeResult Trade,
    OrderBook UpdatedBook,
    IReadOnlyList<OrderFill> Fills); // 追加
```

`Market.Execute` 内で既に `fillResult.Fills` を取得しているのでそのまま渡す。

### 5.4 `TurnProcessor` での集計ルール

- `submittedOrders` = `placedOrders`（コンピューター注文）+ プレイヤー注文（作成された場合）
- `fills` = `matchResult.Fills`（または空配列）

エッジケースの扱い:

| シナリオ | SubmittedOrders | Fills | Warning |
|---|---|---|---|
| Wait | コンピューター注文のみ | 空 | null |
| Buy/Sell 引数バリデーション失敗 (qty<=0 等) | 空 | 空 | エラーメッセージ |
| Buy/Sell 成行・約定ゼロ | コンピューター + プレイヤー | 空 | NoMatching... |
| Buy/Sell 残高不足 | コンピューター + プレイヤー | **空** (ロールバック扱い) | エラーメッセージ |
| Buy/Sell 通常約定 | コンピューター + プレイヤー | 全約定明細 | null |

「残高不足等で約定をロールバックする場合は Fills を空にする」という判断は、ログ＝確定事実の対応関係を保つため。失敗した約定試行は Warning フィールドから間接的に追える。

## 6. Api 側のロギング実装

### 6.1 追加 NuGet パッケージ（`FinLearn.Api` のみ）

| パッケージ | 用途 |
|---|---|
| `Serilog.AspNetCore` | ASP.NET Core 統合（`builder.Host.UseSerilog()`） |
| `Serilog.Sinks.Console` | コンソール出力 |
| `Serilog.Sinks.File` | ファイル出力 |
| `Serilog.Formatting.Compact` | `CompactJsonFormatter`（1行=1JSON） |

`FinLearn.Core` には何も追加しない。

### 6.2 `Program.cs` での Serilog 構成

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}")
    .WriteTo.File(
        formatter: new CompactJsonFormatter(),
        path: "logs/finlearn-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();
```

コードベースで構成（環境差し替えが必要になったら `ReadFrom.Configuration` に移行可能）。

### 6.3 `GameEndpoints` でのログ出力

`ProcessOrder` ヘルパー内に集約し、Buy/Sell/Wait での重複を防ぐ:

```csharp
private static void LogTurnEvents(ILogger logger, string gameId, TurnResult result)
{
    logger.LogInformation(
        "OrdersSubmitted Game={GameId} Turn={Turn} Count={Count} Warning={Warning} {@Orders}",
        gameId, result.ProcessedTurn, result.SubmittedOrders.Count,
        result.Warning, result.SubmittedOrders);

    logger.LogInformation(
        "OrdersMatched Game={GameId} Turn={Turn} Count={Count} {@Fills}",
        gameId, result.ProcessedTurn, result.Fills.Count, result.Fills);
}
```

`ILogger<Program>` を Minimal API のハンドラーパラメータとして注入。

## 7. ログイベントスキーマ

### 7.1 OrdersSubmitted

| プロパティ | 型 | 説明 |
|---|---|---|
| GameId | string | ゲームID |
| Turn | int | 処理されたターン |
| Count | int | 届いた注文の件数 |
| Warning | string? | エラーメッセージ（成功時 null） |
| Orders | Order[] | 注文オブジェクト配列（`@` で構造化シリアライズ） |

### 7.2 OrdersMatched

| プロパティ | 型 | 説明 |
|---|---|---|
| GameId | string | ゲームID |
| Turn | int | 処理されたターン |
| Count | int | 約定明細の件数 |
| Fills | OrderFill[] | 約定明細配列（OrderId, FilledQuantity, TotalAmount） |

### 7.3 出力例

**コンソール:**

```
10:23:45 [INF] OrdersSubmitted Game="abc123" Turn=5 Count=21 Warning=null Orders=[Order { Id: 100, ... }, ...]
10:23:45 [INF] OrdersMatched Game="abc123" Turn=5 Count=4 Fills=[OrderFill { OrderId: 100, FilledQuantity: 10, TotalAmount: 1050 }, ...]
```

**ファイル (CompactJson、整形して表示):**

```json
{
  "@t": "2026-04-26T10:23:45.123Z",
  "@m": "OrdersSubmitted Game=\"abc123\" Turn=5 Count=21 ...",
  "GameId": "abc123",
  "Turn": 5,
  "Count": 21,
  "Warning": null,
  "Orders": [
    { "Id": 100, "TraderId": "computer", "Instrument": { "Id": 1 },
      "Side": "Buy", "Type": "Limit", "Quantity": 10, "Price": 95,
      "StopPrice": null, "CreatedAtTurn": 5 }
  ]
}
```

### 7.4 デバッグでの jq 利用例

```bash
# ターン5の全注文を見る
jq 'select(.Turn == 5 and (.["@m"] | startswith("OrdersSubmitted")))' logs/finlearn-20260426.log

# 特定銘柄の約定だけ抽出
jq 'select(.Fills != null) | .Fills[] | select(.OrderId == 100)' logs/finlearn-20260426.log

# ワーニング発生ターンだけ
jq 'select(.Warning != null)' logs/finlearn-20260426.log
```

## 8. ファイル配置

| 新規/変更 | パス | 内容 |
|---|---|---|
| 新規 | `src/FinLearn.Core/Results/TurnResult.cs` | 新しい戻り値レコード |
| 変更 | `src/FinLearn.Core/Services/IOrderPlacer.cs` | `PlaceOrders` の戻り値拡張 |
| 変更 | `src/FinLearn.Core/Services/ComputerTrader.cs` | 配置した注文リストを返す |
| 変更 | `src/FinLearn.Core/Results/MatchResult.cs` | `Fills` プロパティ追加 |
| 変更 | `src/FinLearn.Core/Services/Market.cs` | `MatchResult` に `fillResult.Fills` を渡す |
| 変更 | `src/FinLearn.Core/TurnProcessor.cs` | `TurnResult` を組み立てて返す |
| 変更 | `src/FinLearn.Api/Program.cs` | Serilog 構成 + `UseSerilog()` |
| 変更 | `src/FinLearn.Api/Endpoints/GameEndpoints.cs` | `LogTurnEvents` 呼び出し |
| 変更 | `.gitignore` | `**/logs/` を追記 |

ログ出力先は `src/FinLearn.Api/logs/finlearn-{date}.log`（カレントディレクトリ基準）。

## 9. テスト戦略

### 9.1 Core 単体テスト (`FinLearn.Tests`)

- 既存の `TurnProcessor` 戻り値分解箇所を `TurnResult` プロパティアクセスに置換
- `IOrderPlacer` のテストダブルを新シグネチャに対応
- 新規テスト:
  - `Buy` 後の `TurnResult.SubmittedOrders` にコンピューター注文 + プレイヤー注文が含まれる
  - `TurnResult.Fills` が `MatchResult.Fills` と一致する
  - 残高不足ワーニング時に `Fills` が空配列になる
  - `Wait` で `SubmittedOrders` がコンピューター注文のみになる
  - 引数バリデーション失敗時に `SubmittedOrders` も `Fills` も空になる

### 9.2 Api 統合テスト (`FinLearn.Api.Tests`)

- ロギング自体はテストしない（Serilog 出力のテストは脆く価値が薄い）
- 既存のレスポンス形状は変わらないため、既存テストは無修正で通る想定

## 10. スコープ外 (YAGNI)

- ログを Web API 経由で配信する機能
- DB 永続化
- リアルタイム監視・アラート
- ログから取引履歴を復元する機能
- 外部 sink (Sentry / Seq 等)
- ゲームIDごとのファイル分割（必要になったら `Map` シンクで拡張可能）
