# Task 6: 仕上げと文書更新

[← Back to plan](../htmx-frontend.md)

最低限の見栄えを整え、ルートパス `/` で React 版・HTMX 版どちらに進むか選べるようにする。`docs/FRONT.md` 冒頭に HTMX 版の存在と起動方法を追記する。

**Files:**
- Modify: `src/FinLearn.Api/wwwroot/site.css`
- Modify: `src/FinLearn.Api/Program.cs`
- Modify: `docs/FRONT.md`
- Modify: `tests/FinLearn.Api.Tests/HtmxPagesTests.cs`

---

- [ ] **Step 1: 失敗テスト追加（ルートパスのナビゲーション）**

`tests/FinLearn.Api.Tests/HtmxPagesTests.cs` に追加:

```csharp
    [Fact]
    public async Task GET_root_は_play_へのリンクを含むHTMLを返す()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/play", body);
    }
```

- [ ] **Step 2: 失敗確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 1 件 FAIL（`/` が 404）

- [ ] **Step 3: ルートパスにナビゲーション HTML を返す**

`src/FinLearn.Api/Program.cs` の `app.MapAdminEndpoints();` の直後に追加:

```csharp
    app.MapGet("/", () => Results.Content("""
        <!DOCTYPE html>
        <html lang="ja"><head><meta charset="utf-8"><title>FinLearn</title></head>
        <body style="font-family:system-ui;padding:2rem;">
            <h1>株売買シミュレーター</h1>
            <ul>
                <li><a href="/play">HTMX 版（同一プロセス）</a></li>
                <li>React 版: 別途 <code>npm run dev</code> 起動 → http://localhost:5173</li>
            </ul>
        </body></html>
        """, "text/html"));
```

- [ ] **Step 4: `site.css` を最低限整える**

`src/FinLearn.Api/wwwroot/site.css` を以下に置換:

```css
body { font-family: system-ui, -apple-system, sans-serif; margin: 1rem; max-width: 1100px; }
section { margin: 1.5rem 0; padding: 1rem; border: 1px solid #ddd; border-radius: 6px; }
h1, h2 { margin: 0 0 0.5rem; }
table { border-collapse: collapse; width: 100%; }
th, td { padding: 0.25rem 0.5rem; border-bottom: 1px solid #eee; text-align: left; }
button { padding: 0.4rem 1rem; cursor: pointer; }
button[disabled] { opacity: 0.5; cursor: not-allowed; }
.warning { background: #fff3cd; border: 1px solid #f0c36d; padding: 0.5rem 1rem; border-radius: 4px; }
.buy { color: #c0392b; font-weight: bold; }
.sell { color: #2c5fa8; font-weight: bold; }
.profit { color: #27ae60; }
.loss { color: #c0392b; }
form label { display: inline-block; margin-right: 1rem; }
form input, form select { margin-left: 0.25rem; }
```

- [ ] **Step 5: テスト確認**

Run: `dotnet test`
Expected: 全件 PASS

- [ ] **Step 6: 手動動作確認**

```powershell
dotnet run --project src/FinLearn.Api
```

ブラウザで `http://localhost:5088/` → リンクから `/play` → ゲーム作成 → 一連の操作。スタイルが当たって見やすくなっていることを確認。

- [ ] **Step 7: `docs/FRONT.md` 冒頭に HTMX 版を追記**

`docs/FRONT.md` の `# フロントエンド画面設計` 直下に以下のセクションを挿入する（`## 技術構成` の前）:

```markdown
## バリエーション

このアプリには 2 種類のフロントエンドがある:

| 名称 | 場所 | プロセス | 配信 |
|---|---|---|---|
| React 版 | `frontend/` | 別プロセス（`npm run dev` で `localhost:5173`） | React Router v7 |
| HTMX 版 | `src/FinLearn.Api/Pages/` | API と同一プロセス（`localhost:5088/play`） | Razor Pages |

両者は独立しており並存可能。本ドキュメント以下は **React 版**の画面設計を記述する。HTMX 版の画面構成は React 版と同じパネル群（GameHeader / PlayerPanel / PendingOrders / MarketBoard / PositionList / TradeForm / TradeHistory / OrderBookPanel / WarningMessage）を Razor 部分ビューで実装している。
```

- [ ] **Step 8: コミット**

```powershell
git add src/FinLearn.Api/Program.cs `
        src/FinLearn.Api/wwwroot/site.css `
        docs/FRONT.md `
        tests/FinLearn.Api.Tests/HtmxPagesTests.cs
git commit -m "feat(htmx): root navigation page, basic css, and FRONT.md note"
```

- [ ] **Step 9: 完了確認**

最終的に以下が成立していることを確認:

1. `dotnet test` で全件 PASS
2. `dotnet run --project src/FinLearn.Api` 起動後、`http://localhost:5088/` で React 版・HTMX 版の選択肢が表示される
3. `/play` → ゲーム開始 → 取引・待ち・板ページングが動作
4. `frontend/` の React 版を `npm run dev` 起動 → `localhost:5173` で従来どおり動作（CORS 接続）
5. `/api/...` 配下の JSON エンドポイントが従来どおり動作（`POST /api/games`、`GET /api/games/{id}` 等）
