## Tests

2 つのテストプロジェクトで構成。xUnit 2.9.2 + .NET 9.0。

### プロジェクト構成

| Project | Purpose | Test Count |
|---|---|---|
| `FinLearn.Tests` | Core ドメインモデルの単体テスト | 13 クラス、100+ メソッド |
| `FinLearn.Api.Tests` | Minimal API の統合テスト | 1 クラス、11 メソッド |

### テストヘルパー・テストダブル

| File | Description |
|---|---|
| `FinLearn.Tests/TestData.cs` | 共通テストデータ（Instrument1/2、CreateExchange ファクトリ） |
| `FinLearn.Tests/TestExchange.cs` | `IExchange` テストダブル（固定価格辞書 + 設定可能な手数料） |
| `FinLearn.Tests/NoPriceFluctuator.cs` | `IPriceFluctuator` テストダブル（価格変動なし、テスト分離用） |

### テスト規約

- **日本語テスト名**: `[Fact] public void 購入成功でターンが1進む()`
- **不変性検証**: 操作後に元のオブジェクトが変更されていないことをアサート
- **決定的乱数**: `new Random(seed)` でシード固定して再現性確保（ComputerTrader、PriceFluctuator）
- **API 統合テスト**: `WebApplicationFactory<Program>` + `IClassFixture` パターン

### テストダブルの使い分け

- **TestExchange** — 単体テストで固定価格・手数料を注入する場合
- **SimpleExchange** — 統合テストやプロダクション挙動を確認する場合
- **NoPriceFluctuator** — 価格変動を除外してターン処理ロジックをテストする場合

### 実行コマンド

```shell
dotnet test                                        # 全テスト実行
cd tests/FinLearn.Tests && dotnet watch test        # TDD ウォッチモード
```

<!-- Last updated by agent: 2026-03-08 -->
