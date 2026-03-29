# ドメイン設計

## アプリの目的

株の基本（売買・ターン制シミュレーション）が分かる人向けの、株取引シミュレーションアプリ。

---

## ユーザーフロー

1. 銘柄一覧画面 → 銘柄一覧取得・表示
2. 銘柄をクリック → 銘柄詳細取得・表示
3. アクションを選択（BuyNow / SellNow / BuyLimit / SellLimit / Wait）
4. アクションに応じたシミュレーション実行 → 結果表示

または

1. ポートフォリオ一覧画面 → ポートフォリオ取得・表示
2. 銘柄詳細へ遷移 → アクション選択 → シミュレーション実行

---

## 用語定義

| 用語 | 説明 |
|---|---|
| 企業 | 銘柄を発行する会社 |
| 投資家 | 銘柄を売買するユーザー |
| 銘柄 | どの会社の株かを区別する識別単位。株価・単位を持つ |
| ポートフォリオ | 保有銘柄と現金の集合。評価額・損益を計算できる |
| 評価額 | 保有銘柄の現在価値 + 現金の合計 |
| 損益 | 評価額 - 初期資産 |
| 取引所 | 売買注文を管理し、約定を判定する場所 |
| 注文帳 | 売買注文の一覧。約定の成否を判定する |
| 約定 | 売買が成立すること |
| 手数料 | 売買成立時にかかる固定費用（売買双方にかかる） |

---

## ドメインモデル

### Value Objects

| 型 | 説明 |
|---|---|
| `Money` | 金額と通貨（JPY固定）を表す。加減算・乗算を提供 |
| `CompanyId` / `InvestorId` / `TickerId` / `PortfolioId` / `OrderId` / `TradeId` | 各エンティティの型安全な識別子 |

### Enums

| 型 | 値 |
|---|---|
| `Currency` | JPY のみ |
| `OrderSide` | Buy / Sell |
| `OrderOrigin` | Investor / System |
| `InvestorAction` | BuyNow / SellNow / BuyLimit / SellLimit / Wait |

### Entities

| エンティティ | 主なプロパティ・責務 |
|---|---|
| `Company` | `Id`, `Name` |
| `Investor` | `Id`, `Name` |
| `Ticker` | `CompanyId`, `Symbol`, `UnitSize`, `CurrentPrice`。価格更新が可能 |
| `Holding` | `TickerId`, `Quantity`。増減・評価額計算を提供 |
| `Portfolio` | `Cash`, `Holdings`。評価額・損益計算、入出金を提供 |
| `Order` | `TickerId`, `Side`, `Price`, `Quantity`, `Origin`, `CreatedAt` |
| `OrderBook` | 買い/売り注文を分けて管理 |
| `Trade` | 約定結果。買い/売り注文ID・価格・数量・手数料・実行日時を保持 |
| `Exchange` | 固定手数料 `Fee` と `OrderBook` を持つ。注文管理・約定判定 |

### Events

| イベント | 説明 |
|---|---|
| `TradeExecuted` | 約定完了を通知するドメインイベント |

---

## ビジネスルール

### ターン制
- 1ターンにつき投資家は1アクションのみ実行できる
- `Wait` もターンを1進める
- ターン進行時: 銘柄価格がランダム変動（97%〜103%）

### 注文制約
- 持っている現金以上の買い注文はできない（手数料込み）
- 持っている株以上の売り注文はできない

### 手数料
- 売買成立ごとに固定500円（`Exchange.Fee`）
- 株数によらず一定

### コンピュータ注文（毎ターン自動生成）
- 買い注文: 3銘柄にランダムに10口ずつ、価格は現在値の95%
- 売り注文: 3銘柄にランダムに10口ずつ、価格は現在値の100%

### オーダーマッチング
- 価格優先・時間優先（FIFO）でマッチング
- 投資家注文とコンピュータ注文が条件を満たせば自動約定

---

## 依存関係

```
Portfolio → Holding, Money
Order     → OrderSide, OrderOrigin, Money
Trade     → Order（の結果として生成）
Exchange  → OrderBook（注文管理）
TradeExecuted → Trade（約定完了イベント）
```
