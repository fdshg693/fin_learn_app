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

### 設計方針

- **状態管理**: 全状態（ポートフォリオ・注文・取引・価格）をメモリで管理。永続化なし
- **初期データ**: 起動時に銘柄・企業・投資家をシードする
- **CQRS**: 売買アクションはすべて `Application/Actions/` の Command + Handler で実装
- **ターン制**: アクション実行時にターンが進む。ターンが進むと価格変動・コンピュータ注文の生成・クロス注文の自動マッチングが発生

### シミュレーションの仕組み

- **価格変動**: ターンごとにランダム変動する
- **コンピュータ注文**: ターンごとにシステムが自動で買い注文・売り注文を生成する
- **オーダーマッチング**: 価格優先・時間優先の FIFO マッチング。クロス注文はターン進行時に自動解消される
- **手数料**: 約定ごとに固定手数料が発生する

### フロントエンドの構成

- `src/api/` — バックエンド API 呼び出しと TypeScript 型定義
- `src/pages/` — 各ルートに対応するページコンポーネント（詳細は `src/pages/CLAUDE.md` 参照）
- Vite の `proxy` 設定で `/api/*` をバックエンドに転送

### 値オブジェクト・型安全 ID

`Domain/ValueObjects/` に `Money`（金額）や各種 ID の型安全ラッパーがある。プリミティブ型で ID を扱わず、これらを使う。

## セッション開始時

**ユーザーとの直接会話の場合のみ**、会話の最初のメッセージを受け取ったら「`/session-start` でセッションを開始しますか？」と提案してください。
サブエージェントとして呼び出された場合（Agent ツール経由）はこの提案をスキップして、タスクをすぐに開始してください。

## Claude Code メモ

- サブフォルダに `CLAUDE.md` を置くと、そのフォルダを読む際に自動で読み込まれる（フォルダ固有のコンテキストを注入できる）
