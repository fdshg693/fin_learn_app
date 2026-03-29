# MCPのメモ

- Claude Code `.mcp.json`
    - `cwd` キーワードは公式にサポートされていないので、要注意
    - npx コマンドには `cmd /c npx` を指定する必要があると公式Docsに記載されているが、つけると逆に上手くいかなかった
        - 少なくとも、現在のWindows環境では
        - https://code.claude.com/docs/en/mcp#push-messages-with-channels