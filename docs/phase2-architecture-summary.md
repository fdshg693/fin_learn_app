# Phase2 実施サマリー（現状設計と次の改善）

更新日: 2026-02-15

## 1. 現状の設計（Clean Architectureを意識した構成）

現状は以下の責務分離になっています。

- **API層（Presentation）**
  - HTTPの入出力を扱う
  - 例: `ActionsController` が DTO を受け取り、UseCaseを呼び、HTTPレスポンスへ変換
- **Application層（UseCase）**
  - 業務フローを実行（BuyNow / SellNow / Wait）
  - 例: `ActionUseCaseService`
- **Domain層**
  - `Portfolio`, `Holding`, `Money` などのドメインルール
- **Infrastructure相当（今はAPIプロジェクト内）**
  - InMemoryデータ実装
  - 例: `InMemoryActionExecutionStore`（Applicationの抽象をInMemoryで実装）

### 関係する主要ファイル

- Controller
  - `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
- UseCase
  - `src/Application/Actions/ActionUseCaseService.cs`
  - `src/Application/Actions/ActionCommands.cs`
  - `src/Application/Actions/ActionExecutionResult.cs`
  - `src/Application/Actions/IActionExecutionStore.cs`
- InMemoryアダプタ
  - `backend/FinLearnApp.Api/Services/InMemoryActionExecutionStore.cs`
- Mapper
  - `backend/FinLearnApp.Api/Mappers/PortfolioMapper.cs`
- DI設定
  - `backend/FinLearnApp.Api/Program.cs`

---

## 2. Mapperで何が具体的に変わるか

`PortfolioMapper` 導入で、以下が改善されています。

### 変更前
- Controller内で `Portfolio -> PortfolioDto` の変換処理を持つ
- Controllerが「HTTP + 変換 + ユースケース判断」を持って肥大化しやすい

### 変更後
- 変換責務を `PortfolioMapper` に集約
- Controllerは「受ける・呼ぶ・返す」に集中
- 同じ変換ロジックを複数Controllerで再利用可能

### 効果
- **可読性向上**: Controllerが短くなる
- **再利用性向上**: 変換処理の重複を削減
- **保守性向上**: DTO変更時の修正ポイントが減る

---

## 3. ControllerからInMemoryまでの呼び出し経路

現在の `POST /api/actions/buy-now` の流れは次の通りです。

1. `ActionsController.BuyNow()` が `ActionTradeRequestDto` を受け取る
2. Controllerで `BuyNowCommand` を作る
3. `ActionUseCaseService.ExecuteBuyNow(command)` を呼ぶ
4. UseCaseは `IActionExecutionStore` を通してデータ取得
5. DIにより実体 `InMemoryActionExecutionStore` が呼ばれる
6. `InMemoryActionExecutionStore` は `InMemoryStore` を参照して `Portfolio` / `Ticker` を返す
7. UseCaseが売買ロジックを実行し `ActionExecutionResult` を返す
8. Controllerが `ActionExecutionResult` をHTTPレスポンスに変換
9. `PortfolioMapper` で `PortfolioDto` に変換して返却

### 依存方向（重要）

- API層 → Application層（呼び出し）
- Application層 → 抽象 (`IActionExecutionStore`) のみ依存
- Infrastructure（InMemory実装） → Application抽象を実装

この形で、将来DB実装に置き換えるときは `IActionExecutionStore` 実装を差し替えればよい設計になります。

---

## 4. 現時点で改善すべき箇所

### A. 旧サービスの残存
- `backend/FinLearnApp.Api/Services/ActionExecutionService.cs` が残っており、現在ルートでは未使用
- 混乱を避けるため削除 or `Legacy` へ隔離推奨

### B. Mapperの層位置
- `PortfolioMapper` は現在API層にある
- 今後は `Application` で返す結果モデルを工夫し、API固有DTOへの変換はPresentationに限定する方針を明文化するとよい

### C. 読み取り系のクエリ分離
- `TickersController` / `PortfoliosController` も UseCase/Query層に寄せると整合性が上がる
- 例: `GetPortfolioQueryHandler`, `GetTickersQueryHandler`

### D. エラーモデル統一
- `BadRequest(new { message = ... })` が各所に散る可能性
- 共通 `ProblemDetails` 形式へ寄せるとフロント実装が安定

### E. テスト追加
- `ActionUseCaseService` の単体テスト（最優先）
  - 数量0以下
  - 現金不足
  - 保有不足
  - 正常Buy/Sell/Wait

---

## 5. MediatRを使うべきか

結論: **UseCaseが増えるなら導入メリットは大きい**。

### 導入メリット
- Handler単位で責務を小さくできる
- `Command/Query` ごとにファイル分割しやすい
- Pipeline Behaviorで横断関心事を共通化できる
  - Validation
  - Logging
  - Transaction

### このプロジェクトでの導入順（推奨）
1. `BuyNow`, `SellNow`, `Wait` を MediatR Command + Handler 化
2. `ActionUseCaseService` から段階移行
3. Validation を FluentValidation + Pipeline Behaviorへ移動
4. 読み取りAPIも Query Handler化

---

## 6. 次アクション（提案）

1. 未使用 `ActionExecutionService.cs` を整理
2. ActionsをMediatR Command化（3本）
3. Actions以外の読み取りAPIも Query化
4. UseCaseテストを先に追加してから置換


以上が「現状設計」と「次に改善すべきポイント」です。
