# プロジェクト現状と今後のタスク

更新日: 2026-03-29

## 実装済み

### アーキテクチャ
- **Clean Architecture** (API / Application / Domain の3層分離)
- **CQRS with MediatR**: 各アクション（BuyNow, SellNow, BuyLimit, SellLimit, Wait）が `Command + Handler` で実装済み
- **PortfolioMapper**: Controller が肥大化しないよう変換責務を分離済み
- 旧 `ActionExecutionService.cs` は削除済み

### 機能
- ターン制: 1アクションごとにターン進行、`ExpectedTurn` 不一致で `409 Conflict`
- 指値注文 (BuyLimit / SellLimit): 実装済み
- コンピュータ注文: ターン進行時に自動生成（10件 × 買/売 × 3銘柄）
- 構造化ログ: Serilog 導入済み（Console + File）
- エラーハンドリング: `ApiProblemFactory` で統一レスポンス

### フロントエンド
- React + TypeScript + Vite
- Vite proxy で `/api/*` → `localhost:5059` 転送
- ティッカー一覧・詳細、ポートフォリオ、アクション画面が API 連携済み

---

## 未実装・TODO

### 優先度: 高
- **テストコード**: xUnit によるユニットテスト・インテグレーションテストが未着手
  - 最優先: 各 CommandHandler のテスト（正常系・異常系）
  - オーダーマッチングロジックのテスト
- **FluentValidation**: バックエンドの入力バリデーション未導入
  - MediatR Pipeline Behavior として組み込む予定
- **Zod**: フロントエンドのスキーマバリデーション未導入

### 優先度: 中
- **読み取り系の Query Handler 化**: TickersController / PortfoliosController が直接 InMemoryStore を参照しており、Application 層の CQRS に寄せると整合性が上がる
- **エラーモデル統一**: フロントエンド側のエラー表示ルールを整理

### 優先度: 低
- **UI の充実**: グラフ描画（Recharts 等）、TanStack Table の活用
- **将来的な DB 移行**: `IActionExecutionStore` を差し替えれば対応可能な設計になっている

---

## アーキテクチャメモ

### Controller → Handler の呼び出し経路（BuyNow の例）
```
ActionsController.BuyNow()
  → BuyNowCommand 生成
  → IMediator.Send(command)
  → BuyNowCommandHandler.Handle()
  → IActionExecutionStore 経由でデータ取得
  → InMemoryStore でオーダーマッチング実行
  → ActionExecutionResult を返す
  → PortfolioMapper で DTO 変換してレスポンス
```

### 依存方向
```
API層 → Application層 → Domain層
               ↑
       Infrastructure（InMemory）が Application 抽象を実装
```
