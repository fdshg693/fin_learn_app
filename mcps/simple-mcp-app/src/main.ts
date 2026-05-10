import { App } from "@modelcontextprotocol/ext-apps";

const app = new App(
  { name: "SimpleSelectApp", version: "1.0.0" },
  { tools: { listChanged: true } },
);

const container = document.getElementById("app")!;

// ── 状態管理 ──────────────────────────────────────────────────────────

type Question = { choices: string[]; prompt?: string };
let questions: Question[] = [];
let answerInputs: HTMLInputElement[] = [];

// ── UI 描画 ───────────────────────────────────────────────────────────

function renderQuestions() {
  container.innerHTML = "";
  answerInputs = [];

  questions.forEach((q, idx) => {
    const block = document.createElement("div");
    block.className = "question";

    const promptText = q.prompt ?? `質問 ${idx + 1}`;
    const p = document.createElement("p");
    p.className = "prompt";
    p.textContent = `${idx + 1}. ${promptText}`;
    block.appendChild(p);

    const choicesDiv = document.createElement("div");
    choicesDiv.className = "choices";

    const input = document.createElement("input");
    input.type = "text";
    input.className = "answer-input";
    input.placeholder = "選択肢から選ぶ、または自由入力";
    answerInputs.push(input);

    for (const choice of q.choices) {
      const btn = document.createElement("button");
      btn.className = "choice-btn";
      btn.type = "button";
      btn.textContent = choice;
      btn.addEventListener("click", () => {
        input.value = choice;
        choicesDiv
          .querySelectorAll(".choice-btn")
          .forEach((b) => b.classList.remove("selected"));
        btn.classList.add("selected");
      });
      choicesDiv.appendChild(btn);
    }

    input.addEventListener("input", () => {
      // 自由入力されたら選択ボタンのハイライトを外す
      choicesDiv.querySelectorAll(".choice-btn").forEach((b) => {
        if ((b as HTMLButtonElement).textContent !== input.value) {
          b.classList.remove("selected");
        } else {
          b.classList.add("selected");
        }
      });
    });

    block.appendChild(choicesDiv);
    block.appendChild(input);
    container.appendChild(block);
  });

  const submitBtn = document.createElement("button");
  submitBtn.className = "submit-btn";
  submitBtn.type = "button";
  submitBtn.textContent = "送信";
  submitBtn.addEventListener("click", () => handleSubmit(submitBtn));
  container.appendChild(submitBtn);
}

function showResult(text: string, isError = false) {
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

function setInteractive(enabled: boolean) {
  answerInputs.forEach((i) => (i.disabled = !enabled));
  container
    .querySelectorAll(".choice-btn, .submit-btn")
    .forEach((b) => ((b as HTMLButtonElement).disabled = !enabled));
}

// ── ユーザー送信 → callServerTool ────────────────────────────────────

async function handleSubmit(submitBtn: HTMLButtonElement) {
  const answers = answerInputs.map((i) => i.value.trim());
  const missing = answers.findIndex((a) => a.length === 0);
  if (missing !== -1) {
    showResult(`質問 ${missing + 1} に回答してください`, true);
    return;
  }

  setInteractive(false);
  showLoading();

  try {
    const result = await app.callServerTool({
      name: "process-choice",
      arguments: { answers },
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

  // 結果表示後は再編集を許可しない（送信ボタンのみ無効のまま）
  answerInputs.forEach((i) => (i.disabled = true));
  submitBtn.disabled = true;
}

// ── ontoolinput: ツールが呼ばれたときの引数を受信 → 質問群を表示 ──

app.ontoolinput = (params) => {
  const args = params.arguments as
    | { questions?: Question[] }
    | undefined;
  questions = args?.questions ?? [];
  renderQuestions();
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
    const current = answerInputs.map((i) => i.value);
    return {
      content: [
        {
          type: "text" as const,
          text: current.length === 0
            ? "(未表示)"
            : current.map((v, i) => `${i + 1}. ${v || "(未入力)"}`).join("\n"),
        },
      ],
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
