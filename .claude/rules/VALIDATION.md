---
paths:
  - "src/FinLearn.Core/Messages.cs"
  - "src/FinLearn.Core/TurnProcessor.cs"
  - "src/FinLearn.Core/Models/Order.cs"
  - "src/FinLearn.Core/Models/Portfolio.cs"
  - "src/FinLearn.Core/Models/Position.cs"
  - "src/FinLearn.Api/Endpoints/GameEndpoints.cs"
  - "src/FinLearn.Api/Endpoints/AdminEndpoints.cs"
  - "src/FinLearn.Api/Dtos/OrderRequest.cs"
  - "tests/FinLearn.Api.Tests/GameApiTests.cs"
  - "tests/FinLearn.Tests/PortfolioTests.cs"
  - "tests/FinLearn.Tests/TurnProcessorTests.cs"
  - frontend/app/components/TradeForm.tsx
  - frontend/app/components/WarningMessage.tsx
  - frontend/app/routes/games.$id.tsx
---

## バリデーション関連ドキュメント

`docs/FEATURES/VALIDATION/` 配下に以下のドキュメントがあります：

- **LOGIC.md** (`docs/FEATURES/VALIDATION/LOGIC.md`) — 3層モデル、形式不正 vs 状態依存失敗の分類、エラーメッセージカタログ
- **API.UI.md** (`docs/FEATURES/VALIDATION/API.UI.md`) — 400 BadRequest と 200+warning の使い分け、フロント表示
