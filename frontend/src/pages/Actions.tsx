import { useEffect, useState, type FormEvent } from 'react'
import { fetchJson } from '../api/client'
import type { ActionRequestDto, ActionResultDto, PortfolioDto, TickerSummaryDto } from '../api/types'

const demoInvestorId = '7b3e6c8d-6a8d-4e9f-9b7c-7c8d6c0e7f07'

type ActionType = 'BuyNow' | 'SellNow' | 'Wait'

export default function Actions() {
  const [tickers, setTickers] = useState<TickerSummaryDto[]>([])
  const [portfolio, setPortfolio] = useState<PortfolioDto | null>(null)
  const [action, setAction] = useState<ActionType>('BuyNow')
  const [tickerId, setTickerId] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [resultMessage, setResultMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    Promise.all([
      fetchJson<TickerSummaryDto[]>('/api/tickers'),
      fetchJson<PortfolioDto>(`/api/portfolios/${demoInvestorId}`),
    ])
      .then(([tickersResult, portfolioResult]) => {
        setTickers(tickersResult)
        setPortfolio(portfolioResult)
        if (tickersResult.length > 0) {
          setTickerId(tickersResult[0].tickerId)
        }
      })
      .catch((err) => setError(err.message))
      .finally(() => setIsLoading(false))
  }, [])

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setResultMessage(null)

    if (!tickerId) {
      setError('銘柄を選択してください。')
      return
    }

    if (action !== 'Wait' && quantity <= 0) {
      setError('数量は1以上を指定してください。')
      return
    }

    const payload: ActionRequestDto = {
      investorId: demoInvestorId,
      tickerId,
      action,
      quantity: action === 'Wait' ? 1 : quantity,
    }

    setIsSubmitting(true)
    try {
      const result = await fetchJson<ActionResultDto>('/api/actions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      })
      setResultMessage(result.message)
      setPortfolio(result.portfolio)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section>
      <h1>アクション実行</h1>
      {isLoading && <p>読み込み中...</p>}
      {error && <p>エラー: {error}</p>}
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
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div>
          <label>
            アクション
            <select value={action} onChange={(event) => setAction(event.target.value as ActionType)}>
              <option value="BuyNow">BuyNow</option>
              <option value="SellNow">SellNow</option>
              <option value="Wait">Wait</option>
            </select>
          </label>
        </div>
        <div>
          <label>
            銘柄
            <select value={tickerId} onChange={(event) => setTickerId(event.target.value)}>
              {tickers.map((ticker) => (
                <option key={ticker.tickerId} value={ticker.tickerId}>
                  {ticker.symbol} ({ticker.companyName})
                </option>
              ))}
            </select>
          </label>
        </div>
        <div>
          <label>
            数量
            <input
              type="number"
              min={1}
              value={quantity}
              onChange={(event) => setQuantity(Number(event.target.value))}
              disabled={action === 'Wait'}
            />
          </label>
        </div>
        <button type="submit" disabled={isSubmitting || isLoading}>
          {isSubmitting ? '送信中...' : '実行'}
        </button>
      </form>

      {resultMessage && <p>{resultMessage}</p>}

      {portfolio && portfolio.holdings.length > 0 && (
        <div>
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
    </section>
  )
}
