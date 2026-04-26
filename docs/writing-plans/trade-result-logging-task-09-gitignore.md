# Task 9: .gitignore がログ出力先をカバーしているか確認

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Verify only: `.gitignore`

仕様 §8 では「`.gitignore` に `**/logs/` を追記」となっているが、既存の `[Ll]ogs/`（`.gitignore:32`）が大文字小文字違いを含めて `logs/` ディレクトリを再帰的に無視するため、新規追記は不要。

- [ ] **Step 1: ログディレクトリが Git で無視されることを確認**

Run: `git status --ignored src/FinLearn.Api/logs 2>&1 | head -20`
Expected: `Ignored files:` セクションに `src/FinLearn.Api/logs/` が表示される（または `logs/` がもとから無視されているため何も出ない）。

`git check-ignore -v src/FinLearn.Api/logs/dummy.log` を実行し、`.gitignore:32:[Ll]ogs/	src/FinLearn.Api/logs/dummy.log` のような既存ルールにマッチする出力が得られることを確認。

- [ ] **Step 2: 必要に応じて補足コメントを追加（任意）**

既存ルールでカバーできているのでスキップしてよい。

ステップ 1 で `logs/` が無視されていることが確認できなかった場合のみ、`.gitignore` の末尾に以下を追記:

```
# Application logs (Serilog file sink)
src/FinLearn.Api/logs/
```

追加した場合のみコミット:

```bash
git add .gitignore
git commit -m "chore: ignore Serilog file sink output"
```

確認のみで終わった場合はコミット不要。
