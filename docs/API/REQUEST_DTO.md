# リクエストDTO

クライアント → API のリクエストボディに対応する型。

DTO 定義: [src/FinLearn.Api/Dtos/OrderRequest.cs](../../src/FinLearn.Api/Dtos/OrderRequest.cs)

## OrderRequest

`POST /api/games/{id}/orders` のリクエストボディ。

```
side             : string        # "Buy" | "Sell"（必須）
instrumentId     : int           # 銘柄ID（必須）
quantity         : int           # 数量（必須、1以上）
price            : int?          # 指値価格（null = 成行）
stopPrice        : int?          # 逆指値価格（null = 通常注文）
expiresInTurns   : int?          # 有効ターン数（未指定時はデフォルト2）
```

各フィールドのバリデーション仕様は [ENDPOINTS.md#post-apigamesidorders](ENDPOINTS.md#post-apigamesidorders) を参照。

## 関連

- [ENDPOINTS.md](ENDPOINTS.md) — エンドポイント詳細
- [RESPONSE_DTO.md](RESPONSE_DTO.md) — レスポンスDTO
