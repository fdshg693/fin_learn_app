# プロジェクト構成 / DI構成

## プロジェクト構成

```
src/
  FinLearn.Api/
    Program.cs                        # DI 登録 + CORS + エンドポイントマップ
    Endpoints/
      GameEndpoints.cs                # /api/games/*（Create/Get/Buy/Sell/Wait）
      AdminEndpoints.cs               # /api/admin/games/{id}/orderbook
    Dtos/
      GameResponse.cs                 # GameResponse, PlayerDto, PositionDto, InstrumentDto, TradeResultDto
      OrderRequest.cs                 # OrderRequest (instrumentId, quantity, price?, stopPrice?, expiresInTurns?)
      OrderBookResponse.cs            # OrderBookResponse, OrderDto
    Mappers/
      GameMapper.cs                   # Game → GameResponse 変換
      OrderBookMapper.cs              # OrderBook → OrderBookResponse 変換
    Services/
      GameConfig.cs                   # 銘柄数・初期株価・手数料
      GameStore.cs                    # ゲーム状態 + 直近取引履歴（最大3件）
```

## DI 構成（[Program.cs](../../src/FinLearn.Api/Program.cs)）

```
GameConfig          → Singleton
GameStore           → Singleton（ConcurrentDictionary + 取引履歴）
IExchangeFactory    → Singleton（SimpleExchangeFactory）
TurnProcessor       → Transient（ComputerTrader + Market + RandomPriceFluctuator + IExchangeFactory）
Random              → Random.Shared を直接利用
```

## 関連

- [DESIGN.md](DESIGN.md) — 設計判断（DI・状態保持の理由など）
