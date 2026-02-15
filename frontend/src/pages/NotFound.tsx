import { Link } from 'react-router-dom'

export default function NotFound() {
  return (
    <section>
      <h1>ページが見つかりません</h1>
      <Link to="/">ホームへ戻る</Link>
    </section>
  )
}
