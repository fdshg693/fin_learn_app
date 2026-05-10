import Database from "better-sqlite3";
import { randomUUID } from "crypto";
import { mkdirSync } from "fs";
import { dirname } from "path";
import {
  taskSchema,
  type SortOrder,
  type Task,
  type TaskSortField,
  type TaskStatus,
} from "../task-shared/taskTypes.js";

type TaskRow = {
  id: string;
  title: string;
  description: string;
  status: TaskStatus;
  created_at: string;
  updated_at: string;
};

type NewTaskRecord = {
  title: string;
  description: string;
  status: TaskStatus;
};

type UpdateTaskRecord = {
  id: string;
  title?: string;
  description?: string;
  status?: TaskStatus;
};

type ListTaskFilters = {
  status?: TaskStatus;
  search?: string;
  sortBy: TaskSortField;
  sortOrder: SortOrder;
};

function mapRow(row: TaskRow): Task {
  return taskSchema.parse({
    id: row.id,
    title: row.title,
    description: row.description,
    status: row.status,
    createdAt: row.created_at,
    updatedAt: row.updated_at,
  });
}

export class TaskRepository {
  private readonly db: Database.Database;

  constructor(private readonly dbPath: string) {
    mkdirSync(dirname(dbPath), { recursive: true });
    this.db = new Database(dbPath);
    this.db.pragma("journal_mode = WAL");
    this.initialize();
  }

  private initialize() {
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS tasks (
        id TEXT PRIMARY KEY,
        title TEXT NOT NULL,
        description TEXT NOT NULL,
        status TEXT NOT NULL,
        created_at TEXT NOT NULL,
        updated_at TEXT NOT NULL
      );

      CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
      CREATE INDEX IF NOT EXISTS idx_tasks_updated_at ON tasks(updated_at DESC);
    `);
  }

  getDbPath() {
    return this.dbPath;
  }

  countTasks() {
    const row = this.db.prepare("SELECT COUNT(*) AS count FROM tasks").get() as { count: number };
    return row.count;
  }

  createTask(input: NewTaskRecord): Task {
    const now = new Date().toISOString();
    const task: Task = {
      id: randomUUID(),
      title: input.title,
      description: input.description,
      status: input.status,
      createdAt: now,
      updatedAt: now,
    };

    this.db
      .prepare(
        `
          INSERT INTO tasks (id, title, description, status, created_at, updated_at)
          VALUES (?, ?, ?, ?, ?, ?)
        `,
      )
      .run(
        task.id,
        task.title,
        task.description,
        task.status,
        task.createdAt,
        task.updatedAt,
      );

    return task;
  }

  getTask(id: string): Task | null {
    const row = this.db
      .prepare(
        `
          SELECT id, title, description, status, created_at, updated_at
          FROM tasks
          WHERE id = ?
        `,
      )
      .get(id) as TaskRow | undefined;

    return row ? mapRow(row) : null;
  }

  listTasks(filters: ListTaskFilters): Task[] {
    const clauses: string[] = [];
    const params: string[] = [];

    if (filters.status) {
      clauses.push("status = ?");
      params.push(filters.status);
    }

    if (filters.search) {
      clauses.push("(LOWER(title) LIKE ? OR LOWER(description) LIKE ?)");
      const search = `%${filters.search.toLowerCase()}%`;
      params.push(search, search);
    }

    const whereClause = clauses.length > 0 ? `WHERE ${clauses.join(" AND ")}` : "";
    const orderColumn =
      filters.sortBy === "createdAt" ? "created_at"
      : filters.sortBy === "title" ? "title"
      : "updated_at";
    const orderDirection = filters.sortOrder === "asc" ? "ASC" : "DESC";

    const rows = this.db
      .prepare(
        `
          SELECT id, title, description, status, created_at, updated_at
          FROM tasks
          ${whereClause}
          ORDER BY ${orderColumn} ${orderDirection}, updated_at DESC
        `,
      )
      .all(...params) as TaskRow[];

    return rows.map(mapRow);
  }

  updateTask(input: UpdateTaskRecord): Task | null {
    const current = this.getTask(input.id);
    if (!current) {
      return null;
    }

    const next: Task = {
      ...current,
      title: input.title ?? current.title,
      description: input.description ?? current.description,
      status: input.status ?? current.status,
      updatedAt: new Date().toISOString(),
    };

    this.db
      .prepare(
        `
          UPDATE tasks
          SET title = ?, description = ?, status = ?, updated_at = ?
          WHERE id = ?
        `,
      )
      .run(next.title, next.description, next.status, next.updatedAt, next.id);

    return next;
  }

  deleteTask(id: string) {
    const result = this.db.prepare("DELETE FROM tasks WHERE id = ?").run(id);
    return result.changes > 0;
  }

  close() {
    this.db.close();
  }
}