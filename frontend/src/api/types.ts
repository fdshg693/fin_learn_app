export type MoneyDto = {
  amount: number
  currency: string
}

export type TickerSummaryDto = {
  tickerId: string
  symbol: string
  companyName: string
  currentPrice: MoneyDto
}

export type TickerDetailDto = {
  tickerId: string
  symbol: string
  companyName: string
  unitSize: number
  currentPrice: MoneyDto
}

export type HoldingDto = {
  tickerId: string
  symbol: string
  quantity: number
  marketValue: MoneyDto
}

export type PortfolioDto = {
  portfolioId: string
  investorId: string
  cash: MoneyDto
  valuation: MoneyDto
  profitLoss: MoneyDto
  holdings: HoldingDto[]
}

export type ActionTradeRequestDto = {
  investorId: string
  tickerId: string
  quantity: number
}

export type ActionWaitRequestDto = {
  investorId: string
}

export type ActionResultDto = {
  success: boolean
  message: string
  portfolio: PortfolioDto
}
