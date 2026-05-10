# API Actions 統合設計

**日付**: 2026-05-10  
**対象ブランチ**: takuma

---

## 背景・目的

現在、売買アクションのエンドポイントが成行・指値で分かれており、フロントエンドも4つの関数を使い分けている。これをエンドポイント単位で統合し、`limitPrice` フィールドの有無で成行/指値を判別する設計に変更する。

---

## API契約

### 変更後のエンドポイント

| エンドポイント | 役割 |
|---|---|
| `POST /api/actions/buy` | 買い注文（成行 or 指値） |
| `POST /api/actions/sell` | 売り注文（成行 or 指値） |
| `POST /api/actions/wait` | 見送り（変更なし） |

### 削除するエンドポイント

- `POST /api/actions/buy-now`
- `POST /api/actions/buy-limit`
- `POST /api/actions/sell-now`
- `POST /api/actions/sell-limit`

### リクエストボディ（buy / sell 共通）

```json
{
  "investorId": "uuid",
  "tickerId": "uuid",
  "quantity": 10,
  "limitPrice": 1200,
  "expectedTurn": 3
}
```

- `limitPrice` を**省略**すると成行注文 → `BuyNowCommand` / `SellNowCommand` へ振り分け
- `limitPrice` を**指定**すると指値注文 → `BuyLimitCommand` / `SellLimitCommand` へ振り分け
- `limitPrice` に明示的な `null` は送らない（TypeScript 側で `?` オプショナルにより省略）

### レスポンス

変更なし。`ActionResultDto` をそのまま返す。

---

## バックエンド変更

### `backend/FinLearnApp.Api/Models/Api/ActionDtos.cs`

`ActionTradeRequestDto` と `ActionLimitRequestDto` を削除し、以下を追加：

```csharp
public sealed record ActionBuyRequestDto(
    Guid InvestorId,
    Guid TickerId,
    int Quantity,
    decimal? LimitPrice,
    int ExpectedTurn
);

public sealed record ActionSellRequestDto(
    Guid InvestorId,
    Guid TickerId,
    int Quantity,
    decimal? LimitPrice,
    int ExpectedTurn
);
```

`ActionWaitRequestDto` と `ActionResultDto` は変更なし。

### `backend/FinLearnApp.Api/Controllers/ActionsController.cs`

4メソッド（`BuyNow`、`BuyLimit`、`SellNow`、`SellLimit`）を削除し、2メソッドに置き換え：

```csharp
[HttpPost("buy")]
public async Task<ActionResult<ActionResultDto>> Buy(ActionBuyRequestDto request)
{
    IRequest<ActionExecutionResult> command = request.LimitPrice.HasValue
        ? new BuyLimitCommand(request.InvestorId, request.TickerId, request.Quantity, request.LimitPrice.Value, request.ExpectedTurn)
        : new BuyNowCommand(request.InvestorId, request.TickerId, request.Quantity, request.ExpectedTurn);

    var response = await _mediator.Send(command);
    return ToHttpResult(response);
}

[HttpPost("sell")]
public async Task<ActionResult<ActionResultDto>> Sell(ActionSellRequestDto request)
{
    IRequest<ActionExecutionResult> command = request.LimitPrice.HasValue
        ? new SellLimitCommand(request.InvestorId, request.TickerId, request.Quantity, request.LimitPrice.Value, request.ExpectedTurn)
        : new SellNowCommand(request.InvestorId, request.TickerId, request.Quantity, request.ExpectedTurn);

    var response = await _mediator.Send(command);
    return ToHttpResult(response);
}
```

Application層の Command / Handler（`BuyNowCommand`、`BuyLimitCommand`、`SellNowCommand`、`SellLimitCommand`）は**変更なし**。

---

## フロントエンド変更

### `frontend/src/api/types.ts`

`ActionTradeRequestDto` と `ActionLimitRequestDto` を削除し、以下を追加：

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

### `frontend/src/api/actions.ts`

`buyNow`、`buyLimit`、`sellNow`、`sellLimit` の4関数を削除し、2関数に置き換え：

```ts
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
```

### `frontend/src/pages/Actions.tsx`

`executeTradeAction` と `executeLimitAction` の2関数を1つに統合：

```ts
const executeAction = async (side: 'buy' | 'sell', limitPrice?: number) => {
  const payload = { investorId: demoInvestorId, tickerId, quantity, limitPrice, expectedTurn: currentTurn }
  const result = side === 'buy' ? await buy(payload) : await sell(payload)
  // ...
}
```

ボタンの呼び出し：

| ボタン | 呼び出し |
|---|---|
| BuyNow | `executeAction('buy')` |
| BuyLimit | `executeAction('buy', limitPriceAmount)` |
| SellNow | `executeAction('sell')` |
| SellLimit | `executeAction('sell', limitPriceAmount)` |

---

## テスト変更

`backend/FinLearnApp.Tests/` 内の既存テスト：

- `POST /api/actions/buy-now` → `POST /api/actions/buy`（`limitPrice` なし）に更新
- `POST /api/actions/buy-limit` → `POST /api/actions/buy`（`limitPrice` あり）に更新
- `POST /api/actions/sell-now` → `POST /api/actions/sell`（`limitPrice` なし）に更新
- `POST /api/actions/sell-limit` → `POST /api/actions/sell`（`limitPrice` あり）に更新

追加テスト（統合テストで動作を確認）：

- `limitPrice` 省略で `POST /api/actions/buy` → 即時約定（成行）が発生することを確認
- `limitPrice` 指定で `POST /api/actions/buy` → 指値注文が板に積まれることを確認（Sell も同様）

---

## 変更しないもの

- `Application/Actions/` 配下のすべての Command / Handler
- `ActionWaitRequestDto`、`ActionResultDto`
- `POST /api/actions/wait` エンドポイント
- その他のコントローラー・サービス
