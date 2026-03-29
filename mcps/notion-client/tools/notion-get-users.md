# `notion-get-users`

Retrieves a list of users in the current workspace. Shows workspace members and guests with their IDs, names, emails (if available), and types (person or bot).
Supports cursor-based pagination to iterate through all users in the workspace.
<examples>
1. List all users (first page): {}
2. Search for users by name or email: {"query": "john"}
3. Get next page of results: {"start_cursor": "abc123"}
4. Set custom page size: {"page_size": 20}
5. Fetch a specific user by ID: {"user_id": "00000000-0000-4000-8000-000000000000"}
6. Fetch the current user: {"user_id": "self"}
</examples>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `query` | string |  | Optional search query to filter users by name or email (case-insensitive). |
| `start_cursor` | string |  | Cursor for pagination. Use the next_cursor value from the previous response to get the next page. |
| `page_size` | integer |  | Number of users to return per page (default: 100, max: 100). |
| `user_id` | string |  | Return only the user matching this ID. Pass "self" to fetch the current user. |
