# Task 1: API パッケージと設定

[← Back to plan](../entra-auth.md)

`Microsoft.Identity.Web` を `FinLearn.Api` に追加し、`AzureAd` 設定を appsettings に置く。この時点では `Program.cs` に認証ミドルウェアを配線しないため**挙動は一切変わらない**（既存テストは全てグリーンのまま）。設定値はすべて公開情報でシークレットを含まない。

**Files:**
- Modify: `src/FinLearn.Api/FinLearn.Api.csproj`
- Modify: `src/FinLearn.Api/appsettings.json`
- Modify: `src/FinLearn.Api/appsettings.Development.json`

---

- [x] **Step 1: 現状の全テストがグリーンであることを確認（ベースライン）**

Run: `dotnet test`
Expected: PASS（全件）。この後の変更で挙動が変わらないことの基準にする。

- [x] **Step 2: `Microsoft.Identity.Web` の最新 3.x 安定版バージョンを確認**

Run: `dotnet package search Microsoft.Identity.Web --take 1`
Expected: 3.x 系の安定版が表示される（例: `3.5.0` 以上）。表示されたバージョン文字列を次の Step で使う。最低ラインは `3.5.0`。

> 補足: `dotnet package search` が使えない環境なら https://www.nuget.org/packages/Microsoft.Identity.Web の最新 3.x 安定版を確認する。

- [x] **Step 3: csproj にパッケージ参照を追加**

`src/FinLearn.Api/FinLearn.Api.csproj` の Serilog 群がある `<ItemGroup>` に1行追加する。`<Version>` は Step 2 で確認した最新 3.x（最低 `3.5.0`）に置き換える:

```xml
  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
    <PackageReference Include="Microsoft.Identity.Web" Version="3.5.0" />
  </ItemGroup>
```

- [x] **Step 4: パッケージが復元されビルドが通ることを確認**

Run: `dotnet build src/FinLearn.Api`
Expected: ビルド成功（`Microsoft.Identity.Web` が復元される）。警告 `NU1605` 等が出ないこと。

- [x] **Step 5: `appsettings.json` に AzureAd プレースホルダを追加**

`src/FinLearn.Api/appsettings.json` を以下に書き換える（本番は環境変数 `AzureAd__*` で上書きする前提。プレースホルダのまま実値は埋めない）:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<api-client-id>",
    "Audience": "api://<api-client-id>"
  },
  "Game": {
    "InstrumentCount": 3,
    "InitialPrice": 100,
    "Fee": 10
  },
  "Admin": {
    "DefaultPageSize": 50,
    "MaxPageSize": 200
  },
  "GameStore": {
    "MaxRecentTrades": 3
  },
  "OrderLog": {
    "RetainedFileCountLimit": 7
  }
}
```

- [x] **Step 6: `appsettings.Development.json` に AzureAd セクションを追加**

`src/FinLearn.Api/appsettings.Development.json` を以下に書き換える。`<tenant-id>` / `<api-client-id>` は開発用 Entra App Registration の値（公開情報・シークレットなし）。実値が未発行ならプレースホルダのまま置く（Task 3 のテストはテスト用認証スキームを使うため実値不要）:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<tenant-id>",
    "ClientId": "<api-client-id>",
    "Audience": "api://<api-client-id>"
  }
}
```

- [x] **Step 7: 全テストが依然グリーンであることを確認（挙動不変の検証）**

Run: `dotnet test`
Expected: PASS（全件）。Step 1 と同じ結果＝認証配線前なので挙動は変わっていない。

- [x] **Step 8: コミット**

```powershell
git add src/FinLearn.Api/FinLearn.Api.csproj `
        src/FinLearn.Api/appsettings.json `
        src/FinLearn.Api/appsettings.Development.json
git commit -m "chore(api): add Microsoft.Identity.Web and AzureAd settings (no wiring yet)"
```
