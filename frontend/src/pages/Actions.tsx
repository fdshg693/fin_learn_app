import { useEffect, useState } from 'react'
import { buy, sell, waitAction } from '../api/actions'
import { fetchMarketSnapshot } from '../api/market'
import { fetchJson } from '../api/client'
import type {
  ActionWaitRequestDto,
  MarketSnapshotDto,
  PortfolioDto,
  TickerSummaryDto,
} from '../api/types'

const demoInvestorId = '7b3e6c8d-6a8d-4e9f-9b7c-7c8d6c0e7f07'

export default function Actions() {
  const [tickers, setTickers] = useState<TickerSummaryDto[]>([])
  const [portfolio, setPortfolio] = useState<PortfolioDto | null>(null)
  const [tickerId, setTickerId] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [limitPriceAmount, setLimitPriceAmount] = useState(1000)
  const [resultMessage, setResultMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [currentTurn, setCurrentTurn] = useState(0)
  const [marketSnapshot, setMarketSnapshot] = useState<MarketSnapshotDto | null>(null)

  const sortedBuyOrders = [...(marketSnapshot?.buyOrders ?? [])].sort((a, b) => {
    const symbolCompare = a.symbol.localeCompare(b.symbol)
    if (symbolCompare !== 0) {
      return symbolCompare
    }

    if (b.price.amount !== a.price.amount) {
      return b.price.amount - a.price.amount
    }

    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })

  const sortedSellOrders = [...(marketSnapshot?.sellOrders ?? [])].sort((a, b) => {
    const symbolCompare = a.symbol.localeCompare(b.symbol)
    if (symbolCompare !== 0) {
      return symbolCompare
    }

    if (a.price.amount !== b.price.amount) {
      return a.price.amount - b.price.amount
    }

    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })

  const buySummaryBySymbol = Array.from(
    sortedBuyOrders.reduce(
      (map, order) => {
        const current = map.get(order.symbol)
        if (current) {
          current.totalQuantity += order.quantity
          current.bestPrice = Math.max(current.bestPrice, order.price.amount)
          return map
        }

        map.set(order.symbol, {
          symbol: order.symbol,
          totalQuantity: order.quantity,
          bestPrice: order.price.amount,
          currency: order.price.currency,
        })

        return map
      },
      new Map<
        string,
        { symbol: string; totalQuantity: number; bestPrice: number; currency: string }
      >(),
    ).values(),
  )

  const sellSummaryBySymbol = Array.from(
    sortedSellOrders.reduce(
      (map, order) => {
        const current = map.get(order.symbol)
        if (current) {
          current.totalQuantity += order.quantity
          current.bestPrice = Math.min(current.bestPrice, order.price.amount)
          return map
        }

        map.set(order.symbol, {
          symbol: order.symbol,
          totalQuantity: order.quantity,
          bestPrice: order.price.amount,
          currency: order.price.currency,
        })

        return map
      },
      new Map<
        string,
        { symbol: string; totalQuantity: number; bestPrice: number; currency: string }
      >(),
    ).values(),
  )

  useEffect(() => {
    Promise.all([
      fetchJson<TickerSummaryDto[]>('/api/tickers'),
      fetchJson<PortfolioDto>(`/api/portfolios/${demoInvestorId}`),
      fetchMarketSnapshot(),
    ])
      .then(([tickersResult, portfolioResult, marketSnapshotResult]) => {
        setTickers(tickersResult)
        setPortfolio(portfolioResult)
        setCurrentTurn(portfolioResult.currentTurn)
        setMarketSnapshot(marketSnapshotResult)
        if (tickersResult.length > 0) {
          setTickerId(tickersResult[0].tickerId)
          setLimitPriceAmount(tickersResult[0].currentPrice.amount)
        }
      })
      .catch((err) => setError(err.message))
      .finally(() => setIsLoading(false))
  }, [])

  const executeAction = async (side: 'buy' | 'sell', limitPrice?: number) => {
    setError(null)
    setResultMessage(null)

    if (!tickerId) {
      setError('銘柄を選択してください。')
      return
    }

    if (quantity <= 0) {
      setError('数量は1以上を指定してください。')
      return
    }

    if (limitPrice !== undefined && limitPrice <= 0) {
      setError('指値価格は1以上を指定してください。')
      return
    }

    const payload = {
      investorId: demoInvestorId,
      tickerId,
      quantity,
      limitPrice,
      expectedTurn: currentTurn,
    }

    setIsSubmitting(true)
    try {
      const result = side === 'buy'
        ? await buy(payload)
        : await sell(payload)

      const latestSnapshot = await fetchMarketSnapshot()
      setResultMessage(result.message)
      setPortfolio(result.portfolio)
      setCurrentTurn(result.currentTurn)
      setMarketSnapshot(latestSnapshot)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setIsSubmitting(false)
    }
  }

  const executeWaitAction = async () => {
    setError(null)
    setResultMessage(null)

    const payload: ActionWaitRequestDto = {
      investorId: demoInvestorId,
      expectedTurn: currentTurn,
    }

    setIsSubmitting(true)
    try {
      const result = await waitAction(payload)

      const latestSnapshot = await fetchMarketSnapshot()
      setResultMessage(result.message)
      setPortfolio(result.portfolio)
      setCurrentTurn(result.currentTurn)
      setMarketSnapshot(latestSnapshot)
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div>
      <h1 style={{ margin: '0 0 1rem' }}>アクション実行</h1>
      {isLoading && <p style={{ color: '#94a3b8' }}>読み込み中...</p>}
      {error && <p style={{ color: '#ef4444' }}>エラー: {error}</p>}
      {resultMessage && <p style={{ color: '#4ade80', marginBottom: '1rem' }}>{resultMessage}</p>}

      <div className="page-grid" style={{ marginBottom: '1.25rem' }}>
        {/* 左: 取引フォーム */}
        <div className="card">
          <h2>注文</h2>
          <form style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem', color: '#94a3b8' }}>
              銘柄
              <select value={tickerId} onChange={(e) => setTickerId(e.target.value)}
                style={{ background: '#1e293b', color: '#e2e8f0', border: '1px solid #334155', borderRadius: '6px', padding: '0.4rem 0.6rem' }}>
                {tickers.map((ticker) => (
                  <option key={ticker.tickerId} value={ticker.tickerId}>
                    {ticker.symbol} ({ticker.companyName})
                  </option>
                ))}
              </select>
            </label>
            <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem', color: '#94a3b8' }}>
              数量
              <input type="number" min={1} value={quantity}
                onChange={(e) => setQuantity(Number(e.target.value))}
                style={{ background: '#1e293b', color: '#e2e8f0', border: '1px solid #334155', borderRadius: '6px', padding: '0.4rem 0.6rem' }} />
            </label>
            <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.875rem', color: '#94a3b8' }}>
              指値価格
              <input type="number" min={1} value={limitPriceAmount}
                onChange={(e) => setLimitPriceAmount(Number(e.target.value))}
                style={{ background: '#1e293b', color: '#e2e8f0', border: '1px solid #334155', borderRadius: '6px', padding: '0.4rem 0.6rem' }} />
            </label>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginTop: '0.25rem' }}>
              {[
                { label: 'BuyNow',    action: () => executeAction('buy'),                    color: '#16a34a' },
                { label: 'SellNow',   action: () => executeAction('sell'),                   color: '#dc2626' },
                { label: 'Wait',      action: executeWaitAction,                             color: '#475569' },
                { label: 'BuyLimit',  action: () => executeAction('buy', limitPriceAmount),  color: '#065f46' },
                { label: 'SellLimit', action: () => executeAction('sell', limitPriceAmount), color: '#7f1d1d' },
              ].map(({ label, action, color }) => (
                <button key={label} type="button" disabled={isSubmitting || isLoading} onClick={action}
                  style={{ background: color, color: '#fff', border: 'none', borderRadius: '6px', padding: '0.45rem 0.9rem', cursor: 'pointer', fontSize: '0.875rem', opacity: isSubmitting ? 0.6 : 1 }}>
                  {isSubmitting ? '送信中...' : label}
                </button>
              ))}
            </div>
          </form>
        </div>

        {/* 右: ポートフォリオ */}
        <div className="card">
          <h2>ポートフォリオ</h2>
          {portfolio && (
            <>
              <div className="stat-row">
                <div className="stat">
                  <span className="stat-label">ターン</span>
                  <span className="stat-value">{currentTurn}</span>
                </div>
                <div className="stat">
                  <span className="stat-label">現金</span>
                  <span className="stat-value">¥{portfolio.cash.amount.toLocaleString()}</span>
                </div>
                <div className="stat">
                  <span className="stat-label">評価額</span>
                  <span className="stat-value">¥{portfolio.valuation.amount.toLocaleString()}</span>
                </div>
                <div className="stat">
                  <span className="stat-label">損益</span>
                  <span className="stat-value" style={{ color: portfolio.profitLoss.amount >= 0 ? '#4ade80' : '#f87171' }}>
                    {portfolio.profitLoss.amount >= 0 ? '+' : ''}¥{portfolio.profitLoss.amount.toLocaleString()}
                  </span>
                </div>
              </div>
              {portfolio.holdings.length > 0 && (
                <>
                  <p style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.5rem' }}>保有銘柄</p>
                  <table>
                    <thead><tr><th>銘柄</th><th>株数</th><th>評価額</th></tr></thead>
                    <tbody>
                      {portfolio.holdings.map((h) => (
                        <tr key={h.tickerId}>
                          <td>{h.symbol}</td>
                          <td>{h.quantity}</td>
                          <td>¥{h.marketValue.amount.toLocaleString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </>
              )}
            </>
          )}
        </div>
      </div>

      {marketSnapshot && (
        <>
          {/* 気配値サマリー: 買い・売り横並び */}
          <div className="page-grid" style={{ marginBottom: '1.25rem' }}>
            <div className="card">
              <h2>気配値（買い）</h2>
              {buySummaryBySymbol.length === 0
                ? <p style={{ color: '#64748b' }}>買い注文はありません。</p>
                : <table>
                    <thead><tr><th>銘柄</th><th>合計数量</th><th>最高気配</th></tr></thead>
                    <tbody>
                      {buySummaryBySymbol.map((s) => (
                        <tr key={s.symbol}>
                          <td>{s.symbol}</td>
                          <td>{s.totalQuantity}</td>
                          <td style={{ color: '#4ade80' }}>¥{s.bestPrice.toLocaleString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
              }
            </div>
            <div className="card">
              <h2>気配値（売り）</h2>
              {sellSummaryBySymbol.length === 0
                ? <p style={{ color: '#64748b' }}>売り注文はありません。</p>
                : <table>
                    <thead><tr><th>銘柄</th><th>合計数量</th><th>最安気配</th></tr></thead>
                    <tbody>
                      {sellSummaryBySymbol.map((s) => (
                        <tr key={s.symbol}>
                          <td>{s.symbol}</td>
                          <td>{s.totalQuantity}</td>
                          <td style={{ color: '#f87171' }}>¥{s.bestPrice.toLocaleString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
              }
            </div>
          </div>

          {/* 注文票: 買い・売り横並び */}
          <div className="page-grid" style={{ marginBottom: '1.25rem' }}>
            <div className="card">
              <h2>注文票（買い）</h2>
              {sortedBuyOrders.length === 0
                ? <p style={{ color: '#64748b' }}>買い注文はありません。</p>
                : <table>
                    <thead><tr><th>銘柄</th><th>数量</th><th>価格</th><th>発注元</th></tr></thead>
                    <tbody>
                      {sortedBuyOrders.slice(0, 20).map((o) => (
                        <tr key={o.orderId}>
                          <td>{o.symbol}</td>
                          <td>{o.quantity}</td>
                          <td>¥{o.price.amount.toLocaleString()}</td>
                          <td style={{ color: '#64748b' }}>{o.origin}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
              }
            </div>
            <div className="card">
              <h2>注文票（売り）</h2>
              {sortedSellOrders.length === 0
                ? <p style={{ color: '#64748b' }}>売り注文はありません。</p>
                : <table>
                    <thead><tr><th>銘柄</th><th>数量</th><th>価格</th><th>発注元</th></tr></thead>
                    <tbody>
                      {sortedSellOrders.slice(0, 20).map((o) => (
                        <tr key={o.orderId}>
                          <td>{o.symbol}</td>
                          <td>{o.quantity}</td>
                          <td>¥{o.price.amount.toLocaleString()}</td>
                          <td style={{ color: '#64748b' }}>{o.origin}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
              }
            </div>
          </div>

          {/* 約定履歴: フル幅 */}
          <div className="card">
            <h2>約定履歴</h2>
            {marketSnapshot.trades.length === 0
              ? <p style={{ color: '#64748b' }}>約定はまだありません。</p>
              : <table>
                  <thead><tr><th>銘柄</th><th>数量</th><th>価格</th><th>手数料</th><th>時刻</th></tr></thead>
                  <tbody>
                    {marketSnapshot.trades.slice(0, 10).map((t) => (
                      <tr key={t.tradeId}>
                        <td>{t.symbol}</td>
                        <td>{t.quantity}</td>
                        <td>¥{t.price.amount.toLocaleString()}</td>
                        <td style={{ color: '#64748b' }}>¥{t.fee.amount.toLocaleString()}</td>
                        <td style={{ color: '#64748b' }}>{new Date(t.executedAt).toLocaleString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
            }
          </div>
        </>
      )}
    </div>
  )
}
