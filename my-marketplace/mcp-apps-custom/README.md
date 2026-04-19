# Mcp Apps のスキルをカスタマイズ

`https://github.com/modelcontextprotocol/ext-apps/tree/main/plugins/mcp-apps` にあるMcp Appsのスキルをカスタマイズした。

## インタラクティブなインストール

Claude Codeでの元のプラグイン操作方法

```
/plugin marketplace add modelcontextprotocol/ext-apps
/plugin install mcp-apps@modelcontextprotocol-ext-apps
/plugin enable mcp-apps@modelcontextprotocol-ext-apps
/plugin disable mcp-apps@modelcontextprotocol-ext-apps
/plugin uninstall mcp-apps@modelcontextprotocol-ext-apps
```

カスタマイズ版のインストール方法

```
/plugin marketplace add ./my-marketplace
/plugin install mcp-apps-custom@my-marketplace
```

## コマンドでのインストール

claude plugin marketplace add ./my-marketplace
claude plugin install mcp-apps-custom@my-marketplace --scope project
claude plugin enable mcp-apps-custom@my-marketplace --scope project
claude plugin disable mcp-apps-custom@my-marketplace --scope project
claude plugin uninstall mcp-apps-custom@my-marketplace --scope project