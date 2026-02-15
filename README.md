# ポートフォリオトラッカー

株について学べるアプリケーション

## 概要

保有株式を登録し、現在価値や損益を可視化するアプリ。

特徴: CRUDの基本を押さえつつ、リアルタイムデータ取得やグラフ描画で実用的なスキルが身につく。

## 技術スタック
.NET 9
TypeScript 5
React 19
pnpm

**技術的な実装ポイント:**
- バックエンド: Clean Architecture、Repository Pattern、CQRS（MediatR）, .NET MVC
- フロントエンド: 状態管理（Jotai）、React Query、TanStack Table
- バリデーション: FluentValidation
- ログ: Serilog

## 実装概要
- .NETがバックエンドで、Reactがフロントエンド
    - Reactはサーバーを立てて、API経由でバックエンドと通信

## ローカル起動
### バックエンド
```
cd backend/FinLearnApp.Api
dotnet run
```
起動後: http://localhost:5059

### フロントエンド
```
cd frontend
pnpm install
pnpm dev
```
起動後: Viteの表示URL（通常 http://localhost:5173 ）

## 動作確認（簡易）
1. バックエンドとフロントエンドを起動
2. ブラウザでフロントエンドを開く
3. 画面「アクション実行」で BuyNow / SellNow / Wait を試し、
    - 現金・評価額・損益・保有銘柄が更新されること
    - エラーメッセージが適切に表示されること
    を確認