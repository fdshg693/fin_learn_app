import { App } from "@modelcontextprotocol/ext-apps";

const app = new App(
  { name: "SimpleSelectApp", version: "1.0.0" },
  { tools: { listChanged: true } },
);

const container = document.getElementById("app")!;

// ── 状態管理 ──────────────────────────────────────────────────────────

let choices: string[] = [];
let prompt = "";

// ── UI 描画 ───────────────────────────────────────────────────────────

function renderChoices() {
  container.innerHTML = "";

  if (prompt) {
    const p = document.createElement("p");
    p.className = "prompt";
    p.textContent = prompt;
    container.appendChild(p);
  }

  const choicesDiv = document.createElement("div");
  choicesDiv.className = "choices";

  for (const choice of choices) {
    const btn = document.createElement("button");
    btn.className = "choice-btn";
    btn.textContent = choice;
    btn.addEventListener("click", () => handleSelect(choice, btn));
    choicesDiv.appendChild(btn);
  }

  container.appendChild(choicesDiv);
}

function showResult(text: string, isError = false) {
  // 既存の result があれば削除
  container.querySelector(".result")?.remove();

  const div = document.createElement("div");
  div.className = `result ${isError ? "error" : "success"}`;
  div.textContent = text;
  container.appendChild(div);
}

function showLoading() {
  container.querySelector(".result")?.remove();
  const div = document.createElement("div");
  div.className = "result loading";
  div.textContent = "処理中...";
  container.appendChild(div);
}

// ── ユーザー選択 → callServerTool ────────────────────────────────────

async function handleSelect(choice: string, btn: HTMLButtonElement) {
  // 選択状態を反映
  container.querySelectorAll(".choice-btn").forEach((b) => {
    (b as HTMLButtonElement).disabled = true;
    b.classList.remove("selected");
  });
  btn.classList.add("selected");

  showLoading();

  try {
    // callServerTool: サーバー側ツールを呼び出す
    const result = await app.callServerTool({
      name: "process-choice",
      arguments: { choice },
    });

    if (result.isError) {
      const errorText = result.content
        ?.map((c) => ("text" in c ? c.text : ""))
        .join("\n");
      showResult(errorText || "エラーが発生しました", true);
    } else {
      const text = result.content
        ?.map((c) => ("text" in c ? c.text : ""))
        .join("\n");
      showResult(text || "完了");
    }
  } catch (err) {
    showResult(`ツール呼び出し失敗: ${err}`, true);
  }

  // ボタンを再有効化
  container.querySelectorAll(".choice-btn").forEach((b) => {
    (b as HTMLButtonElement).disabled = false;
  });
}

// ── ontoolinput: ツールが呼ばれたときの引数を受信 → 選択肢を表示 ──

app.ontoolinput = (params) => {
  const args = params.arguments as
    | { choices?: string[]; prompt?: string }
    | undefined;
  choices = args?.choices ?? [];
  prompt = args?.prompt ?? "";
  renderChoices();
};

// ── ontoolresult: 元のツールの実行結果を受信 → 画面を更新 ──────────

app.ontoolresult = (params) => {
  if (params.isError) {
    const errorText = params.content
      ?.map((c: { type: string; text?: string }) => c.text ?? "")
      .join("\n");
    showResult(`ツール結果 (エラー): ${errorText}`, true);
  } else if (params.content) {
    const text = params.content
      .map((c: { type: string; text?: string }) => c.text ?? "")
      .join("\n");
    showResult(`ツール結果: ${text}`);
  }
};

// ── oncalltool: App自身が提供するツール（ホスト/LLMから呼び出し可能）──

app.oncalltool = async (params) => {
  if (params.name === "get-current-selection") {
    const selected = container.querySelector(".choice-btn.selected");
    const selectedText = selected?.textContent ?? "(未選択)";
    return {
      content: [{ type: "text" as const, text: selectedText }],
    };
  }

  throw new Error(`Unknown tool: ${params.name}`);
};

app.onlisttools = async () => {
  return {
    tools: ["get-current-selection"],
  };
};

// ── テーマ追従 ────────────────────────────────────────────────────────

app.onhostcontextchanged = () => {
  // CSS変数はホストが自動注入するため、特に処理不要
};

// ── 接続 ──────────────────────────────────────────────────────────────

await app.connect();

container.innerHTML = "<p class='loading'>ツール呼び出しを待機中...</p>";
