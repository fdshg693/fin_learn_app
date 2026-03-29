# FinLearnApp

株取引シミュレーションで株の基本が学べるアプリ。ターン制でBuy/Sell/Waitを実行し、オーダーマッチングや価格変動を体験できる。

## 技術スタック

- バックエンド: .NET 9 / C#（Clean Architecture + CQRS with MediatR）
- フロントエンド: React 19 / TypeScript 5 / Vite（pnpm）

## ローカル起動

**バックエンド**
```bash
cd backend/FinLearnApp.Api
dotnet run
# → http://localhost:5059
```

**フロントエンド**
```bash
cd frontend
pnpm install
pnpm dev
# → http://localhost:5173
```

## ドキュメント

| ファイル | 内容 |
|---|---|
| `docs/domain-design.md` | ドメインモデル・用語・ビジネスルール |
| `docs/app-requirements.md` | 機能要件と拡張候補 |
| `docs/project-status.md` | 実装状況とTODO |
| `docs/current-spec.md` | 現在の仕様と制約 |
| `docs/action-dto-flow.md` | アクションのDTO→Handlerフロー |
| `docs/folder-structure-guide.md` | フォルダ責務・データフロー |
| `docs/record-usage-guideline.md` | C# record の使い方指針 |
| `docs/specs/` | 機能仕様書（TDD実装の入力） |
