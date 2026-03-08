# アクション機能の DTO 受け渡し図

更新日: 2026-03-08

## 目的
- フロントから送ったデータが、どの型として扱われるかを追えるようにする
- API層の DTO と Application層の Command/Result の対応を明確にする

## 先に結論
- API層では `*RequestDto` / `ActionResultDto` を使う
- Application層では DTO ではなく `*Command` / `ActionExecutionResult` を使う
- `ActionsController` で DTO <-> Command/Result の変換をしている

## 全体フロー（共通）

```text
[Frontend Typescript DTO]
  -> JSON
[API Request DTO]
  -> Commandへ変換
[Application Command]
  -> Handler実行
[ActionExecutionResult]
  -> API Response DTOへ変換
[ActionResultDto]
  -> JSON
[Frontend Typescript DTO]
```

## 型対応表（アクション系）

- BuyNow / SellNow
  - Front request: `frontend/src/api/types.ts` の `ActionTradeRequestDto`
  - API request: `backend/FinLearnApp.Api/Models/Api/ActionTradeRequestDto`
  - Application command:
    - `src/Application/Actions/BuyNowCommand.cs`
    - `src/Application/Actions/SellNowCommand.cs`

- BuyLimit / SellLimit
  - Front request: `frontend/src/api/types.ts` の `ActionLimitRequestDto`
  - API request: `backend/FinLearnApp.Api/Models/Api/ActionLimitRequestDto`
  - Application command:
    - `src/Application/Actions/BuyLimitCommand.cs`
    - `src/Application/Actions/SellLimitCommand.cs`

- Wait
  - Front request: `frontend/src/api/types.ts` の `ActionWaitRequestDto`
  - API request: `backend/FinLearnApp.Api/Models/Api/ActionWaitRequestDto`
  - Application command: `src/Application/Actions/WaitCommand.cs`

- 共通レスポンス
  - Application result: `src/Application/Actions/ActionExecutionResult.cs`
  - API response: `backend/FinLearnApp.Api/Models/Api/ActionResultDto`
  - Front response: `frontend/src/api/types.ts` の `ActionResultDto`

## BuyNow の具体フロー

```text
Actions.tsx
  payload: ActionTradeRequestDto
  { investorId, tickerId, quantity, expectedTurn }
    |
    v
frontend/src/api/actions.ts
  POST /api/actions/buy-now
    |
    v
ActionsController.BuyNow(ActionTradeRequestDto request)
  new BuyNowCommand(
    request.InvestorId,
    request.TickerId,
    request.Quantity,
    request.ExpectedTurn)
    |
    v
_mediator.Send(command)
    |
    v
BuyNowCommandHandler.Handle(BuyNowCommand)
  -> ActionExecutionResult
    |
    v
ActionsController.ToHttpResult(ActionExecutionResult)
  new ActionResultDto(
    response.Success,
    response.Message,
    PortfolioDto,
    response.CurrentTurn)
    |
    v
JSON response
    |
    v
Actions.tsx
  result: ActionResultDto
```

## BuyLimit / SellLimit の具体フロー

```text
Actions.tsx
  payload: ActionLimitRequestDto
  { investorId, tickerId, quantity, limitPriceAmount, expectedTurn }
    |
    v
POST /api/actions/buy-limit or /sell-limit
    |
    v
ActionsController.BuyLimit / SellLimit(ActionLimitRequestDto)
  -> BuyLimitCommand / SellLimitCommand
    |
    v
BuyLimitCommandHandler / SellLimitCommandHandler
  -> ActionExecutionResult
    |
    v
ActionResultDto
```

## Wait の具体フロー

```text
Actions.tsx
  payload: ActionWaitRequestDto
  { investorId, expectedTurn }
    |
    v
POST /api/actions/wait
    |
    v
ActionsController.Wait(ActionWaitRequestDto)
  -> WaitCommand
  -> WaitCommandHandler
  -> ActionExecutionResult
  -> ActionResultDto
```

## DTO 変換が行われている場所

- Request DTO -> Command
  - `backend/FinLearnApp.Api/Controllers/ActionsController.cs`

- Application Result -> Response DTO
  - `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
  - `ToHttpResult(...)`

- Domain `Portfolio` -> API `PortfolioDto`
  - `backend/FinLearnApp.Api/Mappers/PortfolioMapper.cs`

## 補足
- Application層には API DTO を持ち込まない方針
- そのため Application 側は `*Command` と `ActionExecutionResult` で統一されている
- これにより API形式変更があっても、業務ロジックへの影響を小さくできる
