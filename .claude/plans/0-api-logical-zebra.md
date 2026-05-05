# 数量バリデーションの配置とテスト整理プラン

## Context

### 発端
[GameApiTests.cs](tests/FinLearn.Api.Tests/GameApiTests.cs) のテスト群を見直す中で 2 つの違和感が浮上:

1. `POST_buy_sell_ラウンドトリップで売買できる` ([line 148-171](tests/FinLearn.Api.Tests/GameApiTests.cs#L148-L171)) — `Price` 未指定で `Warning is null` を仮定しているが、コンピューター注文の非決定性により API 層から約定保証は不可能。
2. `POST_buy_買い注文でターンが進む` ([line 60-75](tests/FinLearn.Api.Tests/GameApiTests.cs#L60-L75)) — コメントは「`Price: 150` で確実に約定」と謳うが、実は `Warning is null` も `Turn=2` も約定有無に依存しないため、コメントと挙動が乖離。
3. 数量 0 注文と保有なし売り注文で「ターン進行の有無」が分岐している ([line 78-91](tests/FinLearn.Api.Tests/GameApiTests.cs#L78-L91) vs [line 93-108](tests/FinLearn.Api.Tests/GameApiTests.cs#L93-L108)) — UX 的に妥当性が疑問。

### 確定したい仕様
**数量 0 または負数の売買は無効扱いとし、ターンも進めない（コンピューター注文も生成しない）。**

### 現状の挙動マップ
| ケース | 検証層 | ターン進行 | API ストア更新 | Computer注文生成 | レスポンス |
|---|---|---|---|---|---|
| Quantity ≤ 0 | TurnProcessor 冒頭 ([line 41-42](src/FinLearn.Core/TurnProcessor.cs#L41-L42)) → `Rejected()` | **しない** | しない | **しない** | 200 + Warning |
| 保有なし Sell | Portfolio.Sell ([Portfolio.cs:58](src/FinLearn.Core/Portfolio.cs)) → TurnProcessor が `AdvanceTurn` 経由ロールバック ([line 107-112](src/FinLearn.Core/TurnProcessor.cs#L107-L112)) | **する** | しない（[GameEndpoints.cs:81-85](src/FinLearn.Api/Endpoints/GameEndpoints.cs#L81-L85) が warning 時更新スキップ） | する | 200 + Warning + 進んだ Turn |

ドメイン挙動は既にユーザー希望と一致。よって本プランの主目的は **API 層に追加で 400 を返すかどうか** の決定 + テスト整理。

> 注: 「保有なし Sell でターン進む」のレスポンスとストア状態の不一致は別途存在するが、**本プランの対象外**（ユーザーは数量 0/負数のみに言及）。

---

## 選択肢: 数量バリデーションの配置

### Option A: ドメイン層のみ（現状維持・コード変更なし）
- API は常に 200 OK、Warning フィールドで通知
- テストは既存の動作を pin している
- **Pros**:
  - 変更ゼロ。リスクなし
  - 警告の出口が一本化（保有不足・現金不足など他の業務エラーと同じ形式）
  - クライアントは「200 を受けて Warning を見る」だけで一貫した分岐
- **Cons**:
  - REST 慣習との乖離（`{"quantity": 0}` のような形式不正は 400 が自然）
  - 「形式不正（数量0）」と「ゲーム状態依存の業務失敗（保有不足）」がレスポンス上区別不能

### Option B: 両層で防御（API で 400、Domain も現状維持）★推奨
- API 層で `request.Quantity <= 0` を `request.Side is null` と並べて 400 で弾く
- Domain は引き続き `Rejected()` を返す（safety net として残す）
- **Pros**:
  - REST 契約として正統（クライアント側形式不正 → 400）
  - Domain は他経路（テスト・将来の CLI など）でも自律的に安全
  - `Order` コンストラクタは既に `quantity <= 0` で例外スロー（多重防御は部分的に既存）
  - API 層の追加コストは数行（Side チェックと同じパターン）
- **Cons**:
  - 数量バリデーションが概念的に 2 か所に存在
  - 既存テスト [GameApiTests.cs:78-91](tests/FinLearn.Api.Tests/GameApiTests.cs#L78-L91) のアサーション変更が必要（200 → 400）

### Option C: API 層のみで弾き、Domain からガード削除
- TurnProcessor の `quantity <= 0` チェックを除去し、API がゲートキーパー
- **Pros**: 単一責任が明確
- **Cons**:
  - Domain が自律性を失う（API を経由しない呼出で不正データが流れ込む）
  - 既存ドメインテスト ([TurnProcessorTests.cs:176-185](tests/FinLearn.Tests/TurnProcessorTests.cs#L176-L185), [PortfolioTests.cs:119-144](tests/FinLearn.Tests/PortfolioTests.cs#L119-L144)) が大幅に変更必要
  - **非推奨**

---

## 推奨: Option B

**理由**:
- `Order` コンストラクタが既に `quantity <= 0` で例外を投げており、Domain 内の多重防御は既存の方針
- REST API の契約として「クライアントの不正リクエスト」と「ゲーム状態に依存する失敗」は分離すべき
- 追加コードは API ハンドラの 2-3 行のみで、保守負担は最小

---

## 実装計画

### 1. API 層: 形式不正バリデーション追加
**File**: [src/FinLearn.Api/Endpoints/GameEndpoints.cs](src/FinLearn.Api/Endpoints/GameEndpoints.cs)

`PlaceOrder` ハンドラ ([line 42-56](src/FinLearn.Api/Endpoints/GameEndpoints.cs#L42-L56)) に Side チェックと並べて Quantity / Price / StopPrice チェックを追加:

```csharp
if (request.Side is null)
    return Results.BadRequest(new { error = "side は必須です（\"Buy\" または \"Sell\"）" });
if (request.Quantity <= 0)
    return Results.BadRequest(new { error = "quantity は 1 以上を指定してください" });
if (request.Price is not null && request.Price <= 0)
    return Results.BadRequest(new { error = "price は 1 以上を指定してください" });
if (request.StopPrice is not null && request.StopPrice <= 0)
    return Results.BadRequest(new { error = "stopPrice は 1 以上を指定してください" });
```

数量・価格・逆指値ともに「形式不正」として 400 に揃える。Price/StopPrice 未指定（null）はそのまま Domain に流す（成行注文）。

### 2. Domain 層: 変更なし
[TurnProcessor.cs:41-44, 57-60](src/FinLearn.Core/TurnProcessor.cs#L41-L44) の `Rejected()` ガード（数量・価格）はそのまま safety net として残す。

### 3. API テスト整理
**File**: [tests/FinLearn.Api.Tests/GameApiTests.cs](tests/FinLearn.Api.Tests/GameApiTests.cs)

変更:
- `POST_buy_数量0でwarning付きターン不変` ([line 78-91](tests/FinLearn.Api.Tests/GameApiTests.cs#L78-L91))
  → テスト名と内容を `POST_orders_数量0で400` に書き換え。
  ```csharp
  Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  ```
  Warning や Turn の検証は不要に（400 で本文 DTO は返らない）。

追加:
- `POST_orders_数量マイナスで400` — 負数の数量
- `POST_orders_価格0で400` — `Price: 0`
- `POST_orders_価格マイナスで400` — `Price: -1`
- `POST_orders_StopPrice0で400` — `StopPrice: 0`
- `POST_orders_StopPriceマイナスで400` — `StopPrice: -1`

（境界値の代表として 0 と負数の 1 ケース、またはパラメタライズ `[Theory]` で集約してもよい）

削除:
- `POST_buy_sell_ラウンドトリップで売買できる` ([line 148-171](tests/FinLearn.Api.Tests/GameApiTests.cs#L148-L171)) — 約定非決定性によるflaky性、API 層でテストすべき関心事ではない、被覆価値が他テストと重複（前回の議論で合意済）。
- `POST_buy_買い注文でターンが進む` ([line 60-75](tests/FinLearn.Api.Tests/GameApiTests.cs#L60-L75)) — `Wait` テストと検証内容が実質同一、コメントも誤認。Buy 経路特有の値（`Trade` フィールドの中身など）を検証していないため代替価値なし。

維持:
- `POST_sell_保有なし売り注文でwarning付きだがターンは進む` ([line 93-108](tests/FinLearn.Api.Tests/GameApiTests.cs#L93-L108)) — 既存挙動の pin として残す。
- バリデーション系（[line 174-195](tests/FinLearn.Api.Tests/GameApiTests.cs#L174-L195)）、404 系、admin orderbook 系はそのまま。

### 4. Domain テスト
**File**: [tests/FinLearn.Tests/TurnProcessorTests.cs](tests/FinLearn.Tests/TurnProcessorTests.cs), [tests/FinLearn.Tests/PortfolioTests.cs](tests/FinLearn.Tests/PortfolioTests.cs)

変更なし。Option B では Domain の挙動を維持するため、`数量0以下の購入はターンが進まない()` ([TurnProcessorTests.cs:176-185](tests/FinLearn.Tests/TurnProcessorTests.cs#L176-L185)) などの既存テストは引き続き有効。

### 5. ドキュメント更新
**File**: [.claude/rules/src/api-project.md](.claude/rules/src/api-project.md)

`Design Decisions` の `Warning handling` 節に追記:
- 数量・サイドなど **形式不正は 400 BadRequest**
- ゲーム状態依存の失敗（保有不足等）は 200 + Warning

**File**: [docs/API.md](docs/API.md) — 同等の記述があれば更新。

---

## 影響を受ける主なファイル

| File | 変更内容 |
|---|---|
| [src/FinLearn.Api/Endpoints/GameEndpoints.cs](src/FinLearn.Api/Endpoints/GameEndpoints.cs) | `PlaceOrder` で Quantity の 400 チェック追加 |
| [tests/FinLearn.Api.Tests/GameApiTests.cs](tests/FinLearn.Api.Tests/GameApiTests.cs) | 数量0テスト書き換え、負数テスト追加、ラウンドトリップ・Buy 進行テスト削除 |
| [.claude/rules/src/api-project.md](.claude/rules/src/api-project.md) | 400 vs 200+Warning の方針追記 |
| [docs/API.md](docs/API.md) | 該当記述があれば同期 |

ドメイン層 (`TurnProcessor.cs`, `Portfolio.cs`) およびドメインテストは**変更なし**。

---

## 検証手順

1. `dotnet test` を実行し全テストグリーンを確認
2. API を起動し、curl で動作確認:
   - `{"side":"Buy","instrumentId":1,"quantity":0}` → **400**
   - `{"side":"Buy","instrumentId":1,"quantity":-1}` → **400**
   - `{"side":"Buy","instrumentId":1,"quantity":1,"price":0}` → **400**
   - `{"side":"Buy","instrumentId":1,"quantity":1,"price":-1}` → **400**
   - `{"side":"Buy","instrumentId":1,"quantity":1,"stopPrice":0}` → **400**
   - `{"side":"Buy","instrumentId":1,"quantity":1}` → **200**（ターン進行）
   - `{"side":"Buy","instrumentId":1,"quantity":1,"price":150}` → **200**（指値正常）
3. フロントエンド側で数量 0 入力時のエラー表示を確認（既存のクライアント側バリデーションがあるかも要確認・別タスク化）

---

## スコープ外（別タスクとして残す）

- 「保有なし Sell でターン進むがストア更新されない」レスポンス/状態不整合（[GameEndpoints.cs:81-85](src/FinLearn.Api/Endpoints/GameEndpoints.cs#L81-L85) と [TurnProcessor.cs:107-112](src/FinLearn.Core/TurnProcessor.cs#L107-L112) の挙動の食い違い） — 本プランでは触らない。
- 現金不足 Buy・約定ゼロなど他の警告経路の整理 — 本プランでは触らない。
