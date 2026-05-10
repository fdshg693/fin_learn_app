# 注文の有効期限を「必ず指定」に変更し、ユーザーから設定可能にする

## Context

現状、注文の有効期限は `TurnProcessor` のコンストラクタで設定する `ComputerTtl` / `PlayerTtl`（デフォルト `int.MaxValue`）というトレーダー区分単位のグローバル設定になっており、各注文自体は寿命を持たない。実運用上は実質無期限のため、板に古い注文が滞留しうる。

ユーザー要件:
1. **注文生成時に必ず有効期限が指定される**（注文自体のフィールドに）
2. **デフォルト 2 ターン**（生成されたターンと次のターンまで有効）
3. **ユーザーが画面から有効期限を設定できる**（残りターン数の入力）
4. **コンピューター注文も同じデフォルト 2 ターン**

設計の中核: 有効期限を「トレーダー区分単位の TTL」から「**注文単位の絶対ターン番号 `ExpiresAtTurn`**」に移行する。

### 期限切れセマンティクス

`ExpiresAtTurn = CreatedAtTurn + expiresInTurns`。`OrderBook.ExpireOrders(currentTurn)` は `currentTurn >= ExpiresAtTurn` を除去。`TurnProcessor.AdvanceTurn` は `game.Turn + 1`（次ターン番号）で呼ぶ既存の流れに乗せる。

検証: ターン1で `expiresInTurns=2` の注文 → `ExpiresAtTurn=3`。
- ターン1終了時 `ExpireOrders(2)`: `2 >= 3` false → 残る
- ターン2終了時 `ExpireOrders(3)`: `3 >= 3` true → 除去
- 結果: ターン1とターン2で有効。仕様通り ✓

`expiresInTurns >= 1` を要求（0 や負値は 400 BadRequest）。

---

## 変更内容

### 1. ドメイン層（FinLearn.Core）

#### [src/FinLearn.Core/GameRules.cs](src/FinLearn.Core/GameRules.cs)
- 新規: `public const int DefaultOrderTtl = 2;`（トップレベル or `Order` ネスト下）

#### [src/FinLearn.Core/Models/Order.cs](src/FinLearn.Core/Models/Order.cs)
- 新規プロパティ: `public int ExpiresAtTurn { get; }`
- 両コンストラクタ・`CreateMarket` ファクトリ・`WithQuantity` で `expiresAtTurn` を引き回す
- 既存の `createdAtTurn = 0` のデフォルト引数は削除（呼び出し側で常に明示させる方針に統一する）。テストの破壊的変更は許容（移行コストが低い）。
- バリデーション: `expiresAtTurn > createdAtTurn` を要求（同一ターン以下の期限は無意味）

#### [src/FinLearn.Core/Models/Player.cs:24](src/FinLearn.Core/Models/Player.cs#L24)
- `CreateOrder` シグネチャに `int expiresAtTurn` を追加（必須・デフォルト無し）
- 内部で `Order` 構築時に渡す

#### [src/FinLearn.Core/Models/OrderBook.cs:48-61](src/FinLearn.Core/Models/OrderBook.cs#L48-L61)
- `ExpireOrders` シグネチャを `(int currentTurn)` のみに簡略化
- ロジック: `_orders.Where(o => currentTurn < o.ExpiresAtTurn)`
- `ComputerTrader.IsComputerTrader` への依存を削除

#### [src/FinLearn.Core/TurnProcessor.cs](src/FinLearn.Core/TurnProcessor.cs)
- プロパティ削除: `ComputerTtl`、`PlayerTtl`
- コンストラクタ引数からも削除（破壊的変更）
- `Buy` / `Sell` シグネチャに `int expiresInTurns` パラメータを追加（API から渡す）。デフォルト値はここでは持たず、API 層で適用
- バリデーション: `expiresInTurns < 1` で `Rejected(game, Messages.ExpiresInTurnsMustBePositive)`
- `PlaceOrder` ヘルパー内で `Player.CreateOrder` 呼び出し時に `expiresAtTurn = game.Turn + expiresInTurns` を計算して渡す
- `AdvanceTurn` の `book.ExpireOrders(...)` 呼び出しを新シグネチャに合わせる

#### [src/FinLearn.Core/Services/ComputerTrader.cs:47, 61](src/FinLearn.Core/Services/ComputerTrader.cs#L47)
- `Order` 構築時に `expiresAtTurn = currentTurn + GameRules.DefaultOrderTtl` を渡す
- `IsComputerTrader` メソッドは `ExpireOrders` から不要になるが、ログ用途やテストで残ってる可能性があるので呼び出し元を確認したうえで削除/維持を判断

#### [src/FinLearn.Core/Messages.cs](src/FinLearn.Core/Messages.cs)
- 新規: `public const string ExpiresInTurnsMustBePositive = "有効期限は1ターン以上を指定してください";`

### 2. API 層（FinLearn.Api）

#### [src/FinLearn.Api/Dtos/OrderRequest.cs](src/FinLearn.Api/Dtos/OrderRequest.cs)
- フィールド追加: `int? ExpiresInTurns = null`（null → サーバー側でデフォルト 2 を適用）

#### [src/FinLearn.Api/Dtos/OrderBookResponse.cs](src/FinLearn.Api/Dtos/OrderBookResponse.cs)
- `OrderDto` にフィールド追加: `int ExpiresAtTurn`

#### [src/FinLearn.Api/Endpoints/GameEndpoints.cs:42-68](src/FinLearn.Api/Endpoints/GameEndpoints.cs#L42-L68)
- `PlaceOrder` ハンドラで `var expiresInTurns = request.ExpiresInTurns ?? GameRules.DefaultOrderTtl;`
- バリデーション追加: `expiresInTurns < 1` で `Results.BadRequest(new { error = "expiresInTurns は 1 以上を指定してください" });`
- `processor.Buy` / `processor.Sell` 呼び出しに `expiresInTurns` を渡す

#### Mapper（OrderDto を生成している箇所）
- `Order.ExpiresAtTurn` を `OrderDto.ExpiresAtTurn` にマップ。`Mappers/` 配下のグレップで該当箇所を確認して更新。

### 3. フロントエンド（frontend/）

#### [frontend/app/types/game.ts:38-43](frontend/app/types/game.ts#L38-L43)
- `OrderRequest` に `expiresInTurns?: number` 追加
- `OrderDto` に `expiresAtTurn: number` 追加

#### [frontend/app/components/TradeForm.tsx](frontend/app/components/TradeForm.tsx)
- 新規 state: `const [expiresInTurns, setExpiresInTurns] = useState(2);`
- 新規入力欄: 「有効期限（ターン）」 `<input type="number" min={1}>` を価格欄の後に追加
- `orderFields` に `expiresInTurns` を含める

#### [frontend/app/routes/games.$id.tsx:52-93](frontend/app/routes/games.$id.tsx#L52-L93)
- `clientAction` で `formData.get("expiresInTurns")` をパース、`Number.isNaN` 判定、`< 1` 判定
- `OrderRequest` ペイロードに `expiresInTurns` を含める

#### [frontend/app/components/OrderBookPanel.tsx:101-122](frontend/app/components/OrderBookPanel.tsx#L101-L122)
- `<th>` に「期限」列を追加（既存「ターン」列の隣など）
- `<td>` に `残り {Math.max(0, order.expiresAtTurn - currentTurn)}` を表示
- `currentTurn` を props として受け取る必要あり（呼び出し元 `games.$id.tsx` で `game.turn` を渡す）

### 4. テスト更新

#### tests/FinLearn.Tests
- [tests/FinLearn.Tests/OrderBookTests.cs:681-745](tests/FinLearn.Tests/OrderBookTests.cs#L681-L745) — `ExpireOrders` の旧シグネチャを使うテスト群を、`Order` に `ExpiresAtTurn` を直接持たせる新セマンティクスに書き換え。トレーダー区分による違いはなくなるので、対応するテスト（`コンピューターとプレイヤーで異なるTTLが適用される`）は「異なる ExpiresAtTurn を持つ注文が個別に判定される」テストに置き換え
- [tests/FinLearn.Tests/TurnProcessorTests.cs:15-19, 425-462](tests/FinLearn.Tests/TurnProcessorTests.cs#L15-L19) — `CreateProcessor` の `computerTtl`/`playerTtl` を削除。代わりに `Buy`/`Sell` 呼び出しで `expiresInTurns` を渡すよう修正。「TTL超過の…」テストはデフォルト 2 ターンで動作するように調整
- 新規テスト:
  - `Order に ExpiresAtTurn が必須となる`
  - `expiresInTurns < 1 で Rejected が返る`
  - `デフォルト 2 ターンで生成されたターンと次のターンに残る`
  - `ComputerTrader が DefaultOrderTtl で注文を生成する`

#### tests/FinLearn.Api.Tests
- `POST /api/games/{id}/orders` で `expiresInTurns` を省略 → 200 + デフォルト 2 が適用される
- `expiresInTurns: 0` → 400
- `OrderDto.ExpiresAtTurn` がレスポンスに含まれる

#### frontend tests
- [frontend/app/routes/games.$id.test.tsx](frontend/app/routes/games.$id.test.tsx) — `placeOrder` モックが `expiresInTurns` を受け取るアサーションを追加（既存の hidden field 検証パターンに準拠）

### 5. ドキュメント更新

#### [docs/DDD/EXCHANGE_RULE.md:50-61](docs/DDD/EXCHANGE_RULE.md#L50-L61)
- 「注文の有効期限」セクションを書き換え:
  - 注文単位の `ExpiresAtTurn` フィールドを持つことを明記
  - デフォルト 2 ターン（コンピューター・プレイヤー共通）
  - API: `expiresInTurns` パラメータで指定可能、未指定時はサーバーがデフォルトを適用

#### [.claude/rules/src/core-domain.md](.claude/rules/src/core-domain.md)
- `Order.cs` の説明に `expiresAtTurn` を追加
- `OrderBook.cs` の `ExpireOrders` の説明を「TTL-based」から「per-order ExpiresAtTurn」に修正

#### [docs/FEATURES/](docs/FEATURES/)
- 既存の `FillResult/`, `COMPUTER_ORDER/` を確認し、有効期限に関する記述があれば追従

---

## 主要な変更ファイル（実装順）

優先度順:
1. `src/FinLearn.Core/GameRules.cs` — 定数追加
2. `src/FinLearn.Core/Models/Order.cs` — フィールド追加
3. `src/FinLearn.Core/Models/Player.cs` — シグネチャ拡張
4. `src/FinLearn.Core/Models/OrderBook.cs` — `ExpireOrders` 簡略化
5. `src/FinLearn.Core/Services/ComputerTrader.cs` — デフォルト適用
6. `src/FinLearn.Core/Messages.cs` + `TurnProcessor.cs` — バリデーション・配線
7. `src/FinLearn.Api/Dtos/*` + `Endpoints/GameEndpoints.cs` + Mapper — API 表層
8. `tests/FinLearn.Tests/*` + `tests/FinLearn.Api.Tests/*` — テスト追従
9. `frontend/app/types/game.ts` + `TradeForm.tsx` + `routes/games.$id.tsx` + `OrderBookPanel.tsx` — UI
10. `docs/DDD/EXCHANGE_RULE.md` + `.claude/rules/*` — ドキュメント

---

## 既存資産の再利用

- `Order.CreatedAtTurn` — そのまま残す（表示用途）
- `OrderBook.ExpireOrders` — 既存メソッドを書き換えて再利用
- `TurnProcessor.AdvanceTurn` の `ExpireOrders` 呼び出し位置（[src/FinLearn.Core/TurnProcessor.cs:161](src/FinLearn.Core/TurnProcessor.cs#L161)）— そのまま
- 入力バリデーションの「形式不正 → 400 BadRequest」パターン（[src/FinLearn.Api/Endpoints/GameEndpoints.cs:42-58](src/FinLearn.Api/Endpoints/GameEndpoints.cs#L42-L58)）— `expiresInTurns` も同パターンで追加
- 警告レスポンス（200 + `warning` フィールド）パターン — `Messages.ExpiresInTurnsMustBePositive` をドメイン層で使用しても良いが、API 層で 400 を返すなら不要
- `TradeForm.tsx` の `orderFields` hidden field パターン — そのまま使える
- `OrderDto.CreatedAtTurn` の表示パターン — `ExpiresAtTurn` も同パターン

---

## 検証方法（end-to-end）

1. ビルド: `dotnet build fin_learn_app.sln`
2. ユニットテスト: `dotnet test tests/FinLearn.Tests`
3. API 統合テスト: `dotnet test tests/FinLearn.Api.Tests`
4. フロントエンド型チェック: `cd frontend && npm run typecheck`
5. フロントエンドテスト: `cd frontend && npm test`
6. 手動確認:
   - `dotnet run --project src/FinLearn.Api` で API 起動（localhost:5088）
   - `cd frontend && npm run dev` で UI 起動（localhost:5173）
   - ゲーム作成 → 「有効期限（ターン）」欄が表示され、デフォルト 2 が入っている
   - 約定しない高い指値で買い注文を出す → 板に表示され「残り 2」など表示
   - 「待つ」を 2 回押す → 注文が板から消える
   - 有効期限 5 で注文 → 5 ターン後に消える
   - 有効期限 0 で送信 → エラーメッセージ表示
   - コンピューター注文も板で「残り N」が表示されることを確認
