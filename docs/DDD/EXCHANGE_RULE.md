# 取引ルール

> バリデーション・エラー応答の詳細は [docs/FEATURES/VALIDATION/LOGIC.md](../FEATURES/VALIDATION/LOGIC.md) 参照。

## 手数料

- **固定手数料制**: 1取引あたり固定額（JPY）を徴収
- 手数料は `TurnProcessor` の各メソッド (`Buy`/`Sell`/`Wait`) に `int fee` パラメータとして注入
- **per-order セマンティクス**: 注文1件につき手数料1回。指値の部分約定では fee=0、最終約定（残数量0）の fill で fee 全額を計上
- **買い指値**: 発注時に `数量 × 指値価格 + 手数料` を予約 → 約定で確定（差額は available に返金） → 失効時は残予約を解放
- **売り指値**: 発注時は cash を予約しない（保有株のみ予約）。約定時に `数量 × 約定価格 - 手数料` を available に加算
- **成行注文**: 予約しない。`Portfolio.ApplyTrade` で `現金 - 約定金額 - 手数料`（買い）/ `現金 + 約定金額 - 手数料`（売り）の同期適用

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

> 注文の入力バリデーション（数量・価格・ストップ価格 > 0）は [docs/FEATURES/VALIDATION/LOGIC.md](../FEATURES/VALIDATION/LOGIC.md) を参照。

## 発注時資源予約モデル（Reservation Model）

実取引所と同じく、**指値注文は発注時に必要資源を予約**する。これにより約定の settlement は失敗しない（残高/数量不足は発注時バリデーションで弾かれる）。

| 注文種別 | 発注時の予約 | 約定時の挙動 | 失効時の挙動 |
|---|---|---|---|
| 買い指値 | `available cash` から `qty × price + fee` を `reserved cash` に移動 | `reserved cash` から `qty × actualPrice + feeIfFinal` を確定。差額（指値 − 約定価格 + 未計上 fee）は `available` に返金 | 残予約 cash を `available` に解放 |
| 売り指値 | `available positions` から `qty` 株を `reserved positions` に移動（全保有数は不変） | `reserved positions` から `qty` 減算、`available cash` に proceeds 加算 | `reserved positions` のみ減算（全保有数は不変） |
| 買い成行 | 予約なし | `Portfolio.ApplyTrade` で同期適用。残高不足ならロールバック（`Fills` を空に） | （板に乗らないので失効なし） |
| 売り成行 | 予約なし | `Portfolio.ApplyTrade` で同期適用 | （板に乗らないので失効なし） |

- `Portfolio.Cash` は **available cash** のみを表す（`ReservedCash` は別フィールド）。総資産 `TotalAmount = Cash + ReservedCash + 全保有評価`
- `Portfolio.QuantityOf` は全保有を表す。売却可能な数量は `AvailableQuantityOf`、予約中は `ReservedQuantityOf`
- 発注時の予約失敗（残高/数量不足）は warning + `Fills` 空でターンを進める（コンピューター注文の settlement は確定維持）
- `Portfolio.CreateInfinite()` は予約系メソッドも全て no-op（computer 用）

### Settlement の責務統一（`SettlementProcessor`）

注文生成（intent）と settlement（マーケット結果反映）を分離。当ターンに発生した全 `OrderFill` を `traderId → Portfolio` 統一マップに対して `SettlementProcessor.SettleFills` で適用する。これにより以下のすべてが同じ仕組みで反映される：

- computer 同士の約定（同ターン中の computer 注文交差）
- computer 注文と player の **過去ターンの resting 指値** の約定 ← 旧コードで未反映だったケース
- player の incoming 注文と既存 resting 注文の約定

失効注文の予約解放は `SettlementProcessor.ReleaseExpired` が担う。

## コンピューター注文生成（`ComputerTrader`）

毎ターン、プレイヤー注文の**前**に自動生成される。注文は1件ずつ `OrderBook.Match` を通し、約定しなかった残りだけ板に追加する。発注時に `Portfolio.ReserveBuy` / `ReserveSell` を呼ぶ（Infinite なので no-op）。約定の Portfolio 反映は `SettlementProcessor.SettleFills` に委譲。

> 詳細: [docs/FEATURES/COMPUTER_ORDER/CURRENT.md](../FEATURES/COMPUTER_ORDER/CURRENT.md)

| 項目 | 買い注文 | 売り注文 |
|---|---|---|
| 件数 | 10件（各 `computer{i}` から1件） | 10件（各 `computer{i}` から1件） |
| 数量 | 各1株 | 各1株 |
| 価格 | `Max(1, 市場価格 * [85..105] / 100)` | `Max(1, 市場価格 * [95..115] / 100)` |
| 銘柄 | ランダム分散 | ランダム分散 |
| 注文者ID | `"computer1"` 〜 `"computer10"` | `"computer1"` 〜 `"computer10"` |

- 買い価格帯（85〜105%）と売り価格帯（95〜115%）が重なるため、TraderIdの異なるコンピューター同士で約定が発生する（同一 `computer{i}` の自己約定はOrderBookで防止）
- `IExchange.TryGetPrice` が `false` の銘柄はスキップ（価格不明・価格0以下）
- `Random` インスタンスを外部注入（シード固定でテスト決定性を確保）

## 注文の有効期限

注文ごとに `ExpiresAtTurn`（有効期限ターン番号・絶対値）を持ち、ターン進行時に自動的に板から除去される。

| 項目 | 値 |
|---|---|
| デフォルト有効ターン数 | `GameRules.DefaultOrderTtl = 2`（生成ターンと次のターンまで有効） |
| `ExpiresAtTurn` の計算 | `CreatedAtTurn + expiresInTurns` |
| 期限切れ判定 | `currentTurn >= ExpiresAtTurn` |
| 実行タイミング | ターン進行時（`TurnProcessor.AdvanceTurn`）に `OrderBook.ExpireOrders(currentTurn)` を呼び出す |
| API 指定 | `OrderRequest.expiresInTurns`（未指定時はサーバー側でデフォルト 2 を適用、`<= 0` は 400 BadRequest） |

- コンピューター注文・プレイヤー注文ともに同じデフォルト（2 ターン）を使用
- ユーザーは画面の「有効期限（ターン）」入力で各注文ごとに値を指定可能
- `Order` の `ExpiresAtTurn` のデフォルトは `int.MaxValue`（実質無期限）。プロダクションパス（`Player.CreateOrder`・`ComputerTrader`）は明示的に値を渡す

## 約定ロジック（`OrderBook.Match`）

> 詳細: [docs/FEATURES/FillResult/LOGIC.md](../FEATURES/FillResult/LOGIC.md)

- **価格優先**: 売り注文は安い順、買い注文は高い順にマッチング
- **約定価格 = 常に待機注文（板に既存の注文）の価格**
- 部分約定あり。指値の未約定分は板に追加、成行の未約定分は消滅
- 約定ゼロ時: 成行は警告、指値は全数量を板に追加。いずれもターンは進行する

## ポートフォリオ更新

> 失敗時の Warning 設計は [docs/FEATURES/VALIDATION/LOGIC.md](../FEATURES/VALIDATION/LOGIC.md) 参照。

### 指値の予約・確定・解放

- **`ReserveBuy(instrumentId, quantity, price, fee)`**: `available cash` 不足なら `InsufficientCashToBuy`、成功なら `available → reserved` へ移動
- **`ReserveSell(instrumentId, quantity)`**: `available 数量` 不足なら `InsufficientQuantityToSell`、成功なら `_reservedPositions` に加算（全保有数は不変）
- **`SettleReservedBuy(...)`**: `reserved cash` から `qty × reservedPrice + feeIfFinal` を消費、差額は `available cash` に返金、保有を加算
- **`SettleReservedSell(...)`**: 全保有と `_reservedPositions` から filled 数量を減算、`available cash` に proceeds を加算
- **`ReleaseBuyReservation` / `ReleaseSellReservation`**: 失効・全約定時に残予約を `available` に戻す

### 成行の `ApplyTrade`

板に乗らない成行 fill 専用。指値の settlement は `SettleReserved*` を使うこと。

#### 買い
1. `FilledQuantity > 0` チェック（0以下で拒否）
2. `available cash >= 約定金額 + 手数料` チェック（不足で拒否 → ロールバック）
3. ポジション数量加算、`available cash` 減算

#### 売り
1. `FilledQuantity > 0` チェック（0以下で拒否）
2. `保有数量 >= FilledQuantity` チェック（超過で拒否）
3. ポジション数量減算、`available cash` 加算（手数料差引）
4. 数量0のポジションは `SetQuantity` により自動除去

## ターン進行フロー（`TurnProcessor.PlaceOrder`）

```
1. IExchangeFactory.Create(Prices, fee)
2. BuildAllPortfolios(game)                — Player + ComputerPortfolios の統合 view
3. IOrderPlacer.PlaceOrders(...)           — computer 注文の発注 + 約定 + SettlementProcessor で全 trader に反映
4. Player.CreateOrder(nextId, ...)         — プレイヤー注文を生成
5. (指値のみ) Portfolio.ReserveBuy/Sell    — 失敗なら Wait 化 + warning（computer settlement は確定維持）
6. IMarket.Execute(book, order, exchange)
7. fills の settlement
   - 指値 → SettlementProcessor.SettleFills（予約消費 + 差額返金、失敗パス無し）
   - 成行 → Portfolio.ApplyTrade（残高不足ならロールバック: Fills 破棄）
8. AddRemainingLimitOrder(...)             — 指値未約定分を板に追加
9. AdvanceTurn:
     a. IPriceFluctuator.Fluctuate(prices)
     b. (newBook, expired) = OrderBook.ExpireOrders(turn+1)
     c. SettlementProcessor.ReleaseExpired(expired, ..., fee)  — 失効注文の予約を解放
     d. SplitPortfolios → Player / ComputerPortfolios に分解して新 Game を構築
```

- **Wait**: コンピューター注文生成 + settlement + 株価変動 + 失効処理（プレイヤー注文なし）
- **ロールバック対象**は **player の市場注文 fill のみ**。Computer 同士・computer-vs-player resting の settlement は確定事実として維持される
- **失敗時のターン進行ルール**（形式不正／約定ゼロ／状態依存失敗）: [docs/FEATURES/VALIDATION/LOGIC.md](../FEATURES/VALIDATION/LOGIC.md) 「失敗時のターン進行ルール」表を参照

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
