import { Link } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { fetchJson } from '../api/client'
import type { PortfolioDto } from '../api/types'

const demoInvestorId = '7b3e6c8d-6a8d-4e9f-9b7c-7c8d6c0e7f07'

export default function Portfolios() {
  const [portfolio, setPortfolio] = useState<PortfolioDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    fetchJson<PortfolioDto>(`/api/portfolios/${demoInvestorId}`)
      .then(setPortfolio)
      .catch((err) => setError(err.message))
      .finally(() => setIsLoading(false))
  }, [])

  return (
    <section>
      <h1>ポートフォリオ一覧</h1>
      {isLoading && <p>読み込み中...</p>}
      {error && <p>取得に失敗しました: {error}</p>}
      {portfolio && (
        <div>
          <p>投資家ID: {portfolio.investorId}</p>
          <p>
            評価額: {portfolio.valuation.amount.toLocaleString()} {portfolio.valuation.currency}
          </p>
          <p>
            損益: {portfolio.profitLoss.amount.toLocaleString()} {portfolio.profitLoss.currency}
          </p>
          <Link to={`/portfolios/${portfolio.investorId}`}>詳細を見る</Link>
        </div>
      )}
    </section>
  )
}
