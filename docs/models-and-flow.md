# モデル定義まとめ

## ValueObjects
- Money
  - 金額と通貨を表す値オブジェクト。
  - 通貨一致を担保しつつ、加減算・乗算を提供。
- Ids
  - CompanyId / InvestorId / TickerId / PortfolioId / OrderId / TradeId。
  - 各エンティティの識別子を型安全に扱うためのID群。

## Enums
- Currency
  - 通貨種別。現状はJPYのみ。
- OrderSide
  - 注文の売買区分（Buy / Sell）。
- OrderOrigin
  - 注文の発生元（Investor / System）。
- InvestorAction
  - 投資家アクション（BuyNow / SellNow / Wait）。

## Entities
- Company
  - 銘柄を発行する企業。`Id`と`Name`を保持。
- Investor
  - 投資家（ユーザー）。`Id`と`Name`を保持。
- Ticker
  - 銘柄情報。`CompanyId`、`Symbol`、`UnitSize`、`CurrentPrice`を保持し価格更新が可能。
- Holding
  - 保有株。`TickerId`と`Quantity`を保持し、増減・評価額計算が可能。
- Portfolio
  - 投資家の資産。`Cash`と`Holdings`を管理し、評価額・損益計算、入出金を提供。
- Order
  - 注文。`TickerId`、`Side`、`Price`、`Quantity`、`Origin`、`CreatedAt`を保持。
- OrderBook
  - 注文一覧。買い/売り注文を分けて保持。
- Trade
  - 約定結果。買い/売り注文ID、価格、数量、手数料、実行日時を保持。
- Exchange
  - 取引所。固定手数料`Fee`と`OrderBook`を保持。

## Events
- TradeExecuted
  - 約定イベント。`Trade`と発生時刻を保持。

---

# 全体の通信（処理）フロー

## 画面遷移と取得フロー（DDD.mdに準拠）
1. 銘柄一覧画面へ遷移
2. 銘柄一覧取得
   - `Ticker` と `Company` の情報を表示用に取得
3. 銘柄詳細画面へ遷移
4. 銘柄詳細取得
   - 選択された `Ticker` の詳細（`CurrentPrice` 等）を表示
5. 投資家アクション選択
   - `InvestorAction` を選択（BuyNow / SellNow / Wait）
6. アクションに応じたシミュレーション実行
   - 注文作成：`Order`（`OrderOrigin=Investor`）
   - 取引所に追加：`Exchange` → `OrderBook`
   - 約定判定：`OrderBook` 同士のマッチング
   - 約定生成：`Trade` と `TradeExecuted` を発行
   - 保有更新：`Portfolio` / `Holding` / `Cash`
7. 結果表示
   - 価格、保有、評価額、損益など

## ポートフォリオ一覧フロー
1. ポートフォリオ一覧画面へ遷移
2. ポートフォリオ取得
   - `Portfolio`（`Cash`・`Holdings`）を表示
3. 銘柄詳細へ遷移
   - `Ticker`・`Holding`・評価額を表示
4. 投資家アクション選択 → シミュレーション
   - 上記と同様の注文〜約定〜保有更新フロー

## ターン制シミュレーション（ドメインルール）
- 1ターンにつき投資家は1アクション。
- 毎ターン、銘柄価格がランダム変動（`Ticker.CurrentPrice` 更新）。
- 資金不足の買い注文は禁止（手数料込み）。
- 保有不足の売り注文は禁止（手数料込み）。
- 手数料は固定額（`Exchange.Fee`）。
- コンピューター注文は毎ターン生成。
  - 買い：3銘柄へ10口ずつ、価格は現在値の95%。
  - 売り：3銘柄へ10口ずつ、価格は現在値の100%。

## 依存関係の要約
- `Portfolio` は `Holding` と `Money` を通じて資産を管理。
- `Order` は `OrderSide` / `OrderOrigin` / `Money` を参照。
- `Trade` は `Order` の結果として生成。
- `Exchange` は `OrderBook` を介して注文管理。
- `TradeExecuted` は約定完了を外部へ通知するイベント。
