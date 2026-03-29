# `notion-fetch`

Retrieves details about a Notion entity (page, database, or data source) by URL or ID.
Provide URL or ID in `id` parameter. Make multiple calls to fetch multiple entities.
Pages use enhanced Markdown format. For the complete specification, fetch the MCP resource at `notion://docs/enhanced-markdown-spec`.
Databases return all data sources (collections). Each data source has a unique ID shown in `<data-source url="collection://...">` tags. You can pass a data source ID directly to this tool to fetch details about that specific data source, including its schema and properties. Use data source IDs with update_data_source and query_data_sources tools. Multi-source databases (e.g., with linked sources) will show multiple data sources.
Set `include_discussions` to true to see discussion counts and inline discussion markers that correlate with the `get_comments` tool. The page output will include a `<page-discussions>` summary tag with discussion count, preview snippets, and `discussion://` URLs that match the discussion IDs returned by `get_comments`.
<example>{"id": "https://notion.so/workspace/Page-a1b2c3d4e5f67890"}</example>
<example>{"id": "12345678-90ab-cdef-1234-567890abcdef"}</example>
<example>{"id": "https://myspace.notion.site/Page-Title-abc123def456"}</example>
<example>{"id": "page-uuid", "include_discussions": true}</example>
<example>{"id": "collection://12345678-90ab-cdef-1234-567890abcdef"}</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `id` | string | ✓ | The ID or URL of the Notion page, database, or data source to fetch. Supports notion.so URLs, Notion Sites URLs (*.notion.site), raw UUIDs, and data source URLs (collection://...). |
| `include_transcript` | boolean |  |  |
| `include_discussions` | boolean |  |  |
