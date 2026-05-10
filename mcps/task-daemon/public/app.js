const state = {
  tasks: [],
  selectedTaskId: null,
  filters: {
    status: "",
    search: "",
    sortBy: "updatedAt",
    sortOrder: "desc",
  },
  toastTimer: null,
  searchTimer: null,
};

const elements = {
  serverStatus: document.getElementById("server-status"),
  taskCount: document.getElementById("task-count"),
  lastSync: document.getElementById("last-sync"),
  taskList: document.getElementById("task-list"),
  emptyState: document.getElementById("empty-state"),
  editForm: document.getElementById("edit-form"),
  editId: document.getElementById("edit-id"),
  editTitle: document.getElementById("edit-title"),
  editDescription: document.getElementById("edit-description"),
  editStatus: document.getElementById("edit-status"),
  editCreatedAt: document.getElementById("edit-created-at"),
  editUpdatedAt: document.getElementById("edit-updated-at"),
  deleteButton: document.getElementById("delete-button"),
  createForm: document.getElementById("create-form"),
  refreshButton: document.getElementById("refresh-button"),
  searchInput: document.getElementById("search-input"),
  statusFilter: document.getElementById("status-filter"),
  sortByFilter: document.getElementById("sort-by-filter"),
  sortOrderFilter: document.getElementById("sort-order-filter"),
  toast: document.getElementById("toast"),
};

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function formatDate(value) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function statusLabel(status) {
  if (status === "in_progress") {
    return "In progress";
  }
  if (status === "done") {
    return "Done";
  }
  return "Todo";
}

function statusClass(status) {
  if (status === "in_progress") {
    return "status-in-progress";
  }
  if (status === "done") {
    return "status-done";
  }
  return "status-todo";
}

function updateHeader(taskCount) {
  elements.taskCount.textContent = String(taskCount);
  elements.lastSync.textContent = formatDate(new Date().toISOString());
}

async function request(path, options = {}) {
  const response = await fetch(path, {
    headers: {
      "Content-Type": "application/json",
      ...(options.headers ?? {}),
    },
    ...options,
  });

  const isJson = response.headers.get("content-type")?.includes("application/json");
  const payload = isJson ? await response.json() : await response.text();

  if (!response.ok) {
    const message =
      payload && typeof payload === "object" && "error" in payload
        ? payload.error
        : response.statusText;
    throw new Error(message || "Request failed.");
  }

  return payload;
}

function showToast(message) {
  elements.toast.textContent = message;
  elements.toast.classList.remove("hidden");

  if (state.toastTimer) {
    window.clearTimeout(state.toastTimer);
  }

  state.toastTimer = window.setTimeout(() => {
    elements.toast.classList.add("hidden");
  }, 2400);
}

function renderTaskList() {
  if (state.tasks.length === 0) {
    elements.taskList.innerHTML = `
      <div class="empty-state">
        <p>No tasks match the current filters. Create one above or relax the filters.</p>
      </div>
    `;
    renderInspector(null);
    return;
  }

  elements.taskList.innerHTML = state.tasks
    .map((task) => {
      const selected = task.id === state.selectedTaskId;

      return `
        <article class="task-card ${selected ? "is-selected" : ""}" data-task-id="${task.id}">
          <header>
            <div>
              <h3>${escapeHtml(task.title)}</h3>
              <p>${escapeHtml(task.description || "No description")}</p>
            </div>
            <span class="status-pill ${statusClass(task.status)}">${escapeHtml(statusLabel(task.status))}</span>
          </header>

          <div class="task-meta">
            <span>ID ${escapeHtml(task.id)}</span>
            <span>Updated ${escapeHtml(formatDate(task.updatedAt))}</span>
          </div>

          <div class="task-actions">
            <button type="button" data-action="select" data-task-id="${task.id}">Inspect</button>
          </div>
        </article>
      `;
    })
    .join("");

  const selectedTask = state.tasks.find((task) => task.id === state.selectedTaskId) ?? state.tasks[0];
  state.selectedTaskId = selectedTask.id;
  renderInspector(selectedTask);
}

function renderInspector(task) {
  if (!task) {
    elements.emptyState.classList.remove("hidden");
    elements.editForm.classList.add("hidden");
    return;
  }

  elements.emptyState.classList.add("hidden");
  elements.editForm.classList.remove("hidden");

  elements.editId.value = task.id;
  elements.editTitle.value = task.title;
  elements.editDescription.value = task.description;
  elements.editStatus.value = task.status;
  elements.editCreatedAt.textContent = formatDate(task.createdAt);
  elements.editUpdatedAt.textContent = formatDate(task.updatedAt);
}

async function refreshHealth(taskCount = state.tasks.length) {
  const health = await request("/health");
  elements.serverStatus.textContent = health.status === "ok" ? "Live" : health.status;
  updateHeader(taskCount ?? health.taskCount ?? 0);
}

async function loadTasks() {
  const search = new URLSearchParams();
  if (state.filters.status) {
    search.set("status", state.filters.status);
  }
  if (state.filters.search) {
    search.set("search", state.filters.search);
  }
  search.set("sortBy", state.filters.sortBy);
  search.set("sortOrder", state.filters.sortOrder);

  const response = await request(`/api/tasks?${search.toString()}`);
  state.tasks = response.tasks;

  if (!state.tasks.some((task) => task.id === state.selectedTaskId)) {
    state.selectedTaskId = state.tasks[0]?.id ?? null;
  }

  renderTaskList();
  await refreshHealth(state.tasks.length);
}

async function createTask(event) {
  event.preventDefault();

  const formData = new FormData(elements.createForm);
  const payload = {
    title: formData.get("title"),
    description: formData.get("description"),
    status: formData.get("status"),
  };

  const response = await request("/api/tasks", {
    method: "POST",
    body: JSON.stringify(payload),
  });

  elements.createForm.reset();
  document.getElementById("create-status").value = "todo";
  state.selectedTaskId = response.task.id;
  await loadTasks();
  showToast("Task created.");
}

async function saveTask(event) {
  event.preventDefault();

  const taskId = elements.editId.value;
  if (!taskId) {
    return;
  }

  const payload = {
    title: elements.editTitle.value,
    description: elements.editDescription.value,
    status: elements.editStatus.value,
  };

  await request(`/api/tasks/${taskId}`, {
    method: "PATCH",
    body: JSON.stringify(payload),
  });

  await loadTasks();
  showToast("Task updated.");
}

async function deleteTask() {
  const taskId = elements.editId.value;
  if (!taskId) {
    return;
  }

  const target = state.tasks.find((task) => task.id === taskId);
  const confirmed = window.confirm(`Delete task \"${target?.title ?? taskId}\"?`);
  if (!confirmed) {
    return;
  }

  await request(`/api/tasks/${taskId}`, { method: "DELETE" });
  state.selectedTaskId = null;
  await loadTasks();
  showToast("Task deleted.");
}

function updateFiltersFromUi() {
  state.filters.status = elements.statusFilter.value;
  state.filters.search = elements.searchInput.value.trim();
  state.filters.sortBy = elements.sortByFilter.value;
  state.filters.sortOrder = elements.sortOrderFilter.value;
}

function bindEvents() {
  elements.createForm.addEventListener("submit", (event) => {
    createTask(event).catch(handleError);
  });

  elements.editForm.addEventListener("submit", (event) => {
    saveTask(event).catch(handleError);
  });

  elements.deleteButton.addEventListener("click", () => {
    deleteTask().catch(handleError);
  });

  elements.refreshButton.addEventListener("click", () => {
    updateFiltersFromUi();
    loadTasks().catch(handleError);
  });

  elements.statusFilter.addEventListener("change", () => {
    updateFiltersFromUi();
    loadTasks().catch(handleError);
  });

  elements.sortByFilter.addEventListener("change", () => {
    updateFiltersFromUi();
    loadTasks().catch(handleError);
  });

  elements.sortOrderFilter.addEventListener("change", () => {
    updateFiltersFromUi();
    loadTasks().catch(handleError);
  });

  elements.searchInput.addEventListener("input", () => {
    if (state.searchTimer) {
      window.clearTimeout(state.searchTimer);
    }

    state.searchTimer = window.setTimeout(() => {
      updateFiltersFromUi();
      loadTasks().catch(handleError);
    }, 220);
  });

  elements.taskList.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    const taskId = target.dataset.taskId;
    if (!taskId) {
      return;
    }

    state.selectedTaskId = taskId;
    renderTaskList();
  });
}

function handleError(error) {
  const message = error instanceof Error ? error.message : "Unexpected task daemon error.";
  elements.serverStatus.textContent = "Error";
  showToast(message);
  console.error(error);
}

async function bootstrap() {
  bindEvents();
  await loadTasks();
}

bootstrap().catch(handleError);