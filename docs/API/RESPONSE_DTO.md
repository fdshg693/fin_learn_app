# レスポンスDTO（ゲーム状態）

ゲーム系エンドポイント（`/api/games/*`）の共通レスポンスに使われる DTO 群。

DTO 定義: [src/FinLearn.Api/Dtos/GameResponse.cs](../../src/FinLearn.Api/Dtos/GameResponse.cs)
変換: [src/FinLearn.Api/Mappers/GameMapper.cs](../../src/FinLearn.Api/Mappers/GameMapper.cs)

## GameResponse（共通レスポンス）

```
gameId        : string
turn          : int
player        : PlayerDto
instruments   : InstrumentDto[]
recentTrades  : TradeResultDto[]  # 直近最大3件（GameStore.MaxRecentTrades）
warning       : string?           # アクション失敗時のみ
```

## PlayerDto

```
name          : string
cash          : int
positions     : PositionDto[]
totalAssets   : int             # cash + 全ポジション評価額
profitLoss    : int             # totalAssets - 初期資金
```

## PositionDto

```
instrumentId  : int
quantity      : int
currentPrice  : int
amount        : int             # quantity * currentPrice
```

## InstrumentDto

```
id            : int
price         : int             # 現在の市場価格
```

## TradeResultDto

```
instrumentId    : int
side            : string        # "Buy" | "Sell"
filledQuantity  : int
totalAmount     : int           # 約定金額（手数料抜き）
fee             : int
```

## 関連

- [ENDPOINTS.md](ENDPOINTS.md) — どのエンドポイントがこの DTO を返すか
- [ORDERBOOK_DTO.md](ORDERBOOK_DTO.md) — 板レスポンスDTO（別系統）
- [REQUEST_DTO.md](REQUEST_DTO.md) — リクエストDTO
