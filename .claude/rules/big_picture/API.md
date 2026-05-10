---
paths:
  - "src/FinLearn.Api/Program.cs"
  - "src/FinLearn.Api/Endpoints/GameEndpoints.cs"
  - "src/FinLearn.Api/Endpoints/AdminEndpoints.cs"
  - "src/FinLearn.Api/Dtos/GameResponse.cs"
  - "src/FinLearn.Api/Dtos/OrderRequest.cs"
  - "src/FinLearn.Api/Dtos/OrderBookResponse.cs"
  - "src/FinLearn.Api/Mappers/GameMapper.cs"
  - "src/FinLearn.Api/Mappers/OrderBookMapper.cs"
  - "src/FinLearn.Api/Services/GameConfig.cs"
  - "src/FinLearn.Api/Services/GameStore.cs"
  - "tests/FinLearn.Api.Tests/**/*.cs"
---

## REST API 関連ドキュメント

`FinLearn.Api` プロジェクト（エンドポイント・DTO・マッパー・`GameStore`）や API 統合テストを編集する際は、
API 全体像・レスポンス形式・設計判断がまとまっている以下のドキュメントに目を通すことを推奨します：

- **docs/API/** — 責務別に分割された API 横断ドキュメント。エントリ: [docs/API/README.md](../../../docs/API/README.md)
  - `OVERVIEW.md` — 概要・技術スタック・`GameConfig`
  - `ENDPOINTS.md` — エンドポイント一覧 + 詳細
  - `REQUEST_DTO.md` / `RESPONSE_DTO.md` / `ORDERBOOK_DTO.md` — リクエスト/レスポンスDTO
  - `STRUCTURE.md` — プロジェクト構成・DI構成
  - `DESIGN.md` — 設計判断（警告レスポンス、板取得、取引履歴キャッシュ等）

必須ではありませんが、DTO の形状やエンドポイントの挙動を変更する際は整合性維持のため参照してください。
