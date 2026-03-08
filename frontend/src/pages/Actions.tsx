import { useEffect, useState } from 'react'
import { buyLimit, buyNow, sellLimit, sellNow, waitAction } from '../api/actions'
import { fetchMarketSnapshot } from '../api/market'
import { fetchJson } from '../api/client'
import type {
  ActionLimitRequestDto,
  ActionTradeRequestDto,
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

  const executeTradeAction = async (actionType: 'buy' | 'sell') => {
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

    const payload: ActionTradeRequestDto = {
      investorId: demoInvestorId,
      tickerId,
      quantity,
      expectedTurn: currentTurn,
    }

    setIsSubmitting(true)
    try {
      const result =
        actionType === 'buy' ? await buyNow(payload) : await sellNow(payload)

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

  const executeLimitAction = async (actionType: 'buy' | 'sell') => {
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

    if (limitPriceAmount <= 0) {
      setError('指値価格は1以上を指定してください。')
      return
    }

    const payload: ActionLimitRequestDto = {
      investorId: demoInvestorId,
      tickerId,
      quantity,
      limitPriceAmount,
      expectedTurn: currentTurn,
    }

    setIsSubmitting(true)
    try {
      const result =
        actionType === 'buy' ? await buyLimit(payload) : await sellLimit(payload)

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
          <p>現在ターン: {currentTurn}</p>
        </div>
      )}

      <form>
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
            />
          </label>
        </div>
        <div>
          <label>
            指値価格
            <input
              type="number"
              min={1}
              value={limitPriceAmount}
              onChange={(event) => setLimitPriceAmount(Number(event.target.value))}
            />
          </label>
        </div>
        <div className="actions">
          <button
            type="button"
            disabled={isSubmitting || isLoading}
            onClick={() => executeTradeAction('buy')}
          >
            {isSubmitting ? '送信中...' : 'BuyNow'}
          </button>
          <button
            type="button"
            disabled={isSubmitting || isLoading}
            onClick={() => executeTradeAction('sell')}
          >
            {isSubmitting ? '送信中...' : 'SellNow'}
          </button>
          <button type="button" disabled={isSubmitting || isLoading} onClick={executeWaitAction}>
            {isSubmitting ? '送信中...' : 'Wait'}
          </button>
          <button
            type="button"
            disabled={isSubmitting || isLoading}
            onClick={() => executeLimitAction('buy')}
          >
            {isSubmitting ? '送信中...' : 'BuyLimit'}
          </button>
          <button
            type="button"
            disabled={isSubmitting || isLoading}
            onClick={() => executeLimitAction('sell')}
          >
            {isSubmitting ? '送信中...' : 'SellLimit'}
          </button>
        </div>
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

      {marketSnapshot && (
        <div>
          <h2>銘柄別サマリー（買い）</h2>
          {buySummaryBySymbol.length === 0 && <p>買い注文はありません。</p>}
          {buySummaryBySymbol.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>銘柄</th>
                  <th>合計数量</th>
                  <th>最高買い気配</th>
                </tr>
              </thead>
              <tbody>
                {buySummaryBySymbol.map((summary) => (
                  <tr key={summary.symbol}>
                    <td>{summary.symbol}</td>
                    <td>{summary.totalQuantity}</td>
                    <td>
                      {summary.bestPrice.toLocaleString()} {summary.currency}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <h2>銘柄別サマリー（売り）</h2>
          {sellSummaryBySymbol.length === 0 && <p>売り注文はありません。</p>}
          {sellSummaryBySymbol.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>銘柄</th>
                  <th>合計数量</th>
                  <th>最安売り気配</th>
                </tr>
              </thead>
              <tbody>
                {sellSummaryBySymbol.map((summary) => (
                  <tr key={summary.symbol}>
                    <td>{summary.symbol}</td>
                    <td>{summary.totalQuantity}</td>
                    <td>
                      {summary.bestPrice.toLocaleString()} {summary.currency}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <h2>注文票（買い）</h2>
          {sortedBuyOrders.length === 0 && <p>買い注文はありません。</p>}
          {sortedBuyOrders.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>銘柄</th>
                  <th>数量</th>
                  <th>価格</th>
                  <th>発注元</th>
                  <th>発注時刻</th>
                </tr>
              </thead>
              <tbody>
                {sortedBuyOrders.slice(0, 20).map((order) => (
                  <tr key={order.orderId}>
                    <td>{order.symbol}</td>
                    <td>{order.quantity}</td>
                    <td>
                      {order.price.amount.toLocaleString()} {order.price.currency}
                    </td>
                    <td>{order.origin}</td>
                    <td>{new Date(order.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <h2>注文票（売り）</h2>
          {sortedSellOrders.length === 0 && <p>売り注文はありません。</p>}
          {sortedSellOrders.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>銘柄</th>
                  <th>数量</th>
                  <th>価格</th>
                  <th>発注元</th>
                  <th>発注時刻</th>
                </tr>
              </thead>
              <tbody>
                {sortedSellOrders.slice(0, 20).map((order) => (
                  <tr key={order.orderId}>
                    <td>{order.symbol}</td>
                    <td>{order.quantity}</td>
                    <td>
                      {order.price.amount.toLocaleString()} {order.price.currency}
                    </td>
                    <td>{order.origin}</td>
                    <td>{new Date(order.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <h2>約定履歴</h2>
          {marketSnapshot.trades.length === 0 && <p>約定はまだありません。</p>}
          {marketSnapshot.trades.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>銘柄</th>
                  <th>数量</th>
                  <th>価格</th>
                  <th>手数料</th>
                  <th>時刻</th>
                </tr>
              </thead>
              <tbody>
                {marketSnapshot.trades.slice(0, 10).map((trade) => (
                  <tr key={trade.tradeId}>
                    <td>{trade.symbol}</td>
                    <td>{trade.quantity}</td>
                    <td>
                      {trade.price.amount.toLocaleString()} {trade.price.currency}
                    </td>
                    <td>
                      {trade.fee.amount.toLocaleString()} {trade.fee.currency}
                    </td>
                    <td>{new Date(trade.executedAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </section>
  )
}
