# Task 6: Serilog NuGet パッケージを追加

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Modify: `src/FinLearn.Api/FinLearn.Api.csproj`

`FinLearn.Core` には Serilog を追加しないことを再確認（外部依存ゼロ原則）。

- [ ] **Step 1: csproj にパッケージ参照を追加**

`src/FinLearn.Api/FinLearn.Api.csproj` を以下に置き換え:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <ItemGroup>
    <ProjectReference Include="..\FinLearn.Core\FinLearn.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

`Serilog.AspNetCore` 8.0.x は `Microsoft.Extensions.Hosting.Abstractions` 8.0 を要求するが、.NET 9 は後方互換のため動作する。互換性問題が出たら最新の 9.x 系（リリースされていれば）に上げる。

- [ ] **Step 2: パッケージ復元 + ビルド確認**

Run: `dotnet restore && dotnet build`
Expected: ビルド成功

- [ ] **Step 3: コミット**

```bash
git add src/FinLearn.Api/FinLearn.Api.csproj
git commit -m "build(api): add Serilog packages"
```
