# 実装プラン: 注文板表示機能

## 概要

ゲーム中の注文板（OrderBook）に残っている注文一覧をAPI経由で取得し、
フロントエンド画面で確認できるようにする。

**設計方針:** 既存のゲームAPIとは分離した管理用APIグループとして実装し、
将来的に認証・認可ミドルウェアを適用可能な構造にする。

---

## Phase 1: バックエンド — 管理用APIエンドポイント

### 1-1. OrderBook用DTOの作成

**新規ファイル:** `src/FinLearn.Api/Dtos/OrderBookResponse.cs`

```csharp
public sealed record OrderBookResponse(
    IReadOnlyList<OrderDto> Orders);

public sealed record OrderDto(
    int Id,
    string TraderId,        // "computer" or player名
    int InstrumentId,
    string Side,             // "Buy" / "Sell"
    string Type,             // "Market" / "Limit"
    int Quantity,
    int? Price,              // Limit注文の場合のみ
    int? StopPrice,
    int CreatedAtTurn);
```

**設計判断:**
- `Side` と `Type` は文字列で返す（フロントエンドで表示しやすく、enum依存を避ける）
- `TraderId` をそのまま返す（将来的にフィルタリングに使える）

### 1-2. OrderBookマッパーの作成

**新規ファイル:** `src/FinLearn.Api/Mappers/OrderBookMapper.cs`

```csharp
public static class OrderBookMapper
{
    public static OrderBookResponse ToResponse(OrderBook book) { ... }
}
```

OrderBook内部の `_orders` は private なので、OrderBook に公開プロパティを追加する必要がある。

### 1-3. OrderBook に注文一覧の公開プロパティを追加

**変更ファイル:** `src/FinLearn.Core/Models/OrderBook.cs`

```csharp
public IReadOnlyList<Order> Orders => _orders;
```

現在 `_orders` は private。DTO変換のために読み取り専用プロパティを追加する。
既存の `SellOrders()` / `BuyOrders()` はフィルタ・ソート済みなので、全件取得用に必要。

### 1-4. 管理用エンドポイントグループの作成

**新規ファイル:** `src/FinLearn.Api/Endpoints/AdminEndpoints.cs`

```csharp
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin");
        // 将来ここに .RequireAuthorization() を追加可能

        group.MapGet("/games/{id}/orderbook", GetOrderBook);

        return group;
    }

    private static IResult GetOrderBook(
        string id,
        GameStore store)
    {
        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();
        return Results.Ok(OrderBookMapper.ToResponse(game.OrderBook));
    }
}
```

**ルート設計:**
- `/api/admin/games/{id}/orderbook` — ゲームIDを指定して注文板を取得
- `/api/admin` プレフィックス配下にグルーピング
- 将来 `group.RequireAuthorization("AdminPolicy")` を1行追加するだけで保護可能

### 1-5. Program.cs への登録

```csharp
app.MapGameEndpoints();    // 既存（リファクタ後）
app.MapAdminEndpoints();   // 新規
```

### 1-6. APIテストの作成

**新規ファイル（or既存に追加）:** `tests/FinLearn.Api.Tests/` 配下

- 正常系: ゲーム作成 → 注文実行 → `/api/admin/games/{id}/orderbook` で未約定注文が返る
- 異常系: 存在しないゲームIDで404が返る
- 空の注文板: 注文がない場合に空リストが返る

---

## Phase 2: フロントエンド — 注文板表示UI

### 2-1. API関数の追加

**変更ファイル:** `frontend/app/api/gameApi.ts`

```typescript
export async function getOrderBook(gameId: string): Promise<OrderBookResponse> {
  const res = await fetch(`${BASE}/api/admin/games/${gameId}/orderbook`);
  return handleResponse<OrderBookResponse>(res);
}
```

### 2-2. 型定義の追加

**変更ファイル:** `frontend/app/types/game.ts`

```typescript
export type OrderBookResponse = {
  orders: OrderDto[];
};

export type OrderDto = {
  id: number;
  traderId: string;
  instrumentId: number;
  side: string;       // "Buy" | "Sell"
  type: string;       // "Market" | "Limit"
  quantity: number;
  price: number | null;
  stopPrice: number | null;
  createdAtTurn: number;
};
```

### 2-3. OrderBook表示コンポーネントの作成

**新規ファイル:** `frontend/app/components/OrderBookPanel.tsx`

注文板の全注文をテーブル形式で表示するコンポーネント。

**表示カラム:**
| 注文ID | 銘柄 | 売買 | 種類 | 数量 | 価格 | ストップ価格 | 発注ターン | トレーダー |

**機能:**
- 売り注文と買い注文を色分け（既存の赤=買い / 青=売りパターンに合わせる）
- 銘柄ごとのフィルタリング（将来拡張）
- 注文がない場合は「注文なし」を表示

### 2-4. ゲーム画面への統合

**変更ファイル:** `frontend/app/routes/games.$id.tsx`

ゲーム画面に注文板パネルを追加する。

**方針:**
- ゲーム画面下部または右カラムに配置
- `clientLoader` で `getGame` と `getOrderBook` を並行fetch
- `clientAction` 後（注文/待機アクション後）にも注文板を再取得

```typescript
// clientLoader内
const [game, orderBook] = await Promise.all([
  getGame(id),
  getOrderBook(id),
]);
return { game, orderBook };
```

---

## Phase 3: 将来の拡張ポイント（参考）

今回は実装しないが、設計時に考慮しておく事項:

1. **認証・認可の追加**
   - `MapAdminEndpoints` 内で `.RequireAuthorization()` を呼ぶだけ
   - ASP.NET Core の Authorization ポリシーを追加

2. **Admin APIの拡張**
   - `/api/admin/games/{id}/history` — 約定履歴
   - `/api/admin/games/{id}/players` — プレイヤー一覧（マルチプレイヤー対応時）

3. **フロントエンドのアクセス制御**
   - 注文板パネルの表示/非表示をフラグで制御
   - 管理者ログイン画面の追加

---

## 実装順序まとめ

```
リファクタプラン実行（前提条件）
  ↓
Phase 1-3: OrderBook に Orders プロパティ追加
  ↓
Phase 1-1: OrderBook用DTO作成
  ↓
Phase 1-2: OrderBookMapper作成
  ↓
Phase 1-4: AdminEndpoints作成
  ↓
Phase 1-5: Program.cs登録
  ↓
Phase 1-6: APIテスト作成・全テスト通過確認
  ↓
Phase 2-1: API関数追加
  ↓
Phase 2-2: 型定義追加
  ↓
Phase 2-3: OrderBookPanelコンポーネント作成
  ↓
Phase 2-4: ゲーム画面統合
```

## 完了条件

- [ ] `GET /api/admin/games/{id}/orderbook` が注文一覧を返す
- [ ] 既存APIに影響がない（全テストパス）
- [ ] ゲーム画面で注文板の内容が確認できる
- [ ] 売り/買い注文が視覚的に区別できる
- [ ] 管理用APIが既存APIとルートグループレベルで分離されている
