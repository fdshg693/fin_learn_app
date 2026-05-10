import { z } from "zod";

export const taskStatuses = ["todo", "in_progress", "done"] as const;
export type TaskStatus = (typeof taskStatuses)[number];

export const taskSortFields = ["updatedAt", "createdAt", "title"] as const;
export type TaskSortField = (typeof taskSortFields)[number];

export const sortOrders = ["asc", "desc"] as const;
export type SortOrder = (typeof sortOrders)[number];

export const defaultTaskStatus: TaskStatus = "todo";
export const defaultTaskSortField: TaskSortField = "updatedAt";
export const defaultSortOrder: SortOrder = "desc";

export const taskStatusSchema = z.enum(taskStatuses);

export const taskSchema = z.object({
  id: z.string().uuid(),
  title: z.string().min(1).max(200),
  description: z.string().max(4000),
  status: taskStatusSchema,
  createdAt: z.string().datetime({ offset: true }),
  updatedAt: z.string().datetime({ offset: true }),
});

export type Task = z.infer<typeof taskSchema>;

export const createTaskInputSchema = z.object({
  title: z.string().min(1).max(200),
  description: z.string().max(4000).optional(),
  status: taskStatusSchema.optional(),
});

export type CreateTaskInput = z.infer<typeof createTaskInputSchema>;

export const taskIdSchema = z.object({
  id: z.string().uuid(),
});

export type TaskIdInput = z.infer<typeof taskIdSchema>;

export const listTasksInputSchema = z.object({
  status: taskStatusSchema.optional(),
  search: z.string().max(200).optional(),
  sortBy: z.enum(taskSortFields).optional(),
  sortOrder: z.enum(sortOrders).optional(),
});

export type ListTasksInput = z.infer<typeof listTasksInputSchema>;

export const updateTaskInputSchema = taskIdSchema
  .extend({
    title: z.string().min(1).max(200).optional(),
    description: z.string().max(4000).optional(),
    status: taskStatusSchema.optional(),
  })
  .refine(
    (value) =>
      value.title !== undefined ||
      value.description !== undefined ||
      value.status !== undefined,
    { message: "At least one field must be updated." },
  );

export type UpdateTaskInput = z.infer<typeof updateTaskInputSchema>;