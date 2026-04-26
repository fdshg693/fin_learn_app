# Task 10: 仕上げ確認とドキュメント整合

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Verify: 全テスト + 全エンドポイント

- [ ] **Step 1: 全テストグリーン確認**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 2: ログファイル形式確認**

サーバを 5 秒起動して Buy + Sell + Wait をそれぞれ 1 回ずつ実行し、CompactJson 出力を `jq` で検証:

```bash
dotnet run --project src/FinLearn.Api/FinLearn.Api.csproj &
sleep 3
GAMEID=$(curl -s -X POST http://localhost:5000/api/games | jq -r '.gameId')
curl -s -X POST http://localhost:5000/api/games/$GAMEID/buy -H "Content-Type: application/json" -d '{"instrumentId":1,"quantity":1,"price":150}' > /dev/null
curl -s -X POST http://localhost:5000/api/games/$GAMEID/sell -H "Content-Type: application/json" -d '{"instrumentId":1,"quantity":1}' > /dev/null
curl -s -X POST http://localhost:5000/api/games/$GAMEID/wait > /dev/null
kill %1

LOGFILE=$(ls -t src/FinLearn.Api/logs/finlearn-*.log | head -1)
jq -c 'select(.["@m"] | startswith("OrdersSubmitted"))' "$LOGFILE" | head -3
jq -c 'select(.["@m"] | startswith("OrdersMatched"))' "$LOGFILE" | head -3
jq 'select(.Turn == 1 and (.["@m"] | startswith("OrdersSubmitted"))) | .Orders | length' "$LOGFILE" | head -1
```

Expected:
- `OrdersSubmitted` 行が少なくとも 3 件（Buy/Sell/Wait の 1 ターンずつ）
- `OrdersMatched` 行が少なくとも 3 件
- `Turn=1` の `OrdersSubmitted` の `Orders` 配列長が 20+1=21 以下（コンピューター注文 20 + プレイヤー注文 1。Buy の場合）または 20（Wait の場合）

- [ ] **Step 3: フロントエンドが回帰していないか確認（任意）**

API レスポンス DTO (`GameResponse`) は変えていないので React 側は無修正でよい。動作確認したい場合のみ:

```bash
cd frontend && npm install && npm run build
```

Expected: ビルド成功

- [ ] **Step 4: 最終コミット（必要な場合）**

ここまでで未コミットの変更が無ければスキップ。あれば `git status` を確認してから:

```bash
git add -A
git commit -m "chore: trade result logging implementation complete"
```
