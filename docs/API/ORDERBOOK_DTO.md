# レスポンスDTO（板）

管理用エンドポイント `GET /api/admin/games/{id}/orderbook` のレスポンスに使われる DTO 群。

DTO 定義: [src/FinLearn.Api/Dtos/OrderBookResponse.cs](../../src/FinLearn.Api/Dtos/OrderBookResponse.cs)
変換: [src/FinLearn.Api/Mappers/OrderBookMapper.cs](../../src/FinLearn.Api/Mappers/OrderBookMapper.cs)

## OrderBookResponse

```
orders      : OrderDto[]
totalCount  : int   # 全注文件数（ページング前）
page        : int   # 現在のページ番号（1始まり）
pageSize    : int   # 1ページあたりの最大件数
```

## OrderDto

```
id             : int
traderId       : string
instrumentId   : int
side           : string       # "Buy" | "Sell"
type           : string       # "Market" | "Limit" | "Stop" | "StopLimit"
quantity       : int
price          : int?
stopPrice      : int?
createdAtTurn  : int
expiresAtTurn  : int          # 有効期限ターン番号（絶対値、currentTurn >= expiresAtTurn で除去）
```

## 関連

- [ENDPOINTS.md#get-apiadmingamesidorderbook](ENDPOINTS.md#get-apiadmingamesidorderbook) — エンドポイント詳細
- [RESPONSE_DTO.md](RESPONSE_DTO.md) — ゲーム状態レスポンスDTO（別系統）
