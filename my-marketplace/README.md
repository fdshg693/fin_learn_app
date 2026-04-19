# ローカル用のプラグインマーケット

プラグイン単位で有効無効を切り替えられるので、煩雑にならない範囲で、プラグインを細かく分割して管理することが望ましい。

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
