# Trade Result Logging Implementation Plan

**Goal:** ターン進行時に取引所へ届いた全注文と発生した全約定を Serilog 経由でファイルログ（CompactJson, 日次, 7日保持）として永続化する。

**Architecture:** `FinLearn.Core` は Serilog を一切知らない純粋ドメイン層に保つため、`TurnProcessor.Buy/Sell/Wait` の戻り値を `TurnResult` レコード（提出注文・約定明細・処理ターンを含む）に置き換える。`FinLearn.Api` 側で Serilog を構成し、`GameEndpoints` の `ProcessOrder` ヘルパーから 1 ターンあたり 2 イベント（OrdersSubmitted / OrdersMatched）を出力する。

**Tech Stack:** .NET 9, C#, xUnit, Serilog.AspNetCore, Serilog.Sinks.Console, Serilog.Sinks.File, Serilog.Formatting.Compact

**Spec:** [docs/superpowers/specs/2026-04-26-trade-result-logging-design.md](../superpowers/specs/2026-04-26-trade-result-logging-design.md)

---

## File Structure

| 新規 / 変更 | パス | 責務 |
|---|---|---|
| 変更 | `src/FinLearn.Core/Results/MatchResult.cs` | `Fills` プロパティを追加 |
| 変更 | `src/FinLearn.Core/Services/Market.cs` | `MatchResult` に `fillResult.Fills` を渡す |
| 変更 | `src/FinLearn.Core/Services/IOrderPlacer.cs` | 戻り値に `PlacedOrders` を追加 |
| 変更 | `src/FinLearn.Core/Services/ComputerTrader.cs` | 板に追加した注文をリスト化して返す |
| 変更 | `tests/FinLearn.Tests/NoOpOrderPlacer.cs` | 新シグネチャに合わせて空配列を返す |
| 新規 | `src/FinLearn.Core/Results/TurnResult.cs` | ターン処理結果（Game + Trade + Warning + ProcessedTurn + SubmittedOrders + Fills） |
| 変更 | `src/FinLearn.Core/TurnProcessor.cs` | `Buy/Sell/Wait` 戻り値を `TurnResult` に置換 |
| 変更 | `tests/FinLearn.Tests/TurnProcessorTests.cs` | 既存テストを `TurnResult` プロパティアクセスに移行 |
| 新規 | `tests/FinLearn.Tests/TurnProcessorLoggingTests.cs` | `SubmittedOrders` / `Fills` の集計仕様を検証 |
| 変更 | `src/FinLearn.Api/FinLearn.Api.csproj` | Serilog 4 パッケージを追加 |
| 変更 | `src/FinLearn.Api/Program.cs` | Serilog 構成 + `UseSerilog()` |
| 変更 | `src/FinLearn.Api/Endpoints/GameEndpoints.cs` | `TurnResult` 受領 + `LogTurnEvents` |
| 確認 | `.gitignore` | 既存 `[Ll]ogs/` がログ出力先をカバーしているかを目視確認 |

実装順序は依存に従って Core → Tests → Api。Core 段階で `dotnet build` と `dotnet test` がグリーンになることを各タスクで確認し、Api 段階に進む。

---

## タスク一覧

各タスクは独立した Markdown ファイルに分割しています。上から順に実装してください。

| # | タスク | 概要 | リンク |
|---|---|---|---|
| 1 | MatchResult に Fills を追加し Market から渡す | `MatchResult` レコードに `Fills` を生やし、`Market.Execute` で `fillResult.Fills` を伝播 | [task-01-match-result-fills.md](./trade-result-logging-task-01-match-result-fills.md) |
| 2 | IOrderPlacer の戻り値に PlacedOrders を追加 | インターフェースを 3 タプル化し、`ComputerTrader` / `NoOpOrderPlacer` を追従。`TurnProcessor` は暫定 `_` 破棄 | [task-02-order-placer-signature.md](./trade-result-logging-task-02-order-placer-signature.md) |
| 3 | TurnResult レコードを追加 | `Game + Trade + Warning + ProcessedTurn + SubmittedOrders + Fills` の record を新規定義 | [task-03-turn-result.md](./trade-result-logging-task-03-turn-result.md) |
| 4 | TurnProcessor の戻り値を TurnResult に置換 | `Buy/Sell/Wait` を `TurnResult` 返却に書き換え。既存テストは分解構文を機械的に移行 | [task-04-turn-processor.md](./trade-result-logging-task-04-turn-processor.md) |
| 5 | TurnResult の集計仕様を検証する新規テスト | 仕様 §5.4 のエッジケース表 5 シナリオ + ProcessedTurn を網羅 | [task-05-aggregation-tests.md](./trade-result-logging-task-05-aggregation-tests.md) |
| 6 | Serilog NuGet パッケージを追加 | `FinLearn.Api.csproj` のみ（Core には絶対に入れない） | [task-06-serilog-packages.md](./trade-result-logging-task-06-serilog-packages.md) |
| 7 | Program.cs で Serilog を構成 | Console 人間可読 + File CompactJson、日次ローテ、7日保持。`try/finally` で `CloseAndFlush` | [task-07-program-cs.md](./trade-result-logging-task-07-program-cs.md) |
| 8 | GameEndpoints で OrdersSubmitted / OrdersMatched をログ出力 | `LogTurnEvents` ヘルパーで 1 ターン 2 イベント出力 | [task-08-game-endpoints-logging.md](./trade-result-logging-task-08-game-endpoints-logging.md) |
| 9 | .gitignore がログ出力先をカバーしているか確認 | 既存 `[Ll]ogs/` がカバーしているはず。確認のみで終わるのが期待値 | [task-09-gitignore.md](./trade-result-logging-task-09-gitignore.md) |
| 10 | 仕上げ確認とドキュメント整合 | 全テスト + ログファイル形式の jq 検証 + フロントエンド回帰確認 | [task-10-finalize.md](./trade-result-logging-task-10-finalize.md) |

---

## Out of Scope (YAGNI)

以下は仕様 §10 で明確にスコープ外と宣言されている。本プランも踏襲する:

- ログを Web API 経由で配信する機能
- DB 永続化
- リアルタイム監視・アラート
- ログから取引履歴を復元する機能
- 外部 sink (Sentry / Seq 等)
- ゲームIDごとのファイル分割
- ロギング自体の単体テスト（Serilog 出力検証は脆く価値が薄いため）
