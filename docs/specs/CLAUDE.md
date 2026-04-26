# fin_learn_app 仕様書インデックス

このディレクトリには fin_learn_app の現状実装に基づく機能仕様書が格納されています。
後続の TDD 実装エージェント（finlearn.test-writer）がテストを書く際の入力として使用します。

## 仕様書一覧

| ファイル | 機能 | APIエンドポイント |
|---|---|---|
| [01-buy-now.md](./01-buy-now.md) | 即時買い（BuyNow） | POST /api/actions/buy-now |
| [02-sell-now.md](./02-sell-now.md) | 即時売り（SellNow） | POST /api/actions/sell-now |
| [03-buy-limit.md](./03-buy-limit.md) | 指値買い（BuyLimit） | POST /api/actions/buy-limit |
| [04-sell-limit.md](./04-sell-limit.md) | 指値売り（SellLimit） | POST /api/actions/sell-limit |
| [05-wait.md](./05-wait.md) | 見送り（Wait） | POST /api/actions/wait |
| [06-turn-system.md](./06-turn-system.md) | ターン制システム | （内部処理） |
| [07-portfolio.md](./07-portfolio.md) | ポートフォリオ参照 | GET /api/portfolios/{investorId} |
| [08-market.md](./08-market.md) | マーケットスナップショット | GET /api/market/snapshot |
| [09-tickers.md](./09-tickers.md) | 銘柄参照 | GET /api/tickers, GET /api/tickers/{tickerId} |
| [10-order-matching.md](./10-order-matching.md) | オーダーマッチングエンジン | （内部処理） |

## 共通仕様

### HTTPエラーレスポンス形式

すべてのエラーは ProblemDetails 形式（RFC 7807）で返される。

| ステータス | 意味 | 発生条件 |
|---|---|---|
| 400 Bad Request | リクエストパラメータ不正 | `quantity <= 0`, `limitPrice <= 0` など |
| 404 Not Found | リソースが見つからない | 存在しない investorId / tickerId |
| 409 Conflict | ターン番号の不一致 | `expectedTurn != currentTurn` |

### ターン制の共通動作

- 全アクション（BuyNow / SellNow / BuyLimit / SellLimit / Wait）はターンを1進める
- ターン進行時に価格変動（全銘柄、97%〜103%）とシステム注文生成（ランダム3銘柄 × 各2注文）が発生する
- 400 / 404 / 409 エラーの場合はターンが進まない

### アクションレスポンス共通形式

```json
{
  "success": true,
  "message": "<メッセージ>",
  "portfolio": { ... },
  "currentTurn": 1
}
```

### 初期データ

- 起動時に SeedData からメモリに読み込まれる（永続化なし）
- 銘柄: AOKI（1,200円）, HND（860円）, SKR（540円）
- 投資家: Demo Investor（初期資産100万円、現金70万円、AOKI 120株・HND 80株を保有）
