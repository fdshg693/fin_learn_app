# ログ機能 — インフラログ（Serilog）

> プレイヤー向けの約定履歴は [API.UI.md](./API.UI.md)、ドメイン層のログ構造は [LOGIC.md](./LOGIC.md) を参照。

## 目的

`recentTrades` がプレイヤー向けの集約結果なのに対し、本層はサーバー運用・デバッグ用に**ターン内で起きたすべての注文と約定**を構造化ログとして残す。クライアントには公開されない。

## 出力チャネル分離

ASP.NET Core フレームワークのログ（CORS / ルーティング / Kestrel など）と、注文関連のドメインログは出力先が分かれている:

| チャネル | 出力先 | 内容 |
|---|---|---|
| **Console** | 標準出力 | 全ログ（フレームワーク + 注文） |
| **File** | `logs/orders-{Date}.log` | 注文関連のみ（フィルタ済み） |

これにより、ファイルは注文監査用途に特化し、フレームワークの大量ログがファイルを埋め尽くすことがない。

## ロガー構成

[`Program.cs`](../../../src/FinLearn.Api/Program.cs):

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}")
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(Matching.FromSource<OrderLog>())
        .WriteTo.File(
            formatter: new CompactJsonFormatter(),
            path: "logs/orders-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7))
    .CreateLogger();

builder.Host.UseSerilog();
```

| 項目 | 設定 |
|---|---|
| 最低レベル | `Information` |
| エンリッチ | `FromLogContext`（スコープ情報の取り込み） |
| Console シンク | 全ログ。`SourceContext` 込みで人間可読フォーマット |
| File シンク | サブロガー経由で `OrderLog` ソースのみ抽出。Compact JSON（JSONL）形式 |
| ファイル名 | `logs/orders-{Date}.log` |
| ローテーション | 日次 |
| 保持数 | 7 ファイル |

`WriteTo.Logger(...)` でサブパイプラインを作り、その中だけにフィルタを掛けることで、親パイプラインの Console には影響を与えずに File シンクのみ絞り込んでいる。

ASP.NET Core の `ILogger<T>` を経由するため、エンドポイントハンドラから普通に DI でロガーを受け取れる。

## SourceContext マーカー: `OrderLog`

`GameEndpoints` は static class のため `ILogger<GameEndpoints>` は使えない。代わりに、注文関連ログ専用のマーカー型を [`Endpoints/GameEndpoints.cs`](../../../src/FinLearn.Api/Endpoints/GameEndpoints.cs) に定義している:

```csharp
public sealed class OrderLog { }
```

エンドポイントハンドラはこのマーカーを使って `ILogger<OrderLog>` を DI で受け取る。Serilog が `SourceContext = "FinLearn.Api.Endpoints.OrderLog"` を自動で付与し、File シンク側のフィルタ `Matching.FromSource<OrderLog>()` で完全一致でき、ファイル出力対象になる。

注文関連の新しいログを追加したいときは、同じ `ILogger<OrderLog>` を経由させればファイルに自動的に流れる。

## ターンイベントの記録

[`GameEndpoints.LogTurnEvents`](../../../src/FinLearn.Api/Endpoints/GameEndpoints.cs):

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

`ProcessOrder` ヘルパーと `Wait` ハンドラの両方から呼ばれる。買い・売り・Wait のいずれでも 1 ターンにつき 2 行が出力される。

### 出力イベント

| イベント名 | プロパティ | 内容 |
|---|---|---|
| `OrdersSubmitted` | `GameId`, `Turn`, `Count`, `Warning`, `Orders` | 板に届いた全注文（コンピューター + プレイヤー） |
| `OrdersMatched` | `GameId`, `Turn`, `Count`, `Fills` | 発生した全約定明細 |

`{@Orders}` / `{@Fills}` の `@` プレフィックスは Serilog の destructuring 構文で、オブジェクトを文字列化せず構造化プロパティとして JSON に展開する。

### Warning の扱い

`OrdersSubmitted` には `Warning` プロパティを含めるため、ロールバックや成行不成立のような失敗ケースでもサーバーログだけは原因が残る。一方 `Fills` は失敗時に空になる（[LOGIC.md](./LOGIC.md) 参照）ので、`OrdersMatched` の `Count=0` でロールバックを判別できる。

## ログファイルの形式

`CompactJsonFormatter` による JSONL（1 行 1 イベント）。例:

```json
{"@t":"2026-04-26T12:34:56.789Z","@mt":"OrdersSubmitted Game={GameId} Turn={Turn} Count={Count} Warning={Warning} {@Orders}","GameId":"abc","Turn":3,"Count":21,"Warning":null,"Orders":[...],"SourceContext":"FinLearn.Api.Endpoints.OrderLog"}
{"@t":"2026-04-26T12:34:56.790Z","@mt":"OrdersMatched Game={GameId} Turn={Turn} Count={Count} {@Fills}","GameId":"abc","Turn":3,"Count":4,"Fills":[...],"SourceContext":"FinLearn.Api.Endpoints.OrderLog"}
```

`jq` などで集計可能:

```bash
# 特定ゲームの全ターンの約定数を集計
grep '"OrdersMatched"' logs/orders-*.log | jq '.GameId, .Turn, .Count'
```

## クライアント向けログとの違い

| 観点 | `recentTrades`（[API.UI.md](./API.UI.md)） | Serilog（本ドキュメント） |
|---|---|---|
| 対象 | プレイヤーの約定のみ | コンピューター含む全注文・全約定 |
| 形式 | 集約済み `TradeResultDto` | 個別の `Order` / `OrderFill` |
| 保持期間 | 直近 3 件・メモリ上 | 7 日間・ファイル |
| 公開先 | クライアント | サーバーオペレータ |
| 失敗ケースの記録 | なし（追加されない） | あり（`Warning` 込みで残る） |

## 拡張ポイント

- **注文関連の新規ログを追加**: `ILogger<OrderLog>` を経由させればファイルにも自動的に流れる
- **ASP.NET Core 側の Information ログを抑制**: `LoggerConfiguration` に `.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)` を追加する（現状はコンソールに全部出るが、それで十分という方針）
- **ログ出力先を変更**: `LoggerConfiguration` に追加シンクを差し込む。サブロガーごとフィルタする現構造はそのまま維持できる
- **イベント名（`OrdersSubmitted` / `OrdersMatched`）は変更しない**: 集計クエリやアラートが依存する可能性があるため
