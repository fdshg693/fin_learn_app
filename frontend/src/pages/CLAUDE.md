# frontend/src/pages/

各ファイルが対応するページ・ルートの一覧。

| ファイル | ルート | 概要 |
|---|---|---|
| `Home.tsx` | `/` | トップページ |
| `Tickers.tsx` | `/tickers` | 銘柄一覧 |
| `TickerDetail.tsx` | `/tickers/:tickerId` | 銘柄詳細 |
| `Portfolios.tsx` | `/portfolios` | ポートフォリオ一覧 |
| `PortfolioDetail.tsx` | `/portfolios/:investorId` | ポートフォリオ詳細 |
| `Actions.tsx` | `/actions` | メイン取引画面（BuyNow / SellNow / Wait / BuyLimit / SellLimit） |
| `Chart.tsx` | `/chart` | 銘柄価格推移チャート |
| `NotFound.tsx` | `*` | 404 ページ |

ルーティングの定義は `frontend/src/App.tsx` にある。
