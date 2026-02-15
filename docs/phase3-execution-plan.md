# Phase3 実行計画（record整理 / ログ / ターン制 / バリデーション）

更新日: 2026-02-15

## 目的
- 不要な `record` を整理して意図を明確化
- 追跡しやすい構造化ログを導入
- アクション実行にターン制を導入
- 入力バリデーションをバック/フロントで統一的に強化

---

## 1) record整理（STEP10）

## 方針
- **残す（record推奨）**: 値オブジェクト（`Money`, `*Id`）
- **残す（recordでも可）**: Command/DTO（immutableデータ運搬）
- **見直し対象**: 状態や振る舞い中心の型で recordの利点が薄いもの

## 今回の対象判断
- `Holding` はすでに `class` なので対応不要
- `ActionExecutionResult` は値比較要件が薄いので `sealed class` へ変更候補
- Domain Event (`TradeExecuted`) は record維持で可（イベントは値として扱うため）

## 実装タスク
1. `ActionExecutionResult` を class化（必要なら）
2. 「record利用基準」を docs 化
3. 命名/コメントに「なぜrecordか」を残す

---

## 2) ログ基盤導入（STEP11）

## 採用
- バックエンド: **Serilog**（構造化ログ）

## 出力方針
- 開発: Console + Rolling File
- 本番: Console（将来は外部集約先に送信）

## 最低限ログ項目
- `RequestId`, `InvestorId`, `TickerId`, `Action`, `Quantity`, `TurnNumber`, `ElapsedMs`, `Result`

## 実装タスク
1. `Program.cs` に Serilog設定
2. Actions Controller / Handlerに情報ログ追加
3. 失敗パターン（BadRequest/NotFound/業務失敗）を警告ログ化

---

## 3) ターン制導入（STEP12）

## 最小仕様
- ポートフォリオごとに `CurrentTurn` を保持
- 1アクション実行でターン +1
- `wait` もターンを進める
- リクエストに `ExpectedTurn` を持たせ、ずれたら `409 Conflict`

## 追加予定モデル
- `TurnState`（DomainまたはApplication）
- `ActionRequest` に `ExpectedTurn` 追加
- `ActionResult` に `CurrentTurn` 追加

## 実装タスク
1. InMemoryStoreにターン状態追加
2. Handlerでターン検証・更新
3. APIレスポンスへターン情報反映
4. フロントでターン表示/競合表示

---

## 4) バリデーション基盤導入（STEP13）

## 採用
- バックエンド: **FluentValidation** + MediatR Pipeline Behavior
- フロントエンド: **Zod**

## 分担
- バック: 正式ルール（最終防衛線）
- フロント: UX向上（早期フィードバック）

## バック実装タスク
1. FluentValidationパッケージ導入
2. `BuyNowCommandValidator`, `SellNowCommandValidator`, `WaitCommandValidator` 作成
3. ValidationBehavior追加（MediatRパイプライン）

## フロント実装タスク
1. `actionsSchema` 作成（Zod）
2. `Actions.tsx` で submit前検証
3. APIエラーとの表示ルール統一

---

## 実施順（推奨）
1. STEP10: record整理
2. STEP11: ログ導入
3. STEP12: ターン制（API仕様変更あり）
4. STEP13: バリデーション（バック→フロント）

---

## 完了条件
- record利用基準が明文化され、不要recordが整理されている
- 各アクション実行ログが追跡可能
- ターン競合時に `409` が返る
- FluentValidation + Zod で入力不正を安定検知できる
