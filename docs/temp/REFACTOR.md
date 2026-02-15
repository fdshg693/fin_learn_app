3) **`PositionSet` の不変・正規化ロジックと `Portfolio` の再構築の二重化（中）**  
`PositionSet` は内部で正規化（銘柄IDで集約・ソート）していますが、`Portfolio` 側で既に銘柄ごとに集約した `newPositions` を作るなど二重の努力になっています。`PositionSet` APIを整備して、組み立てを一箇所に寄せると単純化できます。  
該当: `src/MyApp.Core/PositionSet.cs`, `src/MyApp.Core/Portfolio.cs`

4) **テストのDRY不足（中）**  
`PortfolioTests` と `PositionSetTests` で `Instrument`/`Position`/`TestExchange`/`Portfolio` の生成が大量に重複しています。  
- テストヘルパ（`CreateExchange`、`CreatePortfolio`、`CreatePosition`）  
- `[Theory]` + `InlineData` の導入（数量/現金/結果パターンの共通化）  
で可読性と保守性が上がります。  
該当: `tests/MyApp.Tests/PortfolioTests.cs`, `tests/MyApp.Tests/PositionSetTests.cs`, `tests/MyApp.Tests/PlayerTests.cs`

5) **`Portfolio.TotalAmount` と `PositionSet.Amount` の機能重複（中）**  
`PositionSet` に合計評価額があるのに、`Portfolio.TotalAmount` では `Positions.Sum` を再度書いています。`PositionSet.Amount` の再利用で一貫性が上がります。  
該当: `src/MyApp.Core/Portfolio.cs`, `src/MyApp.Core/PositionSet.cs`

6) **`Player.Buy` の戻り値設計（低〜中）**  
`Buy` が `(Player Result, string? Warning)` を返し、常に `this` を返しています。戻り値の冗長さや誤解（新しいPlayerが返るのか）を避けるなら `string? Warning` だけ返す/`bool`+`out` へ整理が可能です。  
該当: `src/MyApp.Core/Player.cs`, `tests/MyApp.Tests/PlayerTests.cs`