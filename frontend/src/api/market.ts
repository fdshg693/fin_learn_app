import { fetchJson } from './client'
import type { MarketSnapshotDto } from './types'

export async function fetchMarketSnapshot(): Promise<MarketSnapshotDto> {
  return fetchJson<MarketSnapshotDto>('/api/market/snapshot')
}
