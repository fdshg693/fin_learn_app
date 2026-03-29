# CLAUDE CODE の使い方指針

## Agents

2026/3/29 時点
https://code.claude.com/docs/en/sub-agents#built-in-subagents

- Prebuiltで積極的に活用すべきエージェント
    - Explore 
        - Haiku 
        - Read-only tools
        - 探索に最適。とはいえ、ほっといてもよく使われるので、明確な指示が必要な場面は少なそう。
    - Plan 
        - Inherits 
        - Read-only tools
    - Bash 
        - Inherits 
    - Claude Code Guide
        - Haiku
        - Claude Code に関する質問に答えてくれる
- その他のエージェント
    - General-purpose
        - Inherits
        - All tools