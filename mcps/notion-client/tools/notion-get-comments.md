# `notion-get-comments`

Get comments and discussions from a Notion page.
Returns discussions with full comment content in XML format. By default, returns page-level discussions only.
Tip: Use the `fetch` tool with `include_discussions: true` first to see where discussions are anchored in the page content, then use this tool to retrieve full discussion threads. The `discussion://` URLs in the fetch output match the discussion IDs returned here.
Parameters:
- `include_all_blocks`: Include discussions on child blocks (default: false)
- `include_resolved`: Include resolved discussions (default: false)
- `discussion_id`: Fetch a specific discussion by ID or URL

<example>{"page_id": "page-uuid"}</example>
<example>{"page_id": "page-uuid", "include_all_blocks": true}</example>
<example>{"page_id": "page-uuid", "discussion_id": "discussion://pageId/blockId/discussionId"}</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `page_id` | string | ✓ | Identifier for a Notion page. |
| `include_resolved` | boolean |  |  |
| `include_all_blocks` | boolean |  |  |
| `discussion_id` | string |  | Fetch a specific discussion by ID or discussion URL (e.g., discussion://pageId/blockId/discussionId). |
