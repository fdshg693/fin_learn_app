# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 概要

株取引シミュレーションで株の仕組みを学ぶ教育アプリ。ターン制で、売買・指値注文・コンピュータ注文が動くオーダーマッチングエンジンを持つ。

## 起動コマンド

**バックエンド**
```bash
cd backend/FinLearnApp.Api
dotnet run        # http://localhost:5059
dotnet build
```

**フロントエンド**
```bash
cd frontend
pnpm install
pnpm dev          # http://localhost:5173
pnpm build        # TypeScript コンパイル + Vite ビルド
pnpm lint         # ESLint
```

## アーキテクチャ

### レイヤー構成（Clean Architecture）

```
src/Domain/           # エンティティ・値オブジェクト（ビジネスルールのコア）
src/Application/      # CQRS コマンド・ハンドラ（MediatR）
backend/FinLearnApp.Api/  # ASP.NET Core Web API（Controllers, InMemoryStore）
frontend/src/         # React + TypeScript フロントエンド
```

依存の方向: `Api → Application → Domain`（Domainは他に依存しない）

### バックエンドの重要な設計

- **InMemoryStore** (`Api/Data/InMemoryStore.cs`): 全状態（ポートフォリオ・注文・取引・価格）をメモリで管理。永続化なし
- **SeedData** (`Api/Data/SeedData.cs`): 起動時の初期データ（銘柄・企業・投資家）
- **CQRS**: 売買アクションはすべて `Application/Actions/` の `Command + Handler` で実装
- **ターン制**: 売買・Wait の各アクション実行時にターンが進む。ターンが進むと価格変動・コンピュータ注文が発生

### シミュレーション仕様

- **価格変動**: ターンごとに 97%〜103% のランダム変動
- **コンピュータ注文**: ターンごとにランダムな3銘柄に買い注文10件（市場価格×95%）・売り注文10件（市場価格×100%）を生成
- **オーダーマッチング**: 価格優先・時間優先の FIFO マッチング
- **手数料**: 約定ごとに固定500円

### フロントエンドの構成

- `src/api/client.ts` — `fetchJson` ヘルパー（全 API 呼び出しの基盤）
- `src/api/types.ts` — バックエンド DTO と対応する TypeScript 型
- `src/pages/Actions.tsx` — メイン取引画面（BuyNow / SellNow / Wait / BuyLimit / SellLimit）
- Vite の `proxy` 設定で `/api/*` を `localhost:5059` に転送

### 値オブジェクト・型安全 ID

`Domain/ValueObjects/` に `Money`（金額）や `TickerId`・`InvestorId` 等の型安全ラッパーがある。プリミティブ型で ID を扱わず、これらを使う。

## セッション開始時

会話の最初のメッセージを受け取ったら、まず「`/session-start` でセッションを開始しますか？」と提案してください。

## Claude Code メモ

- サブフォルダに `CLAUDE.md` を置くと、そのフォルダを読む際に自動で読み込まれる（フォルダ固有のコンテキストを注入できる）
