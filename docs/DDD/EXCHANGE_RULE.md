# 取引ルール

## 手数料

- **固定手数料制**: 1取引あたり固定額（JPY）を徴収
- 手数料は `TurnProcessor` の各メソッド (`Buy`/`Sell`/`Wait`) に `int fee` パラメータとして注入
- **買い**: `新キャッシュ = 現金 - 約定金額 - 手数料`
- **売り**: `新キャッシュ = 現金 + 約定金額 - 手数料`
- 買いバリデーション: `現金 < 約定金額 + 手数料` で拒否（手数料込みチェック）

## 注文の種類

| 種類 | 価格指定 | ストップ価格 | マッチング条件 |
|---|---|---|---|
| 指値 (`Limit`) | `int Price > 0` | なし | 買い: `買価格 >= 売価格`, 売り: `売価格 <= 買価格` |
| 成行 (`Market`) | `null` | `int? StopPrice`（任意） | 下記参照 |

### 成行注文のストップ価格

成行注文にはオプショナルな `StopPrice` を設定でき、不利な約定を防止する:

- **買い + StopPrice**: `StopPrice` **以下**の売り注文のみ約定（上限ガード）
- **売り + StopPrice**: `StopPrice` **以上**の買い注文のみ約定（下限ガード）
- **StopPrice 未指定**: 反対側の全注文とマッチ（価格条件なし）

例: 市場価格100の銘柄で売り板に `[100, 100, 500]` がある場合、`StopPrice=200` の成行買い3株では `100 + 100 = 200` のみ約定し、500の売り注文はスキップされる。

### バリデーション

- 数量は `int Quantity > 0` 必須（`Order` コンストラクタで `ArgumentException`）
- 指値注文の価格は `> 0` 必須
- ストップ価格は `> 0` 必須（指定時）

## コンピューター注文生成（`ComputerTrader`）

毎ターン、プレイヤー注文の**前**に自動生成される。注文は1件ずつ `OrderBook.Match` を通し、約定しなかった残りだけ板に追加する。

> 詳細: [docs/FEATURES/COMPUTER_ORDER/CURRENT.md](../FEATURES/COMPUTER_ORDER/CURRENT.md)

| 項目 | 買い注文 | 売り注文 |
|---|---|---|
| 件数 | 10件 | 10件 |
| 数量 | 各1株 | 各1株 |
| 価格 | `Max(1, 市場価格 * [85..105] / 100)` | `Max(1, 市場価格 * [95..115] / 100)` |
| 銘柄 | ランダム分散 | ランダム分散 |
| 注文者ID | `"computer"` | `"computer"` |

- 買い価格帯（85〜105%）と売り価格帯（95〜115%）が重なるため、コンピューター同士でも約定が発生する
- `IExchange.TryGetPrice` が `false` の銘柄はスキップ（価格不明・価格0以下）
- `Random` インスタンスを外部注入（シード固定でテスト決定性を確保）

## 注文の有効期限

注文には `CreatedAtTurn`（作成ターン）が記録され、一定ターン経過後に自動的に板から除去される。

| 項目 | 変数名 | デフォルト値 |
|---|---|---|
| コンピューター注文の有効期限 | `ComputerTtl` | `int.MaxValue`（実質無期限） |
| プレイヤー注文の有効期限 | `PlayerTtl` | `int.MaxValue`（実質無期限） |

- **期限切れ判定**: `currentTurn - createdAtTurn >= ttl` で期限切れ
- **実行タイミング**: ターン進行時（`TurnProcessor.AdvanceTurn`）に `OrderBook.ExpireOrders` を呼び出す
- TTL は `TurnProcessor` のコンストラクタで設定

## 約定ロジック（`OrderBook.Match`）

### ソート順

- **売り注文**: 価格昇順（安い順） — `OrderBy(o => o.Price)`
- **買い注文**: 価格降順（高い順） — `OrderByDescending(o => o.Price)`

### マッチング手順

1. 受注注文（incoming）の反対側から、価格条件を満たす注文を `TakeWhile` で抽出
2. ソート順に1件ずつマッチング（**価格優先**）
3. 各マッチで `Min(残数量, 相手数量)` を約定
4. **約定価格 = 常に待機注文（板に既存の注文）の価格**

### 部分約定

- 約定数量が注文数量に満たない場合、相手注文の残数量を `WithQuantity` で更新
- 指値注文の未約定分は `AddRemainingLimitOrder` で板に追加（次ターン以降に約定可能）
- 成行注文の未約定分は板に追加**されない**

### 約定ゼロ時の挙動

| 注文種別 | 約定ゼロ時 | ターン進行 | コンピューター注文 |
|---|---|---|---|
| 成行 | 警告を返す。ポートフォリオ不変 | する | 板に残る |
| 指値 | 全数量を板に追加 | する | 板に残る |

## ポートフォリオ更新（`Portfolio.ApplyTrade`）

### 買い

1. `FilledQuantity > 0` チェック（0以下で拒否）
2. `現金 >= 約定金額 + 手数料` チェック（不足で拒否）
3. ポジション数量加算、キャッシュ減算

### 売り

1. `FilledQuantity > 0` チェック（0以下で拒否）
2. `保有数量 >= FilledQuantity` チェック（超過で拒否）
3. ポジション数量減算、キャッシュ加算（手数料差引）
4. 数量0のポジションは `SetQuantity` により自動除去

## ターン進行フロー（`TurnProcessor.PlaceOrder`）

```
1. IExchangeFactory.Create(Prices, fee)    — 取引所生成
2. IOrderPlacer.PlaceOrders(...)           — コンピューター注文20件を1件ずつMatch＋残りAdd
3. Player.CreateOrder(nextId, ...)         — プレイヤー注文を生成
4. IMarket.Execute(book, order, exchange)  — 板でマッチング → MatchResult
5. Portfolio.ApplyTrade(trade)             — ポートフォリオ更新
6. AddRemainingLimitOrder(...)             — 指値未約定分を板に追加
7. IPriceFluctuator.Fluctuate(prices)      — 株価変動（±5%）
8. OrderBook.ExpireOrders(...)             — 期限切れ注文を除去
9. Turn + 1                                — ターン番号インクリメント
```

- **Wait**: コンピューター注文生成 + 株価変動のみ（プレイヤー注文なし）
- **入力バリデーション失敗時**（数量0以下、指値0以下）: 元の `Game` をそのまま返す（状態不変、コンピューター注文未生成）
- **約定・ポートフォリオ更新失敗時**（約定ゼロ、現金不足、保有不足）: コンピューター注文は板に残し、ターンを進め、株価も変動する（Waitと同じ挙動）。ポートフォリオは不変

## 株価変動（`RandomPriceFluctuator`）

- 毎ターン終了時に全銘柄の価格を変動
- **変動幅**: `factor = Random.Next(95, 106)` → -5% ～ +5%
- **最低価格**: `Max(1, price * factor / 100)` — 0以下にならない
- 各銘柄独立に変動（相関なし）

## 初期状態

| 項目 | 値 |
|---|---|
| 初期資産 | 10,000 JPY |
| 初期ポジション | なし |
| 初期ターン | 1 |
| 初期注文ID | 1 |
| 通貨 | JPY のみ（整数演算） |

## 注文ID管理

- `Game.NextOrderId` で一意性を保証
- コンピューター注文20件 → `nextId` 返却 → プレイヤーが `nextId` を使用 → 次ターンは `nextId + 1`
- `OrderBook.Add` は重複IDを冪等的に無視（同一IDの2回目追加はスキップ）
