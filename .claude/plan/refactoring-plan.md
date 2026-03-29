# リファクタプラン: 注文板表示機能の事前準備

## 背景

注文板(OrderBook)の内容を画面から確認できる機能を追加するにあたり、
将来的に「特別なユーザーのみ閲覧可能」にしたいため、既存APIとは分離した形で新APIを追加する。
そのための事前リファクタリングを行う。

## 現状の課題

### 1. Program.cs にエンドポイント定義が密集している

現在 `src/FinLearn.Api/Program.cs` に5つのエンドポイントとヘルパーメソッド（`ProcessOrder`, `ToResponse`）が
すべてフラットに定義されている（34-118行）。新しいAPIグループを追加すると Program.cs が肥大化する。

### 2. ルートグルーピングが未導入

全エンドポイントが `app.MapPost("/api/games/...")` のように個別登録されており、
`MapGroup()` によるグルーピングがない。将来の認証ミドルウェア適用単位が存在しない。

### 3. ToResponse ヘルパーが再利用できない

`ToResponse` は Program.cs のローカル静的メソッドであり、
新しいエンドポイントから共用するには抽出が必要。

---

## リファクタ手順

### Step 1: 既存エンドポイントを拡張メソッドに抽出

**対象:** `src/FinLearn.Api/Program.cs` のエンドポイント定義部分

新規ファイル `src/FinLearn.Api/Endpoints/GameEndpoints.cs` を作成し、
既存の5エンドポイントと `ProcessOrder` ヘルパーを移動する。

```csharp
// Endpoints/GameEndpoints.cs
public static class GameEndpoints
{
    public static RouteGroupBuilder MapGameEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games");

        group.MapPost("/", CreateGame);
        group.MapGet("/{id}", GetGame);
        group.MapPost("/{id}/buy", Buy);
        group.MapPost("/{id}/sell", Sell);
        group.MapPost("/{id}/wait", Wait);

        return group;
    }

    // 各ハンドラメソッドとProcessOrderをここに移動
}
```

**Program.cs 側の変更:**
```csharp
app.UseCors();
app.MapGameEndpoints();   // 1行で済む
app.Run();
```

### Step 2: ToResponse を共通ヘルパーに抽出

**新規ファイル:** `src/FinLearn.Api/Mappers/GameMapper.cs`

`ToResponse` を静的クラスに移動し、既存エンドポイントと新規エンドポイントの両方から利用可能にする。

```csharp
// Mappers/GameMapper.cs
public static class GameMapper
{
    public static GameResponse ToResponse(
        string gameId, Game game,
        IExchangeFactory factory, GameConfig config,
        string? warning = null) { ... }
}
```

### Step 3: 既存テストの動作確認

リファクタ後、既存のAPIテスト (`tests/FinLearn.Api.Tests`) が全てパスすることを確認。
外部から見たAPIの振る舞いは一切変更しない（パス・レスポンス形式とも不変）。

---

## リファクタ後の構成

```
src/FinLearn.Api/
├── Program.cs                  # DI設定 + app.MapGameEndpoints() のみ
├── Endpoints/
│   └── GameEndpoints.cs        # 既存5エンドポイント + ProcessOrder
├── Mappers/
│   └── GameMapper.cs           # ToResponse (共通)
├── Dtos/
│   ├── GameResponse.cs         # 既存（変更なし）
│   └── OrderRequest.cs         # 既存（変更なし）
└── Services/
    ├── GameConfig.cs            # 既存（変更なし）
    └── GameStore.cs             # 既存（変更なし）
```

## 完了条件

- [ ] `dotnet test` 全テストパス
- [ ] 既存APIの動作が完全に同一（パス・レスポンス不変）
- [ ] Program.cs がDI設定とミドルウェア設定のみになっている
- [ ] 新しいエンドポイントグループを追加する準備が整っている
