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
  currentTurn: number
  cash: MoneyDto
  valuation: MoneyDto
  profitLoss: MoneyDto
  holdings: HoldingDto[]
}

export type ActionTradeRequestDto = {
  investorId: string
  tickerId: string
  quantity: number
  expectedTurn: number
}

export type ActionLimitRequestDto = {
  investorId: string
  tickerId: string
  quantity: number
  limitPriceAmount: number
  expectedTurn: number
}

export type ActionWaitRequestDto = {
  investorId: string
  expectedTurn: number
}

export type ActionResultDto = {
  success: boolean
  message: string
  portfolio: PortfolioDto
  currentTurn: number
}

export type MarketOrderDto = {
  orderId: string
  tickerId: string
  symbol: string
  side: string
  origin: string
  price: MoneyDto
  quantity: number
  createdAt: string
}

export type MarketTradeDto = {
  tradeId: string
  tickerId: string
  symbol: string
  quantity: number
  price: MoneyDto
  fee: MoneyDto
  executedAt: string
}

export type MarketSnapshotDto = {
  buyOrders: MarketOrderDto[]
  sellOrders: MarketOrderDto[]
  trades: MarketTradeDto[]
}

export type PriceRecordDto = {
  turn: number
  price: MoneyDto
}
