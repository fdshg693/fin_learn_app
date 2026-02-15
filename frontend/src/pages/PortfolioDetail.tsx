import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { fetchJson } from '../api/client'
import type { PortfolioDto } from '../api/types'

export default function PortfolioDetail() {
  const { investorId } = useParams()
  const [portfolio, setPortfolio] = useState<PortfolioDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    if (!investorId) {
      setError('投資家IDが指定されていません。')
      setIsLoading(false)
      return
    }

    fetchJson<PortfolioDto>(`/api/portfolios/${investorId}`)
      .then(setPortfolio)
      .catch((err) => setError(err.message))
      .finally(() => setIsLoading(false))
  }, [investorId])

  return (
    <section>
      <h1>ポートフォリオ詳細</h1>
      {isLoading && <p>読み込み中...</p>}
      {error && <p>取得に失敗しました: {error}</p>}
      {portfolio && (
        <div>
          <p>投資家ID: {portfolio.investorId}</p>
          <p>
            現金: {portfolio.cash.amount.toLocaleString()} {portfolio.cash.currency}
          </p>
          <p>
            評価額: {portfolio.valuation.amount.toLocaleString()} {portfolio.valuation.currency}
          </p>
          <p>
            損益: {portfolio.profitLoss.amount.toLocaleString()} {portfolio.profitLoss.currency}
          </p>
          <h2>保有銘柄</h2>
          <ul>
            {portfolio.holdings.map((holding) => (
              <li key={holding.tickerId}>
                {holding.symbol} - {holding.quantity} 株 / 評価額{' '}
                {holding.marketValue.amount.toLocaleString()} {holding.marketValue.currency}
              </li>
            ))}
          </ul>
        </div>
      )}
      <div className="actions">
        <Link to="/actions">アクションを実行</Link>
      </div>
    </section>
  )
}
