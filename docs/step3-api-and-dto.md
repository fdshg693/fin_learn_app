# STEP3: API設計とDTO定義

## 目的
フロントとバックエンドのやり取りを最小限のAPIとして設計し、DTOを定義する。

## 想定API（最小構成）
- GET /api/tickers
  - 銘柄一覧を取得
- GET /api/tickers/{tickerId}
  - 銘柄詳細を取得
- GET /api/portfolios/{investorId}
  - 投資家のポートフォリオを取得
- POST /api/actions
  - 投資家アクション（BuyNow / SellNow / Wait）を実行

## DTO定義
バックエンドのモデルを直接返さず、API専用DTOで応答する。

### 共通
- MoneyDto
  - Amount / Currency

### 銘柄
- TickerSummaryDto
  - TickerId / Symbol / CompanyName / CurrentPrice
- TickerDetailDto
  - TickerId / Symbol / CompanyName / UnitSize / CurrentPrice

### ポートフォリオ
- HoldingDto
  - TickerId / Symbol / Quantity / MarketValue
- PortfolioDto
  - PortfolioId / InvestorId / Cash / Valuation / ProfitLoss / Holdings

### アクション
- ActionRequestDto
  - InvestorId / TickerId / Action / Quantity
- ActionResultDto
  - Success / Message / Portfolio

## 配置
- DTO実装:
  - backend/FinLearnApp.Api/Models/Api/MoneyDto.cs
  - backend/FinLearnApp.Api/Models/Api/TickerDtos.cs
  - backend/FinLearnApp.Api/Models/Api/PortfolioDtos.cs
  - backend/FinLearnApp.Api/Models/Api/ActionDtos.cs
