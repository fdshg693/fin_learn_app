# フロントエンド リファクタ・パフォーマンス改善

## 高優先度

### 7. SSR設定とclientLoaderの不整合

`react-router.config.ts` で `ssr: true` だが、データ取得は `clientLoader` のみ。初回HTMLが空でクライアント側でfetchするためSSRの恩恵がない。サーバーサイド `loader` を使うか、SSRを無効にするか統一すべき。

## 低優先度

### 8. エラーハンドリング

- グローバルの `ErrorBoundary` のみでルート単位のネストされた ErrorBoundary がない
- API呼び出しのエラーメッセージがユーザーに不親切（ステータスコードのみ）

### 9. ローディングUX

`app/routes/games.$id.tsx:58` で送信中にUI全体を `opacity-60` で無効化。ボタンのみ無効化する方がUX上望ましい。

### 10. Google Fonts のCDN読み込み

`app/root.tsx:14-23` でネットワークリクエストが増える。セルフホスティングやサブセット化で最適化可能。
