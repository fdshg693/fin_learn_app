# STEP4: InMemoryデータとシード

## 目的
API実装前に、Domainモデルで扱うデータをインメモリで用意し、シードとして利用可能にする。

## 実施内容
1. InMemoryストア作成
   - `InMemoryStore` で会社・銘柄・投資家・ポートフォリオを保持。
   - `FindTicker` / `FindPortfolioByInvestor` 等の簡易参照を提供。
2. シード作成
   - `SeedData.Create()` でデモデータを生成。
   - 会社3件、銘柄3件、投資家1件、ポートフォリオ1件。
3. DI登録
   - `Program.cs` で `SeedData.Create()` をSingleton登録。

## 追加・更新ファイル
- backend/FinLearnApp.Api/Data/InMemoryStore.cs
- backend/FinLearnApp.Api/Data/SeedData.cs
- backend/FinLearnApp.Api/Program.cs

## 補足
- 価格・保有数はデモ用の固定値。
- 今後のSTEP5でコントローラから参照してAPIを実装する。
