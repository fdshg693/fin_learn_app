# リファクタ・バグ候補

修正した後で、 `docs\DDD\EXCHANGE_RULE.md` も更新すること。

## 3. 成行注文の約定価格が不利になりうる

**箇所**: [OrderBook.cs:48-51](Models/OrderBook.cs#L48-L51)

成行買い注文は**全ての売り注文**とマッチする（価格フィルタなし）。売り注文が安い順にソートされているため通常は問題ないが、**極端に高い売り注文**とも約定してしまう。

例: 市場価格100の銘柄で、売り板に `[100, 100, 500]` がある場合、成行買い3株で `100 + 100 + 500 = 700` 支払う。プレイヤーに不利な約定を防ぐガードがない。

**検討**: 成行注文にも価格上限（ストップ価格）を導入するか、約定前に確認UIを挟む。

## 4. コンピューター買い注文の価格が常に市場価格以下

**箇所**: [ComputerTrader.cs:38](Services/ComputerTrader.cs#L38)

```csharp
var price = Math.Max(1, marketPrice * 95 / 100);
```

コンピューターの買い注文は常に95%、売り注文は100%。つまり**コンピューター同士では約定しない**（買95 < 売100）。プレイヤーが100%以上で買うか、95%以下で売るときのみ約定する。

**影響**: 板にコンピューター注文が毎ターン蓄積され続ける。古い注文の有効期限がないため、ターンが進むにつれて板が肥大化する。

## 5. 指値注文の有効期限がない

**箇所**: `OrderBook` 全体

板に追加された指値注文は永続的に残る。コンピューター注文（毎ターン20件）とプレイヤーの未約定指値が無期限に蓄積する。

**影響**:
- メモリ使用量が線形増加
- 古い価格帯の注文が意図せず約定するリスク（株価変動で到達）
- `SellOrders`/`BuyOrders` のフィルタリングコストが増加（毎回全注文をスキャン）

**検討**: ターン制限（例: 3ターンで失効）を導入するか、ターン開始時に古い注文をクリアする。

## 6. 整数除算による価格の切り捨て

**箇所**: [ComputerTrader.cs:38](Services/ComputerTrader.cs#L38), [RandomPriceFluctuator.cs:22](Services/RandomPriceFluctuator.cs#L22)

```csharp
// ComputerTrader: 95% 計算
var price = Math.Max(1, marketPrice * 95 / 100);
// RandomPriceFluctuator: 変動計算
var newPrice = Math.Max(1, price * factor / 100);
```

整数除算のため常に**切り捨て**になる。例: `marketPrice = 1` → `1 * 95 / 100 = 0` → `Max(1, 0) = 1`。低価格帯では変動が効かなくなり、**価格1に収束して動かなくなる可能性**がある。

## 7. Market.Execute で incomingFill が null になるケース

**箇所**: [Market.cs:16-17](Services/Market.cs#L16-L17)

```csharp
FilledQuantity: incomingFill?.FilledQuantity ?? 0,
TotalAmount: incomingFill?.TotalAmount ?? 0,
```

`OrderBook.Fill` は常に incoming の `OrderFill` を追加する（[OrderBook.cs:90](Models/OrderBook.cs#L90)）ため、`GetFill(order.Id)` が `null` を返すことは現実装では起きない。しかし null ガードが残っているため、将来の変更で `Fill` の挙動が変わった場合にサイレントに0約定を返すリスクがある。

**検討**: null ガードを除去して明示的に例外を投げるか、テストで null にならないことを保証する。

## 8. テストカバレッジの不足箇所

以下のシナリオにテストがない:

| シナリオ | リスク |
|---|---|
| 指値売り注文の部分約定 + 板への残留 | 買いのみテスト済、売りは未検証 |
| コンピューター注文とプレイヤー注文の相互約定 | 統合レベルのフロー未検証 |
| 複数ターンにわたる注文ID重複チェック | ID管理ロジックの結合テストなし |
| 低価格帯（価格1）での株価変動 | 切り捨てで変動しないケースの検証なし |
| 複数回の売買を繰り返すシナリオ | 1回の売買のみテスト済 |
