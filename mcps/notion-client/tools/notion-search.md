# `notion-search`

Perform a search over:
- "internal": Semantic search over Notion workspace and connected sources (Slack, Google Drive, Github, Jira, Microsoft Teams, Sharepoint, OneDrive, Linear). Supports filtering by creation date and creator.
- "user": Search for users by name or email.

Auto-selects AI search (with connected sources) or workspace search (workspace-only, faster) based on user's access to Notion AI. Use content_search_mode to override.
Use "fetch" tool for full page/database contents after getting search results. Each result's "url" field contains a page ID for Notion results (pass directly to fetch tool's "id" param) or a full URL for external connector results (Slack, Google Drive, etc.). Set page_size (default 10, max 25) and max_highlight_length (default 200, 0 to omit) as low as possible to minimize response size.
To search within a database: First fetch the database to get the data source URL (collection://...) from <data-source url="..."> tags, then use that as data_source_url. For multi-source databases, match by view ID (?v=...) in URL or search all sources separately.
Don't combine database URL/ID with collection:// prefix for data_source_url. Don't use database URL as page_url.
		<example description="Search with date range filter (only documents created in 2024)">
		{
			"query": "quarterly revenue report",
			"query_type": "internal",
			"filters": {
				"created_date_range": {
					"start_date": "2024-01-01",
					"end_date": "2025-01-01"
				}
			}
		}
		</example>
		<example description="Teamspace + creator filter">
		{"query": "project updates", "query_type": "internal", "teamspace_id": "f336d0bc-b841-465b-8045-024475c079dd", "filters": {"created_by_user_ids": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"]}}
		</example>
		<example description="Database with date + creator filters">
		{"query": "design review", "data_source_url": "collection://f336d0bc-b841-465b-8045-024475c079dd", "filters": {"created_date_range": {"start_date": "2024-10-01"}, "created_by_user_ids": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890", "b2c3d4e5-f6a7-8901-bcde-f12345678901"]}}
		</example>
		<example description="User search">
		{"query": "john@example.com", "query_type": "user"}
		</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `query` | string | ✓ | Semantic search query over your entire Notion workspace and connected sources (Slack, Google Drive, Github, Jira, Microsoft Teams, Sharepoint, OneDrive, or Linear). For best results, don't provide more than one question per tool call. Use a separate "search" tool call for each search you want to perform. Alternatively, the query can be a substring or keyword to find users by matching against their name or email address. For example: "john" or "john@example.com" |
| `query_type` | string |  |  |
| `content_search_mode` | string |  |  |
| `data_source_url` | string |  | Optionally, provide the URL of a Data source to search. This will perform a semantic search over the pages in the Data Source. Note: must be a Data Source, not a Database. <data-source> tags are part of the Notion flavored Markdown format returned by tools like fetch. The full spec is available in the create-pages tool description. |
| `page_url` | string |  | Optionally, provide the URL or ID of a page to search within. This will perform a semantic search over the content within and under the specified page. Accepts either a full page URL (e.g. https://notion.so/workspace/Page-Title-1234567890) or just the page ID (UUIDv4) with or without dashes. |
| `teamspace_id` | string |  | Optionally, provide the ID of a teamspace to restrict search results to. This will perform a search over content within the specified teamspace only. Accepts the teamspace ID (UUIDv4) with or without dashes. |
| `filters` | object | ✓ | Optionally provide filters to apply to the search results. Only valid when query_type is 'internal'. |
| `page_size` | integer |  | Maximum number of results to return (default 10). Lower values reduce response size. |
| `max_highlight_length` | integer |  | Maximum character length for result highlights (default 200). Set to 0 to omit highlights entirely. |
