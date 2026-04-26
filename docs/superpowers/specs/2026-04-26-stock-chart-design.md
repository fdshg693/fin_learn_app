# 銘柄チャート機能 設計ドキュメント

## 概要

株取引シミュレーションアプリに銘柄の価格推移チャートを追加する。
合わせてUIレイアウトをサイドバーナビに変更し、コンテンツ表示領域を広くする。

## 要件

- 各銘柄のターンごとの価格推移をラインチャートで表示する
- 表示件数を 10 / 20 / 50 ターンから選択できる（デフォルト: 20）
- 全銘柄をタブで切り替えて表示できる
- 将来的にローソク足チャートへの切り替えも追加予定（今回はスコープ外）
- 上部ナビを左サイドバーに変更し、コンテンツ領域をフル幅で使用する

---

## バックエンド設計

### Ticker エンティティの変更

`src/Domain/Entities/Ticker.cs` に価格履歴リストを追加する。

```csharp
public IReadOnlyList<PriceRecord> PriceHistory => _priceHistory.AsReadOnly();
private readonly List<PriceRecord> _priceHistory = new();
private const int MaxHistorySize = 100;
```

`UpdatePrice()` 呼び出し時に自動で履歴を追記する。最大100件を超えた場合は古いものから削除する。

### PriceRecord 値オブジェクト

`src/Domain/ValueObjects/PriceRecord.cs` として新規追加。

```csharp
public readonly record struct PriceRecord(int Turn, Money Price);
```

### APIエンドポイント

`GET /api/tickers/{tickerId}/price-history?limit=20`

レスポンス:
```json
[
  { "turn": 1, "price": { "amount": 1000, "currency": "JPY" } },
  { "turn": 2, "price": { "amount": 1023, "currency": "JPY" } }
]
```

- `limit` は 1〜100 の範囲。デフォルト 20。
- 新しいターン順（昇順）で返す。
- 銘柄が存在しない場合は 404 を返す。

---

## フロントエンド設計

### サイドバーレイアウト

`frontend/src/App.tsx` と `frontend/src/App.css` を変更する。

- `.app-shell` を `display: flex; flex-direction: row` に変更
- `.app-sidebar` を新規追加（幅 200px、縦ナビ）
- `.app-main` から `max-width` と `margin: 0 auto` を除去してフル幅に
- チャートページのみでなく全ページがフル幅になるが、既存ページは `section` タグ内で自然に収まる

### チャートページ

`frontend/src/pages/Chart.tsx` を新規作成。ルート `/chart` で表示。

**構成:**
- 上部に銘柄タブ（全銘柄を横並びで表示）
- 右上に表示件数セレクター（10 / 20 / 50）
- Recharts の `LineChart` + `ResponsiveContainer` でチャート描画
- X軸: ターン番号、Y軸: 価格（円）

**データフロー:**
1. ページ表示時に `/api/tickers` で全銘柄一覧を取得
2. タブ選択・件数変更時に `/api/tickers/{id}/price-history?limit=N` を呼ぶ
3. レスポンスを Recharts のデータ形式に変換して描画

### 依存ライブラリ

```bash
pnpm add recharts
```

### App.tsx への追加

```tsx
import Chart from './pages/Chart'
// Routes に追加
<Route path="/chart" element={<Chart />} />
// サイドバーナビに追加
<NavLink to="/chart">チャート</NavLink>
```

---

## テスト方針

### バックエンド

- `PriceRecord` 値オブジェクトのユニットテスト
- `Ticker.UpdatePrice()` 呼び出し後に履歴が追記されることのテスト
- 最大100件を超えた場合に古いレコードが削除されることのテスト
- `GET /api/tickers/{id}/price-history` のコントローラーテスト（正常系・limit・404）

### フロントエンド

- チャートページの手動動作確認（銘柄タブ切り替え、件数変更）

---

## スコープ外（将来対応）

- ローソク足チャートへの切り替え（始値・高値・安値・終値が必要）
- リアルタイム更新（ターンが進んだら自動でチャートを更新）
- 複数銘柄の重ね表示
