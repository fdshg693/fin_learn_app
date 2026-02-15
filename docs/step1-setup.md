# STEP1: プロジェクト構成の作成（backend/frontend）

## 目的
MVCバックエンドとReactフロントエンドの初期構成を作成する。

## 実施内容
1. .NET ソリューションの作成
   - FinLearnApp.sln を作成。
2. MVCバックエンドの作成
   - backend/FinLearnApp.Api を生成。
   - ソリューションへ追加。
3. フロントエンドの作成
   - frontend を Vite（React + TypeScript）で生成。
   - 依存関係をインストール。

## 生成/更新された主な構成
- backend/FinLearnApp.Api（ASP.NET Core MVC）
- FinLearnApp.sln
- frontend（Vite React + TypeScript）

## 補足
- pnpm が未導入だったため Corepack を有効化し、pnpm を有効化した。
- フロントの dev サーバー起動は確認済み（停止済み）。
