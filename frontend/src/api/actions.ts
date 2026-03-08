import { fetchJson } from './client'
import type {
  ActionLimitRequestDto,
  ActionResultDto,
  ActionTradeRequestDto,
  ActionWaitRequestDto,
} from './types'

export async function buyNow(request: ActionTradeRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/buy-now', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}

export async function sellNow(request: ActionTradeRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/sell-now', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}

export async function waitAction(request: ActionWaitRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/wait', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}

export async function buyLimit(request: ActionLimitRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/buy-limit', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}

export async function sellLimit(request: ActionLimitRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/sell-limit', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}
