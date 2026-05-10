import { fetchJson } from './client'
import type {
  ActionBuyRequestDto,
  ActionResultDto,
  ActionSellRequestDto,
  ActionWaitRequestDto,
} from './types'

export async function buy(request: ActionBuyRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/buy', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export async function sell(request: ActionSellRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/sell', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export async function waitAction(request: ActionWaitRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/wait', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}
