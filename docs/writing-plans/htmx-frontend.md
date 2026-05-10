# HTMX Frontend Implementation Plan

**Goal:** `FinLearn.Api` プロジェクト 1 プロセス内に Razor Pages による HTMX フロントエンドを追加し、現 React フロントエンド（別プロセス）と並走させる。既存 JSON API (`/api/...`) は一切触らない。

**Architecture:** Razor Pages を `FinLearn.Api` に同居させる。HTML を返すルートは `/play/...` 配下に新設し、`/api/...` の JSON エンドポイントとは独立。HTMX は同一オリジンで `/play/{id}` にアクセスし、各アクション後はサーバが `_GameContainer` 部分ビューを返してクライアントが `outerHTML` swap する。注文板パネルだけは独立した部分ビューで部分更新する。

**Tech Stack:**
- ASP.NET Core 9 Razor Pages（Web SDK 内蔵、追加 NuGet 不要）
- htmx 2.x（CDN ではなくローカル `wwwroot/htmx.min.js` に配置）
- 既存 `GameMapper` / `GameResponse` DTO をそのまま View Model として使用
- xUnit + `WebApplicationFactory<Program>` で HTML レスポンスを文字列アサート

---

## File Structure

新規作成:

```
src/FinLearn.Api/
  Pages/
    _ViewImports.cshtml                ← @namespace, @addTagHelper
    _ViewStart.cshtml                   ← Layout = "_Layout"
    Shared/
      _Layout.cshtml                    ← <html>骨格 + htmx.min.js 読み込み
      _GameContainer.cshtml             ← #game div を丸ごと返す部分ビュー（全パネル内包）
      _OrderBookPanel.cshtml            ← 注文板単独の部分ビュー（ページング用）
    Play/
      Index.cshtml                      ← /play  ゲーム開始画面
      Index.cshtml.cs
      Game.cshtml                       ← /play/{id}  メイン画面
      Game.cshtml.cs                    ← OnGet/OnPost{Buy,Sell,Wait}/OnGetOrderBook
  wwwroot/
    htmx.min.js                         ← htmx 2.x ローカルコピー
    site.css                            ← 最小限のスタイル
```

修正:

```
src/FinLearn.Api/
  FinLearn.Api.csproj                   ← 変更なし（Web SDK が Razor Pages を内包）
  Program.cs                             ← AddRazorPages / MapRazorPages / UseStaticFiles 追加
tests/FinLearn.Api.Tests/
  HtmxPagesTests.cs                     ← 新規（Razor Pages 統合テスト）
docs/
  FRONT.md                               ← HTMX 版があることを冒頭に追記
```

`/api/...` 以下、既存 `Endpoints/`, `Dtos/`, `Mappers/`, `Services/` には**一切手を入れない**。

---

## Tasks

1. [Razor Pages インフラと smoke test](htmx-frontend/01-razor-infrastructure.md) — Razor Pages を有効化し、`/play/ping` で疎通確認。htmx.min.js 配置、共通レイアウト作成。
2. [ホーム画面: ゲーム作成](htmx-frontend/02-home-page.md) — `/play` の Razor ページにゲーム開始ボタンを置き、POST → `GameStore.CreateGame` → `/play/{id}` へリダイレクト。
3. [ゲーム画面: 読み取り専用レンダリング](htmx-frontend/03-game-page-readonly.md) — `/play/{id}` で `GameMapper.ToResponse` を呼び `_GameContainer` 部分ビューに全パネル（Header/PlayerPanel/PendingOrders/MarketBoard/PositionList/TradeForm/TradeHistory/OrderBookPanel/WarningMessage）を描画。アクションなし。
4. [取引アクション: Buy/Sell/Wait](htmx-frontend/04-trade-actions.md) — `_TradeForm` の 3 ボタンを `hx-post` 化。Razor Page ハンドラが `TurnProcessor` を呼び、更新後の `_GameContainer` を返す。`warning` 表示と入力バリデーション 400 を含む。
5. [注文板ページング](htmx-frontend/05-orderbook-pagination.md) — `_OrderBookPanel` の前へ／次へボタンを `hx-get` 化。`OnGetOrderBookAsync(int page)` ハンドラが `_OrderBookPanel` 単体の部分ビューを返す（コンテナ全体は再描画しない）。
6. [仕上げと文書更新](htmx-frontend/06-polish-and-docs.md) — `site.css` 微調整、`/` でルート選択ページを表示、`docs/FRONT.md` 冒頭に HTMX 版へのリンクを追記。
