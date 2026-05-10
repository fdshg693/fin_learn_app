# コンピューター注文の現状仕様

コンピューター注文（`ComputerTrader`）の生成・マッチング・約定に関する詳細仕様。

## コンピューターは仮想プレイヤー

`computer1` 〜 `computer10` は単なる注文生成器ではなく、**各自が `Portfolio` を保持する仮想プレイヤー**として扱う。約定が発生すると当事者の Portfolio に `ApplyTrade` が呼ばれる（プレイヤーと対称）。

- 保管場所: `Game.ComputerPortfolios`（型 `IReadOnlyDictionary<string, Portfolio>`、key は `"computer1"` 〜 `"computer10"`）
- 初期値: 全員 `Portfolio.CreateInfinite()` で作成された「∞モード」Portfolio
- ∞モードの挙動: `ApplyTrade` がノーオペとなり、現金 (`Cash = int.MaxValue`) と保有数量 (常に 0) は加減算後も**完全に不変**（∞ ± n = ∞）。検証（残高不足・保有不足）もスキップされるため、コンピューターはどんな価格・数量でも自由に発注できる
- 将来 ∞モードを外せば、`Portfolio.Buy/Sell` の通常検証ロジックがそのまま適用される（既存プレイヤー側のロジックと同一）

## 注文生成

毎ターン、プレイヤー注文の**前**に `ComputerTrader.PlaceOrders` が呼ばれ、`computer1` 〜 `computer10` の10プレイヤーが各自 買い1件・売り1件 ずつ計20件の指値注文を生成する。

| 項目 | 買い注文 | 売り注文 |
|---|---|---|
| 件数 | 10件（各 `computer{i}` から1件） | 10件（各 `computer{i}` から1件） |
| 数量 | 各1株 | 各1株 |
| 価格 | `Max(1, 市場価格 * [85..105] / 100)` | `Max(1, 市場価格 * [95..115] / 100)` |
| 銘柄 | 全銘柄からランダム選択 | 全銘柄からランダム選択 |
| 注文者ID | `"computer1"` 〜 `"computer10"` | `"computer1"` 〜 `"computer10"` |

- `IExchange.TryGetPrice` が `false` の銘柄はスキップ
- `Random` インスタンスを外部注入（シード固定でテスト決定性を確保）
- TraderId は `ComputerTrader.IsComputerTrader(string)` で識別（`computer{i}` プレフィックス）
- コンピューター注文は `GameRules.DefaultOrderTtl`（デフォルト 2 ターン）で `ExpiresAtTurn` を設定（`createdAtTurn + DefaultOrderTtl`）

## マッチング方式

注文は **1件ずつ** `OrderBook.Match` → 未約定分 `Add` のサイクルで処理される。

```
各注文について:
  1. order を生成
  2. fillResult = book.Match(order)       — 板の反対側とマッチング
  3. 約定分は板から消える（FillResult.UpdatedBook）
  4. 未約定分があれば board.Add(order.WithQuantity(remaining))
```

処理順序は **買い注文10件 → 売り注文10件** の固定順。売り注文の処理時には、先に板に乗った買い注文がマッチング相手になりうる。

各 `Match` 後、`FillResult.Fills` に含まれる `OrderFill` のうち TraderId が `computer{i}` のものについて、対応する Portfolio に `TradeResult` を構築して `ApplyTrade` する（incoming 側・resting 側の両方）。

- `Order` の `TraderId` / `Side` / `Instrument` は注文IDから逆引き（既存板＋本ターン新規発注のマップ）
- 板上のプレイヤーの resting order が computer 注文と約定した場合は ComputerTrader 側では touch せず、現状はその対応が無い（後述「未解決」）
- 手数料は `exchange.Fee` をそのまま適用（∞モード下では効果なし）

### コンピューター注文同士の約定

TraderIdが `computer1` 〜 `computer10` で分かれているため、買い価格帯（85〜105%）と売り価格帯（95〜115%）が重なる範囲では、同一ターン内で別プレイヤー同士のコンピューター注文が約定する（自己約定防止フィルタは同一TraderIdのみブロックするため、別 `computer{i}` 同士は対象になる）。

**例**: 市場価格100の場合

```
買い@105 → 板に追加（売り注文がまだないため）
  ...（他の買い注文も同様に追加）
売り@95  → Match実行 → 買い@105 >= 売り@95 → 約定（約定価格=板側の105）
売り@97  → Match実行 → 買い@104 >= 売り@97 → 約定（約定価格=104）
  ...交差する注文が順次約定
売り@110 → マッチする買い注文なし → 板に追加
```

結果として、板には交差しない（=現実的な）注文のみが残る。

### 異なるターン間の約定

板（`OrderBook`）はターン間で永続する。前ターンの未約定注文と新ターンのコンピューター注文もマッチングされる。

```
ターン1: コンピューター売り@110 が未約定 → 板に残る
ターン2: コンピューター買い@112 を生成 → Match → 売り@110 と約定（価格110）
```

## プレイヤー注文 vs コンピューター注文の約定

プレイヤー注文が板上の computer の resting order と約定した場合、`TurnProcessor.PlaceOrder` で以下を行う:

1. `Market.Execute` の結果 `MatchResult.Fills` を走査
2. incoming（プレイヤー注文）以外の各 OrderFill について、resting order の TraderId が `computer{i}` ならその Portfolio に `ApplyTrade`
3. 更新後の `ComputerPortfolios` を `AdvanceTurn` 経由で次の `Game` に引き渡す

ロールバック分岐（残高不足等）では player と同じ理由で computer 側も適用前に巻き戻す。

## プレイヤー注文との関係

コンピューター注文のマッチングは `TurnProcessor` のフロー上、プレイヤー注文の**前**に完了する:

```
1. ComputerTrader.PlaceOrders  — コンピューター注文20件を1件ずつMatch＋Add
2. Player.CreateOrder          — プレイヤー注文を生成
3. Market.Execute              — プレイヤー注文を板でマッチング
```

プレイヤーがマッチングする時点では、コンピューター同士で約定済みの注文はすでに板から消えている。残っている注文のみがプレイヤーのマッチング相手となる。

## マッチングの公平性

`OrderBook.Match` は `TraderId` を一切参照しない。マッチング条件は純粋に価格のみ:

- **価格条件**: 買い価格 >= 売り価格
- **優先順位**: 価格優先（売りは安い順、買いは高い順）
- **約定価格**: 常に板側（待機注文）の価格

コンピューター注文もプレイヤー注文も、すべて `OrderBook.Match` を通る対称的な設計。

## 板への影響

コンピューター同士のマッチングにより:

- **交差する注文が自然に消化される** — 買い@105 と 売り@95 が板に同時に残ることはない
- **板の注文数はターンごとに20件未満** — 約定分が差し引かれるため
- **板の流動性は現実的な価格帯に集中する** — 非現実的な価格の注文は即座に約定して消える

## 実装箇所

| ファイル | 役割 |
|---|---|
| `src/FinLearn.Core/Services/ComputerTrader.cs` | 注文生成 + `PlaceWithMatching` でマッチング + 当事者 Portfolio への `ApplyTrade` |
| `src/FinLearn.Core/Models/OrderBook.cs` | `Match(Order)` — 価格ベースのマッチング |
| `src/FinLearn.Core/Results/FillResult.cs` | `GetFill(orderId)` — 約定結果の取得 |
| `src/FinLearn.Core/Models/Order.cs` | `WithQuantity(int)` — 未約定分の注文生成 |
| `src/FinLearn.Core/Models/Portfolio.cs` | `Portfolio.CreateInfinite()` — ∞モードの Portfolio 生成（ApplyTrade ノーオペ） |
| `src/FinLearn.Core/Game.cs` | `Game.ComputerPortfolios` — computer1〜10 の Portfolio を保管 |
| `src/FinLearn.Core/TurnProcessor.cs` | プレイヤー注文 vs computer resting order の約定を Portfolio に反映 |

## 未解決

- 板上の **プレイヤー** resting order が computer 注文と約定したケース（`ComputerTrader.PlaceOrders` の中で発生）は、現状プレイヤー Portfolio に反映されない（変更前から残る既知の不整合）。今回のスコープ外。
