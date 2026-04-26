import { useEffect, useState } from 'react'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts'
import { fetchPriceHistory, fetchTickers } from '../api/tickers'
import type { PriceRecordDto, TickerSummaryDto } from '../api/types'

const LIMIT_OPTIONS = [10, 20, 50] as const

export default function Chart() {
  const [tickers, setTickers] = useState<TickerSummaryDto[]>([])
  const [selectedTickerId, setSelectedTickerId] = useState<string | null>(null)
  const [history, setHistory] = useState<PriceRecordDto[]>([])
  const [limit, setLimit] = useState<number>(20)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetchTickers()
      .then((data) => {
        setTickers(data)
        if (data.length > 0) setSelectedTickerId(data[0].tickerId)
      })
      .catch((e) => setError(String(e)))
      .finally(() => setIsLoading(false))
  }, [])

  useEffect(() => {
    if (!selectedTickerId) return
    setIsLoading(true)
    fetchPriceHistory(selectedTickerId, limit)
      .then(setHistory)
      .catch((e) => setError(String(e)))
      .finally(() => setIsLoading(false))
  }, [selectedTickerId, limit])

  const chartData = history.map((r) => ({
    turn: `T${r.turn}`,
    price: r.price.amount,
  }))

  const selectedTicker = tickers.find((t) => t.tickerId === selectedTickerId)

  return (
    <section style={{ maxWidth: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
        <h1 style={{ margin: 0 }}>銘柄チャート</h1>
        <select
          value={limit}
          onChange={(e) => setLimit(Number(e.target.value))}
          style={{ background: '#1e293b', color: '#e2e8f0', border: '1px solid #334155', borderRadius: '6px', padding: '0.35rem 0.75rem' }}
        >
          {LIMIT_OPTIONS.map((n) => (
            <option key={n} value={n}>直近 {n} ターン</option>
          ))}
        </select>
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1.5rem', flexWrap: 'wrap' }}>
        {tickers.map((t) => (
          <button
            key={t.tickerId}
            onClick={() => setSelectedTickerId(t.tickerId)}
            style={{
              padding: '0.35rem 1rem',
              borderRadius: '999px',
              border: 'none',
              cursor: 'pointer',
              background: t.tickerId === selectedTickerId ? '#2563eb' : '#1e293b',
              color: '#e2e8f0',
            }}
          >
            {t.symbol}
          </button>
        ))}
      </div>

      {isLoading && <p style={{ color: '#94a3b8' }}>読み込み中...</p>}
      {error && <p style={{ color: '#ef4444' }}>{error}</p>}

      {!isLoading && !error && selectedTicker && (
        <>
          <p style={{ color: '#94a3b8', marginBottom: '1rem' }}>
            {selectedTicker.symbol} — 現在値: ¥{selectedTicker.currentPrice.amount.toLocaleString()}
          </p>
          <ResponsiveContainer width="100%" height={400}>
            <LineChart data={chartData} margin={{ top: 8, right: 24, left: 16, bottom: 8 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
              <XAxis dataKey="turn" stroke="#64748b" tick={{ fill: '#94a3b8', fontSize: 12 }} />
              <YAxis stroke="#64748b" tick={{ fill: '#94a3b8', fontSize: 12 }} tickFormatter={(v) => `¥${v.toLocaleString()}`} />
              <Tooltip
                contentStyle={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: '8px' }}
                labelStyle={{ color: '#94a3b8' }}
                formatter={(value) => [`¥${Number(value).toLocaleString()}`, '価格']}
              />
              <Line type="monotone" dataKey="price" stroke="#4f8ef7" strokeWidth={2} dot={{ r: 3, fill: '#4f8ef7' }} activeDot={{ r: 5 }} />
            </LineChart>
          </ResponsiveContainer>
        </>
      )}

      {!isLoading && !error && chartData.length === 0 && (
        <p style={{ color: '#64748b' }}>まだ価格履歴がありません。アクションを実行してターンを進めてください。</p>
      )}
    </section>
  )
}
