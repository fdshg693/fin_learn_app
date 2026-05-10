# API Actions 統合 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `buy-now`/`buy-limit`/`sell-now`/`sell-limit` の4エンドポイントを `buy`/`sell` の2エンドポイントに統合し、`limitPrice` フィールドの有無で成行/指値を判別する。

**Architecture:** コントローラー層のみ変更。`limitPrice == null` なら `BuyNowCommand`/`SellNowCommand`、`limitPrice != null` なら `BuyLimitCommand`/`SellLimitCommand` へ振り分ける。Application層の Command/Handler は一切変更しない。

**Tech Stack:** ASP.NET Core 9 / MediatR 12 / React + TypeScript / xUnit

---

## ファイル変更マップ

| 操作 | ファイル |
|---|---|
| 変更 | `backend/FinLearnApp.Api/Models/Api/ActionDtos.cs` |
| 変更 | `backend/FinLearnApp.Api/Controllers/ActionsController.cs` |
| 新規作成 | `backend/FinLearnApp.Tests/Controllers/ActionsControllerTests.cs` |
| 変更 | `frontend/src/api/types.ts` |
| 変更 | `frontend/src/api/actions.ts` |
| 変更 | `frontend/src/pages/Actions.tsx` |

---

### Task 1: バックエンド DTO 更新

**Files:**
- Modify: `backend/FinLearnApp.Api/Models/Api/ActionDtos.cs`

- [ ] **Step 1: `ActionTradeRequestDto` と `ActionLimitRequestDto` を削除し、`ActionBuyRequestDto`・`ActionSellRequestDto` を追加する**

`backend/FinLearnApp.Api/Models/Api/ActionDtos.cs` を以下の内容に書き換える:

```csharp
using System;

namespace FinLearnApp.Api.Models.Api;

public sealed record ActionBuyRequestDto(
    Guid InvestorId,
    Guid TickerId,
    int Quantity,
    decimal? LimitPrice,
    int ExpectedTurn);

public sealed record ActionSellRequestDto(
    Guid InvestorId,
    Guid TickerId,
    int Quantity,
    decimal? LimitPrice,
    int ExpectedTurn);

/// <summary>
/// Waitアクション（見送り）用のリクエスト。
/// </summary>
public sealed record ActionWaitRequestDto
{
    public Guid InvestorId { get; }
    public int ExpectedTurn { get; }

    public ActionWaitRequestDto(Guid investorId, int expectedTurn)
    {
        InvestorId = investorId;
        ExpectedTurn = expectedTurn;
    }
}

/// <summary>
/// アクション実行結果。
/// </summary>
public sealed record ActionResultDto
{
    public bool Success { get; }
    public string Message { get; }
    public PortfolioDto Portfolio { get; }
    public int CurrentTurn { get; }

    public ActionResultDto(bool success, string message, PortfolioDto portfolio, int currentTurn)
    {
        Success = success;
        Message = message;
        Portfolio = portfolio;
        CurrentTurn = currentTurn;
    }
}
```

- [ ] **Step 2: ビルドエラーを確認する（コントローラーが古い型を参照しているため失敗する）**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app
dotnet build backend/FinLearnApp.Api
```

Expected: `ActionTradeRequestDto`/`ActionLimitRequestDto` への参照でビルドエラーが出ることを確認

---

### Task 2: コントローラーのディスパッチテストを書く

**Files:**
- Create: `backend/FinLearnApp.Tests/Controllers/ActionsControllerTests.cs`

- [ ] **Step 1: テストファイルを作成する（`Buy`/`Sell` メソッドが未定義なのでビルドエラーになる）**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FinLearnApp.Api.Controllers;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Mappers;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Application.Actions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinLearnApp.Tests.Controllers;

public class ActionsControllerTests
{
    private static readonly Guid InvestorGuid = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TickerGuid   = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static (FakeMediator mediator, ActionsController controller) CreateController()
    {
        var store = SeedData.Create();
        var mediator = new FakeMediator();
        var controller = new ActionsController(
            mediator,
            new PortfolioMapper(store),
            NullLogger<ActionsController>.Instance);
        return (mediator, controller);
    }

    [Fact]
    public async Task Buy_WithoutLimitPrice_DispatchesBuyNowCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionBuyRequestDto(InvestorGuid, TickerGuid, 5, null, 0);

        await controller.Buy(request);

        Assert.Equal(typeof(BuyNowCommand), mediator.LastCommandType);
    }

    [Fact]
    public async Task Buy_WithLimitPrice_DispatchesBuyLimitCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionBuyRequestDto(InvestorGuid, TickerGuid, 5, 1200m, 0);

        await controller.Buy(request);

        Assert.Equal(typeof(BuyLimitCommand), mediator.LastCommandType);
    }

    [Fact]
    public async Task Sell_WithoutLimitPrice_DispatchesSellNowCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionSellRequestDto(InvestorGuid, TickerGuid, 5, null, 0);

        await controller.Sell(request);

        Assert.Equal(typeof(SellNowCommand), mediator.LastCommandType);
    }

    [Fact]
    public async Task Sell_WithLimitPrice_DispatchesSellLimitCommand()
    {
        var (mediator, controller) = CreateController();
        var request = new ActionSellRequestDto(InvestorGuid, TickerGuid, 5, 900m, 0);

        await controller.Sell(request);

        Assert.Equal(typeof(SellLimitCommand), mediator.LastCommandType);
    }
}

internal sealed class FakeMediator : IMediator
{
    public Type? LastCommandType { get; private set; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        LastCommandType = request.GetType();
        var result = ActionExecutionResult.BadRequest("dispatched");
        return Task.FromResult((TResponse)(object)result);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        => Task.CompletedTask;

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(null);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        => Task.CompletedTask;
}
```

- [ ] **Step 2: ビルドエラーを確認する**

```bash
dotnet build backend/FinLearnApp.Tests
```

Expected: `CS1061: 'ActionsController' does not contain a definition for 'Buy'` のようなビルドエラーが出ることを確認

---

### Task 3: ActionsController を更新する

**Files:**
- Modify: `backend/FinLearnApp.Api/Controllers/ActionsController.cs`

- [ ] **Step 1: `BuyNow`, `BuyLimit`, `SellNow`, `SellLimit` の4メソッドを削除し、`Buy` と `Sell` を追加する**

`ActionsController.cs` の4メソッド（`BuyNow`〜`SellLimit`）を以下の2メソッドに置き換える。`Wait`・`LogActionResult`・`ToHttpResult` は変更しない。

```csharp
[HttpPost("buy")]
public async Task<ActionResult<ActionResultDto>> Buy(ActionBuyRequestDto request)
{
    _logger.LogInformation(
        "Execute action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity} limitPrice={LimitPrice} expectedTurn={ExpectedTurn}",
        "Buy",
        request.InvestorId,
        request.TickerId,
        request.Quantity,
        request.LimitPrice,
        request.ExpectedTurn);

    IRequest<ActionExecutionResult> command = request.LimitPrice.HasValue
        ? new BuyLimitCommand(request.InvestorId, request.TickerId, request.Quantity, request.LimitPrice.Value, request.ExpectedTurn)
        : new BuyNowCommand(request.InvestorId, request.TickerId, request.Quantity, request.ExpectedTurn);

    var response = await _mediator.Send(command);

    LogActionResult("Buy", request.InvestorId, request.TickerId, request.Quantity, response);
    return ToHttpResult(response);
}

[HttpPost("sell")]
public async Task<ActionResult<ActionResultDto>> Sell(ActionSellRequestDto request)
{
    _logger.LogInformation(
        "Execute action={Action} investorId={InvestorId} tickerId={TickerId} quantity={Quantity} limitPrice={LimitPrice} expectedTurn={ExpectedTurn}",
        "Sell",
        request.InvestorId,
        request.TickerId,
        request.Quantity,
        request.LimitPrice,
        request.ExpectedTurn);

    IRequest<ActionExecutionResult> command = request.LimitPrice.HasValue
        ? new SellLimitCommand(request.InvestorId, request.TickerId, request.Quantity, request.LimitPrice.Value, request.ExpectedTurn)
        : new SellNowCommand(request.InvestorId, request.TickerId, request.Quantity, request.ExpectedTurn);

    var response = await _mediator.Send(command);

    LogActionResult("Sell", request.InvestorId, request.TickerId, request.Quantity, response);
    return ToHttpResult(response);
}
```

- [ ] **Step 2: テストを実行して全件通過を確認する**

```bash
dotnet test backend/FinLearnApp.Tests
```

Expected: 全テスト通過（新規4件の `ActionsControllerTests` を含む）

- [ ] **Step 3: バックエンドをコミットする**

```bash
git add backend/FinLearnApp.Api/Models/Api/ActionDtos.cs \
        backend/FinLearnApp.Api/Controllers/ActionsController.cs \
        backend/FinLearnApp.Tests/Controllers/ActionsControllerTests.cs
git commit -m "refactor: buy/sell APIエンドポイントを統合（limitPrice省略=成行、指定=指値）"
```

---

### Task 4: フロントエンド types.ts 更新

**Files:**
- Modify: `frontend/src/api/types.ts`

- [ ] **Step 1: `ActionTradeRequestDto`・`ActionLimitRequestDto` を削除し `ActionBuyRequestDto`・`ActionSellRequestDto` を追加する**

`types.ts` から以下を削除する:

```ts
export type ActionTradeRequestDto = {
  investorId: string
  tickerId: string
  quantity: number
  expectedTurn: number
}

export type ActionLimitRequestDto = {
  investorId: string
  tickerId: string
  quantity: number
  limitPriceAmount: number
  expectedTurn: number
}
```

代わりに以下を追加する:

```ts
export type ActionBuyRequestDto = {
  investorId: string
  tickerId: string
  quantity: number
  limitPrice?: number
  expectedTurn: number
}

export type ActionSellRequestDto = {
  investorId: string
  tickerId: string
  quantity: number
  limitPrice?: number
  expectedTurn: number
}
```

---

### Task 5: フロントエンド actions.ts 更新

**Files:**
- Modify: `frontend/src/api/actions.ts`

- [ ] **Step 1: 4関数を2関数に置き換える**

`frontend/src/api/actions.ts` を以下の内容に書き換える:

```ts
import { fetchJson } from './client'
import type {
  ActionBuyRequestDto,
  ActionResultDto,
  ActionSellRequestDto,
  ActionWaitRequestDto,
} from './types'

export async function buy(request: ActionBuyRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/buy', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export async function sell(request: ActionSellRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/sell', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export async function waitAction(request: ActionWaitRequestDto): Promise<ActionResultDto> {
  return fetchJson<ActionResultDto>('/api/actions/wait', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}
```

注意: `JSON.stringify` は `undefined` のプロパティをシリアライズ時に自動で省略する。`limitPrice: undefined` の場合 JSON に `limitPrice` フィールドは含まれず、C# 側で `LimitPrice == null` として扱われる。

---

### Task 6: Actions.tsx 更新

**Files:**
- Modify: `frontend/src/pages/Actions.tsx`

- [ ] **Step 1: import を更新する**

`Actions.tsx` 冒頭の import を以下に変更する:

```ts
import { buy, sell, waitAction } from '../api/actions'
import type {
  ActionBuyRequestDto,
  ActionSellRequestDto,
  ActionWaitRequestDto,
  MarketSnapshotDto,
  PortfolioDto,
  TickerSummaryDto,
} from '../api/types'
```

- [ ] **Step 2: `executeTradeAction` と `executeLimitAction` を削除し `executeAction` に統合する**

`executeTradeAction` と `executeLimitAction` の2関数（`Actions.tsx` の行 127〜207 付近）を以下1関数に置き換える:

```ts
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
      ? await buy(payload as ActionBuyRequestDto)
      : await sell(payload as ActionSellRequestDto)

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
```

- [ ] **Step 3: ボタン配列の呼び出しを更新する**

`Actions.tsx` のボタン配列（`BuyNow`〜`SellLimit` の5ボタン部分）を以下に書き換える:

```tsx
{[
  { label: 'BuyNow',    action: () => executeAction('buy'),                   color: '#16a34a' },
  { label: 'SellNow',   action: () => executeAction('sell'),                  color: '#dc2626' },
  { label: 'Wait',      action: executeWaitAction,                            color: '#475569' },
  { label: 'BuyLimit',  action: () => executeAction('buy', limitPriceAmount), color: '#065f46' },
  { label: 'SellLimit', action: () => executeAction('sell', limitPriceAmount),color: '#7f1d1d' },
].map(({ label, action, color }) => (
```

- [ ] **Step 4: ビルドと lint を確認する**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app/frontend
pnpm build
pnpm lint
```

Expected: エラーなし

- [ ] **Step 5: フロントエンドをコミットする**

```bash
cd /Users/aokitakuma/workspace/fin_learn_app
git add frontend/src/api/types.ts \
        frontend/src/api/actions.ts \
        frontend/src/pages/Actions.tsx
git commit -m "refactor: フロントエンドのactions APIをbuy/sellに統合"
```
