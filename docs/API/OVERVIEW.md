# API概要

## 概要

株売買シミュレーターの REST API。フロントエンド（React）からの操作を受け付け、`TurnProcessor` を通じてドメインロジックを実行する。

## 技術スタック

- **ASP.NET Core Minimal API**（.NET 9）
- **プロジェクト名:** `FinLearn.Api`
- **状態管理:** インメモリ（`ConcurrentDictionary<string, Game>`）— 学習用アプリのため DB は不要

## ゲーム設定（`GameConfig`）

| パラメータ | デフォルト値 | 備考 |
|---|---|---|
| 銘柄数 | 3 | Instrument ID: 1, 2, 3 |
| 初期株価 | 各100 JPY | |
| 手数料 | 10 JPY | 固定 |
| プレイヤー初期資金 | 10,000 JPY | `Player` の定数 |

実装: [src/FinLearn.Api/Services/GameConfig.cs](../../src/FinLearn.Api/Services/GameConfig.cs)

## 関連

- [ENDPOINTS.md](ENDPOINTS.md) — エンドポイント詳細
- [STRUCTURE.md](STRUCTURE.md) — プロジェクト構成・DI構成
