import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { fetchJson } from '../api/client'
import type { TickerDetailDto } from '../api/types'

export default function TickerDetail() {
  const { tickerId } = useParams()
  const [ticker, setTicker] = useState<TickerDetailDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    if (!tickerId) {
      setError('銘柄IDが指定されていません。')
      setIsLoading(false)
      return
    }

    fetchJson<TickerDetailDto>(`/api/tickers/${tickerId}`)
      .then(setTicker)
      .catch((err) => setError(err.message))
      .finally(() => setIsLoading(false))
  }, [tickerId])

  return (
    <section>
      <h1>銘柄詳細</h1>
      {isLoading && <p>読み込み中...</p>}
      {error && <p>取得に失敗しました: {error}</p>}
      {ticker && (
        <div>
          <p>銘柄: {ticker.symbol}</p>
          <p>企業名: {ticker.companyName}</p>
          <p>
            現在価格: {ticker.currentPrice.amount.toLocaleString()} {ticker.currentPrice.currency}
          </p>
          <p>最小単位: {ticker.unitSize}</p>
        </div>
      )}
      <div className="actions">
        <Link to="/actions">アクションを実行</Link>
      </div>
    </section>
  )
}
