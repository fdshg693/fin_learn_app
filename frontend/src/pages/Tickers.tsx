import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { fetchJson } from '../api/client'
import type { TickerSummaryDto } from '../api/types'

export default function Tickers() {
  const [tickers, setTickers] = useState<TickerSummaryDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    fetchJson<TickerSummaryDto[]>('/api/tickers')
      .then(setTickers)
      .catch((err) => setError(err.message))
      .finally(() => setIsLoading(false))
  }, [])

  return (
    <section>
      <h1>銘柄一覧</h1>
      {isLoading && <p>読み込み中...</p>}
      {error && <p>取得に失敗しました: {error}</p>}
      {!isLoading && !error && (
        <ul>
          {tickers.map((ticker) => (
            <li key={ticker.tickerId}>
              <Link to={`/tickers/${ticker.tickerId}`}>
                {ticker.symbol} ({ticker.companyName})
              </Link>{' '}
              - {ticker.currentPrice.amount.toLocaleString()} {ticker.currentPrice.currency}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
