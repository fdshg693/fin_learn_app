---
name: update-docs
description: always use this SKILL after you write code which could impact existing knowledge in `CLAUDE.md` or which add new features that should be documented. This SKILL will help you update `CLAUDE.md` files with the new knowledge.
---

1. `CLAUDE.md` を grep して今回のしゅうせいコードと関係あるファイルのみ修正
2. ビジネスロジックの変更などがある場合は`docs\DDD`配下のドキュメントも修正
3. `docs\FEATURES` 配下のフォルダ名から今回の変更に関連する機能を探し、そのフォルダ内のドキュメントも修正