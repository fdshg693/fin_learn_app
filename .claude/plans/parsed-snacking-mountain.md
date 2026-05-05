# Buy/Sell エンドポイント統合

## Context

現在 `POST /api/games/{id}/buy` と `/sell` の 2 エンドポイントは、**リクエスト DTO・処理パス・テスト構造が完全に対称**で、ドメイン側でも既に `OrderSide` enum + `TurnProcessor.PlaceOrder` で「同一概念 + side」として表現されている。API 層での分割はドメイン実態と乖離した人為的な分割になっており、REST 的にも「注文リソースの作成」を 2 経路に分けるより `POST /orders` + `side` フィールドの方が素直。

将来ロジックが乖離してもバックエンドで分岐すれば API 契約は不変、リクエスト形が乖離した時点で再分割すればよく、現時点で先回りする必要はない（YAGNI）。

**ゴール:** `POST /api/games/{id}/buy` と `/sell` を **削除** し、単一の `POST /api/games/{id}/orders`（body に `side: "Buy"|"Sell"`）に統合する。フロントエンドも同期して更新する。後方互換は取らない（小規模・FE/BE 同時更新のため）。

---

## 設計判断

- **`side` フィールドの形式:** PascalCase `"Buy"`/`"Sell"`。既存の `TradeResultDto.side`／`OrderDto.side` レスポンスが PascalCase（`JsonStringEnumConverter` のデフォルト）であり、リクエスト側も `OrderSide` enum をそのまま受ければ自動的に対称になる。レスポンスを lowercase 化する変更は範囲拡大で得が薄い。
- **ドメイン層は不変:** `TurnProcessor.Buy`/`Sell` は型付き便利メソッドとして既存テストから多数呼ばれており残す。エンドポイントが `request.Side` で分岐して `Buy`/`Sell` を呼び分けるだけで済む。
- **`TradeForm` の `intent` hidden field は据え置き:** UX は買・売・待の 3 ボタンのまま。ルートアクション境界で `intent` → `side` を変換する。`intent` を `side` にリネームすると周辺コンポーネントへの波及があり割に合わない。

---

## 実装手順

### 1. バックエンド

#### [src/FinLearn.Api/Dtos/OrderRequest.cs](../../src/FinLearn.Api/Dtos/OrderRequest.cs)

`Side` を必須先頭フィールドとして追加:

```csharp
public sealed record OrderRequest(
    OrderSide Side,
    int InstrumentId,
    int Quantity,
    int? Price = null,
    int? StopPrice = null);
```

`using FinLearn.Core;` を追加（`OrderSide` は `FinLearn.Core` 名前空間）。

#### [src/FinLearn.Api/Endpoints/GameEndpoints.cs](../../src/FinLearn.Api/Endpoints/GameEndpoints.cs)

- L21-22: `MapPost("/{id}/buy", Buy)` と `MapPost("/{id}/sell", Sell)` を削除し、`group.MapPost("/{id}/orders", PlaceOrder)` 1 行に置換。
- L43-53: `Buy` / `Sell` ハンドラを削除し、単一ハンドラ `PlaceOrder` を追加:

```csharp
private static IResult PlaceOrder(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<OrderLog> logger)
{
    return ProcessOrder(id, request, store, processor, exchangeFactory, config, logger,
        (g, fee, req) => req.Side switch
        {
            OrderSide.Buy => processor.Buy(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice),
            OrderSide.Sell => processor.Sell(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice),
            _ => throw new ArgumentOutOfRangeException(nameof(req.Side))
        });
}
```

`ProcessOrder` ヘルパーは無変更で再利用。enum バインディング失敗時は ASP.NET Core の自動 400 が返るので、未指定／不正値の明示的検証は不要。

### 2. バックエンドテスト

#### [tests/FinLearn.Api.Tests/GameApiTests.cs](../../tests/FinLearn.Api.Tests/GameApiTests.cs)

L60, 77, 93, 110, 119, 149 の 6 メソッドをすべて新エンドポイントに書き換え:
- URL を `/buy` または `/sell` から `/orders` に変更
- `OrderRequest` 引数の先頭に `Side: OrderSide.Buy` または `OrderSide.Sell` を追加（C# テストなので enum を直接渡せる）

テスト名は意味を保つため変更せず（`POST_buy_買い注文でターンが進む` 等のままでよい）。`POST_buy_sell_ラウンドトリップで売買できる` も URL のみ更新。

新規テスト 2 件を追加:
- `POST_orders_side未指定で400()` — body から `side` を欠落させて `BadRequest` を確認
- `POST_orders_不正なsideで400()` — `side: "Hold"` を送って `BadRequest` を確認

### 3. フロントエンド

#### [frontend/app/types/game.ts](../../frontend/app/types/game.ts)

L38-42 の `OrderRequest` に `side` を先頭追加:

```ts
export type OrderRequest = {
  side: "Buy" | "Sell";
  instrumentId: number;
  quantity: number;
  price: number | null;
};
```

#### [frontend/app/api/gameApi.ts](../../frontend/app/api/gameApi.ts)

L29-45 の `buy` / `sell` 関数を削除し、`placeOrder` 1 つに統合:

```ts
export async function placeOrder(id: string, order: OrderRequest): Promise<GameResponse> {
  const res = await fetch(`${BASE}/api/games/${id}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(order),
  });
  return handleResponse(res);
}
```

#### [frontend/app/routes/games.$id.tsx](../../frontend/app/routes/games.$id.tsx)

L3 の import を `placeOrder` に変更。L48-56 の分岐を簡素化:

```ts
const side = intent === "buy" ? "Buy" : intent === "sell" ? "Sell" : null;
if (side === null) {
  throw new Error(`Invalid intent: ${intent}`);
}
const order: OrderRequest = { side, instrumentId, quantity, price };
game = await placeOrder(id, order);
```

`TradeForm.tsx` は無変更（hidden field `intent="buy"`/`"sell"` 据え置き）。

### 4. ドキュメント

#### [docs/API.md](../../docs/API.md)
- L30-31: エンドポイント表の 2 行を 1 行に: `POST /api/games/{id}/orders` — 注文（買い/売り）
- L76-130: `### POST /api/games/{id}/buy` と `### POST /api/games/{id}/sell` の 2 セクションを 1 つの `### POST /api/games/{id}/orders` セクションに統合。リクエスト例に `"side": "Buy"` を追加し、フィールド表に `side` 行を追加（型: `"Buy" | "Sell"`、必須）。

#### [.claude/rules/src/api-project.md](../../.claude/rules/src/api-project.md)
- L26-27: 同様にエンドポイント表 2 行を 1 行に
- L50-51: `ProcessOrder` の説明を更新（`request.Side` で `Buy`/`Sell` を分岐）

#### [docs/FRONT.md](../../docs/FRONT.md)
- L121-122, L172-173: `TradeForm` ボタンと `gameApi.ts` の説明を `placeOrder` 1 関数に書き換え、`intent` → `side` 変換がルートアクション境界で行われることを記載

---

## 変更ファイル一覧

**バックエンド:**
- `src/FinLearn.Api/Dtos/OrderRequest.cs`
- `src/FinLearn.Api/Endpoints/GameEndpoints.cs`
- `tests/FinLearn.Api.Tests/GameApiTests.cs`

**フロントエンド:**
- `frontend/app/types/game.ts`
- `frontend/app/api/gameApi.ts`
- `frontend/app/routes/games.$id.tsx`

**ドキュメント:**
- `docs/API.md`
- `.claude/rules/src/api-project.md`
- `docs/FRONT.md`

---

## 検証

**バックエンド** (リポジトリルートから):
```powershell
dotnet build fin_learn_app.sln
dotnet test
```
既存 6 テスト + 新規 2 テストすべて緑を確認。

**フロントエンド**:
```powershell
cd frontend
npm run typecheck
npm test
```

**E2E スモーク** (バックエンド + フロント起動):
```powershell
# ターミナル1
dotnet run --project src/FinLearn.Api
# ターミナル2
cd frontend; npm run dev
```
ブラウザで `http://localhost:5173`、ゲーム作成 → 買い注文 → 売り注文 → 待機 をすべて成功させる。Network タブで `POST /api/games/{id}/orders` が body に `"side": "Buy"`/`"Sell"` を含んでいることを確認。
