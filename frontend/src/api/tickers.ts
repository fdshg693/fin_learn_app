import { fetchJson } from './client'
import type { PriceRecordDto, TickerSummaryDto } from './types'

export async function fetchTickers(): Promise<TickerSummaryDto[]> {
  return fetchJson<TickerSummaryDto[]>('/api/tickers')
}

export async function fetchPriceHistory(
  tickerId: string,
  limit: number = 20
): Promise<PriceRecordDto[]> {
  return fetchJson<PriceRecordDto[]>(
    `/api/tickers/${tickerId}/price-history?limit=${limit}`
  )
}
