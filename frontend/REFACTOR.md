# フロントエンド リファクタ・パフォーマンス改善

## 高優先度

### ~~1. 子コンポーネントのメモ化不足~~ ✅ 対応済み

全子コンポーネント (`GameHeader`, `PlayerPanel`, `MarketBoard`, `PositionList`, `TradeForm`, `WarningMessage`) に `React.memo` を適用。

### ~~2. インライン関数によるre-render~~ ✅ 対応済み

`TradeForm` の3つの `onChange` ハンドラを `useCallback` で安定化。`MarketBoard` の `onClick` はループ内で `inst.id` を渡す必要があるためインラインのまま残したが、`React.memo` によりコンポーネント自体の不要な再レンダーは防止。

### 3. フォームデータのバリデーション不足

`app/routes/games.$id.tsx:26-28` で `Number(formData.get(...))` による暗黙変換のみ。NaN がAPIに送られるリスクあり。

## 中優先度

### 4. コード重複: TradeForm の3フォーム

`app/components/TradeForm.tsx:64-101` で Buy/Sell/Wait の3フォームがほぼ同一構造。共通の `TradeButton` コンポーネントに抽出可能。

### 5. コード重複: 数値フォーマット

`toLocaleString()` が4ファイルに散在（`PlayerPanel`, `MarketBoard`, `PositionList`, `TradeForm`）。`formatJPY(amount: number)` ユーティリティに集約すべき。

### 6. コード重複: PlayerPanel の表示カード

`app/components/PlayerPanel.tsx:12-26` で同じスタイルのカードが3つ重複。`StatCard` コンポーネントに抽出可能。

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
