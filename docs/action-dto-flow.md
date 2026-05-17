# アクション機能の DTO 受け渡し図

更新日: 2026-05-10

## 目的
- フロントから送ったデータが、どの型として扱われるかを追えるようにする
- API層の DTO と Application層の Command/Result の対応を明確にする

## 先に結論
- API層では `*RequestDto` / `ActionResultDto` を使う
- Application層では DTO ではなく `*Command` / `ActionExecutionResult` を使う
- `ActionsController` で DTO <-> Command/Result の変換をしている
- 買い/売り API は `buy` / `sell` に統合されており、`limitPrice` の有無で成行/指値が分岐する

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

- BuyNow / BuyLimit
  - Front request: `frontend/src/api/types.ts` の `ActionBuyRequestDto`
  - API request: `backend/FinLearnApp.Api/Models/Api/ActionBuyRequestDto`
  - Application command:
    - `library/Application/Actions/BuyNowCommand.cs`
    - `library/Application/Actions/BuyLimitCommand.cs`

- SellNow / SellLimit
  - Front request: `frontend/src/api/types.ts` の `ActionSellRequestDto`
  - API request: `backend/FinLearnApp.Api/Models/Api/ActionSellRequestDto`
  - Application command:
    - `library/Application/Actions/SellNowCommand.cs`
    - `library/Application/Actions/SellLimitCommand.cs`

- Wait
  - Front request: `frontend/src/api/types.ts` の `ActionWaitRequestDto`
  - API request: `backend/FinLearnApp.Api/Models/Api/ActionWaitRequestDto`
  - Application command: `library/Application/Actions/WaitCommand.cs`

- 共通レスポンス
  - Application result: `library/Application/Actions/ActionExecutionResult.cs`
  - API response: `backend/FinLearnApp.Api/Models/Api/ActionResultDto`
  - Front response: `frontend/src/api/types.ts` の `ActionResultDto`

## BuyNow / BuyLimit の具体フロー

```text
Actions.tsx
  payload: ActionBuyRequestDto
  { investorId, tickerId, quantity, limitPrice?, expectedTurn }
    |
    v
frontend/src/api/actions.ts
  POST /api/actions/buy
    |
    v
ActionsController.Buy(ActionBuyRequestDto request)
  request.LimitPrice == null
    ? new BuyNowCommand(...)
    : new BuyLimitCommand(...)
    |
    v
_mediator.Send(command)
    |
    v
BuyNowCommandHandler / BuyLimitCommandHandler
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

## SellNow / SellLimit の具体フロー

```text
Actions.tsx
  payload: ActionSellRequestDto
  { investorId, tickerId, quantity, limitPrice?, expectedTurn }
    |
    v
POST /api/actions/sell
    |
    v
ActionsController.Sell(ActionSellRequestDto request)
  request.LimitPrice == null
    ? new SellNowCommand(...)
    : new SellLimitCommand(...)
    |
    v
SellNowCommandHandler / SellLimitCommandHandler
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
- `buy` / `sell` API の統合後も、Application 層では成行用と指値用の Command が分かれている
