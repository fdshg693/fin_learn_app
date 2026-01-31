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