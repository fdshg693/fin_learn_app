# MCPのメモ

- Claude Code `.mcp.json`
    - `cwd` キーワードは公式にサポートされていないので、要注意
    - npx コマンドには `cmd /c npx` を指定する必要があると公式Docsに記載されているが、つけると逆に上手くいかなかった
        - 少なくとも、現在のWindows環境では
        - https://code.claude.com/docs/en/mcp#push-messages-with-channels

- 有用そうなSKILL
    - MCP
        - https://modelcontextprotocol.io/docs/develop/build-with-agent-skills
        - https://github.com/modelcontextprotocol/ext-apps/tree/main/plugins/mcp-apps
        - https://github.com/anthropics/claude-plugins-official/tree/main/plugins/mcp-server-dev/skills