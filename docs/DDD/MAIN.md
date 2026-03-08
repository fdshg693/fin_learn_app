# 進捗

## モデル

| ドメイン名 | クラス名  備考 |
|---|---|
| 銘柄 | `Instrument`  ID による値の等価性 |
| ポジション | `Position`  銘柄 + 数量、評価額算出 |
| ポジション集合 | `PositionSet`  同一銘柄の自動集約、`+` 演算子 |
| ポートフォリオ | `Portfolio`  現金 + ポジション集合、売買ロジック |
| 投資家（プレイヤー） | `Player`  識別（Name）・注文生成・損益算出・初期資産 10,000 JPY |
| 取引所 | `IExchange`  価格取得 + 手数料（インターフェース） |
| 注文 | `Order`  ID・注文者・銘柄・売買区分・数量・価格 |
| 売買区分 | `OrderSide`  Buy / Sell の列挙型 |
| 注文帳 | `OrderBook`  売買注文の管理・約定判定（価格条件付き） |
| 約定結果 | `FillResult`  約定数量・合計金額・更新後の注文帳 |
| エラーメッセージ | `Messages`  日本語の定数定義 |
| 注文生成戦略 | `IOrderPlacer`  注文生成のインターフェース（テスト差し替え可能） |
| ゲーム | `Game`  ターン制の進行管理、OrderBook + IOrderPlacer 統合 |
| コンピュータートレーダー | `ComputerTrader`  `IOrderPlacer` 実装。毎ターン自動注文生成（買10・売10） |
| 株価変動戦略 | `IPriceFluctuator`  価格変動ロジックのインターフェース（DI ポイント） |
| ランダム株価変動 | `RandomPriceFluctuator`  `IPriceFluctuator` 実装。毎ターン ±5% 変動（最低価格1） |
| 簡易取引所 | `SimpleExchange`  `IExchange` 実装。Game.Prices + fee から構築 |

## アクション

| 機能  実装箇所 |
|---|---|
| 買う（約定ベース）  `Portfolio.ApplyTrade(TradeResult)` + `Player.WithPortfolio` |
| 売る（約定ベース）  `Portfolio.ApplyTrade(TradeResult)` + `Player.WithPortfolio` |
| 手数料の徴収  `Portfolio.ApplyTrade` 内で加減算 |
| 待つ（パス）  `TurnProcessor.Wait`（Player は関与しない） |

## ビジネスルール

- 現金不足の買い注文を拒否 
- 保有数超過の売り注文を拒否 
- 手数料込みのバリデーション 
- ターン制の進行 
- 株価のランダム変動 
- コンピューターの自動注文 
- 注文帳（オーダーブック）での約定判定 

## テスト

| テストクラス | 対象 |
|---|---|
| `PositionTests` | 評価額算出 |
| `PositionSetTests` | 集約・演算子・合計額 |
| `PortfolioTests` | 売買・手数料・バリデーション |
| `PlayerTests` | 識別（Name）・WithPortfolio・損益・注文生成 |
| `GameTests` | ターン進行・不変性・失敗時ターン不変 |
| `OrderTests` | プロパティ・等価性・バリデーション |
| `OrderBookTests` | 追加・ソート・約定・価格判定・不変性 |
| `ComputerTraderTests` | 注文生成・価格・数量・分散・不変性 |
| `PriceFluctuatorTests` | 変動範囲・最低価格・決定性・NoPriceFluctuator |
| `SimpleExchangeTests` | 価格取得・未登録銘柄・手数料 |

テストダブル: `TestExchange`（価格辞書）, `TestData`（共通フィクスチャ）, `NoPriceFluctuator`（変動なし）
