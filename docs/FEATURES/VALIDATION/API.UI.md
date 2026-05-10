# バリデーションの API・UI 表現

API レイヤーがバリデーション結果をどう HTTP に翻訳し、フロントエンドがそれをどう表示するか。

## 応答コードの設計判断

REST 慣習に沿って 2 系統に分ける。これは「**エラーが呼び出し側の責任か、ゲーム状態の責任か**」で決まる。

| 種類 | HTTP | レスポンス Body | クライアントの責任 |
|---|---|---|---|
| 形式不正（クライアント側のバグ・操作ミス） | **400 BadRequest** | `{ "error": "..." }` | 入力を直して再送 |
| 状態依存の失敗（ゲーム状態に対する不適切な操作） | **200 OK** | `GameResponse` の `warning` フィールド | ゲームを続行（ターンは進んでいる） |

> 設計判断は [`.claude/rules/src/api-project.md`](../../../.claude/rules/src/api-project.md) の「Design Decisions」も参照。

## 400 BadRequest を返す条件

`POST /api/games/{id}/orders`:

| 条件 | レスポンス例 |
|---|---|
| `side` 未指定 | `{"error":"side は必須です（\"Buy\" または \"Sell\"）"}` |
| `quantity <= 0` | `{"error":"quantity は 1 以上を指定してください"}` |
| `price` 指定かつ `<= 0` | `{"error":"price は 1 以上を指定してください"}` |
| `stopPrice` 指定かつ `<= 0` | `{"error":"stopPrice は 1 以上を指定してください"}` |
| `side` が `"Hold"` 等の不正値 | ASP.NET Core モデルバインドが返す既定の 400（メッセージは未制御） |

`GET /api/admin/games/{id}/orderbook`:

| 条件 | レスポンス Body |
|---|---|
| `page < 1` | `"page must be >= 1"`（プレーンテキスト） |
| `pageSize < 1` | `"pageSize must be >= 1"` |
| `pageSize > 200` | `"pageSize must be <= 200"` |

### 400 時の副作用なし保証

400 を返した時、`GameStore` は変更されない（`ProcessOrder` ヘルパーに到達しないため）。**ターン進行・コンピューター注文・板の状態すべて不変**。テスト: `tests/FinLearn.Api.Tests/GameApiTests.cs` の `POST_orders_400後はターンも板も不変`。

## 200 OK + warning フィールド

`GameResponse.Warning`（string?）はドメインからの状態依存 Warning をそのまま透過する。`null` のとき正常系。

```json
{
    "id": "abc123",
    "turn": 5,
    "warning": "現金が不足して購入できません",
    "player": { ... },
    ...
}
```

### Warning が出るケースとターンの動き

| 操作 | Warning | Turn の進行 |
|---|---|---|
| 売り注文（保有なし） | `保有数量を超えて売却できません` | ✅ 進む |
| 買い注文（残高不足） | `現金が不足して購入できません` | ✅ 進む |
| 成行買い（売り板なし） | `約定できる売り注文がありません` | ✅ 進む |
| 成行売り（買い板なし） | `約定できる買い注文がありません` | ✅ 進む |

> ターンが進む理由: コンピューター注文はすでに板に乗っており、副作用として残しても整合性を損なわない。Wait と同等の状態遷移。

`GameStore` の更新は `turn.Warning is null` のみで実行される（[`GameEndpoints.cs:93-97`](../../../src/FinLearn.Api/Endpoints/GameEndpoints.cs)）。Warning が出ているレスポンスでも、Game 自体は変更されない場合とコンピューター注文・株価変動だけ反映された場合がある — **クライアントは常に返却された `GameResponse` を信頼する**（差分を自前で計算しない）。

## DTO

> ソース: [`src/FinLearn.Api/Dtos/OrderRequest.cs`](../../../src/FinLearn.Api/Dtos/OrderRequest.cs) / [`src/FinLearn.Api/Dtos/GameResponse.cs`](../../../src/FinLearn.Api/Dtos/GameResponse.cs)

```csharp
public record OrderRequest(
    OrderSide? Side,        // null は 400
    int InstrumentId,
    int Quantity,
    int? Price = null,      // null = 成行
    int? StopPrice = null   // null = ストップなし
);

public record GameResponse(
    string Id,
    int Turn,
    string? Warning,        // null = 正常
    PlayerDto Player,
    ...
);
```

`Side` を nullable にしているのは、未指定時に「誤った enum 値（例: 0）にデフォルトされない」ことをハンドラ側で明示的に判定するため。

## フロントエンドのバリデーション表示

### 入力時の制約（HTML5）

> ソース: [`frontend/app/components/TradeForm.tsx`](../../../frontend/app/components/TradeForm.tsx)

```tsx
<input type="number" min={1} value={quantity} ... />  {/* 数量 */}
<input type="number" min={1} value={price} ... />     {/* 価格（空欄＝成行） */}
```

`min={1}` はブラウザ UI（スピナーやネイティブバリデーション）には効くが、`useState` の値はそのまま送信される。**真のバリデーションはサーバー側に依存**。

価格は文字列 state（`useState("")`）。空文字を `null` にマップし、成行注文として API へ送る:

```ts
const price = priceRaw ? Number(priceRaw) : null;
```

### action ハンドラの NaN ガード

> ソース: [`frontend/app/routes/games.$id.tsx`](../../../frontend/app/routes/games.$id.tsx)

`Number(...)` の結果を `Number.isNaN` でチェック。失敗時は `throw new Error(...)` → React Router の ErrorBoundary が表示。

### Warning の表示

> ソース: [`frontend/app/components/WarningMessage.tsx`](../../../frontend/app/components/WarningMessage.tsx)

`GameResponse.warning` を受け取り、`null` でなければ警告バナーを表示する責任を持つ単独コンポーネント。`games.$id.tsx` の `useActionData` または `useLoaderData` が返す game オブジェクトの warning をそのまま渡す。

### 400 エラーの扱い

API クライアント [`frontend/app/api/gameApi.ts`](../../../frontend/app/api/gameApi.ts) は非 2xx 応答で `Error` を投げる。`clientAction` 内で投げられたエラーは React Router の ErrorBoundary（`app/root.tsx`）で捕捉・表示される。**ユーザー向けに友好的なメッセージへマップする層は現状なく**、API の `error` 文字列がそのまま見える可能性がある（改善余地）。

## テスト方針

| テスト対象 | 場所 |
|---|---|
| 形式不正 → 400 の確認 | [`tests/FinLearn.Api.Tests/GameApiTests.cs`](../../../tests/FinLearn.Api.Tests/GameApiTests.cs) `POST_orders_数量が0以下で400` ほか |
| 400 後の副作用なし | 同上 `POST_orders_400後はターンも板も不変` |
| 状態依存失敗 → 200 + warning | 同上 `POST_sell_保有なし売り注文でwarning付きだがターンは進む` |
| TurnProcessor の `Rejected` 経路 | [`tests/FinLearn.Tests/TurnProcessorTests.cs`](../../../tests/FinLearn.Tests/TurnProcessorTests.cs) `Messages.QuantityMustBePositive` / `Messages.PriceMustBePositive` の確認 |
| Portfolio 状態依存失敗 | [`tests/FinLearn.Tests/PortfolioTests.cs`](../../../tests/FinLearn.Tests/PortfolioTests.cs) `InsufficientCashToBuy` / `InsufficientQuantityToSell` |
| Order 不変条件例外 | コンストラクタ呼び出しで `Assert.Throws<ArgumentException>` |

## 関連

- [LOGIC.md](LOGIC.md) — バリデーションの 3 層モデル・エラー分類
- [取引ルール](../../DDD/EXCHANGE_RULE.md) — ターン進行と失敗時の挙動
- [API/](../../API/) — エンドポイント・DTO 一覧
