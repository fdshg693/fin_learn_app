# `notion-get-teams`

Retrieves a list of teams (teamspaces) in the current workspace. Shows which teams exist, user membership status, IDs, names, and roles.
Teams are returned split by membership status and limited to a maximum of 10 results.
<examples>
1. List all teams (up to the limit of each type): {}
2. Search for teams by name: {"query": "engineering"}
3. Find a specific team: {"query": "Product Design"}
</examples>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `query` | string |  | Optional search query to filter teams by name (case-insensitive). |
