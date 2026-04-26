# 銘柄チャート機能 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** バックエンドに価格履歴を追加し、フロントエンドに専用チャートページを新設する

**Architecture:** `Ticker` エンティティに `PriceRecord` リストを持たせ、`UpdatePrice` 呼び出し時に自動追記する。`GET /api/tickers/{id}/price-history` エンドポイントを追加し、フロントエンドの `Chart.tsx` で Recharts を使って折れ線グラフを描画する。

**Tech Stack:** C# / ASP.NET Core（バックエンド）、React / TypeScript / Recharts（フロントエンド）

**作業ディレクトリ:** `.worktrees/feature-chart/`

---

## ファイル構成

| ファイル | 操作 | 役割 |
|---|---|---|
| `src/Domain/ValueObjects/PriceRecord.cs` | 新規作成 | ターン番号と価格を保持する値オブジェクト |
| `src/Domain/Entities/Ticker.cs` | 変更 | PriceHistory リストと UpdatePrice シグネチャ変更 |
| `backend/FinLearnApp.Api/Data/InMemoryStore.cs` | 変更 | ApplyPriceFluctuation にターン番号を渡す |
| `backend/FinLearnApp.Tests/Domain/TickerTests.cs` | 新規作成 | Ticker の価格履歴ユニットテスト |
| `backend/FinLearnApp.Api/Models/Api/TickerDtos.cs` | 変更 | PriceRecordDto 追加 |
| `backend/FinLearnApp.Api/Controllers/TickersController.cs` | 変更 | GetPriceHistory エンドポイント追加 |
| `backend/FinLearnApp.Tests/Controllers/TickersControllerTests.cs` | 変更 | GetPriceHistory テスト追加 |
| `frontend/src/api/types.ts` | 変更 | PriceRecordDto 型追加 |
| `frontend/src/api/tickers.ts` | 新規作成 | fetchPriceHistory 関数 |
| `frontend/src/pages/Chart.tsx` | 新規作成 | チャートページ |
| `frontend/src/App.tsx` | 変更 | /chart ルート追加 |

---

### Task 1: PriceRecord 値オブジェクトの追加

**Files:**
- Create: `src/Domain/ValueObjects/PriceRecord.cs`
- Test: `backend/FinLearnApp.Tests/Domain/TickerTests.cs`

- [ ] **Step 1: テストファイルを作成してコンパイルエラーを確認**

`backend/FinLearnApp.Tests/Domain/TickerTests.cs` を新規作成:

```csharp
using System;
using System.Collections.Generic;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

public class TickerTests
{
    private static readonly TickerId TestTickerId = new(Guid.Parse("aaaa0000-0000-0000-0000-000000000001"));
    private static readonly CompanyId TestCompanyId = new(Guid.Parse("bbbb0000-0000-0000-0000-000000000001"));

    private static Ticker CreateTicker(decimal price = 1_000m)
        => new(TestTickerId, TestCompanyId, "TEST", 1, Money.Jpy(price));

    [Fact]
    public void Ticker_InitialPriceHistory_ContainsTurnZeroWithInitialPrice()
    {
        var ticker = CreateTicker(1_000m);

        Assert.Single(ticker.PriceHistory);
        Assert.Equal(0, ticker.PriceHistory[0].Turn);
        Assert.Equal(1_000m, ticker.PriceHistory[0].Price.Amount);
    }
}
```

- [ ] **Step 2: テストを実行してコンパイルエラーを確認**

```bash
dotnet test backend/FinLearnApp.Tests/ --filter "FullyQualifiedName~TickerTests" 2>&1 | tail -10
```

Expected: コンパイルエラー（`PriceHistory` が存在しない）

- [ ] **Step 3: PriceRecord 値オブジェクトを作成**

`src/Domain/ValueObjects/PriceRecord.cs` を新規作成:

```csharp
namespace FinLearnApp.Domain.ValueObjects;

public readonly record struct PriceRecord(int Turn, Money Price);
```

- [ ] **Step 4: Ticker に PriceHistory を追加**

`src/Domain/Entities/Ticker.cs` を以下に置き換え:

```csharp
using System;
using System.Collections.Generic;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Ticker
{
    private readonly List<PriceRecord> _priceHistory = new();
    private const int MaxHistorySize = 100;

    public TickerId Id { get; }
    public CompanyId CompanyId { get; }
    public string Symbol { get; }
    public int UnitSize { get; }
    public Money CurrentPrice { get; private set; }
    public IReadOnlyList<PriceRecord> PriceHistory => _priceHistory.AsReadOnly();

    public Ticker(TickerId id, CompanyId companyId, string symbol, int unitSize, Money currentPrice)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (unitSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitSize), "Unit size must be greater than 0.");
        }

        Id = id;
        CompanyId = companyId;
        Symbol = symbol;
        UnitSize = unitSize;
        CurrentPrice = currentPrice;
        _priceHistory.Add(new PriceRecord(0, currentPrice));
    }

    public void UpdatePrice(Money newPrice, int turn)
    {
        CurrentPrice = newPrice;
        _priceHistory.Add(new PriceRecord(turn, newPrice));
        if (_priceHistory.Count > MaxHistorySize)
        {
            _priceHistory.RemoveAt(0);
        }
    }
}
```

- [ ] **Step 5: テストを実行してパスを確認**

```bash
dotnet test backend/FinLearnApp.Tests/ --filter "FullyQualifiedName~TickerTests" 2>&1 | tail -10
```

Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 6: 既存テストが壊れていないか確認**

```bash
dotnet test backend/FinLearnApp.Tests/ 2>&1 | tail -5
```

Expected: コンパイルエラー（`UpdatePrice` のシグネチャ変更で `InMemoryStore` が壊れている）→ Task 2 で修正

---

### Task 2: InMemoryStore の UpdatePrice 呼び出しを修正

**Files:**
- Modify: `backend/FinLearnApp.Api/Data/InMemoryStore.cs`

- [ ] **Step 1: ApplyPriceFluctuation を修正**

`backend/FinLearnApp.Api/Data/InMemoryStore.cs` の `AdvanceTurn` と `ApplyPriceFluctuation` を以下に変更:

```csharp
public int AdvanceTurn(InvestorId investorId)
{
    var currentTurn = GetCurrentTurn(investorId);
    var nextTurn = currentTurn + 1;
    _turnByInvestor[investorId] = nextTurn;

    ApplyPriceFluctuation(nextTurn);
    GenerateSystemOrdersForTurn();

    return nextTurn;
}

private void ApplyPriceFluctuation(int turn)
{
    foreach (var ticker in Tickers)
    {
        var rate = NextDecimal(MinPriceFluctuationRate, MaxPriceFluctuationRate);
        var newAmount = decimal.Round(ticker.CurrentPrice.Amount * rate, 2, MidpointRounding.AwayFromZero);
        if (newAmount < 1m)
        {
            newAmount = 1m;
        }

        ticker.UpdatePrice(Money.Jpy(newAmount), turn);
    }
}
```

- [ ] **Step 2: 全テストを実行してパスを確認**

```bash
dotnet test backend/FinLearnApp.Tests/ 2>&1 | tail -5
```

Expected: `Passed! - Failed: 0, Passed: 251`

- [ ] **Step 3: コミット**

```bash
git add src/Domain/ValueObjects/PriceRecord.cs src/Domain/Entities/Ticker.cs backend/FinLearnApp.Api/Data/InMemoryStore.cs backend/FinLearnApp.Tests/Domain/TickerTests.cs
git commit -m "feat: Tickerに価格履歴(PriceRecord)を追加"
```

---

### Task 3: Ticker の価格履歴テストを追加

**Files:**
- Modify: `backend/FinLearnApp.Tests/Domain/TickerTests.cs`

- [ ] **Step 1: テストを追記**

`backend/FinLearnApp.Tests/Domain/TickerTests.cs` の `TickerTests` クラスに以下のテストを追加:

```csharp
[Fact]
public void Ticker_UpdatePrice_AppendsToHistory()
{
    var ticker = CreateTicker(1_000m);

    ticker.UpdatePrice(Money.Jpy(1_050m), turn: 1);
    ticker.UpdatePrice(Money.Jpy(1_100m), turn: 2);

    Assert.Equal(3, ticker.PriceHistory.Count); // turn0 + turn1 + turn2
    Assert.Equal(1, ticker.PriceHistory[1].Turn);
    Assert.Equal(1_050m, ticker.PriceHistory[1].Price.Amount);
    Assert.Equal(2, ticker.PriceHistory[2].Turn);
    Assert.Equal(1_100m, ticker.PriceHistory[2].Price.Amount);
}

[Fact]
public void Ticker_UpdatePrice_ExceedsMaxHistory_RemovesOldest()
{
    var ticker = CreateTicker(1_000m);

    // 100件追加（初期1件 + 100件 = 101件になると古い方から削除）
    for (int i = 1; i <= 100; i++)
    {
        ticker.UpdatePrice(Money.Jpy(1_000m + i), turn: i);
    }

    Assert.Equal(100, ticker.PriceHistory.Count);
    // 最初の turn=0 のレコードが削除され turn=1 が先頭になっている
    Assert.Equal(1, ticker.PriceHistory[0].Turn);
}

[Fact]
public void Ticker_PriceHistory_ReturnsInChronologicalOrder()
{
    var ticker = CreateTicker(1_000m);
    ticker.UpdatePrice(Money.Jpy(1_010m), turn: 1);
    ticker.UpdatePrice(Money.Jpy(1_020m), turn: 2);

    Assert.Equal(0, ticker.PriceHistory[0].Turn);
    Assert.Equal(1, ticker.PriceHistory[1].Turn);
    Assert.Equal(2, ticker.PriceHistory[2].Turn);
}
```

- [ ] **Step 2: テストを実行してパスを確認**

```bash
dotnet test backend/FinLearnApp.Tests/ --filter "FullyQualifiedName~TickerTests" 2>&1 | tail -5
```

Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 3: コミット**

```bash
git add backend/FinLearnApp.Tests/Domain/TickerTests.cs
git commit -m "test: Ticker価格履歴のテストを追加"
```

---

### Task 4: API エンドポイント追加

**Files:**
- Modify: `backend/FinLearnApp.Api/Models/Api/TickerDtos.cs`
- Modify: `backend/FinLearnApp.Api/Controllers/TickersController.cs`
- Modify: `backend/FinLearnApp.Tests/Controllers/TickersControllerTests.cs`

- [ ] **Step 1: コントローラーテストを先に追記**

`backend/FinLearnApp.Tests/Controllers/TickersControllerTests.cs` に以下を追加（既存の `TickersControllerTests` クラス内）:

```csharp
[Fact]
public void GetPriceHistory_ExistingTicker_ReturnsHistory()
{
    // Arrange
    var store = CreateStore();
    var controller = new TickersController(store);
    var ticker = store.Tickers.First();

    // Act
    var result = controller.GetPriceHistory(ticker.Id.Value, limit: 20);

    // Assert
    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var history = Assert.IsAssignableFrom<IReadOnlyList<PriceRecordDto>>(ok.Value);
    Assert.NotEmpty(history); // 初期価格(turn=0)が含まれる
}

[Fact]
public void GetPriceHistory_UnknownTicker_Returns404()
{
    // Arrange
    var store = CreateStore();
    var controller = new TickersController(store);

    // Act
    var result = controller.GetPriceHistory(Guid.NewGuid(), limit: 20);

    // Assert
    Assert.IsType<ObjectResult>(result.Result);
    var obj = (ObjectResult)result.Result!;
    Assert.Equal(404, obj.StatusCode);
}

[Fact]
public void GetPriceHistory_LimitApplied_ReturnsAtMostLimitRecords()
{
    // Arrange
    var store = CreateStore();
    // 5ターン進める
    var investorId = store.Portfolios.First().InvestorId;
    for (int i = 0; i < 5; i++) store.AdvanceTurn(investorId);

    var controller = new TickersController(store);
    var ticker = store.Tickers.First();

    // Act
    var result = controller.GetPriceHistory(ticker.Id.Value, limit: 3);

    // Assert
    var ok = Assert.IsType<OkObjectResult>(result.Result);
    var history = Assert.IsAssignableFrom<IReadOnlyList<PriceRecordDto>>(ok.Value);
    Assert.Equal(3, history.Count);
}
```

- [ ] **Step 2: テストを実行してコンパイルエラーを確認**

```bash
dotnet test backend/FinLearnApp.Tests/ --filter "GetPriceHistory" 2>&1 | tail -10
```

Expected: コンパイルエラー（`PriceRecordDto` と `GetPriceHistory` が存在しない）

- [ ] **Step 3: PriceRecordDto を追加**

`backend/FinLearnApp.Api/Models/Api/TickerDtos.cs` の末尾に追加:

```csharp
public sealed record PriceRecordDto(int Turn, MoneyDto Price);
```

- [ ] **Step 4: GetPriceHistory エンドポイントを追加**

`backend/FinLearnApp.Api/Controllers/TickersController.cs` に追加:

```csharp
[HttpGet("{tickerId:guid}/price-history")]
public ActionResult<IReadOnlyList<PriceRecordDto>> GetPriceHistory(
    Guid tickerId,
    [FromQuery] int limit = 20)
{
    var ticker = _store.FindTicker(new TickerId(tickerId));
    if (ticker is null)
    {
        return ApiProblemFactory.NotFound(this, "Ticker was not found.", "tickers.not_found");
    }

    var clampedLimit = Math.Clamp(limit, 1, 100);
    var history = ticker.PriceHistory
        .TakeLast(clampedLimit)
        .Select(r => new PriceRecordDto(r.Turn, ToMoneyDto(r.Price)))
        .ToList();

    return Ok(history);
}
```

- [ ] **Step 5: テストを実行してパスを確認**

```bash
dotnet test backend/FinLearnApp.Tests/ 2>&1 | tail -5
```

Expected: `Passed! - Failed: 0, Passed: 254`（3件追加）

- [ ] **Step 6: コミット**

```bash
git add backend/FinLearnApp.Api/Models/Api/TickerDtos.cs backend/FinLearnApp.Api/Controllers/TickersController.cs backend/FinLearnApp.Tests/Controllers/TickersControllerTests.cs
git commit -m "feat: GET /api/tickers/{id}/price-history エンドポイントを追加"
```

---

### Task 5: フロントエンド — 型・API 関数追加

**Files:**
- Modify: `frontend/src/api/types.ts`
- Create: `frontend/src/api/tickers.ts`

- [ ] **Step 1: PriceRecordDto 型を追加**

`frontend/src/api/types.ts` の末尾に追加:

```typescript
export type PriceRecordDto = {
  turn: number
  price: MoneyDto
}
```

- [ ] **Step 2: tickers.ts を作成**

`frontend/src/api/tickers.ts` を新規作成:

```typescript
import { fetchJson } from './client'
import type { PriceRecordDto, TickerSummaryDto } from './types'

export async function fetchTickers(): Promise<TickerSummaryDto[]> {
  return fetchJson<TickerSummaryDto[]>('/api/tickers')
}

export async function fetchPriceHistory(
  tickerId: string,
  limit: number = 20
): Promise<PriceRecordDto[]> {
  return fetchJson<PriceRecordDto[]>(
    `/api/tickers/${tickerId}/price-history?limit=${limit}`
  )
}
```

- [ ] **Step 3: TypeScript ビルドエラーがないか確認**

```bash
cd frontend && pnpm build 2>&1 | tail -10
```

Expected: ビルド成功

- [ ] **Step 4: コミット**

```bash
git add frontend/src/api/types.ts frontend/src/api/tickers.ts
git commit -m "feat: 価格履歴のフロントエンド型・API関数を追加"
```

---

### Task 6: フロントエンド — Recharts インストール

**Files:**
- Modify: `frontend/package.json`

- [ ] **Step 1: Recharts をインストール**

```bash
cd frontend && pnpm add recharts
```

- [ ] **Step 2: TypeScript 型定義を確認**

```bash
cd frontend && pnpm build 2>&1 | tail -5
```

Expected: ビルド成功（recharts は型定義を同梱している）

- [ ] **Step 3: コミット**

```bash
git add frontend/package.json frontend/pnpm-lock.yaml
git commit -m "feat: recharts を依存に追加"
```

---

### Task 7: フロントエンド — Chart.tsx 作成

**Files:**
- Create: `frontend/src/pages/Chart.tsx`
- Modify: `frontend/src/App.tsx`

- [ ] **Step 1: Chart.tsx を作成**

`frontend/src/pages/Chart.tsx` を新規作成:

```tsx
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
                formatter={(value: number) => [`¥${value.toLocaleString()}`, '価格']}
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
```

- [ ] **Step 2: App.tsx に /chart ルートを追加**

`frontend/src/App.tsx` を以下に変更:

```tsx
import './App.css'
import { NavLink, Route, Routes } from 'react-router-dom'
import Home from './pages/Home'
import Tickers from './pages/Tickers'
import TickerDetail from './pages/TickerDetail'
import Portfolios from './pages/Portfolios'
import PortfolioDetail from './pages/PortfolioDetail'
import Actions from './pages/Actions'
import Chart from './pages/Chart'
import NotFound from './pages/NotFound'

function App() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <span className="app-title">FinLearnApp</span>
        <nav>
          <NavLink to="/" end>ホーム</NavLink>
          <NavLink to="/tickers">銘柄</NavLink>
          <NavLink to="/portfolios">ポートフォリオ</NavLink>
          <NavLink to="/actions">アクション</NavLink>
          <NavLink to="/chart">チャート</NavLink>
        </nav>
      </header>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/tickers" element={<Tickers />} />
          <Route path="/tickers/:tickerId" element={<TickerDetail />} />
          <Route path="/portfolios" element={<Portfolios />} />
          <Route path="/portfolios/:investorId" element={<PortfolioDetail />} />
          <Route path="/actions" element={<Actions />} />
          <Route path="/chart" element={<Chart />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>
    </div>
  )
}

export default App
```

- [ ] **Step 3: TypeScript ビルドで型エラーがないか確認**

```bash
cd frontend && pnpm build 2>&1 | tail -10
```

Expected: ビルド成功

- [ ] **Step 4: 開発サーバーで動作確認**

```bash
# ターミナル1: バックエンド起動
cd backend/FinLearnApp.Api && dotnet run

# ターミナル2: フロントエンド起動
cd frontend && pnpm dev
```

ブラウザで `http://localhost:5173/chart` を開き確認:
- 銘柄タブが表示される
- 直近ターン数セレクターが右上にある
- 初期状態でターン0の価格がプロットされている
- アクションページでBuyNow等を実行後、チャートにデータが追加される

- [ ] **Step 5: コミット**

```bash
git add frontend/src/pages/Chart.tsx frontend/src/App.tsx
git commit -m "feat: 銘柄チャートページを追加"
```
