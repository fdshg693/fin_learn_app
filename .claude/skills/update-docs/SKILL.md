---
name: update-docs
description: always use this SKILL after you write code which could impact existing knowledge in `CLAUDE.md` or which add new features that should be documented. This SKILL will help you update `CLAUDE.md` files with the new knowledge.
---

# ドキュメント更新手順

コード変更後、以下の手順で関連ドキュメントを更新する。

## 1. 影響範囲の特定

変更したファイルのパスから、更新すべきドキュメントの範囲を判定する:

| 変更箇所 | 更新対象 |
|---|---|
| `src/FinLearn.Core/` | `src/FinLearn.Core/CLAUDE.md`, `docs/DDD/MAIN.md` |
| `src/FinLearn.Api/` | `src/FinLearn.Api/CLAUDE.md` |
| `frontend/` | `frontend/CLAUDE.md` |
| `tests/` | `tests/CLAUDE.md` |
| インフラ (`gcp_terraform/`, `azure_infra/`) | 各フォルダの `CLAUDE.md` |
| プロジェクト全体に影響する変更 | ルート `CLAUDE.md` |

## 2. CLAUDE.md の更新

1. 関連する `CLAUDE.md` を Grep で今回の変更に関するキーワードを検索、または直接読むことで、どの部分を更新すべきか特定する
2. 以下に該当する場合のみ更新する:
   - 新しいクラス・インターフェース・ドメインモデルを追加した
   - 既存モデルの責務・振る舞いが変わった
   - ビルド・テスト手順に変更がある
   - 設計パターンや規約が変わった・追加された
3. 既存の記述と矛盾する内容は削除または修正する。まだ有効な記述は残す
4. `write-docs` スキルのスタイルガイドに従うこと（簡潔・具体的・現在形）

## 3. DDD ドキュメントの更新 (`docs/DDD/`)

ドメインモデルに変更があった場合のみ:

- `docs/DDD/MAIN.md` — ドメイン用語集・モデル関係図を更新
- `docs/DDD/EXCHANGE_RULE.md` — 取引ルール・約定ロジックの変更時に更新
- 新しいドメイン概念を追加した場合、MAIN.md の用語集にエントリを追加する

## 4. 機能ドキュメントの更新 (`docs/FEATURES/`)

1. `docs/FEATURES/` 配下のフォルダ名一覧を確認し、今回の変更に関連する機能を特定する
2. 該当フォルダがある場合:
   - `LOGIC.md` — ビジネスロジック・処理フローの変更を反映
   - `API.UI.md` — API エンドポイントや UI の変更を反映
3. 該当フォルダがない場合:
   - 新規機能であれば `docs/FEATURES/{機能名}/` フォルダを作成し、`LOGIC.md`・`API.UI.md` を書く
   - 既存機能の軽微な修正であれば、新規フォルダ作成は不要

## 5. 更新しないもの

- コードを読めば明らかなこと（引数の型、戻り値の型など）
- テストコードだけの変更（テスト手順の変更を除く）
- リファクタリングで外部振る舞いが変わっていない場合
- 既に書かれている内容の言い換え

## 注意

- ドキュメントの書き方は `write-docs` スキルに従う
- 各 `CLAUDE.md` は 150 行以内に収める
- 更新時は最小限の差分で済ませる。不要な並び替えやフォーマット変更はしない
