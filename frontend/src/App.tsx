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
      <header className="app-header">
        <span className="app-title">FinLearnApp</span>
        <nav>
          <NavLink to="/" end>
            ホーム
          </NavLink>
          <NavLink to="/tickers">銘柄</NavLink>
          <NavLink to="/portfolios">ポートフォリオ</NavLink>
          <NavLink to="/actions">アクション</NavLink>
        </nav>
      </header>
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
