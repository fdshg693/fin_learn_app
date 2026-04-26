# サイドバーレイアウト 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 上部ナビゲーションをサイドバーに変更し、コンテンツ領域をフル幅で使えるようにする

**Architecture:** `App.css` の `.app-shell` を横並び flex に変更し、`.app-header` を `.app-sidebar` に置き換える。`App.tsx` の `<header>` を `<aside>` に変更して縦ナビにする。チャートページ追加時に `NavLink to="/chart"` も同時に追加する。

**Tech Stack:** React / TypeScript / CSS

**作業ディレクトリ:** `.worktrees/feature-sidebar-layout/`

---

## ファイル構成

| ファイル | 操作 | 役割 |
|---|---|---|
| `frontend/src/App.css` | 変更 | `.app-shell` を横 flex に変更、`.app-sidebar` 追加、`.app-main` の max-width 削除 |
| `frontend/src/App.tsx` | 変更 | `<header>` を `<aside className="app-sidebar">` に変更し、縦ナビ構造に変更 |

---

### Task 1: App.css をサイドバーレイアウト用に変更

**Files:**
- Modify: `frontend/src/App.css`

- [ ] **Step 1: App.css を変更**

`frontend/src/App.css` 全体を以下に置き換える:

```css
#root {
  min-height: 100vh;
  background: #0f172a;
  color: #e2e8f0;
  font-family: 'Inter', system-ui, -apple-system, sans-serif;
}

.app-shell {
  min-height: 100vh;
  display: flex;
  flex-direction: row;
}

.app-sidebar {
  width: 200px;
  flex-shrink: 0;
  background: #0b1120;
  border-right: 1px solid #1e293b;
  display: flex;
  flex-direction: column;
  padding: 1.5rem 1rem;
  gap: 0.5rem;
}

.app-sidebar .app-title {
  font-weight: 700;
  font-size: 1.125rem;
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #1e293b;
  display: block;
}

.app-sidebar nav {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.app-sidebar a {
  color: #cbd5f5;
  text-decoration: none;
  padding: 0.5rem 0.75rem;
  border-radius: 8px;
  transition: background 0.2s ease;
  font-size: 0.9rem;
}

.app-sidebar a.active {
  background: #1e293b;
  color: #f8fafc;
}

.app-sidebar a:hover {
  background: #1f2937;
}

.app-main {
  flex: 1;
  padding: 2rem;
  min-width: 0;
}

section {
  background: #111827;
  padding: 2rem;
  border-radius: 16px;
  box-shadow: 0 12px 30px rgba(15, 23, 42, 0.4);
}

section h1 {
  margin-top: 0;
  margin-bottom: 0.75rem;
}

section ul {
  padding-left: 1.25rem;
}

.actions {
  margin: 1rem 0;
}

.actions a {
  display: inline-block;
  padding: 0.5rem 1rem;
  background: #2563eb;
  color: white;
  border-radius: 8px;
  text-decoration: none;
}

.actions a:hover {
  background: #1d4ed8;
}
```

- [ ] **Step 2: TypeScript ビルドエラーがないか確認**

```bash
cd frontend && pnpm build 2>&1 | tail -5
```

Expected: ビルド成功（CSS 変更のみなのでエラーなし）

---

### Task 2: App.tsx をサイドバーレイアウト用に変更

**Files:**
- Modify: `frontend/src/App.tsx`

- [ ] **Step 1: App.tsx を変更**

`frontend/src/App.tsx` 全体を以下に置き換える:

```tsx
import './App.css'
import { NavLink, Route, Routes } from 'react-router-dom'
import Home from './pages/Home'
import Tickers from './pages/Tickers'
import TickerDetail from './pages/TickerDetail'
import Portfolios from './pages/Portfolios'
import PortfolioDetail from './pages/PortfolioDetail'
import Actions from './pages/Actions'
import NotFound from './pages/NotFound'

function App() {
  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <span className="app-title">FinLearnApp</span>
        <nav>
          <NavLink to="/" end>ホーム</NavLink>
          <NavLink to="/tickers">銘柄</NavLink>
          <NavLink to="/portfolios">ポートフォリオ</NavLink>
          <NavLink to="/actions">アクション</NavLink>
        </nav>
      </aside>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/tickers" element={<Tickers />} />
          <Route path="/tickers/:tickerId" element={<TickerDetail />} />
          <Route path="/portfolios" element={<Portfolios />} />
          <Route path="/portfolios/:investorId" element={<PortfolioDetail />} />
          <Route path="/actions" element={<Actions />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>
    </div>
  )
}

export default App
```

- [ ] **Step 2: TypeScript ビルドで型エラーがないか確認**

```bash
cd frontend && pnpm build 2>&1 | tail -5
```

Expected: ビルド成功

- [ ] **Step 3: 開発サーバーで動作確認**

```bash
# ターミナル1: バックエンド起動
cd backend/FinLearnApp.Api && dotnet run

# ターミナル2: フロントエンド起動
cd frontend && pnpm dev
```

ブラウザで `http://localhost:5173` を開き確認:
- 左側にサイドバーが表示される（幅 200px）
- ナビリンクが縦並びで表示される
- コンテンツ領域がフル幅に広がっている
- 全ページでサイドバーが機能している

- [ ] **Step 4: コミット**

```bash
git add frontend/src/App.css frontend/src/App.tsx
git commit -m "feat: ナビゲーションをサイドバーレイアウトに変更"
```
